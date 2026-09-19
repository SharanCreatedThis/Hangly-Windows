//
//  CharmArtworkCache.cs
//  Hangly
//
//  Charm artwork: SVG in, a bitmap at exactly the size this frame needs out.
//

using Hangly.Core.Models;
using Hangly.Core.Physics;
using Microsoft.Graphics.Canvas;
using SkiaSharp;
using Svg.Skia;

namespace Hangly.App.Overlay;

/// <summary>One charm in the catalogue: its artwork, its physics and its palette.</summary>
/// <param name="Id">Stable identifier, used in the settings document.</param>
/// <param name="DisplayName">What the menu calls it.</param>
/// <param name="FileName">Its SVG, inside the Charms folder.</param>
/// <param name="Metrics">What the rope has to carry.</param>
/// <param name="Palette">Its four inks.</param>
/// <param name="Beads">What it threads on the cord above it.</param>
public sealed record CharmDescriptor(
    string Id,
    string DisplayName,
    string FileName,
    CharmMetrics Metrics,
    CharmPalette Palette,
    IReadOnlyList<CharmBead> Beads);

/// <summary>Rasterises charm artwork, once per size.</summary>
/// <remarks>
/// The artwork is SVG on both platforms, and both builds rasterise it at the exact size
/// each frame needs rather than shipping baked bitmaps. The macOS original measured what
/// the alternative costs: an asset catalog bakes a bitmap of every vector at each scale
/// factor beside the vector data, and those bitmaps came to fifty-six megabytes against
/// eighteen of vectors, none of which were ever drawn.
///
/// <para>The cache is keyed on the charm and the rounded pixel size, so a charm that is
/// growing as it fades in rasterises once per whole pixel it passes through rather than
/// once per frame, and a settled rope rasterises nothing at all.</para>
///
/// <para>The same folder of SVGs the macOS bundle carries is copied into the output
/// directory by the project file. Neither build has its own copy of the artwork.</para>
/// </remarks>
public sealed class CharmArtworkCache : IDisposable
{
    private readonly string directory;
    private readonly Dictionary<string, SKSvg> documents = [];
    private readonly Dictionary<(string File, int Size), CanvasBitmap> rasters = [];
    private readonly ICanvasResourceCreator resourceCreator;

    public CharmArtworkCache(ICanvasResourceCreator resourceCreator, string directory)
    {
        this.resourceCreator = resourceCreator;
        this.directory = directory;
    }

    /// <summary>The bundled folder of charm artwork, copied in whole from Assets/Charms.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Charms");

    public void Draw(CanvasDrawingSession session, CharmDescriptor? charm, CharmPlacement placement)
    {
        if (charm is null || placement.Radius <= 0)
        {
            return;
        }

        int pixels = (int)Math.Round(placement.Radius * 2);
        if (pixels <= 0)
        {
            return;
        }

        CanvasBitmap? bitmap = Raster(charm.FileName, pixels);
        if (bitmap is null)
        {
            return;
        }

        // The charm hangs the way the cord meets it, not the way it was drawn: the
        // orientation comes from the cord, so the artwork's own loop lines up with the
        // cord drawn into it. Rotated about the charm's centre, which is the node.
        System.Numerics.Matrix3x2 previous = session.Transform;
        var center = new System.Numerics.Vector2((float)placement.Center.X, (float)placement.Center.Y);

        // The artwork is drawn hanging straight down, which is an angle of pi/2.
        float rotation = (float)(placement.Angle - (Math.PI / 2));
        session.Transform = System.Numerics.Matrix3x2.CreateRotation(rotation, center) * previous;

        session.DrawImage(
            bitmap,
            new Windows.Foundation.Rect(
                placement.Center.X - placement.Radius,
                placement.Center.Y - placement.Radius,
                placement.Radius * 2,
                placement.Radius * 2));

        session.Transform = previous;
    }

    private CanvasBitmap? Raster(string fileName, int pixels)
    {
        if (rasters.TryGetValue((fileName, pixels), out CanvasBitmap? cached))
        {
            return cached;
        }

        SKSvg? document = Document(fileName);
        if (document?.Picture is null)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(
            pixels,
            pixels,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        SKRect bounds = document.Picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        // Fitted to the square the charm's radius describes, keeping its aspect: the
        // artwork is authored inside a unit square, and a charm that did not fit its own
        // bounding circle would break every claim CharmStackLayout makes.
        float scale = Math.Min(pixels / bounds.Width, pixels / bounds.Height);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(
            (pixels - (bounds.Width * scale)) / 2,
            (pixels - (bounds.Height * scale)) / 2);
        canvas.Scale(scale);
        canvas.DrawPicture(document.Picture);
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        using SKPixmap pixmap = image.PeekPixels();
        if (pixmap is null)
        {
            return null;
        }

        byte[] pixelBytes = new byte[pixmap.BytesSize];
        System.Runtime.InteropServices.Marshal.Copy(pixmap.GetPixels(), pixelBytes, 0, pixelBytes.Length);

        var bitmap = CanvasBitmap.CreateFromBytes(
            resourceCreator,
            pixelBytes,
            pixels,
            pixels,
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);

        rasters[(fileName, pixels)] = bitmap;
        return bitmap;
    }

    private SKSvg? Document(string fileName)
    {
        if (documents.TryGetValue(fileName, out SKSvg? cached))
        {
            return cached;
        }

        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var svg = new SKSvg();
        svg.Load(path);
        documents[fileName] = svg;
        return svg;
    }

    public void Dispose()
    {
        foreach (CanvasBitmap bitmap in rasters.Values)
        {
            bitmap.Dispose();
        }

        foreach (SKSvg document in documents.Values)
        {
            document.Dispose();
        }

        rasters.Clear();
        documents.Clear();
    }
}
