//
//  LayeredOverlaySurface.cs
//  Hangly
//
//  The window the rope is drawn on: one HWND, one off-screen surface, no rectangle.
//

using Hangly.App.Interop;
using Hangly.App.Services;
using Microsoft.Graphics.Canvas;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX;

namespace Hangly.App.Overlay;

/// <summary>A borderless window whose pixels are whatever was drawn into them.</summary>
/// <remarks>
/// <b>Why this exists instead of a WinUI window.</b> The overlay has to be transparent
/// per pixel: the rope is a few thin strokes and a charm, and everything around them has
/// to be the desktop. A WinUI 3 window cannot do that. Its HWND is created without
/// <c>WS_EX_NOREDIRECTIONBITMAP</c>, so it owns an opaque redirection surface that no
/// XAML property can remove — a null background, a null <c>SystemBackdrop</c> and an
/// extended glass frame all paint onto that surface rather than replacing it, and the
/// window composites as a white rectangle with a perfectly correct rope inside it.
/// That was observed, not assumed.
///
/// <para><b>Why layered rather than DirectComposition.</b> Both give per-pixel alpha.
/// A composition swapchain keeps the pixels on the GPU and is the faster of the two, at
/// the cost of several undocumented-by-order COM vtables. A layered window costs one
/// read-back of the rendered frame per drawn frame and needs nothing but GDI. For a
/// window this size carrying a few hundred stroked segments — which stops redrawing
/// entirely the moment the rope settles — that read-back is not the expensive part, and
/// being able to read the code is worth more here than the frames it saves.</para>
///
/// <para>The surface is premultiplied BGRA because that is what
/// <c>UpdateLayeredWindow</c> blends and what Win2D renders natively. No conversion
/// happens anywhere between the drawing session and the desktop.</para>
/// </remarks>
internal sealed class LayeredOverlaySurface : IDisposable
{
    private const string ClassName = "HanglyOverlay";

    // Held for the process's lifetime because Windows keeps the pointer, not the
    // delegate: letting this be collected leaves the class pointing at freed memory.
    private static readonly NativeMethods.WindowProc WndProcThunk = OnMessage;
    private static bool isClassRegistered;

    private readonly CanvasDevice device;

    private IntPtr handle;
    private IntPtr memoryDc;
    private IntPtr bitmap;
    private IntPtr previousBitmap;
    private IntPtr pixels;

    // The frame is read back through these, once per present, and both are reused.
    // See Present.
    private Windows.Storage.Streams.Buffer? transfer;
    private byte[] scratch = [];

    private CanvasRenderTarget? target;
    private int pixelWidth;
    private int pixelHeight;

    public LayeredOverlaySurface(CanvasDevice device) => this.device = device;

    public IntPtr Handle => handle;

    /// <summary>Creates the window, hidden, with no pixels in it yet.</summary>
    /// <remarks>
    /// Created without <c>WS_VISIBLE</c> on purpose. A layered window that is shown
    /// before its first <see cref="Present"/> shows one frame of undefined content, which
    /// on a transparent overlay reads as a flash of black.
    /// </remarks>
    public void Create()
    {
        RegisterClass();

        uint exStyle = NativeMethods.WsExLayered
            | NativeMethods.WsExToolwindow // no taskbar button, no Alt-Tab entry
            | NativeMethods.WsExTopmost // above every other application
            | NativeMethods.WsExNoactivate // clicking it never steals focus
            | NativeMethods.WsExTransparent; // click-through until the cursor finds the charm

        handle = NativeMethods.CreateWindowEx(
            exStyle,
            ClassName,
            "Hangly",
            NativeMethods.WsPopup,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null),
            IntPtr.Zero);

        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }
    }

    /// <summary>
    /// Sizes the off-screen surface for a window of this many pixels at this scale.
    /// </summary>
    /// <remarks>
    /// The render target is created in points at the display's DPI, so its drawing
    /// session is in the same space the solver works in and the renderer needs no
    /// transform — the same arrangement a <c>CanvasControl</c> gave, restated explicitly
    /// now that there is no control to give it.
    /// </remarks>
    public void Resize(int widthInPixels, int heightInPixels, double scale)
    {
        if (widthInPixels <= 0 || heightInPixels <= 0)
        {
            return;
        }

        if (widthInPixels == pixelWidth && heightInPixels == pixelHeight)
        {
            return;
        }

        ReleaseSurface();

        pixelWidth = widthInPixels;
        pixelHeight = heightInPixels;

        target = new CanvasRenderTarget(
            device,
            (float)(widthInPixels / scale),
            (float)(heightInPixels / scale),
            (float)(96.0 * scale),
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            CanvasAlphaMode.Premultiplied);

        // One buffer for the life of this size, and its address taken once. Win2D will
        // only hand a frame back as a fresh array or into an IBuffer, and at this size a
        // fresh array is 1.27 MB — fifteen times the 85,000-byte threshold that puts an
        // allocation on the Large Object Heap. The LOH is collected only by gen 2 and is
        // not compacted, so allocating one per frame made every collection a full one:
        // measured at 154 MB/s and thirty gen-2 collections a second during a drag,
        // against a frame budget of 8.3 ms. Reusing the buffer removes the allocation
        // entirely.
        int byteCount = widthInPixels * heightInPixels * 4;
        transfer = new Windows.Storage.Streams.Buffer((uint)byteCount)
        {
            Length = (uint)byteCount,
        };
        scratch = new byte[byteCount];

        var header = new NativeMethods.BitmapInfoHeader
        {
            Size = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
            Width = widthInPixels,

            // Negative means top-down. A DIB is bottom-up by default and Win2D reads back
            // top-down, so without this the rope hangs upwards from the bottom edge.
            Height = -heightInPixels,
            Planes = 1,
            BitCount = 32,
            Compression = NativeMethods.BiRgb,
        };

        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            bitmap = NativeMethods.CreateDIBSection(
                screenDc,
                ref header,
                NativeMethods.DibRgbColors,
                out pixels,
                IntPtr.Zero,
                0);

            if (bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"CreateDIBSection failed: {Marshal.GetLastWin32Error()}");
            }

            previousBitmap = NativeMethods.SelectObject(memoryDc, bitmap);
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }

    }

    /// <summary>Draws one frame and hands it to the desktop compositor.</summary>
    /// <param name="draw">Paints the frame. The session is already cleared to nothing.</param>
    /// <param name="origin">Where the window's top-left belongs, in desktop pixels.</param>
    /// <param name="opacity">Applied uniformly, on top of whatever alpha was drawn.</param>
    public void Present(Action<CanvasDrawingSession> draw, NativeMethods.Point origin, double opacity)
    {
        if (target is null || handle == IntPtr.Zero)
        {
            return;
        }

        using (CanvasDrawingSession session = target.CreateDrawingSession())
        {
            // Cleared to fully transparent, not to a colour. Everything the renderer does
            // not paint has to end up as the desktop.
            session.Clear(Microsoft.UI.Colors.Transparent);
            draw(session);
        }

        // Into the reused buffer, out through a reader, and into the DIB. Two copies
        // where one would do, and the second one is the price of not being able to take
        // the buffer's address: IBufferByteAccess is the only route to it, and CsWinRT
        // will not cast a projected WinRT object to a ComImport interface — that was
        // tried, and threw InvalidCastException at the first frame. The copy is cheap
        // next to what it replaces. It is bandwidth, not garbage.
        target.GetPixelBytes(transfer);
        using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(transfer))
        {
            reader.ReadBytes(scratch);
        }

        Marshal.Copy(scratch, 0, pixels, scratch.Length);

        var size = new NativeMethods.Size { Width = pixelWidth, Height = pixelHeight };
        var source = new NativeMethods.Point { X = 0, Y = 0 };
        var destination = origin;
        var blend = new NativeMethods.BlendFunction
        {
            BlendOp = NativeMethods.AcSrcOver,
            BlendFlags = 0,
            SourceConstantAlpha = (byte)Math.Clamp(Math.Round(opacity * 255), 0, 255),

            // Without this the per-pixel alpha is discarded and the whole rectangle is
            // drawn opaque — the exact failure this class replaced.
            AlphaFormat = NativeMethods.AcSrcAlpha,
        };

        bool updated = NativeMethods.UpdateLayeredWindow(
            handle,
            IntPtr.Zero,
            ref destination,
            ref size,
            memoryDc,
            ref source,
            0,
            ref blend,
            NativeMethods.UlwAlpha);

        if (!updated)
        {
            Diagnostics.Log($"UpdateLayeredWindow failed: {Marshal.GetLastWin32Error()}");
        }
    }

    /// <summary>Shows the window, on top of everything, without activating it.</summary>
    /// <remarks>
    /// <c>WS_EX_TOPMOST</c> at creation is what makes it topmost; this puts it above the
    /// windows that were already topmost when it appeared. <c>SW_SHOWNA</c> and
    /// <c>SWP_NOACTIVATE</c> both matter — an ornament that steals focus when it appears
    /// is worse than one that never appears.
    /// </remarks>
    public void Show()
    {
        NativeMethods.ShowWindow(handle, NativeMethods.SwShowna);
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

    private static void RegisterClass()
    {
        if (isClassRegistered)
        {
            return;
        }

        var wndClass = new NativeMethods.WndClassEx
        {
            Size = Marshal.SizeOf<NativeMethods.WndClassEx>(),
            WndProc = Marshal.GetFunctionPointerForDelegate(WndProcThunk),
            Instance = NativeMethods.GetModuleHandle(null),
            ClassName = ClassName,

            // No background brush. A class brush would have GDI erase the window to a
            // colour before anything else drew, which on a layered window is a visible
            // rectangle.
            Background = IntPtr.Zero,
        };

        if (NativeMethods.RegisterClassEx(ref wndClass) == 0)
        {
            int error = Marshal.GetLastWin32Error();

            // 1410 is ERROR_CLASS_ALREADY_EXISTS, which is not a failure: the overlay can
            // be torn down and rebuilt when the settings toggle it off and on again.
            if (error != 1410)
            {
                throw new InvalidOperationException($"RegisterClassEx failed: {error}");
            }
        }

        isClassRegistered = true;
    }

    private static IntPtr OnMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam) =>
        NativeMethods.DefWindowProc(hWnd, message, wParam, lParam);


    private void ReleaseSurface()
    {
        if (memoryDc != IntPtr.Zero)
        {
            if (previousBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, previousBitmap);
                previousBitmap = IntPtr.Zero;
            }

            NativeMethods.DeleteDC(memoryDc);
            memoryDc = IntPtr.Zero;
        }

        if (bitmap != IntPtr.Zero)
        {
            NativeMethods.DeleteObject(bitmap);
            bitmap = IntPtr.Zero;
        }

        pixels = IntPtr.Zero;
        transfer = null;
        scratch = [];
        target?.Dispose();
        target = null;
        pixelWidth = 0;
        pixelHeight = 0;
    }

    public void Dispose()
    {
        ReleaseSurface();

        if (handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(handle);
            handle = IntPtr.Zero;
        }
    }
}
