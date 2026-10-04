//
//  AppIconImage.cs
//  Hangly
//
//  Hangly's own icon, for the pop-up to say whose it is.
//

using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.DirectX;

namespace Hangly.App.Notifications;

/// <summary>The icon embedded in Hangly.exe, as pixels Win2D can draw: what a system banner shows beside its words.</summary>
/// <remarks>
/// Read from the running executable, as <see cref="Interop.WindowIcon"/> does, because hangly.ico is deliberately not
/// shipped as a file. Asked for at 64 pixels so it is sharp at 200%; the colour bitmap of a modern icon carries its own
/// alpha, which is premultiplied here for Win2D. Read once; null if anything about it fails — the card is still a card.
/// </remarks>
internal static class AppIconImage
{
    public const int Side = 64;

    /// <summary>The resource ID the .NET SDK gives <c>ApplicationIcon</c>.</summary>
    private const int ApplicationIconId = 32512;
    private const uint ImageIcon = 1;

    private static byte[]? pixels;
    private static bool tried;

    /// <summary>A bitmap of the icon on <paramref name="device"/>, for the caller to dispose; null if it could not be read.</summary>
    public static CanvasBitmap? Create(ICanvasResourceCreator device)
    {
        if (!tried)
        {
            tried = true;
            try
            {
                pixels = Read();
            }
            catch (Exception exception)
            {
                Services.Diagnostics.Log($"notification icon unavailable: {exception.GetType().Name}");
            }
        }

        return pixels is null
            ? null
            : CanvasBitmap.CreateFromBytes(device, pixels, Side, Side, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied);
    }

    private static byte[]? Read()
    {
        IntPtr icon = LoadImage(GetModuleHandle(null), ApplicationIconId, ImageIcon, Side, Side, 0);
        if (icon == IntPtr.Zero || !GetIconInfo(icon, out IconInfo info))
        {
            return null;
        }

        try
        {
            var header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = Side,
                Height = -Side, // top-down
                Planes = 1,
                BitCount = 32,
            };
            byte[] data = new byte[Side * Side * 4];
            IntPtr screen = GetDC(IntPtr.Zero);
            int rows = GetDIBits(screen, info.Color, 0, Side, data, ref header, 0);
            ReleaseDC(IntPtr.Zero, screen);
            if (rows != Side)
            {
                return null;
            }

            bool anyAlpha = false;
            for (int index = 3; index < data.Length; index += 4)
            {
                anyAlpha |= data[index] != 0;
            }

            if (!anyAlpha)
            {
                // An old-style icon without alpha in its colour bitmap; not worth a mask path for a 30-point picture.
                return null;
            }

            for (int index = 0; index < data.Length; index += 4)
            {
                int alpha = data[index + 3];
                data[index] = (byte)(data[index] * alpha / 255);
                data[index + 1] = (byte)(data[index + 1] * alpha / 255);
                data[index + 2] = (byte)(data[index + 2] * alpha / 255);
            }

            return data;
        }
        finally
        {
            DeleteObject(info.Color);
            DeleteObject(info.Mask);
            DestroyIcon(icon);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public int IsIcon;
        public int HotspotX;
        public int HotspotY;
        public IntPtr Mask;
        public IntPtr Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, nint name, uint type, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfoHeader info, uint usage);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);
}
