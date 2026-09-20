//
//  HostileSvgAudit.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Import;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Every hostile SVG in the corpus, put through the real sanitiser.</summary>
/// <remarks>
/// Read off disk rather than built as strings. An attack that only exists as a C# literal
/// has never been through a file system, an encoding or a byte-order mark, and two of the
/// files here are specifically about that.
///
/// <para>The corpus lives in <c>tests/Hangly.Core.Tests/Hostile</c> and is copied beside
/// the test binary. Adding a file to it adds a case.</para>
/// </remarks>
public class HostileSvgAudit
{
    private static string CorpusPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Hostile", name);

    private static SvgSanitizeResult Run(string name) =>
        SvgSanitizer.Sanitize(File.ReadAllText(CorpusPath(name)));

    /// <summary>Files that must never be accepted, whatever else happens.</summary>
    public static TheoryData<string> MustReject() =>
    [
        "xxe-file.svg", "xxe-unix.svg", "xxe-http.svg", "xxe-parameter.svg",
        "billion-laughs.svg", "external-dtd.svg", "dtd-internal.svg",
        "malformed-unclosed.svg", "malformed-partial.svg", "malformed-junk.svg",
        "empty.svg", "whitespace.svg", "no-drawing.svg", "too-big.svg",
    ];

    [Theory(DisplayName = "Refused outright")]
    [MemberData(nameof(MustReject))]
    public void IsRefused(string name)
    {
        SvgSanitizeResult result = Run(name);
        Assert.False(result.IsAccepted, $"{name} was accepted; rejection was {result.Rejection}");
        Assert.NotEqual(SvgRejection.None, result.Rejection);
    }

    /// <summary>
    /// Files that may be accepted, but only with the dangerous part gone.
    /// </summary>
    /// <remarks>
    /// Refusing these outright would be defensible too. What is not defensible is keeping
    /// the payload, so each case names a string that must not survive.
    /// </remarks>
    public static TheoryData<string, string[]> MustStrip() =>
        new()
        {
            { "script-tag.svg", ["<script", "fetch("] },
            { "script-cdata.svg", ["<script", "alert"] },
            { "onload.svg", ["onload", "onclick"] },
            { "onmouseover.svg", ["onmouseover", "onfocus"] },
            { "foreignobject.svg", ["foreignObject", "iframe"] },
            { "embedded-html.svg", ["script"] },
            { "css-import.svg", ["@import", "evil"] },
            { "css-external-url.svg", ["http://evil"] },
            { "css-file-url.svg", ["file://"] },
            { "href-file.svg", ["file://"] },
            { "href-http.svg", ["http://evil"] },
            { "href-https.svg", ["https://evil"] },
            { "href-javascript.svg", ["javascript:"] },
            { "use-external.svg", ["http://evil"] },
            { "animate.svg", ["<animate"] },
            { "data-uri-html.svg", ["text/html"] },
        };

    [Theory(DisplayName = "Accepted only with the payload gone")]
    [MemberData(nameof(MustStrip))]
    public void PayloadIsStripped(string name, string[] forbidden)
    {
        SvgSanitizeResult result = Run(name);
        if (!result.IsAccepted)
        {
            // Refusing is a stronger answer than cleaning. Either is fine.
            return;
        }

        foreach (string needle in forbidden)
        {
            Assert.DoesNotContain(needle, result.Markup!, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Files that are merely extreme, and must not hang or throw.</summary>
    public static TheoryData<string> MustSurvive() =>
    [
        "nested-svg.svg", "recursive-use.svg", "circular-use.svg",
        "giant-viewbox.svg", "huge-coords.svg", "deep-nesting.svg",
        "utf16-declared-utf8.svg", "bad-encoding.svg",
    ];

    [Theory(DisplayName = "Extreme but not hostile: answered, not hung")]
    [MemberData(nameof(MustSurvive))]
    public void IsAnsweredQuickly(string name)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        SvgSanitizeResult result = Run(name);
        clock.Stop();

        // An answer either way. The failure this guards against is the sanitiser never
        // returning at all, which is what a recursive reference or a billion-laughs
        // expansion does to a parser that allows it.
        Assert.True(
            clock.ElapsedMilliseconds < 5000,
            $"{name} took {clock.ElapsedMilliseconds} ms");

        Assert.True(result.IsAccepted || result.Rejection != SvgRejection.None);
    }

    public static TheoryData<string> MustAccept() =>
        ["benign.svg", "benign-fragment-href.svg", "benign-data-png.svg"];

    [Theory(DisplayName = "Ordinary artwork still imports")]
    [MemberData(nameof(MustAccept))]
    public void BenignIsAccepted(string name)
    {
        SvgSanitizeResult result = Run(name);
        Assert.True(result.IsAccepted, $"{name} was refused: {result.Rejection}");
        Assert.NotNull(result.Markup);
    }

    /// <summary>A fragment reference is the one kind of href that is kept.</summary>
    [Fact(DisplayName = "Fragment references survive, because gradients need them")]
    public void FragmentsSurvive()
    {
        SvgSanitizeResult result = Run("benign-fragment-href.svg");
        Assert.Contains("url(#g)", result.Markup!, StringComparison.Ordinal);
    }

    /// <summary>An embedded raster survives, because most real artwork is one.</summary>
    /// <remarks>
    /// This is the case the audit caught. Every charm this collection ships is an SVG
    /// wrapped around a PNG, and stripping the reference imported them as blank.
    /// </remarks>
    [Fact(DisplayName = "An inline raster survives, but an inline document does not")]
    public void InlineRastersSurvive()
    {
        SvgSanitizeResult kept = Run("benign-data-png.svg");
        Assert.True(kept.IsAccepted);
        Assert.Contains("data:image/png;base64,", kept.Markup!, StringComparison.Ordinal);

        // A data: URI naming a document rather than a picture is still refused, because
        // it would arrive already past everything above.
        SvgSanitizeResult stripped = Run("data-uri-html.svg");
        if (stripped.IsAccepted)
        {
            Assert.DoesNotContain("text/html", stripped.Markup!, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The whole corpus, as a table, so the audit can be read rather than inferred.</summary>
    [Fact(DisplayName = "Every file in the corpus is answered")]
    public void CorpusIsAnswered()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Hostile");
        string[] files = Directory.GetFiles(folder, "*.svg");
        Assert.True(files.Length >= 40, $"corpus is only {files.Length} files");

        var lines = new List<string>();
        foreach (string file in files.OrderBy(f => f))
        {
            SvgSanitizeResult result = SvgSanitizer.Sanitize(File.ReadAllText(file));
            string verdict = result.IsAccepted
                ? $"accepted, removed [{string.Join(" ", result.Removed)}]"
                : $"REFUSED {result.Rejection}";
            lines.Add($"{Path.GetFileName(file),-28} {verdict}");
        }

        File.WriteAllLines(@"C:\Mac\Home\Documents\hangly-shots\hostile.txt", lines);
    }
}
