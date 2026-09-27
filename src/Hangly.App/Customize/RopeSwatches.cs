//
//  RopeSwatches.cs
//  Hangly
//
//  Each rope, drawn once, for the Library's rope cards.
//

using System.Runtime.InteropServices;
using Hangly.App.Overlay;
using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Microsoft.Graphics.Canvas;
using SkiaSharp;

namespace Hangly.App.Customize;

/// <summary>A picture of each rope style, rendered by <see cref="RopeRenderer.DrawSwatch"/> and kept on disk.</summary>
/// <remarks>
/// Static images rather than live canvases: nine small PNGs, drawn once per version of the
/// app and then only read, so the rope shelf costs a Library of images and nothing while
/// it is open — no device, no redraws, and nothing to calm under reduced motion. Drawn at
/// twice the card's size so they are sharp at 200 %. Kept beside the charm thumbnails,
/// under the version, so a renderer change in an update redraws them.
/// </remarks>
public static class RopeSwatches
{
    /// <summary>The card's swatch, in points.</summary>
    public const float Width = 56;

    public const float Height = 128;

    private const float Scale = 2;

    private static readonly Lock Gate = new();

    public static string Directory => Path.Combine(Path.GetTempPath(), "Hangly", "ropes", Services.AppInfo.Version);

    /// <summary>The swatch for <paramref name="style"/>, drawing it the first time; null if it cannot be drawn.</summary>
    public static string? PathFor(RopeStyle style)
    {
        string target = Path.Combine(Directory, $"{style}@2x-v2.png");
        if (File.Exists(target))
        {
            return target;
        }

        lock (Gate)
        {
            try
            {
                return File.Exists(target) || Render(style, target) ? target : null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or COMException)
            {
                Services.Diagnostics.Log($"rope swatch for {style} could not be drawn: {exception.GetType().Name}");
                return null;
            }
        }
    }

    private static bool Render(RopeStyle style, string target)
    {
        CanvasDevice device = CanvasDevice.GetSharedDevice();
        using var surface = new CanvasRenderTarget(device, Width, Height, 96 * Scale);
        using (CanvasDrawingSession session = surface.CreateDrawingSession())
        {
            session.Clear(Microsoft.UI.Colors.Transparent);
            RopeRenderer.DrawSwatch(session, style, new Vec2(Width / 2, 6), new Vec2(Width / 2, Height - 6));
        }

        // Win2D's own save is asynchronous; the pixels are here, and Skia writes a PNG
        // without a round trip through the thread pool.
        byte[] pixels = surface.GetPixelBytes();
        int pixelWidth = (int)surface.SizeInPixels.Width;
        int pixelHeight = (int)surface.SizeInPixels.Height;
        using var bitmap = new SKBitmap(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.Directory.CreateDirectory(Directory);
        using FileStream file = File.Create(target);
        png.SaveTo(file);
        return true;
    }
}
