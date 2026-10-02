//
//  ImageSvgPicture.cs
//  Hangly
//
//  Charm artwork as a Skia picture, without an SVG library.
//

using Hangly.Core.Import;
using SkiaSharp;

namespace Hangly.App.Overlay;

/// <summary>Records an <see cref="ImageSvgDocument"/> as an <see cref="SKPicture"/>.</summary>
/// <remarks>
/// What Svg.Skia's <c>SKSvg.Picture</c> was to the artwork cache, the thumbnails, the
/// importer and the Studio: the same picture, with the same bounds — the viewBox, from
/// the origin — so everything that measured and drew the old one draws this unchanged.
/// Each embedded picture is fitted into its box centred and keeping its aspect, which is
/// what SVG does for an <c>image</c> with no <c>preserveAspectRatio</c>.
/// </remarks>
public static class ImageSvgPicture
{
    /// <summary>The picture for the file at <paramref name="path"/>, or null when it is not pictures in a wrapper.</summary>
    public static SKPicture? Load(string path) => ImageSvg.Load(path) is { } document ? Record(document) : null;

    /// <summary>The picture for <paramref name="markup"/>, or null when it is not pictures in a wrapper.</summary>
    public static SKPicture? FromMarkup(string markup) => ImageSvg.Parse(markup) is { } document ? Record(document) : null;

    public static SKPicture? Record(ImageSvgDocument document)
    {
        var bounds = new SKRect(0, 0, (float)document.ViewBox.Width, (float)document.ViewBox.Height);
        using var recorder = new SKPictureRecorder();
        SKCanvas canvas = recorder.BeginRecording(bounds);
        canvas.Translate(-(float)document.ViewBox.Left, -(float)document.ViewBox.Top);

        foreach (ImageLayer layer in document.Layers)
        {
            using SKImage? image = SKImage.FromEncodedData(layer.Encoded);
            if (image is null || image.Width <= 0 || image.Height <= 0)
            {
                return null;
            }

            Affine t = layer.Transform;
            canvas.Save();
            canvas.Concat(new SKMatrix((float)t.A, (float)t.C, (float)t.E, (float)t.B, (float)t.D, (float)t.F, 0, 0, 1));

            double fit = Math.Min(layer.Box.Width / image.Width, layer.Box.Height / image.Height);
            double width = image.Width * fit;
            double height = image.Height * fit;
            var target = SKRect.Create(
                (float)(layer.Box.Left + ((layer.Box.Width - width) / 2)),
                (float)(layer.Box.Top + ((layer.Box.Height - height) / 2)),
                (float)width,
                (float)height);
            canvas.DrawImage(image, target, new SKSamplingOptions(SKCubicResampler.Mitchell));
            canvas.Restore();
        }

        return recorder.EndRecording();
    }
}
