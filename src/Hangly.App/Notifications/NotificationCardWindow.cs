//
//  NotificationCardWindow.cs
//  Hangly
//
//  The small transparent window the card and the bell hang in, under the charm.
//

using System.Numerics;
using System.Runtime.InteropServices;
using Hangly.App.Interop;
using Hangly.App.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using Windows.Graphics.DirectX;

namespace Hangly.App.Notifications;

/// <summary>Keeps the card under the charm, on screen only while there is something to show.</summary>
/// <remarks>
/// <b>A window of its own</b>, as on macOS, and built the way the overlay is (<see cref="Overlay.LayeredOverlaySurface"/>):
/// a layered Win32 window whose content is a Win2D swap chain in DirectComposition, so frames go straight from the GPU
/// to the compositor — the float costs a small draw thirty times a second and no read-back. WinUI windows cannot be
/// transparent around the card, which is the reason the overlay stopped being one.
///
/// <para><b>Click-through except over the card.</b> A composited window takes clicks across its whole rectangle, so
/// <c>WS_EX_TRANSPARENT</c> is toggled by where the pointer is, read on the same tick that draws — the overlay's own
/// technique, with its guard against writing the style when nothing changed. It never activates: clicking a card
/// does not take focus from whatever was being typed into.</para>
///
/// <para><b>Motion</b> is macOS's: in by fading and rising ten points on a damped spring (response 0.42, damping
/// 0.72); out by shrinking to 96%, fading and sliding eight points down in 250 ms; in between a ±1.5-point float on
/// a 4.2-second sine. The bell floats for its first minute and then rests. Animation effects off in Windows: fades
/// only, and no float.</para>
/// </remarks>
internal sealed class NotificationCardWindow : IDisposable
{
    public const float WindowWidth = 360;
    public const float WindowHeight = 340;
    public const float TopMargin = 12;
    public const float Gap = 4;
    private const string ClassName = "HanglyNotificationCard";
    private static readonly NativeMethods.WindowProc Procedure = OnMessage;
    private static readonly Dictionary<IntPtr, NotificationCardWindow> Instances = [];
    private static bool isRegistered;

    private readonly DispatcherQueue queue;
    private readonly DispatcherQueueTimer frames;
    private readonly CanvasDevice device = CanvasDevice.GetSharedDevice();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly List<(CardLayout Layout, double Since)> leaving = [];

    private IntPtr handle;
    private IntPtr compositionDevice;
    private IntPtr compositionTarget;
    private IntPtr compositionVisual;
    private CanvasSwapChain? swapChain;
    private CardLayout? current;
    private Guid? currentToken;
    private bool currentIsBell;
    private double enteredAt;
    private double floatSince;
    private double scale = 1;
    private (int X, int Y) origin = (int.MinValue, int.MinValue);
    private bool isClickThrough = true;
    private bool isShown;
    private CardCommand? hovered;
    private CardCommand? pressed;
    private bool cardHovered;
    private CardTheme theme = CardTheme.Dark;

    public NotificationCardWindow(DispatcherQueue queue)
    {
        this.queue = queue;
        frames = queue.CreateTimer();
        frames.Tick += (_, _) => Frame();
    }

    /// <summary>A press on the card. Raised on the XAML thread.</summary>
    public event Action<CardCommand>? Command;

    /// <summary>The pointer arrived on or left the card. Raised on the XAML thread.</summary>
    public event Action<bool>? HoverChanged;

    /// <summary>Where the card hangs from — the charm's resting bottom-centre, in desktop pixels — and the display's scale.</summary>
    public (double X, double Y, double Scale)? Anchor { get; set; }

    /// <summary>Shows <paramref name="card"/>, or the bell when it is null and <paramref name="bellCount"/> is set, or nothing.</summary>
    public void Show(CardPresentation? card, bool showsBell, int bellCount)
    {
        bool wantsBell = card is null && showsBell;
        double now = clock.Elapsed.TotalSeconds;
        if (card is null && !wantsBell)
        {
            Leave(now);
        }
        else
        {
            if (!EnsureWindow())
            {
                return;
            }

            theme = ReadTheme();
            // The same card with new words (Update Now's progress), or the bell with a new count: redrawn in place.
            // Anything else leaves, and the new one arrives.
            bool same = card is not null ? currentToken == card.Token : currentIsBell;
            CardLayout layout = NotificationCardPainter.Layout(device, card, bellCount);
            if (same)
            {
                current?.Dispose();
            }
            else
            {
                Leave(now);
                enteredAt = now;
                floatSince = now;
            }

            current = layout;
            currentToken = card?.Token;
            currentIsBell = wantsBell;
            Place();
            if (!isShown)
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SwShowna);
                isShown = true;
            }

            Raise();
            LogTargets();
        }

        Frame();
        Run();
    }

    /// <summary>For the screenshot and click scripts only (<c>HANGLY_AUDIT_NOTIFICATIONS</c>): where each button is.</summary>
    private void LogTargets()
    {
        if (current is null || Environment.GetEnvironmentVariable("HANGLY_AUDIT_NOTIFICATIONS") is not { Length: > 0 })
        {
            return;
        }

        Vector2 at = CardOrigin(current);
        if (Anchor is (double ax, double ay, _))
        {
            Diagnostics.Log($"notification anchor at {ax:0},{ay:0}");
        }

        foreach ((Windows.Foundation.Rect bounds, CardCommand command) in current.Targets)
        {
            int x = origin.X + (int)Math.Round((at.X + bounds.X + (bounds.Width / 2)) * scale);
            int y = origin.Y + (int)Math.Round((at.Y + bounds.Y + (bounds.Height / 2)) * scale);
            Diagnostics.Log($"notification target {command} at {x},{y}");
        }
    }

    private void Leave(double now)
    {
        if (current is not null)
        {
            leaving.Add((current, now));
        }

        current = null;
        currentToken = null;
        currentIsBell = false;
    }

    // Placing

    /// <summary>Under the charm's resting place, inside its display's work area.</summary>
    public void Place()
    {
        if (handle == IntPtr.Zero || Anchor is not (double ax, double ay, double s))
        {
            return;
        }

        bool rescaled = Math.Abs(s - scale) > 0.001;
        scale = s;
        int width = (int)Math.Round(WindowWidth * scale), height = (int)Math.Round(WindowHeight * scale);
        int x = (int)Math.Round(ax - (width / 2.0));
        int y = (int)Math.Round(ay + ((Gap - TopMargin) * scale));
        foreach (DisplayInfo display in DisplayObserver.Displays())
        {
            if (ax >= display.Bounds.Left && ax < display.Bounds.Right && ay >= display.Bounds.Top && ay < display.Bounds.Bottom)
            {
                x = (int)Math.Clamp(x, display.WorkArea.Left, Math.Max(display.WorkArea.Left, display.WorkArea.Right - width));
                y = (int)Math.Clamp(y, display.WorkArea.Top, Math.Max(display.WorkArea.Top, display.WorkArea.Bottom - height));
                break;
            }
        }

        if (rescaled || swapChain is null || swapChain.SizeInPixels.Width != width)
        {
            Resize(width, height);
        }

        if ((x, y) != origin || rescaled)
        {
            origin = (x, y);
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, x, y, width, height, NativeMethods.SwpNozorder | NativeMethods.SwpNoactivate);
        }
    }

    /// <summary>Appearance → Window, as the overlay has it: On the Desktop rather than Always on Top.</summary>
    public bool OnDesktop { get; set; }

    /// <summary>Where the overlay is: above every window, or just above the desktop with the charm.</summary>
    public void Raise()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (OnDesktop)
        {
            Overlay.DesktopLayer.HoldAboveDesktop(handle);
            return;
        }

        NativeMethods.SetWindowPos(
            handle, NativeMethods.HwndTopmost, 0, 0, 0, 0, NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNoactivate);
    }

    // Drawing

    private void Run()
    {
        bool animating = leaving.Count > 0 || (current is not null && (clock.Elapsed.TotalSeconds - enteredAt) < 0.7);
        bool indeterminate = current?.Progress is not null && current.ProgressFraction is null;
        bool floating = current is not null && !SystemMotion.ReducesMotion
            && (!currentIsBell || clock.Elapsed.TotalSeconds - floatSince < 60);
        bool visible = current is not null;
        if (!animating && !visible)
        {
            frames.Stop();
            if (isShown)
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SwHide);
                isShown = false;
                SetClickThrough(true);
            }

            return;
        }

        // Fast while something moves; thirty a second for the float and the pointer; the pointer alone otherwise.
        frames.Interval = TimeSpan.FromMilliseconds(animating || indeterminate ? 16 : floating ? 33 : 50);
        if (!frames.IsRunning)
        {
            frames.Start();
        }
    }

    private void Frame()
    {
        if (swapChain is null)
        {
            return;
        }

        double now = clock.Elapsed.TotalSeconds;
        PollPointer();
        bool reduced = SystemMotion.ReducesMotion;
        using (CanvasDrawingSession session = swapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent))
        {
            for (int index = leaving.Count - 1; index >= 0; index--)
            {
                (CardLayout layout, double since) = leaving[index];
                double p = (now - since) / 0.25;
                if (p >= 1)
                {
                    layout.Dispose();
                    leaving.RemoveAt(index);
                    continue;
                }

                double eased = p * p;
                DrawCard(session, layout, (float)(1 - eased), reduced ? 1 : (float)(1 - (0.04 * eased)), reduced ? 0 : (float)(8 * eased), hover: false, now);
            }

            if (current is not null)
            {
                double t = now - enteredAt;
                float opacity = (float)Math.Min(1, t / 0.2);
                opacity = 1 - ((1 - opacity) * (1 - opacity));
                float rise = reduced ? 0 : (float)(10 * Spring(t));
                bool floats = !reduced && (!currentIsBell || now - floatSince < 60);
                float drift = floats ? (float)(1.5 * Math.Sin(2 * Math.PI * (now - floatSince) / 4.2)) : 0;
                DrawCard(session, current, opacity, 1, rise + drift, cardHovered, now);
            }
        }

        swapChain.Present(0);
        Run();
    }

    /// <summary>A damped spring from 1 to 0: response 0.42 s, damping 0.72 — macOS's entrance.</summary>
    private static double Spring(double t)
    {
        const double omega = 2 * Math.PI / 0.42, zeta = 0.72;
        double damped = omega * Math.Sqrt(1 - (zeta * zeta));
        double value = Math.Exp(-zeta * omega * t) * (Math.Cos(damped * t) + (zeta * omega / damped * Math.Sin(damped * t)));
        return t > 1.2 ? 0 : value;
    }

    private void DrawCard(CanvasDrawingSession session, CardLayout layout, float opacity, float cardScale, float offsetY, bool hover, double now)
    {
        Vector2 at = CardOrigin(layout);
        var centre = new Vector2((float)layout.Bounds.Width / 2, (float)layout.Bounds.Height / 2);
        session.Transform = Matrix3x2.CreateScale(cardScale, centre) * Matrix3x2.CreateTranslation(at.X, at.Y + offsetY);
        using (session.CreateLayer(Math.Clamp(opacity, 0, 1)))
        {
            NotificationCardPainter.Draw(session, layout, theme, hover ? hovered : null, hover ? pressed : null, hover, now);
        }

        session.Transform = Matrix3x2.Identity;
    }

    /// <summary>The card's top-left in the window, in points: centred, below the room for its rise.</summary>
    private static Vector2 CardOrigin(CardLayout layout) => new((WindowWidth - (float)layout.Bounds.Width) / 2, TopMargin);

    // The pointer

    private void PollPointer()
    {
        if (current is null || handle == IntPtr.Zero || !NativeMethods.GetCursorPos(out NativeMethods.Point cursor))
        {
            SetClickThrough(true);
            return;
        }

        Vector2 at = CardOrigin(current);
        double x = ((cursor.X - origin.X) / scale) - at.X, y = ((cursor.Y - origin.Y) / scale) - at.Y;
        bool inside = x >= 0 && y >= 0 && x < current.Bounds.Width && y < current.Bounds.Height;
        CardCommand? target = null;
        if (inside)
        {
            foreach ((Windows.Foundation.Rect bounds, CardCommand command) in current.Targets)
            {
                if (bounds.Contains(new Windows.Foundation.Point(x, y)) && (command != CardCommand.Close || cardHovered))
                {
                    target = command;
                    break;
                }
            }
        }

        hovered = target;
        if (inside != cardHovered)
        {
            cardHovered = inside;
            if (!currentIsBell)
            {
                HoverChanged?.Invoke(inside);
            }
        }

        SetClickThrough(!inside && pressed is null);
    }

    private void SetClickThrough(bool enabled)
    {
        if (enabled == isClickThrough || handle == IntPtr.Zero)
        {
            return;
        }

        uint style = NativeMethods.GetExtendedStyle(handle);
        if (style == 0)
        {
            return;
        }

        isClickThrough = enabled;
        NativeMethods.SetExtendedStyle(handle, enabled ? style | NativeMethods.WsExTransparent : style & ~NativeMethods.WsExTransparent);
    }

    private IntPtr HandleMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmMouseActivate:
                return new IntPtr(MaNoActivate);
            case WmSetCursor when hovered is not null:
                SetCursor(LoadCursor(IntPtr.Zero, IdcHand));
                return new IntPtr(1);
            case WmLButtonDown:
                PollPointer();
                pressed = hovered;
                Frame();
                return IntPtr.Zero;
            case WmLButtonUp:
                PollPointer();
                CardCommand? released = hovered;
                bool click = pressed is not null && pressed == released;
                pressed = null;
                Frame();
                if (click)
                {
                    queue.TryEnqueue(() => Command?.Invoke(released!.Value));
                }

                return IntPtr.Zero;
            case NativeMethods.WmSettingChange:
                theme = ReadTheme();
                Frame();
                break;
            default:
                break;
        }

        return NativeMethods.DefWindowProc(handle, message, wParam, lParam);
    }

    /// <summary>Windows' app theme: Settings → Personalisation → Colours → Choose your app mode.</summary>
    private static CardTheme ReadTheme()
    {
        try
        {
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 1 ? CardTheme.Light : CardTheme.Dark;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return CardTheme.Dark;
        }
    }

    // The window

    private bool EnsureWindow()
    {
        if (handle != IntPtr.Zero)
        {
            return true;
        }

        try
        {
            if (!isRegistered)
            {
                var windowClass = new NativeMethods.WndClassEx
                {
                    Size = Marshal.SizeOf<NativeMethods.WndClassEx>(),
                    WndProc = Marshal.GetFunctionPointerForDelegate(Procedure),
                    Instance = NativeMethods.GetModuleHandle(null),
                    ClassName = ClassName,
                };
                NativeMethods.RegisterClassEx(ref windowClass);
                isRegistered = true;
            }

            handle = NativeMethods.CreateWindowEx(
                NativeMethods.WsExLayered | NativeMethods.WsExToolwindow | NativeMethods.WsExTopmost
                    | NativeMethods.WsExNoactivate | NativeMethods.WsExTransparent | NativeMethods.WsExNoRedirectionBitmap,
                ClassName,
                "Hangly notification",
                NativeMethods.WsPopup,
                0,
                0,
                1,
                1,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.GetModuleHandle(null),
                IntPtr.Zero);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
            }

            Instances[handle] = this;
            NativeMethods.SetLayeredWindowAttributes(handle, 0, 255, NativeMethods.LwaAlpha);
            compositionDevice = DirectComposition.CreateDevice(device);
            compositionTarget = DirectComposition.CreateTarget(compositionDevice, handle);
            compositionVisual = DirectComposition.CreateVisual(compositionDevice);
            DirectComposition.SetRoot(compositionTarget, compositionVisual);
            Diagnostics.Log("notification card window created");
            return true;
        }
        catch (Exception exception)
        {
            // The card is a nicety: without it, notifications still reach the Center and the tray.
            Diagnostics.Failure("notification card window", exception);
            Dispose();
            return false;
        }
    }

    private void Resize(int width, int height)
    {
        var replacement = new CanvasSwapChain(
            device, (float)(width / scale), (float)(height / scale), (float)(96 * scale),
            DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, CanvasAlphaMode.Premultiplied);
        DirectComposition.ResetScale(replacement);
        DirectComposition.SetContent(compositionVisual, replacement);
        DirectComposition.Commit(compositionDevice);
        swapChain?.Dispose();
        swapChain = replacement;
    }

    private static IntPtr OnMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam) =>
        Instances.TryGetValue(hWnd, out NotificationCardWindow? window)
            ? window.HandleMessage(message, wParam, lParam)
            : NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);

    public void Dispose()
    {
        frames.Stop();
        current?.Dispose();
        current = null;
        foreach ((CardLayout layout, _) in leaving)
        {
            layout.Dispose();
        }

        leaving.Clear();
        swapChain?.Dispose();
        swapChain = null;
        DirectComposition.Release(ref compositionVisual);
        DirectComposition.Release(ref compositionTarget);
        DirectComposition.Release(ref compositionDevice);
        if (handle != IntPtr.Zero)
        {
            Instances.Remove(handle);
            NativeMethods.DestroyWindow(handle);
            handle = IntPtr.Zero;
        }
    }

    private const uint WmMouseActivate = 0x0021;
    private const uint WmSetCursor = 0x0020;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const int MaNoActivate = 3;
    private const int IdcHand = 32649;

    [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static extern IntPtr LoadCursor(IntPtr instance, int cursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr cursor);
}
