//
//  NativeMethods.Display.cs
//  Hangly
//
//  Telling one monitor from another, for as long as it stays the same monitor.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Interop;

/// <summary>What the display chooser needs from Win32.</summary>
/// <remarks>
/// <b>Why the display-configuration API.</b> <c>EnumDisplayMonitors</c> hands out monitor
/// handles and GDI names like <c>\\.\DISPLAY2</c>, and both are positions, not identities:
/// they are reassigned when a display is plugged in or the arrangement changes, so a choice
/// remembered by either lands on the wrong monitor after a dock. <c>QueryDisplayConfig</c>
/// maps each GDI name to the monitor's device interface path — the same monitor on the same
/// connector keeps it across reboots — and to the name the monitor reports for itself,
/// which is what a menu should say.
/// </remarks>
internal static partial class NativeMethods
{
    internal const uint WmDisplayChange = 0x007E;
    internal const uint WmSettingChange = 0x001A;
    internal const int SpiSetWorkArea = 0x002F;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoEx(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    /// <summary>A monitor's own effective DPI, which is not the DPI of any window yet.</summary>
    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    internal const int MdtEffectiveDpi = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigPathInfo
    {
        public Luid SourceAdapterId;
        public uint SourceId;
        public uint SourceModeInfoIdx;
        public uint SourceStatusFlags;
        public Luid TargetAdapterId;
        public uint TargetId;
        public uint TargetModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public uint RefreshNumerator;
        public uint RefreshDenominator;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint TargetStatusFlags;
        public uint Flags;
    }

    /// <summary>A mode, read only to be skipped: 64 bytes, which is all that is needed.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct DisplayConfigModeInfo
    {
        public uint InfoType;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayConfigDeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
    }

    internal const uint QdcOnlyActivePaths = 0x00000002;
    internal const uint DisplayConfigDeviceInfoGetSourceName = 1;
    internal const uint DisplayConfigDeviceInfoGetTargetName = 2;

    [DllImport("user32.dll")]
    internal static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    internal static extern int DisplayConfigGetSourceName(ref DisplayConfigSourceDeviceName request);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    internal static extern int DisplayConfigGetTargetName(ref DisplayConfigTargetDeviceName request);
}
