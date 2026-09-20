//
//  SvgSanitizer.cs
//  Hangly
//
//  Taking a drawing out of a file somebody sent, and leaving the rest behind.
//

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Hangly.Core.Import;

/// <summary>Why an SVG could not be accepted.</summary>
public enum SvgRejection
{
    None,
    NotXml,
    NotSvg,
    Empty,
    TooLarge,
    NothingToDraw,
}

/// <summary>The result of cleaning a file: the drawing, or the reason there isn't one.</summary>
public sealed record SvgSanitizeResult(
    SvgRejection Rejection,
    string? Markup,
    IReadOnlyList<string> Removed)
{
    public bool IsAccepted => Rejection == SvgRejection.None && Markup is not null;
}

/// <summary>Strips everything from an SVG that is not drawing.</summary>
/// <remarks>
/// <b>An SVG is a document, not a picture.</b> It can carry script, fetch remote
/// resources, embed other documents through <c>foreignObject</c>, and declare entities
/// that expand until the machine gives up. Hangly's renderer would ignore most of that,
/// but "the library we use today happens not to run it" is not a security position — the
/// file arrives from outside, so it is rewritten into the subset that draws and nothing
/// else survives the trip.
///
/// <para>What is removed, and why:</para>
/// <list type="bullet">
/// <item><description><b>The DTD, always.</b> Parsed with
/// <see cref="DtdProcessing.Prohibit"/>, which closes both XXE — an entity that reads a
/// file off this machine and draws it — and the billion-laughs expansion.</description></item>
/// <item><description><b><c>script</c> and every <c>on*</c> attribute.</b> The obvious
/// one.</description></item>
/// <item><description><b><c>foreignObject</c>.</b> Its contents are not SVG; it is how
/// arbitrary HTML gets inside a picture.</description></item>
/// <item><description><b>Anything in a foreign namespace.</b> Same argument, without
/// having to enumerate the elements.</description></item>
/// <item><description><b>References that leave the file.</b> An <c>href</c> is kept only
/// when it points at a fragment of this same document. Remote ones are a way to make the
/// app fetch a URL of somebody else's choosing; <c>file:</c> reads the disk;
/// <c>javascript:</c> speaks for itself.</description></item>
/// <item><description><b><c>@import</c> and external <c>url()</c> in styles.</b> The same
/// fetch, wearing CSS.</description></item>
/// </list>
///
/// <para><b>What this does not promise.</b> It is not a renderer hardening pass. A file
/// that survives this is still parsed by Skia, and a malformed path can still be a bug in
/// Skia rather than here. What it does promise is that nothing in the file is asking for
/// anything beyond the file itself.</para>
/// </remarks>
public static class SvgSanitizer
{
    /// <summary>The largest file worth accepting.</summary>
    /// <remarks>
    /// The charms that ship are a few kilobytes; the largest is well under a hundred.
    /// Two megabytes is generous enough that no honest drawing is refused and small
    /// enough that a pathological one cannot be handed to the parser.
    /// </remarks>
    public const int MaximumBytes = 2 * 1024 * 1024;

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    private static readonly string[] BannedElements = ["script", "foreignObject", "animate", "set", "handler"];

    /// <summary>`@import`, and any url() that names a scheme rather than a fragment.</summary>
    private static readonly Regex ExternalCss = new(
        @"@import[^;]*;|url\(\s*['""]?\s*(?!#)[a-zA-Z][a-zA-Z0-9+.\-]*:[^)]*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Cleans the markup, or says why it cannot be used.</summary>
    public static SvgSanitizeResult Sanitize(string markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return new SvgSanitizeResult(SvgRejection.Empty, null, []);
        }

        if (System.Text.Encoding.UTF8.GetByteCount(markup) > MaximumBytes)
        {
            return new SvgSanitizeResult(SvgRejection.TooLarge, null, []);
        }

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                // Closes XXE and billion-laughs in one line. Nothing a charm needs is
                // expressed in a DTD.
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            using var reader = XmlReader.Create(new StringReader(markup), settings);
            document = XDocument.Load(reader);
        }
        catch (Exception)
        {
            return new SvgSanitizeResult(SvgRejection.NotXml, null, []);
        }

        if (document.Root is not XElement root || root.Name.LocalName != "svg")
        {
            return new SvgSanitizeResult(SvgRejection.NotSvg, null, []);
        }

        var removed = new List<string>();
        Clean(root, removed);

        if (!HasDrawing(root))
        {
            return new SvgSanitizeResult(SvgRejection.NothingToDraw, null, removed);
        }

        return new SvgSanitizeResult(SvgRejection.None, document.ToString(SaveOptions.DisableFormatting), removed);
    }

    private static void Clean(XElement element, List<string> removed)
    {
        foreach (XElement child in element.Elements().ToList())
        {
            string name = child.Name.LocalName;

            bool foreign = child.Name.Namespace != XNamespace.None && child.Name.Namespace != Svg;
            if (foreign || BannedElements.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                removed.Add($"<{name}>");
                child.Remove();
                continue;
            }

            Clean(child, removed);
        }

        foreach (XAttribute attribute in element.Attributes().ToList())
        {
            string name = attribute.Name.LocalName;

            if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                removed.Add($"@{name}");
                attribute.Remove();
                continue;
            }

            bool isReference = name is "href" &&
                (attribute.Name.Namespace == XLink || attribute.Name.Namespace == XNamespace.None);

            if (isReference && !IsLocalReference(attribute.Value))
            {
                removed.Add($"@{name} (external)");
                attribute.Remove();
                continue;
            }

            if (name is "style" && ExternalCss.IsMatch(attribute.Value))
            {
                removed.Add("@style (external url)");
                attribute.Value = ExternalCss.Replace(attribute.Value, string.Empty);
            }
        }

        if (element.Name.LocalName is "style" && ExternalCss.IsMatch(element.Value))
        {
            removed.Add("<style> (external url)");
            element.Value = ExternalCss.Replace(element.Value, string.Empty);
        }
    }

    /// <summary>A reference is kept only when it points inside this same document.</summary>
    /// <summary>
    /// Whether a reference stays inside this file.
    /// </summary>
    /// <remarks>
    /// Two kinds qualify, and only two.
    ///
    /// <para><b>A fragment</b> — <c>#gradient</c> — which names something in the same
    /// document and is how gradients, clips and masks are wired.</para>
    ///
    /// <para><b>An inline raster</b> — <c>data:image/png;base64,…</c> — which carries its
    /// own bytes and fetches nothing. Refusing these was a real defect, found by putting
    /// an ordinary Illustrator export through the audit: most SVGs that contain a
    /// photograph carry it exactly this way, and every one of this collection's own
    /// seventy charms is built like it. Stripping the reference left a blank charm, which
    /// is a worse answer than refusing the file would have been because it looked like it
    /// had worked.</para>
    ///
    /// <para><c>data:image/svg+xml</c> is deliberately <b>not</b> on the list. It is a
    /// document rather than a picture, it can carry script, and it would arrive already
    /// decoded past everything above.</para>
    /// </remarks>
    private static bool IsLocalReference(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith('#'))
        {
            return true;
        }

        foreach (string prefix in InlineRasterPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The raster media types an inline <c>data:</c> reference may declare.</summary>
    private static readonly string[] InlineRasterPrefixes =
    [
        "data:image/png;",
        "data:image/jpeg;",
        "data:image/jpg;",
        "data:image/gif;",
        "data:image/webp;",
        "data:image/bmp;",
    ];

    /// <summary>Whether anything is left that would put ink on the page.</summary>
    private static bool HasDrawing(XElement root)
    {
        // `g` is not on this list, and `defs` is excluded entirely. A group is a container
        // rather than a mark, and a definition is something to be referenced later — an
        // SVG whose only content is <defs><g/></defs> draws nothing at all, and used to be
        // accepted on the strength of that `g`. It imported as a blank charm.
        string[] drawing =
        [
            "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
            "text", "use", "image",
        ];

        return root.Descendants()
            .Where(element => !element.Ancestors().Any(a => a.Name.LocalName == "defs"))
            .Any(element => drawing.Contains(element.Name.LocalName, StringComparer.Ordinal));
    }
}
