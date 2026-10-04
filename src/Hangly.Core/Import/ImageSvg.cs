//
//  ImageSvg.cs
//  Hangly
//
//  The one kind of SVG Hangly draws on Windows: pictures in a wrapper.
//

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using Hangly.Core.Geometry;

namespace Hangly.Core.Import;

/// <summary>A 2D affine transform, <c>[a c e; b d f; 0 0 1]</c> as SVG writes it.</summary>
public readonly record struct Affine(double A, double B, double C, double D, double E, double F)
{
    public static Affine Identity { get; } = new(1, 0, 0, 1, 0, 0);

    /// <summary>This transform applied after <paramref name="inner"/>: a parent's transform times its child's.</summary>
    public Affine Then(Affine inner) => new(
        (A * inner.A) + (C * inner.B),
        (B * inner.A) + (D * inner.B),
        (A * inner.C) + (C * inner.D),
        (B * inner.C) + (D * inner.D),
        (A * inner.E) + (C * inner.F) + E,
        (B * inner.E) + (D * inner.F) + F);
}

/// <summary>One embedded picture and where it is drawn.</summary>
/// <param name="Transform">Everything between the document and the picture, outermost first.</param>
/// <param name="Box">The <c>image</c> element's own x, y, width and height, before the transform.</param>
/// <param name="Encoded">The PNG or JPEG bytes, as the data URI carried them.</param>
public sealed record ImageLayer(Affine Transform, Rect Box, byte[] Encoded);

/// <summary>A document that is nothing but pictures, in paint order.</summary>
public sealed record ImageSvgDocument(Rect ViewBox, IReadOnlyList<ImageLayer> Layers);

/// <summary>Reads an SVG that only places embedded PNG or JPEG pictures, and refuses anything else.</summary>
/// <remarks>
/// <para><b>Why this exists.</b> Windows' Smart App Control, and the App Control policies
/// companies set, block unsigned DLLs, and the library Hangly drew SVG with (Svg.Skia and
/// six companions) was unsigned. On those PCs it could not load: the overlay failed to
/// start, the installer's last step failed ("Install Partially Succeeded"), and 1,501
/// reports came from 189 installs. Every charm Hangly ships, and every charm made in Create
/// or from a photograph, is pictures placed in an SVG — no paths, no styles — so this
/// reads exactly that, and SkiaSharp, which Microsoft signs, draws it.</para>
///
/// <para><b>Strict on purpose.</b> Only <c>svg</c>, <c>g</c> and <c>image</c>, a short list
/// of attributes, transforms of translate, rotate, scale and matrix, and data URIs of PNG
/// or JPEG. Anything else returns null: a real vector drawing, which Hangly on Windows no
/// longer draws, and nothing this reader could get subtly wrong. No DTD is processed and
/// no external reference is ever followed.</para>
/// </remarks>
public static class ImageSvg
{
    /// <summary>The most this will read, so a hostile file cannot run it out of memory.</summary>
    public const int MaximumBytes = 64 * 1024 * 1024;

    private static readonly HashSet<string> Attributes = new(StringComparer.Ordinal)
    {
        "id", "data-name", "viewBox", "version", "transform", "width", "height", "x", "y", "href",
    };

    private static readonly Regex Transform = new(
        @"\s*(translate|rotate|scale|matrix)\s*\(([^)]*)\)\s*,?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Reads the file at <paramref name="path"/>, or null when it is not pictures in a wrapper.</summary>
    public static ImageSvgDocument? Load(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length is > 0 and <= MaximumBytes ? Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads <paramref name="markup"/>, or null when it is not pictures in a wrapper.</summary>
    public static ImageSvgDocument? Parse(string markup)
    {
        if (string.IsNullOrWhiteSpace(markup) || markup.Length > MaximumBytes)
        {
            return null;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(markup), settings);
            return Read(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static ImageSvgDocument? Read(XmlReader reader)
    {
        Rect? viewBox = null;
        var layers = new List<ImageLayer>();
        var stack = new Stack<Affine>();

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.LocalName == "g" && stack.Count > 1)
                {
                    stack.Pop();
                }

                continue;
            }

            if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA)
            {
                return null;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (!AttributesAllowed(reader))
            {
                return null;
            }

            switch (reader.LocalName)
            {
                case "svg" when viewBox is null && reader.Depth == 0:
                    viewBox = ViewBox(reader.GetAttribute("viewBox"));
                    if (viewBox is null)
                    {
                        return null;
                    }

                    stack.Push(Affine.Identity);
                    break;

                case "g" when viewBox is not null:
                    Affine? group = ParseTransform(reader.GetAttribute("transform"));
                    if (group is null)
                    {
                        return null;
                    }

                    if (!reader.IsEmptyElement)
                    {
                        stack.Push(stack.Peek().Then(group.Value));
                    }

                    break;

                case "image" when viewBox is not null:
                    ImageLayer? layer = Image(reader, stack.Peek());
                    if (layer is null)
                    {
                        return null;
                    }

                    layers.Add(layer);
                    break;

                default:
                    return null;
            }
        }

        return viewBox is Rect box && layers.Count > 0 ? new ImageSvgDocument(box, layers) : null;
    }

    private static bool AttributesAllowed(XmlReader reader)
    {
        if (!reader.MoveToFirstAttribute())
        {
            return true;
        }

        do
        {
            // Namespace declarations are how the document says what it is, not drawing.
            if (reader.Prefix == "xmlns" || reader.LocalName == "xmlns")
            {
                continue;
            }

            if (!Attributes.Contains(reader.LocalName))
            {
                reader.MoveToElement();
                return false;
            }
        }
        while (reader.MoveToNextAttribute());

        reader.MoveToElement();
        return true;
    }

    private static ImageLayer? Image(XmlReader reader, Affine parent)
    {
        Affine? own = ParseTransform(reader.GetAttribute("transform"));
        double? width = Number(reader.GetAttribute("width"));
        double? height = Number(reader.GetAttribute("height"));
        double x = Number(reader.GetAttribute("x")) ?? 0;
        double y = Number(reader.GetAttribute("y")) ?? 0;
        string? href = reader.GetAttribute("href", "http://www.w3.org/1999/xlink") ?? reader.GetAttribute("href");

        if (own is null || width is not > 0 || height is not > 0 || href is null)
        {
            return null;
        }

        int comma = href.IndexOf(',', StringComparison.Ordinal);
        string header = comma > 0 ? href[..comma].Replace(" ", string.Empty, StringComparison.Ordinal) : string.Empty;
        if (header is not ("data:image/png;base64" or "data:image/jpeg;base64" or "data:image/jpg;base64"))
        {
            return null;
        }

        try
        {
            byte[] encoded = Convert.FromBase64String(href[(comma + 1)..].Trim());
            return encoded.Length > 0 ? new ImageLayer(parent.Then(own.Value), new Rect(x, y, width.Value, height.Value), encoded) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Rect? ViewBox(string? text)
    {
        double[]? values = Numbers(text);
        return values is [var x, var y, var width, var height] && width > 0 && height > 0
            ? new Rect(x, y, width, height)
            : null;
    }

    /// <summary>A transform list, or identity for none, or null for anything unrecognised.</summary>
    public static Affine? ParseTransform(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Affine.Identity;
        }

        Affine total = Affine.Identity;
        int consumed = 0;
        foreach (Match match in Transform.Matches(text))
        {
            if (match.Index != consumed)
            {
                return null;
            }

            consumed = match.Index + match.Length;
            double[]? a = Numbers(match.Groups[2].Value);
            Affine? step = (match.Groups[1].Value, a) switch
            {
                ("translate", [var tx]) => new Affine(1, 0, 0, 1, tx, 0),
                ("translate", [var tx, var ty]) => new Affine(1, 0, 0, 1, tx, ty),
                ("scale", [var s]) => new Affine(s, 0, 0, s, 0, 0),
                ("scale", [var sx, var sy]) => new Affine(sx, 0, 0, sy, 0, 0),
                ("rotate", [var angle]) => Rotation(angle, 0, 0),
                ("rotate", [var angle, var cx, var cy]) => Rotation(angle, cx, cy),
                ("matrix", [var ma, var mb, var mc, var md, var me, var mf]) => new Affine(ma, mb, mc, md, me, mf),
                _ => null,
            };

            if (step is null)
            {
                return null;
            }

            total = total.Then(step.Value);
        }

        return consumed == text.TrimEnd().Length ? total : null;
    }

    private static Affine Rotation(double degrees, double cx, double cy)
    {
        double radians = degrees * Math.PI / 180;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        var turn = new Affine(cos, sin, -sin, cos, 0, 0);
        return new Affine(1, 0, 0, 1, cx, cy).Then(turn).Then(new Affine(1, 0, 0, 1, -cx, -cy));
    }

    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)
            ? value
            : null;

    private static double[]? Numbers(string? text)
    {
        if (text is null)
        {
            return null;
        }

        string[] parts = text.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var values = new double[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            if (Number(parts[index]) is not double value)
            {
                return null;
            }

            values[index] = value;
        }

        return values;
    }
}
