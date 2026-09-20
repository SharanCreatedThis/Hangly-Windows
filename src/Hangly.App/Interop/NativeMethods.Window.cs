//
//  NativeMethods.Window.cs
//  Hangly
//
//  Creating a window of our own, and painting it with per-pixel alpha.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Interop;

/// <summary>
/// The window-creation and layered-painting half of the Win32 surface.
/// </summary>
/// <remarks>
/// Split from the rest because it exists for one reason: WinUI cannot give a
/// per-pixel-transparent desktop window, so the overlay creates its own.
///
/// <para>A WinUI 3 <c>Window</c> is created without <c>WS_EX_NOREDIRECTIONBITMAP</c>, and
/// that style cannot be added afterwards — the opaque redirection surface is allocated at
/// <c>CreateWindowEx</c> time. Everything XAML offers above that surface (a null
/// background, no <c>SystemBackdrop</c>, an extended glass frame) paints onto it rather
/// than replacing it, which is why the window composited as a white rectangle with a
/// correctly drawn rope inside it. The Windows App SDK in use here has no
/// <c>TransparentBackdrop</c> to ask for instead.</para>
///
/// <para>So the overlay is a layered window: the frame is rendered off-screen with
/// premultiplied alpha and handed to <see cref="UpdateLayeredWindow"/>, which is the one
/// mechanism Windows has always had for compositing arbitrary shapes against the desktop.
/// It costs a read-back per frame and buys a window with no rectangle at all.</para>
/// </remarks>
internal static partial class NativeMethods
{
    /// <summary>Sent when a window's DPI changes, by a display change or by moving.</summary>
    internal const uint WmDpiChanged = 0x02E0;

    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    internal static extern int RegisterDragDrop(IntPtr hWnd, [MarshalAs(UnmanagedType.Interface)] object target);

    [DllImport("ole32.dll")]
    internal static extern int RevokeDragDrop(IntPtr hWnd);

    [DllImport("ole32.dll")]
    internal static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint DragQueryFile(IntPtr drop, uint index, char[]? buffer, uint length);

    internal const uint WsPopup = 0x80000000;
    internal const uint WsVisible = 0x10000000;

    internal const int SwHide = 0;
    internal const int SwShowna = 8;

    internal const uint WmClose = 0x0010;
    internal const uint WmDestroy = 0x0002;
    internal const uint WmQuit = 0x0012;

    internal const uint PmRemove = 0x0001;

    /// <summary>AC_SRC_OVER, the only blend operation <c>UpdateLayeredWindow</c> has.</summary>
    internal const byte AcSrcOver = 0x00;

    /// <summary>AC_SRC_ALPHA: the source bitmap carries its own premultiplied alpha.</summary>
    internal const byte AcSrcAlpha = 0x01;

    /// <summary>ULW_ALPHA: blend per pixel rather than by colour key.</summary>
    internal const uint UlwAlpha = 0x00000002;

    internal const int BiRgb = 0;
    internal const uint DibRgbColors = 0;

    internal delegate IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WndClassEx
    {
        public int Size;
        public uint Style;
        public IntPtr WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Msg
    {
        public IntPtr HWnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Pt;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Size
    {
        public int Width;
        public int Height;
    }

    /// <summary>
    /// How the layered surface is mixed with what is behind it. <see cref="AlphaFormat"/>
    /// is the field that matters: without <see cref="AcSrcAlpha"/> the per-pixel alpha is
    /// ignored and the whole rectangle is drawn opaque, which is the same failure this
    /// window layer exists to avoid.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        public int Size;
        public int Width;

        /// <summary>Negative for a top-down bitmap; see <c>LayeredOverlaySurface</c>.</summary>
        public int Height;

        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassEx(ref WndClassEx wndClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowEx(
        uint exStyle,
        string className,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PeekMessage(
        out Msg message,
        IntPtr hWnd,
        uint filterMin,
        uint filterMax,
        uint removeMsg);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static extern IntPtr DispatchMessage(ref Msg message);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr obj);

    /// <summary>
    /// A 32-bit surface whose pixels we can write directly, which is what
    /// <see cref="UpdateLayeredWindow"/> needs and what a Win2D render target cannot be.
    /// </summary>
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BitmapInfoHeader header,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(
        IntPtr hWnd,
        IntPtr hdcDst,
        ref Point pptDst,
        ref Size psize,
        IntPtr hdcSrc,
        ref Point pptSrc,
        uint crKey,
        ref BlendFunction blend,
        uint flags);

    /// <summary>
    /// Blocks until the compositor has finished its next frame.
    /// </summary>
    /// <remarks>
    /// This is the port of the original's <c>CADisplayLink</c>, and the replacement for
    /// <c>CompositionTarget.Rendering</c>, which only fires while a XAML tree is being
    /// composed and there is no longer one here. It paces the loop to the display exactly
    /// as the rendering event did, without needing a timer that would drift against the
    /// compositor and show as judder.
    /// </remarks>
    [DllImport("dwmapi.dll")]
    internal static extern int DwmFlush();
}
