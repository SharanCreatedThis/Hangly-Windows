//
//  OverlayWindow.xaml.cs
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
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

// Both names exist in Windows.Foundation as well, and the WinUI namespaces bring that in.
// Aliased rather than fully qualified at each use: the window works in the solver's
// coordinate space throughout, and the two types must never be silently swapped for the
// XAML ones, which measure different things.
using Rect = Hangly.Core.Geometry.Rect;
using Size = Hangly.Core.Geometry.Size;

namespace Hangly.App.Overlay;

/// <summary>The overlay: one window, one rope, no chrome.</summary>
/// <remarks>
/// <b>Why a plain Window and not a backdrop.</b> WinUI has no equivalent of the
/// original's <c>NSPanel</c>, so the five properties that panel gave for free are
/// assembled by hand: transparency from a XAML root with no backdrop over a Win2D
/// swapchain that clears to transparent, and the other four from extended window styles
/// in <see cref="NativeMethods"/>. The mapping is tabulated there.
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
public sealed partial class OverlayWindow : Window
{
    private readonly RopeSimulation rope;
    private readonly RopeRenderer renderer;
    private readonly SimulationClock clock = new();
    private readonly IntPtr handle;

    private OverlaySettings settings;
    private bool isClickThrough = true;
    private bool wasButtonDown;
    private Vec2 lastCursor;
    private Rect frame;
    private double scale = 1;

    public OverlayWindow(
        OverlaySettings settings,
        RopeSimulation rope,
        RopeRenderer renderer,
        IReadOnlyList<CharmDescriptor> charms)
    {
        this.settings = settings;
        this.rope = rope;
        this.renderer = renderer;

        // What hangs on the rope, told to both halves at once: the solver needs the mass
        // and the radius, the renderer needs the artwork and the palette, and they must be
        // the same list or the charm will be drawn somewhere the rope is not carrying it.
        renderer.Charms = charms;
        rope.SetCharmStack([.. charms.Select(charm => charm.Metrics)]);
        rope.SetBeads([.. charms.Select(charm => charm.Beads)]);

        InitializeComponent();

        handle = WindowNative.GetWindowHandle(this);

        // No title bar, no backdrop, no shadow: the window is the rope and nothing else.
        SystemBackdrop = null;
        ExtendsContentIntoTitleBar = true;
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        ApplyExtendedStyles();
        ApplyTransparency();

        clock.Tick += OnTick;
        Closed += (_, _) => clock.Stop();
    }

    /// <summary>Applies a settings change without rebuilding anything.</summary>
    public void Apply(OverlaySettings updated)
    {
        settings = updated;
        rope.SetStyle(updated.RopeStyle);
        Reposition();
        rope.SetCharmSize(updated.CharmSize, CanvasSize);
        rope.SetRopeLength(updated.RopeLength, CanvasSize);
        Root.Opacity = updated.Opacity;
    }

    public void Begin()
    {
        Reposition();
        rope.Start();
        clock.Start();
        Activate();

        // Activate() would ordinarily raise and focus the window. The style bits say it
        // may not take focus, and this puts it above everything without asking for any.
        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove | NativeMethods.SwpNosize
                | NativeMethods.SwpNoactivate | NativeMethods.SwpShowwindow);
    }

    private Size CanvasSize => new(frame.Width / scale, frame.Height / scale);

    private void ApplyExtendedStyles()
    {
        uint style = NativeMethods.GetExtendedStyle(handle);
        style |= NativeMethods.WsExToolwindow // no taskbar button, no Alt-Tab entry
            | NativeMethods.WsExTopmost // above every other application
            | NativeMethods.WsExNoactivate // clicking it never steals focus
            | NativeMethods.WsExTransparent; // click-through until the cursor finds the charm

        // Deliberately NOT WS_EX_LAYERED. A layered window expects its pixels through
        // UpdateLayeredWindow, which a WinUI swapchain never calls, and setting the style
        // without ever supplying those pixels is what leaves the window opaque. The alpha
        // here comes from DWM compositing the swapchain instead — see ApplyTransparency.
        style &= ~NativeMethods.WsExLayered;
        NativeMethods.SetExtendedStyle(handle, style);
    }

    /// <summary>Makes the window genuinely transparent rather than merely backdrop-less.</summary>
    /// <remarks>
    /// Three things have to agree, and all three are necessary: the XAML root paints
    /// nothing, the window has no system backdrop, and DWM is told the glass frame covers
    /// the entire client area. With only the first two the window still carries an opaque
    /// backing and paints white — which is what the first build that ran on Windows did,
    /// a white rectangle with a rope drawn inside it.
    /// </remarks>
    private void ApplyTransparency()
    {
        var margins = new NativeMethods.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(handle, ref margins);

        // Windows 11 rounds every window's corners. An ornament hanging on the desktop is
        // not a window and must not look like one.
        int corners = NativeMethods.DwmwcpDoNotRound;
        NativeMethods.DwmSetWindowAttribute(
            handle,
            NativeMethods.DwmwaWindowCornerPreference,
            ref corners,
            sizeof(int));

        // The XAML tree must paint nothing at all. A Transparent brush is still a brush
        // the compositor has to honour; null is the absence of one.
        Root.Background = null;
    }

    /// <summary>Puts the window where the settings say, on the display they name.</summary>
    private void Reposition()
    {
        DisplayInfo display = DisplayObserver.DisplayAt(settings.DisplayIndex);
        scale = NativeMethods.GetDpiForWindow(handle) / 96.0;
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

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            (int)Math.Round(frame.Left),
            (int)Math.Round(frame.Top),
            (int)Math.Round(frame.Width),
            (int)Math.Round(frame.Height)));

        rope.Resize(CanvasSize);
    }

    /// <summary>One display frame: poll the cursor, step the physics, ask for a redraw.</summary>
    private void OnTick(double deltaTime)
    {
        PollPointer();
        rope.Step(deltaTime);

        // A settled rope is a still image. Stop redrawing it, and drop the tick rate —
        // the clock keeps running because the same tick is what notices the cursor
        // arriving over the charm.
        clock.SetThrottled(rope.IsSleeping && !rope.IsDragging);
        if (!rope.IsSleeping || rope.IsDragging)
        {
            Canvas.Invalidate();
        }
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
        uint style = NativeMethods.GetExtendedStyle(handle);
        style = enabled
            ? style | NativeMethods.WsExTransparent
            : style & ~NativeMethods.WsExTransparent;
        NativeMethods.SetExtendedStyle(handle, style);
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args) =>
        renderer.Draw(args.DrawingSession, rope.Snapshot(), rope.Style);
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
    /// </remarks>
    public static Size CanvasSize(double charmSize, double ropeLength)
    {
        Size room = RopeConfiguration.Layout.CanvasScale(charmSize, ropeLength);
        return new Size(BaseWidth * room.Width, BaseHeight * room.Height);
    }
}
