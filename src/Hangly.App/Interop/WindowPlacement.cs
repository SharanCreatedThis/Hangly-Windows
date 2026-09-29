//
//  WindowPlacement.cs
//  Hangly
//
//  Putting a window where somebody is already looking.
//

using Hangly.App.Services;
using Microsoft.UI.Xaml;

namespace Hangly.App.Interop;

/// <summary>Sizes and centres the app's ordinary windows.</summary>
/// <remarks>
/// <b>Every window, one rule.</b> Each of these used to decide for itself: the Customize
/// window centred, the welcome card and the follow card took whatever the shell gave them,
/// which for a new top-level window is a cascade from the top-left corner. Three windows
/// belonging to one app opening in three different places reads as three different apps.
///
/// <para><b>Sizes are in physical pixels, not points.</b> <c>AppWindow.Resize</c> takes
/// pixels, so a fixed number opens a window half the intended size on a 200% display and a
/// quarter of it at 400%. Every caller here passes points and the scaling is applied once,
/// here, rather than remembered in four places.</para>
/// </remarks>
public static class WindowPlacement
{
    /// <summary>
    /// Resizes a window to a size in points and centres it on the display the pointer is on.
    /// </summary>
    /// <remarks>
    /// <b>The pointer's display, not the window's and not the primary.</b> Somebody who
    /// picks Library from the tray on their second monitor is looking at that monitor; the
    /// window opening on the primary, or wherever it was last left, means turning to find
    /// it. The same rule as macOS's <c>WindowCentering</c>.
    ///
    /// <para><b>Sized for that display's scale</b>, read from the monitor rather than from
    /// the window, which may still be on another display at another scale. Sizing first
    /// and moving after would open a window at the wrong size whenever the two differ.</para>
    ///
    /// <para>Called every time a window opens, not once when it is built: a window that
    /// hides rather than closes comes back in the middle of the display in use, not where
    /// it was left.</para>
    /// </remarks>
    public static void SizeAndCentre(Window window, double widthInPoints, double heightInPoints)
    {
        try
        {
            Microsoft.UI.Windowing.DisplayArea area = UnderPointer(window);
            double scale = ScaleOf(area, window);
            var size = new Windows.Graphics.SizeInt32(
                (int)Math.Round(widthInPoints * scale),
                (int)Math.Round(heightInPoints * scale));

            Windows.Graphics.RectInt32 work = area.WorkArea;
            (int x, int y) = Hangly.Core.Geometry.Centring.Origin(work.X, work.Y, work.Width, work.Height, size.Width, size.Height);

            // Moved and sized in one call, so the window never spends a frame on one
            // display at the other's size.
            window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, size.Width, size.Height));
        }
        catch (Exception exception)
        {
            // A window in the wrong place is still a usable window.
            Diagnostics.Failure("sizing a window", exception);
        }
    }

    /// <summary>The display the pointer is on; the window's own if the pointer cannot be read.</summary>
    /// <remarks>
    /// The <em>work area</em> of it is what windows are centred in, so a taskbar does not
    /// push the window down by its own height and leave it looking low.
    /// </remarks>
    private static Microsoft.UI.Windowing.DisplayArea UnderPointer(Window window)
    {
        if (NativeMethods.GetCursorPos(out NativeMethods.Point pointer))
        {
            return Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
                new Windows.Graphics.PointInt32(pointer.X, pointer.Y),
                Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        }

        return Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            window.AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
    }

    /// <summary>The display's own scale, falling back to the window's.</summary>
    private static double ScaleOf(Microsoft.UI.Windowing.DisplayArea area, Window window)
    {
        IntPtr monitor = Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(area.DisplayId);
        if (monitor != IntPtr.Zero
            && NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out uint dpi, out _) == 0
            && dpi > 0)
        {
            return dpi / 96.0;
        }

        double scale = NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)) / 96.0;
        return scale > 0 ? scale : 1;
    }

    /// <summary>Lifts <paramref name="window"/> out of the taskbar if it is minimised.</summary>
    /// <remarks>
    /// <c>AppWindow.Show</c> does not do this, and neither does <c>Activate</c>: a
    /// minimised window stays minimised through both, so a menu entry that opens the
    /// window looked like it had done nothing. <c>OverlappedPresenter.State</c> was tried
    /// first and does not report the minimisation when it came from outside WinUI, which
    /// includes the taskbar button, so the question is asked of the window itself.
    /// </remarks>
    public static void Restore(Window window)
    {
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (NativeMethods.IsIconic(handle))
        {
            // SW_RESTORE. Puts the window back at the size and place it had before,
            // which for this one is the only size it has.
            NativeMethods.ShowWindow(handle, 9);
        }
    }

    /// <summary>Puts <paramref name="window"/> in front and gives it the keyboard.</summary>
    /// <remarks>
    /// <c>Window.Activate</c> activates within the app, which is enough when the app is
    /// already in front. When it is not — Hangly opened again from the Start menu, so the
    /// request arrives from another process — the window has to ask Windows directly.
    /// <c>SetForegroundWindow</c> only succeeds when this process has been given the right,
    /// and the copy that was just launched hands it over with
    /// <c>AllowSetForegroundWindow</c> before it leaves (see <c>SingleInstance</c>), so
    /// this cannot steal focus from something the person is using.
    /// </remarks>
    public static void BringToFront(Window window)
    {
        Restore(window);
        window.Activate();
        NativeMethods.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
    }

    /// <summary>Fixes a card at the size it was given.</summary>
    /// <remarks>
    /// The welcome and follow cards are laid out for one size and have nothing to do with
    /// extra room, so dragging their edges only breaks the composition. It is also what
    /// keeps the support sheet predictable: a ContentDialog is bounded by the window it
    /// opens over, and that sheet is sized from the host's height.
    /// </remarks>
    public static void FixSize(Window window)
    {
        if (window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }
    }
}
