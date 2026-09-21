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
    private int isNudged;
    private long swings;
    private long lastRaise;
    private CharmDropTarget? dropTarget;

    /// <summary>Which place the cursor is over, or null. Read by the drop target.</summary>
    private int? hoveredCharm;
    private long lastScaleCheck;
    private int lastSide;
    private IReadOnlyList<CharmDescriptor>? pendingCharms;

    /// <summary>How often the overlay reclaims the top of the z-order, in milliseconds.</summary>
    private const long TopmostIntervalMs = 1000;

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

    /// <summary>A file was dropped on a charm: which place, and which file.</summary>
    /// <remarks>
    /// Raised from the frame loop's thread, because that is where the drop target lives.
    /// The handler does the importing, which is why this carries the path rather than a
    /// charm: the overlay knows where the file landed and nothing else about it.
    /// </remarks>
    public event Action<int, string>? FileDropped;

    /// <summary>A file was dragged over a charm for the first time in this drag.</summary>
    public event Action? DragEntered;

    /// <summary>Changes what hangs on the cord, without rebuilding the window.</summary>
    /// <remarks>
    /// Handed over the same way settings are, and for the same reason: the solver and the
    /// renderer belong to the frame loop, and the tray menu is not on it.
    /// </remarks>
    public void SetCharms(IReadOnlyList<CharmDescriptor> charms) =>
        Interlocked.Exchange(ref pendingCharms, charms);

    /// <summary>Takes the swings counted since the last time anyone asked.</summary>
    /// <remarks>
    /// Read-and-reset, because the caller's job is to add them to the stored total and a
    /// counter that is read twice would be counted twice. Kept in memory and flushed
    /// rarely on purpose: a settings write per swing would be a write every half second
    /// for as long as the rope is moving.
    /// </remarks>
    public long TakeSwings() => Interlocked.Exchange(ref swings, 0);

    /// <summary>Gives the rope a push, from anywhere.</summary>
    /// <remarks>
    /// The About page's secret button does this: macOS describes it as "Reveals one of
    /// the app's secrets, and pushes the rope", so the push is half the feature. Handed
    /// over as a flag rather than applied, because the solver belongs to the frame loop
    /// and this is called from the window the person is clicking in.
    /// </remarks>
    public void Nudge() => isNudged = 1;

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

        // Single-threaded apartment, which OLE drag and drop requires: RegisterDragDrop
        // answers E_OUTOFMEMORY on an MTA thread, which is what it did here until this
        // line existed. The loop already pumps messages, which is the other half of what
        // an STA thread owes.
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Close() => Dispose();

    private Size CanvasSize => new(frame.Width / scale, frame.Height / scale);

    private void Run()
    {
        try
        {
            surface.Create();

            // The drop target is registered on this thread because OLE drag and drop is
            // apartment-bound: it has to be the thread that owns the window and pumps it.
            int ole = NativeMethods.OleInitialize(IntPtr.Zero);
            if (ole < 0)
            {
                Diagnostics.Log($"OleInitialize failed: 0x{ole:X8}");
            }

            dropTarget = new CharmDropTarget(
                () => DragEntered?.Invoke(),
                OnFileDropped,
                () => hoveredCharm is not null);

            int registered = NativeMethods.RegisterDragDrop(surface.Handle, dropTarget);
            if (registered != 0)
            {
                // Reported rather than thrown: an overlay that cannot take a dropped file
                // is still an overlay, and the menu can still import.
                Diagnostics.Log($"RegisterDragDrop failed: 0x{registered:X8}; drops are off");
            }

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

                if (Interlocked.Exchange(ref isNudged, 0) == 1)
                {
                    rope.Push();
                    Draw();
                }

                if (LayeredOverlaySurface.TakeScaleChanged() || ScaleDrifted())
                {
                    // Re-fit to the display the window is now on. Reposition re-reads the
                    // DPI, resizes the surface and re-fits the rope in one step, which is
                    // the same path a settings change takes — so there is one way the
                    // overlay comes to terms with its canvas, not two.
                    Reposition();
                    rope.Wake();
                    Draw();
                    Diagnostics.Log($"display scale changed; refitted at {scale:0.##}x");
                }

                HoldTopmost();
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
            if (dropTarget is not null)
            {
                NativeMethods.RevokeDragDrop(surface.Handle);
                dropTarget = null;
            }

            surface.Dispose();
        }
    }

    /// <summary>Whether the display's scale no longer matches what the overlay was fitted at.</summary>
    /// <remarks>
    /// <b>Why this exists when WM_DPICHANGED is already handled.</b> The message is the
    /// fast path and it arrives the instant the scale changes. It is also, on its own, a
    /// single point of failure that cannot be tested: Windows refuses to deliver a
    /// synthetic WM_DPICHANGED from another process — <c>PostMessage</c> returns
    /// ERROR_MESSAGE_SYNC_ONLY and <c>SendMessage</c> is dropped — so nothing outside the
    /// window can exercise the handler, and a bug in it would only ever be found by a
    /// person changing their display settings.
    ///
    /// <para>So the scale is also compared against the window's own DPI on the same slow
    /// cadence that holds the z-order. It costs one <c>GetDpiForWindow</c> a second, it
    /// catches any case the message misses, and unlike the message it can be reasoned
    /// about from the outside: if the two ever disagree, the next second fixes it.</para>
    /// </remarks>
    private bool ScaleDrifted()
    {
        if (Environment.TickCount64 - lastScaleCheck < TopmostIntervalMs)
        {
            return false;
        }

        lastScaleCheck = Environment.TickCount64;
        uint dpi = NativeMethods.GetDpiForWindow(surface.Handle);
        if (dpi == 0)
        {
            return false;
        }

        double current = dpi / 96.0;

        // A tolerance, because scale is a double built by dividing: comparing it exactly
        // would refit the overlay every second on a display whose DPI does not divide
        // cleanly, which is most of them.
        return Math.Abs(current - scale) > 0.001;
    }

    /// <summary>Re-asserts the window's place above everything, about once a second.</summary>
    /// <remarks>
    /// Once a second rather than once a frame. The z-order only changes when something
    /// else claims the top, which is a human-scale event, and the same reasoning that
    /// guards the click-through style write applies here: talking to the window manager
    /// at 120 Hz to say nothing is measurable on a settled overlay.
    ///
    /// <para>The clock is <see cref="Environment.TickCount64"/> rather than a
    /// <c>Stopwatch</c> because this does not need to be accurate, only bounded, and the
    /// frame loop must not take a dependency that can block.</para>
    /// </remarks>
    private void HoldTopmost()
    {
        long now = Environment.TickCount64;
        if (now - lastRaise < TopmostIntervalMs)
        {
            return;
        }

        lastRaise = now;
        surface.RaiseToTop();
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

        // What is actually on the cord, with the bead count each charm's artwork was
        // measured to have. Cheap, once per change, and it is what a report of "my charm
        // has beads it should not have" is answered with.
        Diagnostics.Log(
            "hanging " + string.Join(", ", charms.Select(charm => $"{charm.Id} beads={charm.Beads.Count}")));
        rope.SetCharmStack([.. charms.Select(charm => charm.Metrics)]);
        rope.SetBeads([.. charms.Select(charm => charm.Beads)]);
    }

    /// <summary>One display frame: poll the cursor, step the physics, present.</summary>
    private void OnTick(double deltaTime)
    {
        PollPointer();
        rope.Step(deltaTime);
        CountSwings();

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

    /// <summary>One swing is one crossing of the vertical, which is what a pendulum does.</summary>
    private void CountSwings()
    {
        // The last node, not a snapshot. Snapshot() allocates the points, the charms and
        // the beads every time it is called, and this runs on every tick of a 120 Hz
        // loop — which is the allocation-per-frame that the layered surface was carefully
        // built to avoid. The lowest charm hangs on the last node, so its position is
        // already here for nothing.
        // Against the anchor rather than the canvas centre: they are the same point
        // today, and the day they stop being the same this still counts swings.
        double offset = rope.CharmOffsetFromAnchor;

        // A dead band, so a charm resting a hair off centre does not tick over forever
        // on floating-point noise. A fiftieth of the rope is well inside the smallest
        // swing anyone can see and well outside that noise.
        double band = rope.Configuration.TotalLength / 50;
        int side = offset > band ? 1 : offset < -band ? -1 : 0;
        if (side == 0)
        {
            return;
        }

        if (lastSide != 0 && side != lastSide)
        {
            Interlocked.Increment(ref swings);
        }

        lastSide = side;
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

        // Which place, not just whether: a drop has to land on the charm it was aimed at.
        // This is polled anyway for click-through, so the drop target costs no extra work.
        hoveredCharm = rope.CharmIndexAt(location);
        bool overCharm = hoveredCharm is not null;

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

    /// <summary>Hands the drop on to whoever is listening, with the place it landed on.</summary>
    private void OnFileDropped(string path)
    {
        if (hoveredCharm is int slot)
        {
            FileDropped?.Invoke(slot, path);
        }
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
        if (style == 0)
        {
            // Zero means the read failed, and writing it back would strip layered,
            // topmost, tool-window and no-activate in one go — which is every property
            // the overlay depends on. Better to stay click-through than to do that.
            Diagnostics.Log("could not read the overlay's extended style; leaving it alone");
            return;
        }

        style = enabled
            ? style | NativeMethods.WsExTransparent
            : style & ~NativeMethods.WsExTransparent;
        NativeMethods.SetExtendedStyle(surface.Handle, style);
        surface.RaiseToTop();
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
    /// moving one sweeps.</para>
    ///
    /// <para>The limit is the <em>drag</em>, not the swing. A released rope only carries
    /// <c>InitialAngle</c> either side of the anchor, and sizing for that was the first
    /// attempt and was still wrong: a person can pull the charm anywhere within
    /// <c>MaximumReachRatio</c> of the cord above it, which is very nearly a full circle
    /// around the anchor, and being cut in half at the end of a drag is exactly as wrong
    /// as being cut in half mid-swing. So the half-width is the reach the drag clamp
    /// allows plus the widest the lowest charm can be drawn. Measured in EnvelopeTests,
    /// which walks the whole drag circle and is what fails if these proportions change
    /// without meaning to.</para>
    ///
    /// <para>Every term comes from the solver's own numbers, so there is nothing here to
    /// keep in step by hand. The height is untouched: it was already correct, because
    /// <c>TailFraction</c> is exactly the room the lowest charm and its halo hang in.</para>
    /// </remarks>
    public static Size CanvasSize(double charmSize, double ropeLength)
    {
        Size room = RopeConfiguration.Layout.CanvasScale(charmSize, ropeLength);
        double height = BaseHeight * room.Height;

        // How far the charm's centre can get from the anchor. `unit` in
        // RopeConfiguration.Fitted always works out to BaseHeight, because the canvas is
        // BaseHeight × room.Height and it divides by room.Height — so the rope's length in
        // points is this, with no fitting to do. The drag clamp is what bounds it.
        double rope = BaseHeight * RopeConfiguration.Layout.LengthFraction * ropeLength;
        double swing = rope * RopeConfiguration.Default.MaximumReachRatio;

        // What the lowest charm reaches past its own centre. CharmStackLayout caps its
        // radius at the headroom below the rope divided by the halo extent, and that
        // headroom is TailFraction of the canvas — so this is the widest any charm on
        // this canvas can be drawn, whatever artwork it carries.
        double reach = BaseHeight * RopeConfiguration.Layout.TailFraction * charmSize
            / RopeConfiguration.Layout.CharmHaloExtent;

        return new Size(Math.Max(BaseWidth * room.Width, 2 * (swing + reach)), height);
    }
}
