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
    /// <summary>Resizes a window to a size in points and centres it on its display.</summary>
    public static void SizeAndCentre(Window window, double widthInPoints, double heightInPoints)
    {
        try
        {
            IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            double scale = NativeMethods.GetDpiForWindow(handle) / 96.0;
            if (scale <= 0)
            {
                scale = 1;
            }

            var size = new Windows.Graphics.SizeInt32(
                (int)Math.Round(widthInPoints * scale),
                (int)Math.Round(heightInPoints * scale));

            window.AppWindow.Resize(size);
            Centre(window, size);
        }
        catch (Exception exception)
        {
            // A window in the wrong place is still a usable window.
            Diagnostics.Failure("sizing a window", exception);
        }
    }

    /// <summary>Centres a window of a known size on the display it opened on.</summary>
    /// <remarks>
    /// The <em>work area</em> rather than the whole display, so a taskbar does not push
    /// the window down by its own height and leave it looking low.
    /// </remarks>
    public static void Centre(Window window, Windows.Graphics.SizeInt32 size)
    {
        try
        {
            Microsoft.UI.Windowing.DisplayArea area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                window.AppWindow.Id,
                Microsoft.UI.Windowing.DisplayAreaFallback.Primary);

            Windows.Graphics.RectInt32 work = area.WorkArea;

            window.AppWindow.Move(new Windows.Graphics.PointInt32(
                work.X + Math.Max(0, (work.Width - size.Width) / 2),
                work.Y + Math.Max(0, (work.Height - size.Height) / 2)));
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("centring a window", exception);
        }
    }
}
