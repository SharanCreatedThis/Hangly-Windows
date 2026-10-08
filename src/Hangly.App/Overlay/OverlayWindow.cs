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
    private readonly DisplayTiming displayTiming = new();
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
    private IReadOnlyList<CharmDescriptor>? pendingCharms;

    /// <summary>How often the overlay reclaims the top of the z-order, in milliseconds.</summary>
    private const long TopmostIntervalMs = 1000;

    private OverlaySettings settings;

    /// <summary>Whether this process has decided about the Spider-Man entrance; see IntroTable.</summary>
    private static int introSpent;
    private bool isClickThrough = true;
    private bool wasButtonDown;
    private Vec2 lastCursor;
    private Rect frame;
    private double scale = 1;

    /// <summary>The display the window was last fitted to, to notice when that changes.</summary>
    private DisplayInfo fittedTo;

    public OverlayWindow(
        CanvasDevice device,
        OverlaySettings settings,
        RopeSimulation rope,
        RopeRenderer renderer,
        IReadOnlyList<CharmDescriptor> charms,
        Audio.AudioService? audio = null)
    {
        LiveObjects.Track("OverlayWindow", this);
        this.audio = audio;
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

    /// <summary>The charm was right-clicked. Raised on the frame loop's thread.</summary>
    public event Action? CharmRightClicked;

    /// <summary>
    /// The charm's resting place may have moved — a refit, or the rope settling — or it appeared or went. Raised on
    /// the frame loop's thread; the notification card reads <see cref="RestAnchor"/> and <see cref="IsCharmShown"/>.
    /// </summary>
    public event Action? CharmMoved;

    /// <summary>
    /// The rope and its charms at rest, in desktop pixels, with the display's scale; null before the first frame: from
    /// the anchor down to the bottom of the lowest charm, as wide as the widest. At rest, not where they are — hanging
    /// straight below the anchor — so a card placed by it does not follow every swing.
    /// </summary>
    public (Hangly.Core.Geometry.Rect Column, double Scale)? RestColumn =>
        restAnchor is { } anchor ? (new Hangly.Core.Geometry.Rect(anchor.Left, anchor.Top, anchor.Width, anchor.Height), anchor.Scale) : null;

    /// <summary>Whether the charm is on screen: not hidden for full-screen video.</summary>
    public bool IsCharmShown => !isHiddenForFullscreen;

    /// <summary>The rope's direction now, from the anchor to the charm, as a unit vector in screen space (y down).</summary>
    /// <remarks>
    /// For the notification card, which hangs on the line of the rope and swings with it. Written by the frame loop on
    /// every tick that moves the rope, read from the XAML thread: the two numbers travel together in one box.
    /// </remarks>
    public (double Dx, double Dy)? SwingDirection => swing is { } box ? (box.Dx, box.Dy) : null;

    /// <summary>Whether the rope is moving now (not asleep, or held).</summary>
    public bool IsMoving => !isIdle;

    /// <summary>
    /// Every frame of the loop, on its thread, paced to the compositor: the rope's direction (anchor to charm, a unit
    /// vector, y down) and the frame's length. For the notification card, which swings with the charm.
    /// </summary>
    public event Action<(double Dx, double Dy), double>? Framed;

    private volatile SwingBox? swing;

    private sealed record SwingBox(double Dx, double Dy);

    private volatile AnchorBox? restAnchor;

    private sealed record AnchorBox(double Left, double Top, double Width, double Height, double Scale);

    private const int VkRbutton = 0x02;

    private bool wasRightDown;
    private bool wasIdle;

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

            // Before the first step, so a rope that should appear hanging still never
            // takes one step of the launch swing.
            ApplyMotion();
            rope.SetPhysics(settings.RopePhysics);
            rope.Start();

            // The Spider-Man entrance, once per launch: this loop is started again when
            // the charm is switched off and on, and that is not a launch.
            if (Interlocked.Exchange(ref introSpent, 1) == 0
                && IntroTable.Plays(settings.StartupAnimation, settings.CharmIds, rope.Motion == RopeMotion.Reduced))
            {
                rope.BeginIntro();
                audio?.PlayEntrance();
            }

            // Subscribed here rather than in the constructor so the handler is attached on
            // the thread that will raise it.
            clock.Tick += OnTick;
            clock.Start();

            // The first frame is drawn before the window is shown. A layered window that
            // is shown with no pixels in it yet flashes one frame of whatever was in the
            // bitmap, which on a transparent overlay reads as a black rectangle.
            Draw();
            surface.Show();

            // Held for the life of the loop: Windows keeps the pointer, not the delegate.
            foregroundChanged = (_, _, _, _, _, _, _) => Interlocked.Exchange(ref foregroundMoved, 1);
            foregroundHook = FullscreenWatcher.HookForeground(foregroundChanged);
            Diagnostics.Log("overlay window shown");

            while (isRunning)
            {
                Seg(0);
                PumpMessages();
                Seg(1);

                // Moving: paced to the compositor, which is what CompositionTarget.Rendering
                // did while there was still a XAML tree to hang it on. Settled: nothing is
                // drawn, so there is nothing to pace to; the loop sleeps until a message
                // arrives or the idle interval passes, and polls the cursor at that rate.
                // Waiting on the compositor here woke the thread at the display's full rate
                // to do nothing on three frames in four.
                double? displayDelta = null;
                if (isIdle)
                {
                    NativeMethods.MsgWaitForMultipleObjectsEx(
                        0, IntPtr.Zero, IdleWaitMs, NativeMethods.QsAllInput, NativeMethods.MwmoInputAvailable);
                    displayTiming.Reset();
                }
                else
                {
                    NativeMethods.DwmFlush();
                    displayDelta = displayTiming.Advance();
                }

                Seg(2);

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

                if (Interlocked.Exchange(ref pendingTimeProfile, 0) is int profile and > 0)
                {
                    // Not woken for: noon arriving is not something anybody asked to see,
                    // so a sleeping rope takes the new numbers the next time it moves.
                    rope.SetTimeProfile((RopeTimeProfile)(profile - 1));
                }

                if (LayeredOverlaySurface.TakeClockChanged())
                {
                    ClockChanged?.Invoke();
                }

                if (Interlocked.Exchange(ref isNudged, 0) == 1)
                {
                    rope.Push();
                    Draw();
                }

                if (LayeredOverlaySurface.TakeDisplaysChanged() || DisplayMoved())
                {
                    // A display was plugged in or out, rearranged, or its taskbar moved.
                    // The chosen display may have just arrived — the rope goes back to it —
                    // or just left, and the rope falls back to the main display.
                    Reposition();
                    rope.Wake();
                    Draw();
                    Diagnostics.Log($"displays changed; hanging on '{fittedTo.Name}' at {scale:0.##}x");
                }

                if (LayeredOverlaySurface.TakeMotionChanged())
                {
                    ApplyMotion();
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

                if (Interlocked.Exchange(ref foregroundMoved, 0) == 1)
                {
                    WatchFullscreen();
                }

                Seg(3);
                HoldTopmost();
                Seg(4);
                clock.Advance(displayDelta);
                Seg(5);
            }
        }
        catch (Exception exception)
        {
            failed = true;
            failureHResult = exception.HResult;
            deviceLost = IsDeviceLoss(exception);
            if (deviceLost)
            {
                // Not a bug to report: a driver update, a GPU reset, sleep or Remote Desktop took the device away.
                Diagnostics.Log($"overlay: graphics device lost ({exception.GetType().Name} 0x{exception.HResult:X8}); the app rebuilds the overlay");
            }
            else
            {
                Diagnostics.Failure("overlay frame loop", exception);
            }
        }
        finally
        {
            clock.Stop();
            if (dropTarget is not null)
            {
                NativeMethods.RevokeDragDrop(surface.Handle);
                dropTarget = null;
            }

            FullscreenWatcher.Unhook(foregroundHook);
            foregroundHook = IntPtr.Zero;
            surface.Dispose();
            if (failed)
            {
                Failed?.Invoke(deviceLost, failureHResult);
            }
        }
    }

    private bool failed;
    private bool deviceLost;
    private int failureHResult;

    /// <summary>The frame loop ended on a failure: whether the graphics device went away, and the HRESULT. Raised on the
    /// overlay's thread, once.</summary>
    /// <remarks>
    /// Before 2.3.0 a lost device ended the loop, the window went with it, and the charm was gone until Hangly was
    /// restarted: 191 reports from 151 installations, every one DXGI_ERROR_DEVICE_REMOVED from
    /// <c>CreateDrawingSession</c>. Until 2.3.2 every other failure still did (W-FRAMELOOP). Now each one is raised,
    /// and the app builds a new overlay (<c>AppEnvironment.RecoverOverlay</c>).
    /// </remarks>
    public event Action<bool, int>? Failed;

    /// <summary>Whether an exception means the Direct3D device is gone (removed, reset, hung), not a bug.</summary>
    public static bool IsDeviceLoss(Exception exception) => (uint)exception.HResult is
        0x887A0005 // DXGI_ERROR_DEVICE_REMOVED
        or 0x887A0006 // DXGI_ERROR_DEVICE_HUNG
        or 0x887A0007 // DXGI_ERROR_DEVICE_RESET
        or 0x887A0020 // DXGI_ERROR_DRIVER_INTERNAL_ERROR
        or 0x8899000C; // D2DERR_RECREATE_TARGET

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

    /// <summary>Whether the display the rope belongs on is not the one it was fitted to.</summary>
    /// <remarks>
    /// The same once-a-second cadence as <see cref="ScaleDrifted"/>, and for the same
    /// reason: WM_DISPLAYCHANGE is the fast path, and this is the one that cannot be
    /// missed. Compares the whole record — which display, where, its work area and its
    /// scale — so a taskbar moving or a monitor being rearranged counts as well.
    /// </remarks>
    private bool DisplayMoved()
    {
        if (Environment.TickCount64 - lastDisplayCheck < TopmostIntervalMs)
        {
            return false;
        }

        lastDisplayCheck = Environment.TickCount64;
        return DisplayObserver.Chosen(settings.DisplayId, settings.DisplayIndex) != fittedTo;
    }

    private long lastDisplayCheck;

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

        // The once-a-second housekeeping tick, which already exists: full screen is checked
        // on it too, and only while the setting is on. See FullscreenWatcher.
        WatchFullscreen();
        if (!isHiddenForFullscreen)
        {
            surface.HoldPlace();
        }
    }

    private FullscreenWatcher.WinEventProc? foregroundChanged;
    private IntPtr foregroundHook;
    private int foregroundMoved;
    private bool isHiddenForFullscreen;

    /// <summary>Steps out of the way while a film plays full screen on this display, and comes back after.</summary>
    /// <remarks>
    /// Appearance → "Auto-hide during full-screen video", off by default as on macOS. The
    /// rule is <see cref="Hangly.Core.Fullscreen.FullscreenRule"/>; this only applies it.
    /// Hidden means off the screen and not drawn — cheaper than showing — and turning the
    /// setting off brings the charm straight back.
    /// </remarks>
    private void WatchFullscreen()
    {
        bool hide = settings.HidesDuringFullscreenVideo
            && Hangly.Core.Fullscreen.FullscreenRule.ShouldHide(FullscreenWatcher.Read(surface.Handle));
        if (hide == isHiddenForFullscreen)
        {
            return;
        }

        isHiddenForFullscreen = hide;
        if (hide)
        {
            surface.Hide();
            CharmMoved?.Invoke();
            Diagnostics.Log("full-screen video on this display: charm hidden");
        }
        else
        {
            surface.Show();
            rope.Wake();
            Draw();
            CharmMoved?.Invoke();
            Diagnostics.Log("full-screen video ended: charm back");
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
        renderer.Glow = updated.Glow;
        rope.SetPhysics(updated.RopePhysics);
        bool onDesktop = updated.WindowMode == WindowMode.Desktop;
        if (surface.OnDesktop != onDesktop)
        {
            surface.OnDesktop = onDesktop;
            lastCover = null;
            surface.HoldPlace();
        }

        rope.SetStyle(updated.RopeStyle);
        ApplyMotion();
        WatchFullscreen();

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

    /// <summary>Full or reduced motion, from Appearance → Motion and Windows' own switch.</summary>
    /// <remarks>
    /// Live: a change in either takes effect on the next step, calming a rope that is
    /// already swinging rather than stopping it dead.
    /// </remarks>
    private void ApplyMotion()
    {
        RopeMotion motion = SystemMotion.Resolve(settings.Motion);
        if (motion != rope.Motion)
        {
            rope.SetMotion(motion);
            rope.Wake();
            Diagnostics.Log($"motion: {motion} ({settings.Motion})");
        }
    }

    /// <summary>Puts the window where the settings say, on the display they name.</summary>
    private void Reposition()
    {
        DisplayInfo display = DisplayObserver.Chosen(settings.DisplayId, settings.DisplayIndex);
        fittedTo = display;

        // The destination display's scale, not the window's: the window's DPI is that of
        // wherever it is now, and on a desk of mixed scales that is the wrong display for
        // the one move that matters. Measured the other way, a rope moved from a 100% to a
        // 200% display came out half size until the next second's drift check.
        scale = display.Scale > 0 ? display.Scale : NativeMethods.GetDpiForWindow(surface.Handle) / 96.0;
        if (scale <= 0)
        {
            scale = 1;
        }

        // The canvas is measured in points and the desktop in pixels, so the size the
        // rope is fitted to is scaled up exactly once, here, and never again.
        // Spider-Man and Gwen bring a rope of their own length, and the canvas is made for all of it.
        double? pictureRope = PictureParts.RopeLength(settings);
        double ropeLength = pictureRope ?? settings.RopeLength;
        double? canvasRope = PictureParts.CanvasRopeLength(settings);
        Size canvas = OverlayMetrics.CanvasSize(settings.CharmSize, canvasRope ?? ropeLength, settings.RopePhysics, stretchOf: ropeLength);
        var pixels = new Size(canvas.Width * scale, canvas.Height * scale);

        frame = ScreenPlacement.Frame(
            pixels,
            settings.Position,
            display.WorkArea,
            new Vec2(settings.OffsetX * scale, settings.OffsetY * scale),
            edgeInset: OverlayMetrics.EdgeInset * scale,
            topInset: 0);

        surface.Resize(
            (int)Math.Round(frame.Width),
            (int)Math.Round(frame.Height),
            scale);

        // Fitted to the canvas an unstretched rope would have: an elastic rope's extra room is
        // below it, to stretch into, and must not make the rope itself any longer.
        // The rope's own stretch: Gwen below its end stretches nothing.
        double stretchRoom = OverlayMetrics.StretchRoom(ropeLength, settings.RopePhysics);
        rope.Fit(new Size(CanvasSize.Width, CanvasSize.Height - stretchRoom), settings.CharmSize, ropeLength, canvasRope);
        NoteRestAnchor(rope.Snapshot());
        CharmMoved?.Invoke();
    }

    /// <summary>Where the card hangs from: see <see cref="RestAnchor"/>.</summary>
    private void NoteSwing()
    {
        Vec2 offset = rope.CharmFromAnchor;
        double length = offset.Magnitude;
        if (length > 1)
        {
            swing = new SwingBox(offset.X / length, offset.Y / length);
        }
    }

    private void NoteRestAnchor(RopeSnapshot snapshot)
    {
        if (snapshot.Charms.Count == 0 || snapshot.Points.Count == 0)
        {
            return;
        }

        CharmPlacement charm = snapshot.Charms[^1];
        Vec2 anchor = snapshot.Points[0];
        double reach = (charm.Center - anchor).Magnitude;
        // A charm that hangs by its own drawn rope (Spider-Man and Gwen) fills the canvas below the anchor, well past
        // the radius the solver gives it, so the card goes below the canvas instead.
        bool ownCord = renderer.Charms.Count > 0 && renderer.Charms[^1].HangsByOwnCord;
        double bottom = ownCord ? CanvasSize.Height : Math.Min(anchor.Y + reach + charm.Radius, CanvasSize.Height);
        double halfWidth = Math.Max(snapshot.Charms.Max(placement => placement.Radius), 12);
        var next = new AnchorBox(
            frame.Left + ((anchor.X - halfWidth) * scale),
            frame.Top + (anchor.Y * scale),
            2 * halfWidth * scale,
            Math.Max(0, bottom - anchor.Y) * scale,
            scale);
        if (restAnchor is not { } previous || Math.Abs(previous.Left - next.Left) >= 1 || Math.Abs(previous.Top + previous.Height - next.Top - next.Height) >= 1
            || Math.Abs(previous.Width - next.Width) >= 1 || previous.Scale != next.Scale)
        {
            restAnchor = next;
        }
    }

    /// <summary>
    /// Tells both halves what is on the cord at once: the solver needs the mass and the
    /// radius, the renderer needs the artwork and the palette, and they must be the same
    /// list or a charm is drawn somewhere the rope is not carrying it.
    /// </summary>
    private void HangCharms(IReadOnlyList<CharmDescriptor> charms)
    {
        // A charm that was not on the cord before announces itself — the attach sound, as
        // on macOS: the newest arrival's material, at a fixed 0.6. The first hanging at
        // launch is not an arrival.
        if (renderer.Charms.Count > 0
            && charms.LastOrDefault(charm => !renderer.Charms.Any(old => old.Id == charm.Id)) is CharmDescriptor arrived)
        {
            audio?.Play(arrived.Sound, Hangly.Core.Audio.SoundPolicy.AttachIntensity);
        }

        renderer.Charms = charms;

        // What is actually on the cord, with the bead count each charm's artwork was
        // measured to have. Cheap, once per change, and it is what a report of "my charm
        // has beads it should not have" is answered with.
        Diagnostics.Log(
            "hanging " + string.Join(", ", charms.Select(charm => $"{charm.Id} beads={charm.Beads.Count}")));
        rope.SetCharmStack([.. charms.Select(charm => charm.Metrics)]);

        // The entrance's web belongs to Spider-Man: it comes down with the last of him.
        if (!charms.Any(charm => IntroTable.IsSpiderMan(charm.Id)))
        {
            rope.DetachWeb();
        }
        rope.SetBeads([.. charms.Select(charm => charm.Beads)]);
    }

    /// <summary>One display frame: poll the cursor, step the physics, present.</summary>
    private readonly Audio.AudioService? audio;
    private readonly List<CharmCollision> collisions = [];
    private Vec2 releaseVelocity;

    private Hangly.Core.Audio.CharmSound SoundOf(int slot) =>
        slot >= 0 && slot < renderer.Charms.Count ? renderer.Charms[slot].Sound : Hangly.Core.Audio.CharmSound.Soft;

    /// <summary>Knocks between charms, from the solver's collision events.</summary>
    private void SoundCollisions()
    {
        rope.TakeCollisions(collisions);
        foreach (CharmCollision hit in collisions)
        {
            if (Hangly.Core.Audio.SoundPolicy.CollisionIntensity(hit.Speed, rope.Motion) is double intensity)
            {
                audio?.Play(Hangly.Core.Audio.SoundPolicy.Carrier(SoundOf(hit.First), SoundOf(hit.Second)), intensity);
            }
        }

        collisions.Clear();
    }

    private void OnTick(double deltaTime)
    {
        AuditFailure.MaybeFailOverlay(started);
        long tickStarted = AuditFrames ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        frameAudit?.BeginFrame();
        PollPointer();
        Sub(0);
        long physicsAllocated = AuditFrames ? GC.GetAllocatedBytesForCurrentThread() : 0;
        rope.Step(deltaTime);
        frameAudit?.NotePhysicsBytes(AuditFrames ? GC.GetAllocatedBytesForCurrentThread() - physicsAllocated : 0);
        Sub(1);
        SoundCollisions();
        Sub(2);
        CountSwings();
        Sub(3);

        // A settled rope is a still image. Stop redrawing it, and drop the tick rate —
        // the clock keeps running because the same tick is what notices the cursor
        // arriving over the charm. A layered window keeps the last frame it was given, so
        // not presenting leaves the settled rope on screen rather than blanking it.
        isIdle = rope.IsSleeping && !rope.IsDragging;
        if (!isIdle || swing is null)
        {
            NoteSwing();
        }

        if (isIdle != wasIdle)
        {
            wasIdle = isIdle;
            if (isIdle)
            {
                // Settled: its resting place is exactly where it hangs now.
                NoteRestAnchor(rope.Snapshot());
                CharmMoved?.Invoke();
            }
        }
        bool drew = (!rope.IsSleeping || rope.IsDragging) && !isHiddenForFullscreen;
        if (drew)
        {
            Sub(4);
            long allocated = AuditFrames ? GC.GetAllocatedBytesForCurrentThread() : 0;
            Draw(onlyIfMoved: true);
            if (AuditFrames)
            {
                drawBytesWorst = Math.Max(drawBytesWorst, GC.GetAllocatedBytesForCurrentThread() - allocated);
                drawBytesTotal += GC.GetAllocatedBytesForCurrentThread() - allocated;
            }

            Sub(5);
        }

        if (Framed is { } framed && swing is { } direction)
        {
            framed((direction.Dx, direction.Dy), deltaTime);
        }

        if (AuditFrames && !isIdle)
        {
            NoteFrame(deltaTime, rope.LastStepCount, System.Diagnostics.Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds);
        }

        frameAudit?.EndFrame(drew, !isIdle, deltaTime);
    }

    // Where each loop's time goes, for the smoothness checks only: pump, wait, checks, topmost, tick.
    private static readonly string[] SegmentNames = ["", "pump", "wait", "checks", "topmost", "tick"];
    private readonly double[] segmentWorst = new double[6];
    private long segmentAt;
    private long segmentLogged = System.Diagnostics.Stopwatch.GetTimestamp();

    private void Seg(int index)
    {
        if (!AuditFrames)
        {
            return;
        }

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (index > 0 && !isIdle)
        {
            segmentWorst[index] = Math.Max(segmentWorst[index], System.Diagnostics.Stopwatch.GetElapsedTime(segmentAt, now).TotalMilliseconds);
        }

        segmentAt = now;
        if (index == 5 && System.Diagnostics.Stopwatch.GetElapsedTime(segmentLogged, now).TotalSeconds >= 3)
        {
            Diagnostics.Log("loop worst ms: " + string.Join(", ", Enumerable.Range(1, 5).Select(k => $"{SegmentNames[k]} {segmentWorst[k]:0.0}"))
                + "; in the tick: " + string.Join(", ", Enumerable.Range(1, 5).Select(k => $"{SubNames[k]} {subWorst[k]:0.0}"))
                + $"; GC gen0/1/2 {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}"
                + $"; draw allocated {drawBytesTotal / 1024} KB, worst frame {drawBytesWorst / 1024} KB"
                + $"; GC paused {(GC.GetTotalPauseDuration() - gcPausedBefore).TotalMilliseconds:0.0} ms, last gen2 {LastFullGc()}");
            gcPausedBefore = GC.GetTotalPauseDuration();
            Diagnostics.Log("present worst ms: " + PresentTimes.Take() + "; renderer: " + RenderTimes.Take());
            Diagnostics.Log("frame work: " + frameAudit?.Take());
            drawBytesWorst = 0;
            drawBytesTotal = 0;
            Array.Clear(segmentWorst);
            Array.Clear(subWorst);
            segmentLogged = now;
        }
    }

    private static readonly string[] SubNames = ["", "step", "sounds", "swings", "rest", "draw"];
    private readonly double[] subWorst = new double[6];
    private long subAt;
    private long drawBytesWorst;
    private TimeSpan gcPausedBefore = GC.GetTotalPauseDuration();

    private static string LastFullGc()
    {
        GCMemoryInfo info = GC.GetGCMemoryInfo(GCKind.Background);
        GCMemoryInfo blocking = GC.GetGCMemoryInfo(GCKind.FullBlocking);
        return $"background #{info.Index} pause {(info.PauseDurations.Length > 0 ? info.PauseDurations[0].TotalMilliseconds : 0):0.0}, " +
            $"blocking #{blocking.Index} pause {(blocking.PauseDurations.Length > 0 ? blocking.PauseDurations[0].TotalMilliseconds : 0):0.0}, " +
            $"heap {info.HeapSizeBytes / (1024 * 1024)} MB, load {info.MemoryLoadBytes / (1024 * 1024)}/{info.HighMemoryLoadThresholdBytes / (1024 * 1024)} MB";
    }
    private long drawBytesTotal;

    private void Sub(int index)
    {
        if (!AuditFrames)
        {
            return;
        }

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (index > 0)
        {
            double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(subAt, now).TotalMilliseconds;
            subWorst[index] = Math.Max(subWorst[index], elapsed);
            if (frameAudit is not null)
            {
                frameAudit.Sub[index] = elapsed;
            }
        }

        subAt = now;
    }

    // Frame timing, for the smoothness checks only (HANGLY_AUDIT_FRAMES=1): how even the frames are while the rope moves.
    private static readonly bool AuditFrames = Environment.GetEnvironmentVariable("HANGLY_AUDIT_FRAMES") == "1";

    /// <summary>When this overlay was built, for <see cref="AuditFailure"/>.</summary>
    private readonly long started = System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>Each frame's own breakdown, for the same checks; null — nothing recorded — outside them.</summary>
    private readonly FrameAudit? frameAudit = AuditFrames ? new FrameAudit() : null;
    private readonly List<double> auditDeltas = [];
    private readonly Dictionary<int, int> auditSteps = [];
    private double auditWorst;
    private long auditSince = System.Diagnostics.Stopwatch.GetTimestamp();

    private void NoteFrame(double delta, int steps, double workMs)
    {
        auditDeltas.Add(delta * 1000);
        auditSteps[steps] = auditSteps.GetValueOrDefault(steps) + 1;
        auditWorst = Math.Max(auditWorst, workMs);
        if (System.Diagnostics.Stopwatch.GetElapsedTime(auditSince).TotalSeconds < 3 || auditDeltas.Count < 20)
        {
            return;
        }

        auditDeltas.Sort();
        double median = auditDeltas[auditDeltas.Count / 2];
        int late = auditDeltas.Count(d => d > median * 1.5);
        Diagnostics.Log(
            $"frames: {auditDeltas.Count} moving, interval median {median:0.0} ms, min {auditDeltas[0]:0.0}, max {auditDeltas[^1]:0.0}, " +
            $"late {late}; physics steps per frame {string.Join(" ", auditSteps.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}x{pair.Value}"))}; " +
            $"worst frame work {auditWorst:0.0} ms");
        auditDeltas.Clear();
        auditSteps.Clear();
        auditWorst = 0;
        auditSince = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    /// <summary>One swing is one crossing of the vertical; the rule is <see cref="SwingCounter"/>.</summary>
    /// <remarks>
    /// The last node, not a snapshot: Snapshot() allocates, and this runs on every tick.
    /// Against the anchor rather than the canvas centre, so it stays right if they part.
    /// Asks for the count to be saved at most every five minutes, and only while swings are
    /// happening — a still rope never asks.
    /// </remarks>
    private void CountSwings()
    {
        if (!swingCounter.Observe(rope.CharmOffsetFromAnchor, rope.Configuration.TotalLength))
        {
            return;
        }

        Interlocked.Increment(ref swings);
        long now = Environment.TickCount64;
        if (now - lastSwingBank >= SwingBankInterval)
        {
            lastSwingBank = now;
            SwingsToBank?.Invoke();
        }
    }

    private SwingCounter swingCounter;
    private long lastSwingBank = Environment.TickCount64;

    /// <summary>How often, at most, counted swings are saved while the charm is swinging.</summary>
    private const long SwingBankInterval = 5 * 60 * 1000;

    /// <summary>The time of day for the rope, taken by the frame loop, which owns the solver.</summary>
    public void SetTimeProfile(RopeTimeProfile profile) => Interlocked.Exchange(ref pendingTimeProfile, (int)profile + 1);

    /// <summary>Zero, or the profile plus one, waiting for the frame loop.</summary>
    private int pendingTimeProfile;

    /// <summary>Raised on the frame loop's thread when the clock jumped: changed, or woken from sleep.</summary>
    public event Action? ClockChanged;

    /// <summary>Raised on the frame loop's thread when swings are due to be saved. See CountSwings.</summary>
    public event Action? SwingsToBank;

    private void Draw(bool onlyIfMoved = false)
    {
        RopeSnapshot snapshot = rope.Snapshot();

        // A frame the eye cannot tell from the last one shown is not presented. Only the
        // frame loop's own ticks may skip; a settings change, a nudge or a refit always
        // draws, because what changed there is not the rope's position.
        if (onlyIfMoved && lastShown is not null
            && RopeBounds.LargestMove(lastShown, snapshot) * scale < InvisibleMovePixels)
        {
            return;
        }

        lastShown = snapshot;
        NoteRestAnchor(snapshot);

        // What this frame paints and what the last one did: the only pixels that can have
        // changed. Everything else on the canvas is transparent in both.
        double radius = snapshot.Charms.Count > 0 ? snapshot.Charms[^1].Radius : 10;
        Hangly.Core.Geometry.Rect? painted = RopeBounds.Of(snapshot, 2 * RopeStyleAppearanceTable.WidthFor(rope.Style, radius));
        Hangly.Core.Geometry.Rect? changed = RopeBounds.Union(lastPainted, painted);
        lastPainted = painted;

        // A charm that hangs by its own drawn rope fills the canvas below the anchor,
        // well past what the snapshot's bounds say a charm reaches, so the whole frame
        // is presented while one is on the rope.
        renderer.CanvasHeight = CanvasSize.Height;
        if (renderer.Charms.Any(charm => charm.HangsByOwnCord))
        {
            changed = null;
        }

        surface.Present(
            session => renderer.Draw(session, snapshot, rope.Style),
            new NativeMethods.Point { X = (int)Math.Round(frame.Left), Y = (int)Math.Round(frame.Top) },
            settings.Opacity,
            changed);
    }

    /// <summary>The last frame presented, to compare the next against.</summary>
    private RopeSnapshot? lastShown;

    /// <summary>
    /// A quarter of a device pixel: movement below this cannot change what anti-aliased
    /// edges look like enough to see, and is never allowed to add up past it.
    /// </summary>
    private const double InvisibleMovePixels = 0.25;

    /// <summary>What the last frame painted, in canvas points.</summary>
    private Hangly.Core.Geometry.Rect? lastPainted;

    /// <summary>Whether the rope is settled and nobody is holding it.</summary>
    private volatile bool isIdle;

    /// <summary>
    /// How long the settled loop waits between looks at the cursor: thirty a second, the
    /// rate the old one-tick-in-four throttle delivered on a 120 Hz display. A message —
    /// a settings change, a nudge, a display change — ends the wait at once.
    /// </summary>
    private const uint IdleWaitMs = 33;

    /// <summary>Where the pointer was at the previous poll, and when (Stopwatch ticks), for Reactive's approach speed.</summary>
    private (Vec2 Location, long At)? lastPoll;

    /// <summary>Interaction → Reactive: charms drift away from a pointer moving towards them.</summary>
    /// <remarks>
    /// Rides the poll that already runs for click-through, awake or settled, so it adds no
    /// wake-up of its own: a pointer far from the charm costs one distance check per charm.
    /// The speed is measured between polls — at the settled 33 ms cadence as much as at
    /// 120 Hz — and a gap longer than a tenth of a second is not a speed at all.
    /// </remarks>
    private void Repel(NativeMethods.Point cursor, Vec2 location, bool isButtonDown)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        (Vec2 Location, long At)? previous = lastPoll;
        lastPoll = (location, now);

        if (settings.Interaction != InteractionMode.Reactive || isButtonDown || rope.IsDragging
            || previous is not (Vec2 before, long at))
        {
            return;
        }

        double seconds = (now - at) / (double)System.Diagnostics.Stopwatch.Frequency;
        if (seconds <= 0 || seconds > 0.1)
        {
            return;
        }

        // On the desktop, a charm behind somebody's window does not feel the pointer.
        bool insideFrame = cursor.X >= frame.Left && cursor.X < frame.Right && cursor.Y >= frame.Top && cursor.Y < frame.Bottom;
        if (!insideFrame || IsCoveredOnDesktop(cursor))
        {
            return;
        }

        rope.Repel(location, (location - before) / seconds);
    }

    /// <summary>The last answer to "does a window cover the charm here", and when (TickCount64) it was asked.</summary>
    private (long At, bool Covered)? lastCover;

    /// <summary>Whether the overlay is on the desktop and another window is in front of it at the cursor.</summary>
    /// <remarks>
    /// Asked only once the cursor is over the charm, and remembered for a quarter of a
    /// second: this runs on every frame, and asking the window manager each time while
    /// somebody's pointer rests on the charm is the busy overlay the click-through guard
    /// exists to prevent. An Always on Top install never asks.
    /// </remarks>
    private bool IsCoveredOnDesktop(NativeMethods.Point cursor)
    {
        if (!surface.OnDesktop)
        {
            return false;
        }

        long now = Environment.TickCount64;
        if (lastCover is (long at, bool covered) && now - at < 250)
        {
            return covered;
        }

        bool isCovered = DesktopLayer.IsCovered(surface.Handle, cursor.X, cursor.Y);
        lastCover = (now, isCovered);
        return isCovered;
    }

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

        // On the desktop, a charm behind somebody's window is not under the cursor,
        // whatever the geometry says — and a drag already under way is not interrupted.
        if (hoveredCharm is not null && !rope.IsDragging && IsCoveredOnDesktop(cursor))
        {
            hoveredCharm = null;
        }
        bool overCharm = hoveredCharm is not null;

        // The cursor may only pass through when it is not over the charm — and never
        // mid-drag, or letting go while moving fast would drop the charm the instant the
        // pointer outran it.
        SetClickThrough(!overCharm && !rope.IsDragging);

        // A right-click on the charm opens its menu, on release, as menus open everywhere in Windows.
        bool isRightDown = (NativeMethods.GetAsyncKeyState(VkRbutton) & 0x8000) != 0;
        if (!isRightDown && wasRightDown && overCharm && !rope.IsDragging)
        {
            CharmRightClicked?.Invoke();
        }

        wasRightDown = isRightDown;

        Repel(cursor, location, isButtonDown);

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
            releaseVelocity = velocity;
        }
        else if (!isButtonDown && rope.IsDragging)
        {
            // The throw sound: the grabbed charm's material, louder the faster it went.
            int? slot = rope.DraggedCharmSlot;
            rope.EndDrag();
            if (Environment.GetEnvironmentVariable("HANGLY_AUDIT_SOUND") is { Length: > 0 })
            {
                Diagnostics.Log($"audio: released at {releaseVelocity.Magnitude:0} pt/s");
            }

            if (Hangly.Core.Audio.SoundPolicy.ThrowIntensity(releaseVelocity.Magnitude) is double intensity)
            {
                audio?.Play(SoundOf(slot ?? renderer.Charms.Count - 1), intensity);
            }

            releaseVelocity = Vec2.Zero;
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
        surface.HoldPlace();
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
    /// <summary>The extra height, in points, an elastic rope needs below it to stretch into; zero for Standard.</summary>
    public static double StretchRoom(double ropeLength, RopePhysics physics) =>
        physics == RopePhysics.Elastic
            ? BaseHeight * ElasticTable.CanvasAllowance(RopeConfiguration.Layout.LengthFraction, ropeLength)
            : 0;

    /// <param name="stretchOf">The length that stretches, when the canvas is made for more than the rope (a picture
    /// hanging below its rope's end); the rope's own length otherwise.</param>
    public static Size CanvasSize(
        double charmSize, double ropeLength, RopePhysics physics = RopePhysics.Standard, double? stretchOf = null)
    {
        Size room = RopeConfiguration.Layout.CanvasScale(charmSize, ropeLength);
        double height = (BaseHeight * room.Height) + StretchRoom(stretchOf ?? ropeLength, physics);

        // How far the charm's centre can get from the anchor. `unit` in
        // RopeConfiguration.Fitted always works out to BaseHeight, because the canvas is
        // BaseHeight × room.Height and it divides by room.Height — so the rope's length in
        // points is this, with no fitting to do. The drag clamp is what bounds it.
        double rope = BaseHeight * RopeConfiguration.Layout.LengthFraction * ropeLength;
        // An elastic rope can be pulled out to its stretch ceiling, so its drag circle is
        // that much wider. Standard is unchanged.
        double swing = rope * RopeConfiguration.Default.MaximumReachRatio
            * (physics == RopePhysics.Elastic ? ElasticTable.Ceiling : 1);

        // What the lowest charm reaches past its own centre. CharmStackLayout caps its
        // radius at the headroom below the rope divided by the halo extent, and that
        // headroom is TailFraction of the canvas — so this is the widest any charm on
        // this canvas can be drawn, whatever artwork it carries.
        double reach = BaseHeight * RopeConfiguration.Layout.TailFraction * charmSize
            / RopeConfiguration.Layout.CharmHaloExtent;

        return new Size(Math.Max(BaseWidth * room.Width, 2 * (swing + reach)), height);
    }
}
