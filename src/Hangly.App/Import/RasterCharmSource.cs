//
//  RasterCharmSource.cs
//  Hangly
//
//  A photograph, turned into something the existing importer already understands.
//

using SkiaSharp;

namespace Hangly.App.Import;

/// <summary>Wraps a PNG or JPEG in the smallest SVG that can carry it.</summary>
/// <remarks>
/// <b>Why a wrapper and not a second rendering path.</b> Everything downstream of an
/// import already speaks SVG: the sanitiser, <c>CustomCharmStore</c>,
/// <c>CharmArtworkSplitter</c>, the artwork cache and the renderer. A raster that arrives
/// as <c>data:image/png;base64</c> inside a one-element SVG travels all of it untouched,
/// so a photograph persists, reorders, resizes, favourites and deletes exactly as a drawn
/// charm does — with no new storage format and no branch in the renderer.
///
/// <para>This only became possible when the import audit fixed
/// <c>SvgSanitizer.IsLocalReference</c>: it used to strip every <c>data:</c> reference,
/// which would have left every created charm blank.</para>
///
/// <para><b>What it does to the picture.</b> Three things, all of them the ones macOS's
/// own processor does under "fit" — and none of the ones it does under "isolate", because
/// background removal is deliberately not in this version.</para>
///
/// <list type="number">
/// <item><description>Downsamples anything enormous, so a fifty-megapixel photograph
/// costs about what a screenshot does.</description></item>
/// <item><description>Crops to the visible pixels, so a picture with a wide transparent
/// margin hangs at the size of the thing in it rather than the size of its canvas.
/// A photograph with no transparency is entirely visible, and crops to itself.</description></item>
/// <item><description>Centres what is left in a square with a margin, so the rope's knot
/// lands on the top-centre of the artwork rather than the top-centre of whatever
/// rectangle it arrived in.</description></item>
/// </list>
/// </remarks>
internal static class RasterCharmSource
{
    /// <summary>The longest side anything is decoded at.</summary>
    /// <remarks>
    /// The same ceiling macOS uses. Past this the extra pixels are never drawn — a charm
    /// is a couple of hundred points across — and they cost memory on every rasterise.
    /// </remarks>
    public const int MaximumSide = 2048;

    /// <summary>The side of the square the artwork is centred in.</summary>
    public const int CanvasSide = 512;

    /// <summary>
    /// How much of that square the artwork fills, leaving the rest as margin.
    /// </summary>
    /// <remarks>
    /// The same fraction the SVG importer uses when it corrects for framing, so a created
    /// charm and a drawn one of the same subject hang at the same size.
    /// </remarks>
    public const double SubjectFill = 0.92;

    /// <summary>Alpha at or below which a pixel counts as not there.</summary>
    private const byte Transparent = 8;

    /// <summary>The formats this accepts, lower case, with the dot.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".png", ".jpg", ".jpeg"];

    public static bool Handles(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant(), StringComparer.Ordinal);

    /// <summary>What came of trying to turn an image into artwork.</summary>
    /// <param name="Markup">The SVG, or null if it could not be made.</param>
    /// <param name="Refusal">What to tell the person, or null if it worked.</param>
    public readonly record struct RasterResult(string? Markup, string? Refusal);

    /// <summary>Turns an image file into SVG markup, or says why it cannot.</summary>
    public static RasterResult ToSvg(string path)
    {
        using SKBitmap? decoded = Decode(path);
        if (decoded is null)
        {
            return new RasterResult(null, "That image couldn't be read. It may be damaged.");
        }

        SKRectI bounds = VisibleBounds(decoded);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new RasterResult(null, "That image is completely transparent.");
        }

        using SKBitmap square = Centre(decoded, bounds);
        using SKData? encoded = square.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null)
        {
            return new RasterResult(null, "That image couldn't be prepared.");
        }

        string base64 = Convert.ToBase64String(encoded.ToArray());
        return new RasterResult(
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {CanvasSide} {CanvasSide}">
              <image width="{CanvasSide}" height="{CanvasSide}" xlink:href="data:image/png;base64,{base64}"/>
            </svg>
            """,
            null);
    }

    /// <summary>Decodes, downsampling anything past the ceiling as it goes.</summary>
    private static SKBitmap? Decode(string path)
    {
        try
        {
            using SKBitmap? original = SKBitmap.Decode(path);
            if (original is null || original.Width <= 0 || original.Height <= 0)
            {
                return null;
            }

            int longest = Math.Max(original.Width, original.Height);
            if (longest <= MaximumSide)
            {
                return original.Copy(SKColorType.Bgra8888);
            }

            double scale = (double)MaximumSide / longest;
            var target = new SKImageInfo(
                Math.Max(1, (int)Math.Round(original.Width * scale)),
                Math.Max(1, (int)Math.Round(original.Height * scale)),
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            var resized = new SKBitmap(target);
            return original.ScalePixels(resized, new SKSamplingOptions(SKCubicResampler.Mitchell))
                ? resized
                : null;
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Log($"raster decode failed: {exception.GetType().Name}");
            return null;
        }
    }

    /// <summary>The smallest rectangle holding every pixel that is actually there.</summary>
    /// <remarks>
    /// This is the auto-detected bound the attachment point is placed against: the knot
    /// goes at the top centre of <em>this</em>, not of the file. An image with no alpha —
    /// every JPEG — is visible everywhere and returns its own bounds.
    /// </remarks>
    private static SKRectI VisibleBounds(SKBitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha <= Transparent)
                {
                    continue;
                }

                if (x < left) { left = x; }
                if (x > right) { right = x; }
                if (y < top) { top = y; }
                if (y > bottom) { bottom = y; }
            }
        }

        return right < left || bottom < top
            ? SKRectI.Empty
            : new SKRectI(left, top, right + 1, bottom + 1);
    }

    /// <summary>Draws the visible part, scaled to fill, into the middle of a square.</summary>
    private static SKBitmap Centre(SKBitmap source, SKRectI visible)
    {
        var square = new SKBitmap(new SKImageInfo(
            CanvasSide,
            CanvasSide,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        using var canvas = new SKCanvas(square);
        canvas.Clear(SKColors.Transparent);

        double fill = CanvasSide * SubjectFill;
        double scale = Math.Min(fill / visible.Width, fill / visible.Height);
        var width = (float)(visible.Width * scale);
        var height = (float)(visible.Height * scale);

        var destination = new SKRect(
            (float)((CanvasSide - width) / 2),
            (float)((CanvasSide - height) / 2),
            (float)(((CanvasSide - width) / 2) + width),
            (float)(((CanvasSide - height) / 2) + height));

        using SKImage image = SKImage.FromBitmap(source);
        canvas.DrawImage(
            image,
            new SKRect(visible.Left, visible.Top, visible.Right, visible.Bottom),
            destination,
            new SKSamplingOptions(SKCubicResampler.Mitchell));

        canvas.Flush();
        return square;
    }
}
