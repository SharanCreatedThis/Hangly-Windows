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
/// <param name="Scale">The display's own DPI scale, where 1 is 96 DPI.</param>
/// <param name="IsPrimary">Whether this is the display the shell considers primary.</param>
/// <param name="Id">Stable identity; see <see cref="DisplayIdentity"/>.</param>
/// <param name="Name">What the monitor calls itself.</param>
public readonly record struct DisplayInfo(
    Rect Bounds,
    Rect WorkArea,
    double Scale,
    bool IsPrimary,
    string Id = "",
    string Name = "")
{
    public DisplayIdentity Identity => new(Id, Name, IsPrimary);
}

/// <summary>Enumerates the attached displays.</summary>
/// <remarks>
/// Deliberately reads the system each time rather than caching. Displays are plugged in,
/// unplugged, rearranged and rescaled while the app is running, and a cached list is one
/// that puts the rope on a monitor that is no longer there.
///
/// <para>The rope hangs on the display chosen by its stable id, or the main display when
/// none is chosen or the chosen one is unplugged — never "wherever the mouse is", so it
/// does not hop between displays as the user switches apps. The rule is
/// <see cref="DisplayChoice"/>'s and is the same on macOS.</para>
/// </remarks>
public static class DisplayObserver
{
    public static IReadOnlyList<DisplayInfo> Displays()
    {
        var displays = new List<DisplayInfo>();
        Dictionary<string, (string Id, string Name)> targets = Targets();

        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr monitor, IntPtr _, ref NativeMethods.Rect _, IntPtr _) =>
            {
                var info = new NativeMethods.MonitorInfoEx
                {
                    Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                };

                if (NativeMethods.GetMonitorInfoEx(monitor, ref info))
                {
                    // The monitor's own scale, not the window's: a window moving to a
                    // display at a different scale has to be sized for where it is going.
                    double scale = NativeMethods.GetDpiForMonitor(
                        monitor, NativeMethods.MdtEffectiveDpi, out uint dpi, out _) == 0 && dpi > 0
                        ? dpi / 96.0
                        : 1;

                    (string id, string name) = targets.TryGetValue(info.DeviceName ?? string.Empty, out var target)
                        ? target
                        : (info.DeviceName ?? string.Empty, string.Empty);

                    displays.Add(new DisplayInfo(
                        ToRect(info.Monitor),
                        ToRect(info.WorkArea),
                        scale,
                        IsPrimary: (info.Flags & 1) != 0,
                        id,
                        name));
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

    /// <summary>The display the settings choose, as <see cref="DisplayChoice"/> resolves it.</summary>
    public static DisplayInfo Chosen(string? displayId, int legacyIndex)
    {
        IReadOnlyList<DisplayInfo> displays = Displays();
        return displays[DisplayChoice.Resolve([.. displays.Select(display => display.Identity)], displayId, legacyIndex)];
    }

    /// <summary>Each attached monitor's stable id and name, by GDI device name.</summary>
    /// <remarks>
    /// Empty when the display-configuration API is unavailable — a remote session can say
    /// so — and then every display is known by its GDI name instead, which still works
    /// until the arrangement changes.
    /// </remarks>
    private static Dictionary<string, (string Id, string Name)> Targets()
    {
        var targets = new Dictionary<string, (string Id, string Name)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QdcOnlyActivePaths, out uint pathCount, out uint modeCount) != 0)
            {
                return targets;
            }

            var paths = new NativeMethods.DisplayConfigPathInfo[pathCount];
            var modes = new NativeMethods.DisplayConfigModeInfo[modeCount];
            if (NativeMethods.QueryDisplayConfig(NativeMethods.QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                return targets;
            }

            foreach (NativeMethods.DisplayConfigPathInfo path in paths.AsSpan(0, (int)pathCount))
            {
                var source = new NativeMethods.DisplayConfigSourceDeviceName
                {
                    Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                    {
                        Type = NativeMethods.DisplayConfigDeviceInfoGetSourceName,
                        Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DisplayConfigSourceDeviceName>(),
                        AdapterId = path.SourceAdapterId,
                        Id = path.SourceId,
                    },
                };
                var target = new NativeMethods.DisplayConfigTargetDeviceName
                {
                    Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                    {
                        Type = NativeMethods.DisplayConfigDeviceInfoGetTargetName,
                        Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DisplayConfigTargetDeviceName>(),
                        AdapterId = path.TargetAdapterId,
                        Id = path.TargetId,
                    },
                };

                if (NativeMethods.DisplayConfigGetSourceName(ref source) == 0
                    && NativeMethods.DisplayConfigGetTargetName(ref target) == 0
                    && !string.IsNullOrEmpty(source.ViewGdiDeviceName)
                    && !string.IsNullOrEmpty(target.MonitorDevicePath))
                {
                    // A mirrored pair shares one source; the first monitor found names it.
                    targets.TryAdd(source.ViewGdiDeviceName, (target.MonitorDevicePath, target.MonitorFriendlyDeviceName ?? string.Empty));
                }
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older than Windows 7, or a stripped-down session; GDI names it is.
        }

        return targets;
    }

    private static Rect ToRect(NativeMethods.Rect rect) => new(
        rect.Left,
        rect.Top,
        rect.Right - rect.Left,
        rect.Bottom - rect.Top);
}
