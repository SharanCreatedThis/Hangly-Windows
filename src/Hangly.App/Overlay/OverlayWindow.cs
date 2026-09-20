//
//  OverlayWindow.cs
//  Hangly
//
//  The borderless, click-through, always-on-top window the rope hangs in.
//

using Hangly.App.Interop;
using Hangly.App.Services;
using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Microsoft.Graphics.Canvas;

// Both names exist in Windows.Foundation as well, and the Win2D namespaces bring that in.
// Aliased rather than fully qualified at each use: the window works in the solver's
// coordinate space throughout, and the two types must never be silently swapped for the
// platform ones, which measure different things.
using Rect = Hangly.Core.Geometry.Rect;
using Size = Hangly.Core.Geometry.Size;

namespace Hangly.App.Overlay;

/// <summary>The overlay: one window, one rope, no chrome.</summary>
/// <remarks>
/// <b>Why this is not a WinUI window any more.</b> It was one, and it drew a correct rope
/// inside an opaque white rectangle on every machine it was run on. A WinUI 3 window owns
/// a redirection surface created with its HWND, and nothing XAML exposes — a null
/// background, a null <c>SystemBackdrop</c>, <c>DwmExtendFrameIntoClientArea</c> — replaces
/// that surface; they all paint onto it. The Windows App SDK this builds against has no
/// <c>TransparentBackdrop</c> to ask for instead. So the overlay owns a plain Win32
/// layered window and paints it itself; see <see cref="LayeredOverlaySurface"/>.
///
/// <para><b>One thread owns everything.</b> The window is created on a dedicated thread
/// which then pumps its messages, steps the solver and presents each frame. That is not
/// an optimisation — a window whose thread never pumps is marked unresponsive and
/// replaced by a ghost, and the frame loop has to live wherever the window does. Settings
/// arriving from the tray are handed over as a single volatile reference and picked up at
/// the top of a frame, which is the whole of the cross-thread surface.</para>
///
/// <para><b>Click-through is toggled, not partial.</b> Windows decides hit-testing per
/// window, exactly as AppKit does, so <c>WS_EX_TRANSPARENT</c> is turned on and off once
/// per frame according to whether the cursor is inside the charm's grab radius. The
/// write is guarded on change: setting a window style unconditionally at 120 Hz talks to
/// the window manager often enough to keep a settled overlay measurably busy, which is
/// the same finding the macOS build recorded against <c>ignoresMouseEvents</c>.</para>
///
/// <para><b>Input is polled.</b> <see cref="NativeMethods.GetCursorPos"/> and
/// <see cref="NativeMethods.GetAsyncKeyState"/> are read on the same tick that steps the
/// physics. A click-through window receives no mouse messages by definition, so there is
/// nothing to handle; polling is what lets the charm notice the cursor arriving without
/// installing a global hook.</para>
/// </remarks>
public sealed class OverlayWindow : IDisposable
{
    private readonly RopeSimulation rope;
    private readonly RopeRenderer renderer;
    private readonly SimulationClock clock = new();
    private readonly LayeredOverlaySurface surface;

    private Thread? thread;
    private volatile bool isRunning;
    private OverlaySettings? pending;
    private IReadOnlyList<CharmDescriptor>? pendingCharms;

    private OverlaySettings settings;
    private bool isClickThrough = true;
    private bool wasButtonDown;
    private Vec2 lastCursor;
    private Rect frame;
    private double scale = 1;

    public OverlayWindow(
        CanvasDevice device,
        OverlaySettings settings,
        RopeSimulation rope,
        RopeRenderer renderer,
        IReadOnlyList<CharmDescriptor> charms)
    {
        this.settings = settings;
        this.rope = rope;
        this.renderer = renderer;
        surface = new LayeredOverlaySurface(device);

        HangCharms(charms);
    }

    /// <summary>Applies a settings change without rebuilding anything.</summary>
    /// <remarks>
    /// Called from the thread the tray menu runs on. The change is handed over rather
    /// than applied, because everything it touches — the window, the solver, the surface
    /// — belongs to the frame loop.
    /// </remarks>
    public void Apply(OverlaySettings updated) => Interlocked.Exchange(ref pending, updated);

    /// <summary>Changes what hangs on the cord, without rebuilding the window.</summary>
    /// <remarks>
    /// Handed over the same way settings are, and for the same reason: the solver and the
    /// renderer belong to the frame loop, and the tray menu is not on it.
    /// </remarks>
    public void SetCharms(IReadOnlyList<CharmDescriptor> charms) =>
        Interlocked.Exchange(ref pendingCharms, charms);

    /// <summary>Starts the frame loop, which is also what creates the window.</summary>
    public void Begin()
    {
        if (thread is not null)
        {
            return;
        }

        isRunning = true;
        thread = new Thread(Run)
        {
            Name = "Hangly overlay",

            // Background, so a frame loop that somehow fails to notice Close cannot keep
            // the process alive after the tray has quit it.
            IsBackground = true,
        };
        thread.Start();
    }

    public void Close() => Dispose();

    private Size CanvasSize => new(frame.Width / scale, frame.Height / scale);

    private void Run()
    {
        try
        {
            surface.Create();
            Reposition();
            rope.Start();

            // Subscribed here rather than in the constructor so the handler is attached on
            // the thread that will raise it.
            clock.Tick += OnTick;
            clock.Start();

            // The first frame is drawn before the window is shown. A layered window that
            // is shown with no pixels in it yet flashes one frame of whatever was in the
            // bitmap, which on a transparent overlay reads as a black rectangle.
            Draw();
            surface.Show();
            Diagnostics.Log("overlay window shown");

            while (isRunning)
            {
                PumpMessages();

                // Paces the loop to the compositor, which is what CompositionTarget.Rendering
                // did while there was still a XAML tree to hang it on.
                NativeMethods.DwmFlush();

                // Taken rather than read, so a second change arriving between the read and
                // the clear is not the one that gets dropped.
                if (Interlocked.Exchange(ref pendingCharms, null) is IReadOnlyList<CharmDescriptor> charms)
                {
                    HangCharms(charms);
                    rope.Wake();
                    Draw();
                }

                if (Interlocked.Exchange(ref pending, null) is OverlaySettings updated)
                {
                    ApplyOnLoop(updated);
                }

                clock.Advance();
            }
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("overlay frame loop", exception);
        }
        finally
        {
            clock.Stop();
            surface.Dispose();
        }
    }

    private static void PumpMessages()
    {
        while (NativeMethods.PeekMessage(
            out NativeMethods.Msg message,
            IntPtr.Zero,
            0,
            0,
            NativeMethods.PmRemove))
        {
            NativeMethods.DispatchMessage(ref message);
        }
    }

    private void ApplyOnLoop(OverlaySettings updated)
    {
        settings = updated;
        rope.SetStyle(updated.RopeStyle);

        // Reposition fits the rope to the new canvas and to both sliders together, so
        // there is nothing to set afterwards. Setting them one at a time after the resize
        // is what fitted the rope to a length nobody had asked for on the way past.
        Reposition();

        // Drawn immediately, and not left to the next tick. A settled rope is not redrawn
        // at all, so a new cord colour or a new opacity would otherwise sit unseen until
        // something happened to wake it — and a change of size has already thrown away the
        // surface holding the frame that is currently on screen.
        Draw();
    }

    /// <summary>Puts the window where the settings say, on the display they name.</summary>
    private void Reposition()
    {
        DisplayInfo display = DisplayObserver.DisplayAt(settings.DisplayIndex);
        scale = NativeMethods.GetDpiForWindow(surface.Handle) / 96.0;
        if (scale <= 0)
        {
            scale = display.Scale;
        }

        // The canvas is measured in points and the desktop in pixels, so the size the
        // rope is fitted to is scaled up exactly once, here, and never again.
        Size canvas = OverlayMetrics.CanvasSize(settings.CharmSize, settings.RopeLength);
        var pixels = new Size(canvas.Width * scale, canvas.Height * scale);

        frame = ScreenPlacement.Frame(
            pixels,
            settings.Anchor,
            display.WorkArea,
            new Vec2(settings.OffsetX * scale, settings.OffsetY * scale),
            edgeInset: OverlayMetrics.EdgeInset * scale,
            topInset: 0);

        surface.Resize(
            (int)Math.Round(frame.Width),
            (int)Math.Round(frame.Height),
            scale);

        rope.Fit(CanvasSize, settings.CharmSize, settings.RopeLength);
    }

    /// <summary>
    /// Tells both halves what is on the cord at once: the solver needs the mass and the
    /// radius, the renderer needs the artwork and the palette, and they must be the same
    /// list or a charm is drawn somewhere the rope is not carrying it.
    /// </summary>
    private void HangCharms(IReadOnlyList<CharmDescriptor> charms)
    {
        renderer.Charms = charms;
        rope.SetCharmStack([.. charms.Select(charm => charm.Metrics)]);
        rope.SetBeads([.. charms.Select(charm => charm.Beads)]);
    }

    /// <summary>One display frame: poll the cursor, step the physics, present.</summary>
    private void OnTick(double deltaTime)
    {
        PollPointer();
        rope.Step(deltaTime);

        // A settled rope is a still image. Stop redrawing it, and drop the tick rate —
        // the clock keeps running because the same tick is what notices the cursor
        // arriving over the charm. A layered window keeps the last frame it was given, so
        // not presenting leaves the settled rope on screen rather than blanking it.
        clock.SetThrottled(rope.IsSleeping && !rope.IsDragging);
        if (!rope.IsSleeping || rope.IsDragging)
        {
            Draw();
        }
    }

    private void Draw() => surface.Present(
        session => renderer.Draw(session, rope.Snapshot(), rope.Style),
        new NativeMethods.Point { X = (int)Math.Round(frame.Left), Y = (int)Math.Round(frame.Top) },
        settings.Opacity);

    private void PollPointer()
    {
        if (!NativeMethods.GetCursorPos(out NativeMethods.Point cursor))
        {
            return;
        }

        // Desktop pixels to canvas points, which is the space the solver works in.
        var location = new Vec2(
            (cursor.X - frame.Left) / scale,
            (cursor.Y - frame.Top) / scale);

        bool isButtonDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VkLbutton) & 0x8000) != 0;
        bool overCharm = rope.CanGrab(location);

        // The cursor may only pass through when it is not over the charm — and never
        // mid-drag, or letting go while moving fast would drop the charm the instant the
        // pointer outran it.
        SetClickThrough(!overCharm && !rope.IsDragging);

        if (isButtonDown && !wasButtonDown && overCharm)
        {
            rope.BeginDrag(location);
        }
        else if (isButtonDown && rope.IsDragging)
        {
            // Velocity from the gap between frames, in points per second, which is what
            // the solver writes into the node's history and therefore what it is thrown
            // at when released.
            Vec2 velocity = clock.LastDelta > 0
                ? (location - lastCursor) / clock.LastDelta
                : Vec2.Zero;
            rope.UpdateDrag(location, velocity);
        }
        else if (!isButtonDown && rope.IsDragging)
        {
            rope.EndDrag();
        }

        wasButtonDown = isButtonDown;
        lastCursor = location;
    }

    private void SetClickThrough(bool enabled)
    {
        // Guarded on change. Writing this every frame is a call into the window manager
        // 120 times a second to say nothing.
        if (enabled == isClickThrough)
        {
            return;
        }

        isClickThrough = enabled;
        uint style = NativeMethods.GetExtendedStyle(surface.Handle);
        style = enabled
            ? style | NativeMethods.WsExTransparent
            : style & ~NativeMethods.WsExTransparent;
        NativeMethods.SetExtendedStyle(surface.Handle, style);
    }

    public void Dispose()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;

        // DwmFlush blocks for up to one compositor frame, so the loop always notices
        // within a few milliseconds; the join is bounded anyway so a stuck compositor
        // cannot hang the quit.
        thread?.Join(TimeSpan.FromSeconds(1));
        thread = null;
    }
}

/// <summary>The overlay's own proportions, in points.</summary>
public static class OverlayMetrics
{
    /// <summary>The canvas the shipped rope was drawn in.</summary>
    public const double BaseWidth = 220;

    public const double BaseHeight = 360;

    /// <summary>Margin kept between the overlay and the side of the display.</summary>
    public const double EdgeInset = 24;

    /// <summary>
    /// How large the window has to be for a rope this long carrying charms this big.
    /// </summary>
    /// <remarks>
    /// Read straight from the solver's own layout table rather than restated here, so the
    /// window and the rope cannot disagree about how much room a charm needs.
    ///
    /// <para><b>Why the width is not just <c>BaseWidth × room.Width</c>.</b> It was, and
    /// the rope swung out of the window. <c>CanvasScale</c> grows the width with the charm
    /// size alone, which is the room a <em>hanging</em> charm needs and not the room a
    /// swinging one sweeps: the rope is released at <c>InitialAngle</c> and carries that
    /// excursion either side of the anchor for as long as it takes to settle. At the
    /// shipped values that is 92 points of swing plus the charm's own reach against 110
    /// points of half-canvas, so the charm was clipped by the window edge on every launch
    /// and after every drag. Measured in EnvelopeTests, which is also what fails if these
    /// proportions are changed without meaning to.</para>
    ///
    /// <para>Every term comes from the solver's own numbers, so there is nothing here to
    /// keep in step by hand. The height is untouched: it was already correct, because
    /// <c>TailFraction</c> is exactly the room the lowest charm and its halo hang in.</para>
    /// </remarks>
    public static Size CanvasSize(double charmSize, double ropeLength)
    {
        Size room = RopeConfiguration.Layout.CanvasScale(charmSize, ropeLength);
        double height = BaseHeight * room.Height;

        // How far the charm's centre travels from the anchor, released at the angle the
        // solver starts it at. `unit` in RopeConfiguration.Fitted always works out to
        // BaseHeight, because the canvas is BaseHeight × room.Height and it divides by
        // room.Height — so the rope's length in points is this, with no fitting to do.
        double rope = BaseHeight * RopeConfiguration.Layout.LengthFraction * ropeLength;
        double swing = rope * Math.Sin(RopeConfiguration.Default.InitialAngle);

        // What the lowest charm reaches past its own centre. CharmStackLayout caps its
        // radius at the headroom below the rope divided by the halo extent, and that
        // headroom is TailFraction of the canvas — so this is the widest any charm on
        // this canvas can be drawn, whatever artwork it carries.
        double reach = BaseHeight * RopeConfiguration.Layout.TailFraction * charmSize
            / RopeConfiguration.Layout.CharmHaloExtent;

        return new Size(Math.Max(BaseWidth * room.Width, 2 * (swing + reach)), height);
    }
}
