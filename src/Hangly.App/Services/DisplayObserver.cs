//
//  DisplayObserver.cs
//  Hangly
//
//  Which displays exist, and which one the rope hangs on.
//

using Hangly.App.Interop;
using Hangly.Core.Geometry;

namespace Hangly.App.Services;

/// <param name="Bounds">The whole display, in virtual-desktop pixels.</param>
/// <param name="WorkArea">
/// What is left once the taskbar has had its share. The overlay is placed in here rather
/// than in <paramref name="Bounds"/>, so a top-docked taskbar pushes the rope down
/// instead of hiding its anchor behind itself.
/// </param>
/// <param name="Scale">The display's DPI scale, where 1 is 96 DPI.</param>
/// <param name="IsPrimary">Whether this is the display the shell considers primary.</param>
public readonly record struct DisplayInfo(Rect Bounds, Rect WorkArea, double Scale, bool IsPrimary);

/// <summary>Enumerates the attached displays.</summary>
/// <remarks>
/// Deliberately reads the system each time rather than caching. Displays are plugged in,
/// unplugged, rearranged and rescaled while the app is running, and a cached list is one
/// that puts the rope on a monitor that is no longer there.
///
/// <para>The macOS original hangs on <c>NSScreen.screens.first</c> — the primary display,
/// deliberately not the one with keyboard focus, so the overlay does not hop between
/// displays as the user switches apps. The same rule holds here: the index is a stable
/// position in the enumeration, never "wherever the mouse is".</para>
/// </remarks>
public static class DisplayObserver
{
    public static IReadOnlyList<DisplayInfo> Displays()
    {
        var displays = new List<DisplayInfo>();

        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr monitor, IntPtr _, ref NativeMethods.Rect _, IntPtr _) =>
            {
                var info = new NativeMethods.MonitorInfo
                {
                    Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>(),
                };

                if (NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    displays.Add(new DisplayInfo(
                        ToRect(info.Monitor),
                        ToRect(info.WorkArea),

                        // Per-monitor scale is read from the window once it exists; until
                        // then the system scale is the best available answer.
                        Scale: 1,
                        IsPrimary: (info.Flags & 1) != 0));
                }

                return true;
            },
            IntPtr.Zero);

        if (displays.Count == 0)
        {
            // No display is not a state the app can be in while it is being looked at,
            // but it is a state an RDP session can report for a moment. A unit rectangle
            // keeps the arithmetic finite until one arrives.
            displays.Add(new DisplayInfo(
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1040),
                1,
                true));
        }

        // Primary first, which is what makes index zero mean "the main display" whatever
        // order the system happened to enumerate them in.
        return [.. displays.OrderByDescending(display => display.IsPrimary)];
    }

    /// <summary>
    /// The display at <paramref name="index"/>, falling back to the primary when a
    /// remembered display has been unplugged.
    /// </summary>
    public static DisplayInfo DisplayAt(int index)
    {
        IReadOnlyList<DisplayInfo> displays = Displays();
        return index >= 0 && index < displays.Count ? displays[index] : displays[0];
    }

    private static Rect ToRect(NativeMethods.Rect rect) => new(
        rect.Left,
        rect.Top,
        rect.Right - rect.Left,
        rect.Bottom - rect.Top);
}
