//
//  NativeMethods.cs
//  Hangly
//
//  The Win32 surface the overlay needs, and nothing else.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Interop;

/// <summary>The Win32 calls the window layer is built from.</summary>
/// <remarks>
/// Deliberately small. The macOS original maps each overlay requirement onto one
/// <c>NSPanel</c> property; Windows has no panel, so each requirement maps onto one
/// extended window style or one call here. Keeping them in one file is what makes that
/// table checkable:
///
/// <list type="table">
/// <item><term>Transparent</term><description>a transparent XAML root over a swapchain
/// with no backdrop — see <c>OverlayWindow</c></description></item>
/// <item><term>Above all apps</term><description><c>WS_EX_TOPMOST</c> +
/// <see cref="SetWindowPos"/> to <see cref="HwndTopmost"/></description></item>
/// <item><term>Click-through</term><description><c>WS_EX_TRANSPARENT</c>, toggled as the
/// cursor enters the charm</description></item>
/// <item><term>Never steals focus</term><description><c>WS_EX_NOACTIVATE</c> +
/// <see cref="SwpNoactivate"/></description></item>
/// <item><term>No taskbar button</term><description><c>WS_EX_TOOLWINDOW</c></description></item>
/// </list>
/// </remarks>
internal static partial class NativeMethods
{
    internal const int GwlExstyle = -20;

    internal const uint WsExTransparent = 0x00000020;
    internal const uint WsExToolwindow = 0x00000080;
    internal const uint WsExTopmost = 0x00000008;
    internal const uint WsExLayered = 0x00080000;
    internal const uint WsExNoactivate = 0x08000000;

    internal static readonly IntPtr HwndTopmost = new(-1);

    internal const uint SwpNosize = 0x0001;
    internal const uint SwpNomove = 0x0002;
    internal const uint SwpNoactivate = 0x0010;
    internal const uint SwpShowwindow = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    /// <summary>
    /// Reads an extended window style. Two entry points because the 64-bit form does not
    /// exist on 32-bit Windows; every caller goes through <see cref="GetExtendedStyle"/>.
    /// </summary>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    internal static uint GetExtendedStyle(IntPtr hWnd) => (uint)GetWindowLongPtr(hWnd, GwlExstyle).ToInt64();

    internal static void SetExtendedStyle(IntPtr hWnd, uint style) =>
        SetWindowLongPtr(hWnd, GwlExstyle, new IntPtr(style));

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    /// <summary>
    /// Where the cursor is, in virtual-desktop pixels.
    /// </summary>
    /// <remarks>
    /// Polled once per frame rather than hooked, which is the same choice the macOS build
    /// makes and for the same reason: a low-level mouse hook is a global input filter, it
    /// needs a message pump that never stalls, and a screen ornament has no business
    /// installing one.
    /// </remarks>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point lpPoint);

    /// <summary>Whether a mouse button is currently down, for the drag gesture.</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetAsyncKeyState")]
    internal static partial short GetAsyncKeyState(int vKey);

    internal const int VkLbutton = 0x01;

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint GetDpiForWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    /// <summary>
    /// Extends the glass frame over the whole client area, which is what actually lets a
    /// window composite with per-pixel alpha against the desktop.
    /// </summary>
    /// <remarks>
    /// A transparent XAML root and a null <c>SystemBackdrop</c> are necessary and are not
    /// sufficient: without this the window still has an opaque backing and paints as a
    /// white rectangle, which is exactly what the first working build did. Margins of -1
    /// on all four sides is the documented way to say "all of it".
    /// </remarks>
    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE.</summary>
    internal const int DwmwaWindowCornerPreference = 33;

    /// <summary>DWMWCP_DONOTROUND. An ornament has no corners to round.</summary>
    internal const int DwmwcpDoNotRound = 1;

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial IntPtr MonitorFromPoint(Point pt, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    internal const uint MonitorDefaultToNearest = 0x00000002;

    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref Rect rect, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);
}
