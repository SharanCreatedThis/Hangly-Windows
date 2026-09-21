//
//  CharmArtworkCache.cs
//  Hangly
//
//  Charm artwork: SVG in, a bitmap at exactly the size this frame needs out.
//

using Hangly.Core.Geometry;
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
/// <param name="Body">
/// Which part of the artwork is the charm itself, in the fitted unit square, as measured
/// by <see cref="CharmArtworkSplitter"/>. The whole square when the artwork could not be
/// measured — drawing the beads in as well is a better failure than drawing nothing.
/// </param>
/// <param name="BeadRegions">
/// Where each bead's own artwork lives, in the same fitted unit square, in the order the
/// solver is given them. This is what makes a bead the picture the designer drew rather
/// than a shape this renderer invented: the same rectangles <see cref="Beads"/> was
/// measured from, kept rather than discarded.
/// </param>
public sealed record CharmDescriptor(
    string Id,
    string DisplayName,
    string FileName,
    CharmMetrics Metrics,
    CharmPalette Palette,
    IReadOnlyList<CharmBead> Beads,
    Rect Body,
    IReadOnlyList<Rect> BeadRegions);

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
/// once per frame, and a settled rope rasterises nothing at all. That size is in device
/// pixels, which is also what keys one display's rasters apart from another's.</para>
///
/// <para>The same folder of SVGs the macOS bundle carries is copied into the output
/// directory by the project file. Neither build has its own copy of the artwork.</para>
/// </remarks>
public sealed class CharmArtworkCache : IDisposable
{
    private readonly string directory;
    private readonly Dictionary<string, SKSvg> documents = [];
    private readonly Dictionary<(string File, int Size, Rect Region), CanvasBitmap> rasters = [];
    private readonly Dictionary<(string File, int Beads, int Body), CharmArtworkRegions?> regions = [];
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

        // Rasterised in *device* pixels, not in the points the session is measured in.
        // The drawing session works in DIPs and the surface behind it is at the display's
        // DPI, so a bitmap sized in points is stretched by the DPI factor on its way to
        // the screen: at 200% every source pixel was drawn to four, which is why the
        // artwork read as soft on exactly the displays that should have shown it best.
        // The cord and the beads never had this because they are strokes, resolved at the
        // target's resolution — only the artwork went through a fixed-size raster.
        double density = session.Dpi / 96.0;
        int pixels = (int)Math.Round(placement.Radius * 2 * density);
        if (pixels <= 0)
        {
            return;
        }

        CanvasBitmap? bitmap = Raster(charm.FileName, pixels, charm.Body);
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

        var destination = new Windows.Foundation.Rect(
            placement.Center.X - placement.Radius,
            placement.Center.Y - placement.Radius,
            placement.Radius * 2,
            placement.Radius * 2);

        DrawShadow(session, bitmap, destination, placement.Radius);
        session.DrawImage(bitmap, destination);

        session.Transform = previous;
    }

    /// <summary>Draws one bead, as the artwork drew it.</summary>
    /// <remarks>
    /// The bead is a region of the charm's own SVG, rasterised on its own at the size it
    /// appears — the same call the body goes through, with a different rectangle. It is
    /// not a shape this renderer composes, and deliberately so: a charm's beads carry the
    /// designer's material, and three beads drawn touching are one measured run, so any
    /// attempt to synthesise them draws one blob where the picture has three.
    ///
    /// <para>Rotated with the cord like the charm is, because a bead threaded on a cord
    /// turns with it.</para>
    /// </remarks>
    public void DrawBead(
        CanvasDrawingSession session,
        CharmDescriptor charm,
        BeadPlacement placement,
        Rect region)
    {
        double side = Math.Max(placement.Size.Width, placement.Size.Height);
        if (side <= 0 || region.Width <= 0 || region.Height <= 0)
        {
            return;
        }

        int pixels = (int)Math.Round(side * (session.Dpi / 96.0));
        if (pixels <= 0)
        {
            return;
        }

        CanvasBitmap? bitmap = Raster(charm.FileName, pixels, region);
        if (bitmap is null)
        {
            return;
        }

        System.Numerics.Matrix3x2 previous = session.Transform;
        var center = new System.Numerics.Vector2(
            (float)placement.Position.X,
            (float)placement.Position.Y);

        session.Transform =
            System.Numerics.Matrix3x2.CreateRotation((float)(placement.Angle - (Math.PI / 2)), center)
            * previous;

        session.DrawImage(
            bitmap,
            new Windows.Foundation.Rect(
                placement.Position.X - (side / 2),
                placement.Position.Y - (side / 2),
                side,
                side));

        session.Transform = previous;
    }

    /// <summary>The charm's drop shadow, cast from the artwork's own alpha.</summary>
    /// <remarks>
    /// <b>Where these numbers come from.</b> They are measured off the shipping macOS
    /// 2.0.0 app, not guessed: the overlay was captured over a white backdrop and the
    /// luminance profile read outward from the charm's silhouette in three directions.
    /// Against a 112-point charm the background darkened by 21.6% just below the bottom
    /// edge, 11.0% just above the top edge, and reached white again about 16 points out
    /// on every side.
    ///
    /// <para>A blurred silhouette offset downward fits that exactly. At the bottom edge
    /// the sample sits <c>offset</c> inside the shadow and at the top edge the same
    /// distance outside it, so the two readings sum to the opacity — 32.6% — and their
    /// ratio gives the offset in units of the blur. Solving leaves a standard deviation
    /// of 0.098 of the charm's radius and an offset of 0.041 of it.</para>
    ///
    /// <para><b>What this replaces.</b> A filled ellipse of 1.7 radii at 6% alpha, which
    /// had no counterpart in the original at all. It was a hard-edged disc and read as
    /// one — the visible circle in every screenshot of the Windows build.</para>
    ///
    /// <para>Cast from the bitmap rather than from a circle, so a charm that is not round
    /// — a hamsa, a horseshoe — throws its own shape. That is the same guarantee macOS
    /// documents for imported bitmaps: "a cut-out subject casts the shape of itself and
    /// not of its bounding box".</para>
    /// </remarks>
    private static void DrawShadow(
        CanvasDrawingSession session,
        CanvasBitmap bitmap,
        Windows.Foundation.Rect destination,
        double radius)
    {
        // The effect graph works in the bitmap's own pixels, and the bitmap is rasterised
        // at the display's resolution while the session is in points — so the blur is
        // stated in bitmap pixels and the whole result is scaled into place afterwards.
        // Blurring after the scale would soften by the DPI factor on a 200% display.
        float scale = (float)(destination.Width / bitmap.SizeInPixels.Width);
        if (scale <= 0)
        {
            return;
        }

        using var shadow = new Microsoft.Graphics.Canvas.Effects.ShadowEffect
        {
            Source = bitmap,
            BlurAmount = (float)(radius * ShadowBlurRatio / scale),
            ShadowColor = Windows.UI.Color.FromArgb((byte)Math.Round(255 * ShadowOpacity), 0, 0, 0),
        };

        using var placed = new Microsoft.Graphics.Canvas.Effects.Transform2DEffect
        {
            Source = shadow,
            TransformMatrix =
                System.Numerics.Matrix3x2.CreateScale(scale)
                * System.Numerics.Matrix3x2.CreateTranslation(
                    (float)destination.X,
                    (float)(destination.Y + (radius * ShadowOffsetRatio))),
        };

        session.DrawImage(placed);
    }

    /// <summary>Blur standard deviation, as a fraction of the charm's radius.</summary>
    private const double ShadowBlurRatio = 0.098;

    /// <summary>How far the shadow sits below the charm, as a fraction of its radius.</summary>
    private const double ShadowOffsetRatio = 0.041;

    /// <summary>Peak darkening under the charm.</summary>
    /// <remarks>
    /// Raised from 0.326 after reading the two builds side by side: macOS is darker where
    /// the shadow meets the artwork and carries further before it reaches the ground.
    /// </remarks>
    private const double ShadowOpacity = 0.38;

    /// <summary>
    /// Measures how a charm's artwork divides into beads and body, once per charm.
    /// </summary>
    /// <remarks>
    /// The analysis raster is its own size and its own pass: it is read for its alpha
    /// channel only, and never drawn. <see langword="null"/> when the artwork is missing
    /// or does not have the parts the catalogue claims, which the caller reports rather
    /// than papering over.
    /// </remarks>
    public CharmArtworkRegions? Measure(CharmCatalogEntry entry)
    {
        var key = (entry.FileName, entry.BeadCount, entry.BodyRun);
        if (regions.TryGetValue(key, out CharmArtworkRegions? cached))
        {
            return cached;
        }

        CharmArtworkRegions? measured = MeasureDocument(Document(entry.FileName), entry);
        regions[key] = measured;
        return measured;
    }

    /// <summary>The measurement itself, with no cache and no device behind it.</summary>
    private static CharmArtworkRegions? MeasureDocument(SKSvg? document, CharmCatalogEntry entry)
    {
        if (document?.Picture is null)
        {
            return null;
        }

        SKRect bounds = document.Picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        int side = CharmArtworkSplitter.AnalysisPixels;
        float scale = Math.Min(side / bounds.Width, side / bounds.Height);

        // How much of the fitted square the drawing actually occupies. The cord threshold
        // is a fraction of what was drawn, not of the margin around it.
        double contentWidth = bounds.Width * scale / side;

        byte[]? alpha = AlphaMask(document, bounds, scale, side);
        return alpha is null
            ? null
            : CharmArtworkSplitter.Split(alpha, side, contentWidth, entry.BeadCount, entry.BodyRun);
    }

    /// <summary>What happened when every charm in the catalogue was opened and measured.</summary>
    public readonly record struct ArtworkReport(
        IReadOnlyList<string> Missing,
        IReadOnlyList<string> Unmeasured,
        int Measured);

    /// <summary>
    /// Opens and measures every charm in the catalogue, and says which ones failed.
    /// </summary>
    /// <remarks>
    /// The port of the macOS build's development-only launch check, and deliberately not
    /// something the app does on the way up: this opens eighty-one SVGs and rasterises
    /// each one, which is a second of work a user never asked for. A shipped Hangly
    /// measures a charm when it hangs it and not before.
    ///
    /// <para>Needs no graphics device, because measuring is Skia and arithmetic. That is
    /// what lets it run from a command-line switch on a machine with no window open.</para>
    /// </remarks>
    public static ArtworkReport CheckAll(string directory)
    {
        var missing = new List<string>();
        var unmeasured = new List<string>();
        int measured = 0;

        foreach (CharmCatalogEntry entry in CharmCatalog.All)
        {
            string path = Path.Combine(directory, entry.FileName);
            if (!File.Exists(path))
            {
                missing.Add($"{entry.Id} ({entry.FileName})");
                continue;
            }

            using var document = new SKSvg();
            try
            {
                document.Load(path);
            }
            catch (Exception exception)
            {
                missing.Add($"{entry.Id} ({entry.FileName}): {exception.GetType().Name}");
                continue;
            }

            if (MeasureDocument(document, entry) is null)
            {
                unmeasured.Add($"{entry.Id} (beads {entry.BeadCount}, body {entry.BodyRun})");
            }
            else
            {
                measured++;
            }
        }

        return new ArtworkReport(missing, unmeasured, measured);
    }

    /// <summary>One byte of alpha per pixel of the fitted square, top row first.</summary>
    private static byte[]? AlphaMask(SKSvg document, SKRect bounds, float scale, int side)
    {
        using var surface = SKSurface.Create(new SKImageInfo(
            side,
            side,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate((side - (bounds.Width * scale)) / 2, (side - (bounds.Height * scale)) / 2);
        canvas.Scale(scale);
        canvas.DrawPicture(document.Picture);
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        using SKPixmap pixmap = image.PeekPixels();
        if (pixmap is null)
        {
            return null;
        }

        byte[] pixels = new byte[pixmap.BytesSize];
        System.Runtime.InteropServices.Marshal.Copy(pixmap.GetPixels(), pixels, 0, pixels.Length);

        var alpha = new byte[side * side];
        for (int index = 0; index < alpha.Length; index++)
        {
            // BGRA: alpha is the fourth byte of each pixel.
            alpha[index] = pixels[(index * 4) + 3];
        }

        return alpha;
    }

    private CanvasBitmap? Raster(string fileName, int pixels, Rect region)
    {
        if (rasters.TryGetValue((fileName, pixels, region), out CanvasBitmap? cached))
        {
            return cached;
        }

        SKSvg? document = Document(fileName);
        if (document?.Picture is null)
        {
            return null;
        }

        SKRect bounds = document.Picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        // Rasterised once, straight to the size wanted.
        //
        // Supersampling was tried here — twice the size, filtered down with a Mitchell
        // cubic — on the theory that Skia under-filters the high-resolution rasters most
        // of this artwork is built from. It was measured against a Lanczos reference at
        // the same size and it was worse, not better: mean gradient across the shield
        // fell from 36.6 to 23.6 against a reference of 42.1, because two resampling
        // stages and a deliberately soft cubic lose more than Skia's single stage does.
        // The direct raster keeps 87% of the reference's detail. Left as it is.
        using var surface = SKSurface.Create(new SKImageInfo(
            pixels,
            pixels,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        // Only `region` of the artwork is wanted, fitted to the square the charm's radius
        // describes and keeping its aspect. The region is stated in the fitted unit
        // square, so the whole square is drawn at whatever size makes the region come out
        // at `pixels`, and then shifted so the region lands in the middle of the output.
        //
        // With a region of the whole square this is exactly the old arithmetic, which is
        // the point: a charm whose artwork could not be measured still draws.
        double longest = Math.Max(region.Width, region.Height);
        if (longest <= 0)
        {
            return null;
        }

        var square = (float)(pixels / longest);
        float scale = Math.Min(square / bounds.Width, square / bounds.Height);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // Centre the region in the output box, then bring the region's own origin to it.
        canvas.Translate(
            (float)((pixels - (region.Width * square)) / 2) - (float)(region.Left * square),
            (float)((pixels - (region.Height * square)) / 2) - (float)(region.Top * square));
        canvas.Translate(
            (square - (bounds.Width * scale)) / 2,
            (square - (bounds.Height * scale)) / 2);
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

        rasters[(fileName, pixels, region)] = bitmap;
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
