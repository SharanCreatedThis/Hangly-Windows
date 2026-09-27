//
//  StudioRaster.cs
//  Hangly
//
//  The Studio's drawing steps: fitting a cut-out into its square, and packing it for the store.
//

using Hangly.Core.Studio;
using SkiaSharp;

namespace Hangly.App.Studio;

/// <summary>Where the Studio needs a real resampler, and the encoding at the end.</summary>
internal static class StudioRaster
{
    /// <summary>The macOS <c>fitToSquare</c>: the subject's longest side spans <paramref name="fill"/>, centred.</summary>
    public static StudioImage FitToSquare(StudioImage isolated, double fill)
    {
        ArgumentNullException.ThrowIfNull(isolated);
        PixelBounds bounds = isolated.OpaqueBounds(StudioPipeline.OpaqueThreshold)
            ?? throw new StudioLoadException("There's nothing left of the picture. Try another background setting.");
        (double x, double y, double width, double height) = StudioPipeline.FitRect(bounds, fill);

        int side = StudioPipeline.OutputSide;
        using var target = new SKBitmap(new SKImageInfo(side, side, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(target))
        using (SKImage source = Wrap(isolated))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(
                source,
                new SKRect(bounds.MinX, bounds.MinY, bounds.MaxX + 1, bounds.MaxY + 1),
                new SKRect((float)x, (float)y, (float)(x + width), (float)(y + height)),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        return new StudioImage(side, side, target.Bytes);
    }

    /// <summary>A charm's SVG: the square as a PNG inside the smallest SVG that can carry it.</summary>
    /// <remarks>
    /// The same wrapper <c>RasterCharmSource</c> makes, so a Studio charm travels the store,
    /// the splitter, the artwork cache and the renderer exactly as every import does.
    /// </remarks>
    public static string ToSvg(StudioImage square)
    {
        string base64 = Convert.ToBase64String(EncodePng(square));
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 {square.Width} {square.Height}">
              <image width="{square.Width}" height="{square.Height}" xlink:href="data:image/png;base64,{base64}"/>
            </svg>
            """;
    }

    public static byte[] EncodePng(StudioImage image)
    {
        using SKImage wrapped = Wrap(image);
        using SKData data = wrapped.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKImage Wrap(StudioImage image) =>
        SKImage.FromPixelCopy(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul), image.Pixels);
}
