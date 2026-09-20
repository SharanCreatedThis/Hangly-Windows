//
//  CharmImporter.cs
//  Hangly
//
//  Turning a file somebody chose into a charm on the rope.
//

using Hangly.App.Overlay;
using Hangly.Core.Import;
using Hangly.Core.Models;
using SkiaSharp;
using Svg.Skia;

namespace Hangly.App.Import;

/// <summary>What happened, and what to tell the person who asked.</summary>
public sealed record ImportOutcome(bool IsAccepted, string Message, CustomCharmEntry? Entry)
{
    public static ImportOutcome Refused(string message) => new(false, message, null);
}

/// <summary>Reads a drawing, checks it, measures it, and keeps it.</summary>
/// <remarks>
/// <b>This is not a port of <c>CharmImageProcessor</c>, and cannot be.</b> The macOS
/// importer takes photographs — PNG, JPEG, HEIC — and its hardest work is deciding which
/// pixels are the subject, using Vision's subject lifting with a flood fill behind it.
/// Windows has no equivalent of that, and this takes SVG instead, where the question does
/// not arise: a vector drawing already says which pixels are ink and which are nothing.
///
/// <para>What is reproduced is everything after that point, because those parts are
/// arithmetic rather than platform: the mass formula and its framing correction, the
/// palette derived from the average colour, the naming rule, and the order the analytics
/// events fire in. The consequence is that the two platforms accept different files and
/// then treat what they accept identically. That is a real parity gap and PORTING.md
/// says so rather than implying otherwise.</para>
/// </remarks>
public static class CharmImporter
{
    /// <summary>Resolution the drawing is measured at.</summary>
    private const int AnalysisPixels = CharmArtworkSplitter.AnalysisPixels;

    /// <summary>
    /// How much of its square a shipped charm fills. The mass formula divides the
    /// framing out and multiplies this back in, so re-framing a drawing does not change
    /// what it weighs. Taken from the macOS importer unchanged.
    /// </summary>
    private const double SubjectFill = 0.92;

    /// <summary>Alpha at or below this counts as nothing.</summary>
    private const byte OpaqueThreshold = 8;

    /// <summary>Below this fraction of the square there is not enough drawing to hang.</summary>
    private const double MeaningfulAlphaFraction = 0.02;

    /// <summary>Reads, checks and stores a drawing.</summary>
    public static ImportOutcome Import(string path, CustomCharmStore store)
    {
        if (!File.Exists(path))
        {
            return ImportOutcome.Refused("That file isn't there any more.");
        }

        if (!Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return ImportOutcome.Refused(
                $"Hangly can import SVG drawings. “{Path.GetFileName(path)}” isn't one.");
        }

        var file = new FileInfo(path);
        if (file.Length == 0)
        {
            return ImportOutcome.Refused("That file is empty.");
        }

        if (file.Length > SvgSanitizer.MaximumBytes)
        {
            return ImportOutcome.Refused(
                $"That drawing is {file.Length / (1024 * 1024)} MB. Hangly accepts SVG files up to "
                + $"{SvgSanitizer.MaximumBytes / (1024 * 1024)} MB.");
        }

        string markup;
        try
        {
            markup = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Failure("reading an import", exception);
            return ImportOutcome.Refused("That file couldn't be read.");
        }

        return ImportMarkup(markup, NameFor(path), store);
    }

    /// <summary>
    /// Everything an import does once the bytes are in hand: clean, measure, keep.
    /// </summary>
    /// <remarks>
    /// Split out so a created charm and a dropped file travel the same road. The Create
    /// tab turns a photograph into SVG and hands it here, which is what keeps there from
    /// being a second importer with its own idea of what a charm weighs.
    /// </remarks>
    public static ImportOutcome ImportMarkup(string markup, string name, CustomCharmStore store)
    {
        SvgSanitizeResult cleaned = SvgSanitizer.Sanitize(markup);
        if (!cleaned.IsAccepted)
        {
            return ImportOutcome.Refused(Explain(cleaned.Rejection));
        }

        Measured? measured = Measure(cleaned.Markup!);
        if (measured is not Measured shape)
        {
            return ImportOutcome.Refused("That drawing couldn't be rendered.");
        }

        if (shape.Coverage < MeaningfulAlphaFraction)
        {
            return ImportOutcome.Refused("There's almost nothing drawn in that file.");
        }

        CustomCharmEntry entry;
        try
        {
            entry = store.Add(cleaned.Markup!, name, shape.Metrics, shape.Palette);
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Failure("storing an import", exception);
            return ImportOutcome.Refused("That charm couldn't be saved.");
        }

        string note = cleaned.Removed.Count > 0
            ? $" {cleaned.Removed.Count} thing(s) that weren't drawing were left out."
            : string.Empty;

        return new ImportOutcome(true, $"“{entry.Name}” is in your Library.{note}", entry);
    }

    /// <summary>Imports anything the Create tab accepts: a drawing or a photograph.</summary>
    /// <remarks>
    /// A raster is wrapped in an SVG carrying it as a data URI and then goes through the
    /// same path a drawing does, so nothing downstream — the store, the splitter, the
    /// renderer, persistence, favourites, reordering — learns that photographs exist.
    /// </remarks>
    public static ImportOutcome ImportAny(string path, string name, CustomCharmStore store)
    {
        if (!File.Exists(path))
        {
            return ImportOutcome.Refused("That file isn't there any more.");
        }

        var file = new FileInfo(path);
        if (file.Length == 0)
        {
            return ImportOutcome.Refused("That file is empty.");
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".svg")
        {
            if (file.Length > SvgSanitizer.MaximumBytes)
            {
                return ImportOutcome.Refused(
                    $"That drawing is {file.Length / (1024 * 1024)} MB. Hangly accepts SVG files up to "
                    + $"{SvgSanitizer.MaximumBytes / (1024 * 1024)} MB.");
            }

            try
            {
                return ImportMarkup(File.ReadAllText(path), name, store);
            }
            catch (Exception exception)
            {
                Services.Diagnostics.Failure("reading an import", exception);
                return ImportOutcome.Refused("That file couldn't be read.");
            }
        }

        if (!RasterCharmSource.Handles(path))
        {
            return ImportOutcome.Refused(
                $"Hangly can use PNG, JPG and SVG. “{Path.GetFileName(path)}” isn't one of those.");
        }

        RasterCharmSource.RasterResult raster = RasterCharmSource.ToSvg(path);
        return raster.Markup is null
            ? ImportOutcome.Refused(raster.Refusal ?? "That image couldn't be used.")
            : ImportMarkup(raster.Markup, name, store);
    }

    private static string Explain(SvgRejection rejection) => rejection switch
    {
        SvgRejection.Empty => "That file is empty.",
        SvgRejection.TooLarge => "That drawing is too large to import.",
        SvgRejection.NotXml => "That file isn't a readable SVG. If it has a document type "
            + "declaration, Hangly refuses it on purpose.",
        SvgRejection.NotSvg => "That file isn't an SVG drawing.",
        SvgRejection.NothingToDraw => "There's nothing to draw in that file.",
        _ => "That file couldn't be imported.",
    };

    /// <summary>
    /// The file's name, tidied.
    /// </summary>
    /// <remarks>
    /// The macOS rule, unchanged: underscores and hyphens become spaces, and a file with
    /// no usable name still produces a charm. "IMG_0421" is better than nothing, and the
    /// user never has to type a name to get a charm.
    /// </remarks>
    public static string NameFor(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path)
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();

        return stem.Length == 0 ? "Custom Charm" : stem;
    }

    private readonly record struct Measured(CharmMetrics Metrics, CharmPalette Palette, double Coverage);

    /// <summary>Rasterises once, and reads the weight, the framing and the colour off it.</summary>
    private static Measured? Measure(string markup)
    {
        using var document = new SKSvg();
        try
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markup));
            document.Load(stream);
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Failure("rendering an import", exception);
            return null;
        }

        if (document.Picture is null)
        {
            return null;
        }

        SKRect bounds = document.Picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(
            AnalysisPixels,
            AnalysisPixels,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        float scale = Math.Min(AnalysisPixels / bounds.Width, AnalysisPixels / bounds.Height);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(
            (AnalysisPixels - (bounds.Width * scale)) / 2,
            (AnalysisPixels - (bounds.Height * scale)) / 2);
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

        long opaque = 0;
        double red = 0;
        double green = 0;
        double blue = 0;
        int minX = int.MaxValue;
        int maxX = -1;
        int minY = int.MaxValue;
        int maxY = -1;

        for (int y = 0; y < AnalysisPixels; y++)
        {
            for (int x = 0; x < AnalysisPixels; x++)
            {
                int offset = ((y * AnalysisPixels) + x) * 4;
                byte alpha = pixels[offset + 3];
                if (alpha <= OpaqueThreshold)
                {
                    continue;
                }

                opaque++;

                // Premultiplied, so the channels are divided back out before averaging.
                double a = alpha / 255.0;
                blue += pixels[offset] / 255.0 / a;
                green += pixels[offset + 1] / 255.0 / a;
                red += pixels[offset + 2] / 255.0 / a;

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        if (opaque == 0 || maxX < minX)
        {
            return null;
        }

        double coverage = (double)opaque / (AnalysisPixels * (double)AnalysisPixels);

        // How the drawing was framed, read back off the square — the same measurement the
        // macOS importer makes after fitting a subject into one.
        double longest = Math.Max(maxX - minX + 1, maxY - minY + 1);
        double framing = Math.Clamp(longest / AnalysisPixels, 0.3, 1);

        // Coverage as it would have been at the shipped framing. Coverage alone measures
        // how much shape there is — a thin bar covers little of its square however it is
        // framed — but it also moves when the drawing is merely re-framed, and framing is
        // not weight. Dividing the framing out and multiplying the shipped one back in
        // keeps the first property and drops the second.
        double density = coverage * (SubjectFill * SubjectFill) / (framing * framing);
        double mass = Math.Clamp(2.0 + (3.2 * density), 2.0, 4.5);

        // Where the drawing stops at the top, as a fraction of the half-square. Only a
        // fallback: the splitter measures the knot from the artwork when the charm is
        // hung, exactly as it does for a shipped one.
        double topFromTop = (double)minY / AnalysisPixels;
        double knotInset = Math.Clamp((0.5 - topFromTop) / 0.5, 0.3, 1.0);

        var average = new CharmColor(red / opaque, green / opaque, blue / opaque);
        return new Measured(
            new CharmMetrics(mass, RadiusRatio: 0.12, knotInset),
            Derived(average),
            coverage);
    }

    /// <summary>A whole palette from one colour, by the macOS build's proportions.</summary>
    private static CharmPalette Derived(CharmColor baseColour) => new(
        baseColour,
        baseColour.Scaled(0.72),
        baseColour.Scaled(0.45),
        CharmColor.Interpolate(baseColour, new CharmColor(1, 1, 1), 0.55));
}
