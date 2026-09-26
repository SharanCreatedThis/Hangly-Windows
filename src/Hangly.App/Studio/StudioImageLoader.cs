//
//  StudioImageLoader.cs
//  Hangly
//
//  Any picture the Studio accepts, read into the pixels it works on.
//

using Hangly.Core.Import;
using Hangly.Core.Studio;
using SkiaSharp;
using Svg.Skia;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Hangly.App.Studio;

/// <summary>A picture that could not be opened, with what to tell the person.</summary>
internal sealed class StudioLoadException(string message, Uri? help = null) : Exception(message)
{
    /// <summary>Where to get what is missing, when something is.</summary>
    public Uri? Help { get; } = help;
}

/// <summary>Reads PNG, JPEG, WebP, HEIC and SVG into a <see cref="StudioImage"/>.</summary>
/// <remarks>
/// The formats the macOS Studio reads, plus SVG, which both builds take (Phase A, Creator
/// Studio). Every picture comes out upright, in sRGB, premultiplied, at most
/// <see cref="MaximumSide"/> on its longest side — the macOS ceiling — so a fifty-megapixel
/// photo costs what a screenshot does.
///
/// <para><b>Which decoder, and why.</b> Skia reads PNG, JPEG and WebP itself, so those
/// work on every Windows install whatever codecs it has, and the tests see the same
/// pixels the app does. HEIC is the exception: it goes to the Windows codec, as decision
/// D2 says, and no third-party HEIC decoder ships. When the codec is missing the message
/// says which Store extension provides it.</para>
/// </remarks>
internal static class StudioImageLoader
{
    public const int MaximumSide = 2048;

    /// <summary>The side an SVG is drawn at, which is past anything the charm is shown at.</summary>
    public const int VectorSide = 1024;

    public static IReadOnlyList<string> Extensions { get; } = [".png", ".jpg", ".jpeg", ".webp", ".heic", ".heif", ".svg"];

    /// <summary>For the Open dialog.</summary>
    public const string DialogPattern = "*.png;*.jpg;*.jpeg;*.webp;*.heic;*.heif;*.svg";

    /// <summary>The Microsoft Store page for HEIF Image Extensions.</summary>
    public static Uri HeifExtension { get; } = new("ms-windows-store://pdp/?ProductId=9PMMSR1CGPWG");

    public static bool Handles(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant(), StringComparer.Ordinal);

    public static async Task<StudioImage> LoadAsync(string path)
    {
        if (!Handles(path))
        {
            throw new StudioLoadException($"Hangly can use PNG, JPEG, WebP, HEIC and SVG. “{Path.GetFileName(path)}” isn't one of those.");
        }

        if (!File.Exists(path))
        {
            throw new StudioLoadException("That file isn't there any more.");
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".heic" or ".heif" => await LoadHeifAsync(path).ConfigureAwait(false),
            ".svg" => await Task.Run(() => LoadSvg(path)).ConfigureAwait(false),
            _ => await Task.Run(() => LoadRaster(path)).ConfigureAwait(false),
        };
    }

    /// <summary>Pixels from the clipboard or a drag, already decoded by the caller as PNG bytes.</summary>
    public static StudioImage FromEncoded(byte[] encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using SKCodec? codec = SKCodec.Create(data);
        return codec is null
            ? throw new StudioLoadException("That picture couldn't be read.")
            : Decode(codec);
    }

    private static StudioImage LoadRaster(string path)
    {
        using SKCodec? codec = SKCodec.Create(path);
        return codec is null
            ? throw new StudioLoadException("That picture couldn't be read. It may be damaged.")
            : Decode(codec);
    }

    /// <summary>Decodes, turns upright by the EXIF orientation, and shrinks past the ceiling, in one draw.</summary>
    private static StudioImage Decode(SKCodec codec)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using var decoded = new SKBitmap(info);
        SKCodecResult result = codec.GetPixels(info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new StudioLoadException("That picture couldn't be read. It may be damaged.");
        }

        return Upright(decoded, codec.EncodedOrigin);
    }

    private static StudioImage Upright(SKBitmap source, SKEncodedOrigin origin)
    {
        bool swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int uprightWidth = swaps ? source.Height : source.Width;
        int uprightHeight = swaps ? source.Width : source.Height;
        double scale = Math.Min(1, MaximumSide / (double)Math.Max(uprightWidth, uprightHeight));
        int width = Math.Max(1, (int)Math.Round(uprightWidth * scale));
        int height = Math.Max(1, (int)Math.Round(uprightHeight * scale));

        using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)(width / (double)uprightWidth), (float)(height / (double)uprightHeight));
            canvas.Concat(OrientationMatrix(origin, source.Width, source.Height));
            using SKImage image = SKImage.FromBitmap(source);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        return new StudioImage(width, height, target.Bytes);
    }

    /// <summary>Maps the stored pixels onto the upright picture, for each of EXIF's eight orientations.</summary>
    private static SKMatrix OrientationMatrix(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity,
    };

    /// <summary>HEIC and HEIF, through the Windows codec only.</summary>
    private static async Task<StudioImage> LoadHeifAsync(string path)
    {
        if (!BitmapDecoder.GetDecoderInformationEnumerator().Any(decoder => decoder.CodecId == BitmapDecoder.HeifDecoderId))
        {
            throw new StudioLoadException(
                "To open HEIC photos, install HEIF Image Extensions from the Microsoft Store. It's free and made by Microsoft.",
                HeifExtension);
        }

        try
        {
            using IRandomAccessStream stream = await FileRandomAccessStream.OpenAsync(path, FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.HeifDecoderId, stream);

            // The transform's size is in the stored orientation; the pixels come back upright.
            double scale = Math.Min(1, MaximumSide / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
            var transform = new BitmapTransform
            {
                ScaledWidth = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * scale)),
                ScaledHeight = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * scale)),
                InterpolationMode = BitmapInterpolationMode.Fant,
            };
            bool swaps = decoder.OrientedPixelWidth != decoder.PixelWidth;
            PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Rgba8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
            int width = (int)(swaps ? transform.ScaledHeight : transform.ScaledWidth);
            int height = (int)(swaps ? transform.ScaledWidth : transform.ScaledHeight);
            return new StudioImage(width, height, pixels.DetachPixelData());
        }
        catch (Exception exception) when (exception is not StudioLoadException)
        {
            // Most often a photo stored as HEVC on a PC without the HEVC codec: the HEIF
            // extension reads the container and needs HEVC Video Extensions for the picture.
            Services.Diagnostics.Log($"studio: HEIC decode failed: 0x{exception.HResult:X8}");
            throw new StudioLoadException(
                "Windows couldn't read that HEIC photo. Photos from phones usually also need HEVC Video Extensions from the Microsoft Store.",
                new Uri("ms-windows-store://pdp/?ProductId=9NMZLZ57R3T7"));
        }
    }

    /// <summary>A drawing, cleaned by the same sanitiser imports use, drawn onto transparency.</summary>
    private static StudioImage LoadSvg(string path)
    {
        var file = new FileInfo(path);
        if (file.Length > SvgSanitizer.MaximumBytes)
        {
            throw new StudioLoadException($"That drawing is {file.Length / (1024 * 1024)} MB. Hangly accepts SVG files up to {SvgSanitizer.MaximumBytes / (1024 * 1024)} MB.");
        }

        SvgSanitizeResult cleaned = SvgSanitizer.Sanitize(File.ReadAllText(path));
        if (!cleaned.IsAccepted)
        {
            throw new StudioLoadException("That file isn't a drawing Hangly can use.");
        }

        using var document = new SKSvg();
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(cleaned.Markup!)))
        {
            document.Load(stream);
        }

        SKRect bounds = document.Picture?.CullRect ?? SKRect.Empty;
        if (document.Picture is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new StudioLoadException("There's nothing to draw in that file.");
        }

        float scale = VectorSide / Math.Max(bounds.Width, bounds.Height);
        int width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
        int height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
        using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(document.Picture);
        }

        return new StudioImage(width, height, target.Bytes);
    }
}
