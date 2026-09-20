//
//  SvgSanitizerTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Import;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// The file arrives from outside, so these are not tests of a formatter — each one is a
/// thing an SVG can ask a program to do, and an assertion that it no longer asks.
/// </summary>
public class SvgSanitizerTests
{
    private const string Drawing = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 10 10"">
        <circle cx=""5"" cy=""5"" r=""4"" fill=""red""/></svg>";

    private static string Clean(string markup)
    {
        SvgSanitizeResult result = SvgSanitizer.Sanitize(markup);
        Assert.True(result.IsAccepted, $"rejected: {result.Rejection}");
        return result.Markup!;
    }

    [Fact(DisplayName = "An ordinary drawing comes through intact")]
    public void OrdinaryDrawingSurvives()
    {
        string cleaned = Clean(Drawing);
        Assert.Contains("circle", cleaned, StringComparison.Ordinal);
        Assert.Contains("red", cleaned, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Script is removed")]
    public void ScriptIsRemoved()
    {
        string cleaned = Clean(@"<svg xmlns=""http://www.w3.org/2000/svg"">
            <script>fetch('http://example.com')</script><circle r=""1""/></svg>");

        Assert.DoesNotContain("script", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fetch", cleaned, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Event handler attributes are removed")]
    public void EventHandlersAreRemoved()
    {
        string cleaned = Clean(@"<svg xmlns=""http://www.w3.org/2000/svg"">
            <circle r=""1"" onload=""alert(1)"" onclick=""alert(2)"" fill=""blue""/></svg>");

        Assert.DoesNotContain("onload", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blue", cleaned, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "foreignObject is removed, because its contents are not a drawing")]
    public void ForeignObjectIsRemoved()
    {
        string cleaned = Clean(@"<svg xmlns=""http://www.w3.org/2000/svg"">
            <foreignObject><body xmlns=""http://www.w3.org/1999/xhtml"">hello</body></foreignObject>
            <rect width=""1"" height=""1""/></svg>");

        Assert.DoesNotContain("foreignObject", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hello", cleaned, StringComparison.Ordinal);
    }

    /// <summary>
    /// XXE. An entity that reads a file off this machine and draws it is the reason the
    /// DTD is prohibited outright rather than filtered.
    /// </summary>
    [Fact(DisplayName = "A document with a DTD is refused outright")]
    public void DtdIsRefused()
    {
        SvgSanitizeResult result = SvgSanitizer.Sanitize(
            @"<?xml version=""1.0""?><!DOCTYPE svg [<!ENTITY xxe SYSTEM ""file:///etc/passwd"">]>
              <svg xmlns=""http://www.w3.org/2000/svg""><text>&xxe;</text></svg>");

        Assert.False(result.IsAccepted);
        Assert.Equal(SvgRejection.NotXml, result.Rejection);
    }

    /// <summary>Billion laughs. Prohibiting the DTD closes this at the same door.</summary>
    [Fact(DisplayName = "An entity-expansion bomb is refused")]
    public void EntityBombIsRefused()
    {
        SvgSanitizeResult result = SvgSanitizer.Sanitize(
            @"<?xml version=""1.0""?><!DOCTYPE svg [
              <!ENTITY a ""aaaaaaaaaa""><!ENTITY b ""&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;"">
              <!ENTITY c ""&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;"">]>
              <svg xmlns=""http://www.w3.org/2000/svg""><text>&c;</text></svg>");

        Assert.False(result.IsAccepted);
    }

    [Theory(DisplayName = "A reference that leaves the file is removed")]
    [InlineData("http://example.com/x.png")]
    [InlineData("https://example.com/x.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void ExternalReferencesAreRemoved(string target)
    {
        string cleaned = Clean($@"<svg xmlns=""http://www.w3.org/2000/svg"">
            <image href=""{target}"" width=""10"" height=""10""/><rect width=""1"" height=""1""/></svg>");

        Assert.DoesNotContain("example.com", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwd", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "A reference inside the same file is kept, because that is how gradients work")]
    public void LocalReferencesAreKept()
    {
        string cleaned = Clean(@"<svg xmlns=""http://www.w3.org/2000/svg"">
            <defs><linearGradient id=""g""><stop offset=""0"" stop-color=""red""/></linearGradient></defs>
            <rect width=""10"" height=""10"" fill=""url(#g)""/></svg>");

        Assert.Contains("linearGradient", cleaned, StringComparison.Ordinal);
        Assert.Contains("url(#g)", cleaned, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A stylesheet that fetches is defused, and the rest of it kept")]
    public void ExternalCssIsRemoved()
    {
        string cleaned = Clean(@"<svg xmlns=""http://www.w3.org/2000/svg"">
            <style>@import url('http://example.com/evil.css'); .a { fill: green; }</style>
            <rect class=""a"" width=""1"" height=""1""/></svg>");

        Assert.DoesNotContain("@import", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("green", cleaned, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Elements from another namespace do not survive")]
    public void ForeignNamespacesAreRemoved()
    {
        string cleaned = Clean(@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:h=""http://www.w3.org/1999/xhtml"">
            <h:iframe src=""http://example.com""/><rect width=""1"" height=""1""/></svg>");

        Assert.DoesNotContain("iframe", cleaned, StringComparison.OrdinalIgnoreCase);
    }

    [Theory(DisplayName = "What is not a drawing is refused with a reason")]
    [InlineData("", SvgRejection.Empty)]
    [InlineData("   ", SvgRejection.Empty)]
    [InlineData("not xml at all", SvgRejection.NotXml)]
    [InlineData("<html><body/></html>", SvgRejection.NotSvg)]
    public void RejectionsAreExplained(string markup, SvgRejection expected) =>
        Assert.Equal(expected, SvgSanitizer.Sanitize(markup).Rejection);

    [Fact(DisplayName = "An SVG with nothing in it is refused rather than accepted as a blank charm")]
    public void EmptySvgIsRefused() => Assert.Equal(
        SvgRejection.NothingToDraw,
        SvgSanitizer.Sanitize(@"<svg xmlns=""http://www.w3.org/2000/svg""></svg>").Rejection);

    [Fact(DisplayName = "An SVG that is only script is refused, because nothing is left after cleaning")]
    public void ScriptOnlySvgIsRefused() => Assert.Equal(
        SvgRejection.NothingToDraw,
        SvgSanitizer.Sanitize(@"<svg xmlns=""http://www.w3.org/2000/svg""><script>x</script></svg>").Rejection);

    [Fact(DisplayName = "A file larger than the limit is refused before it reaches the parser")]
    public void OversizeIsRefused()
    {
        string huge = @"<svg xmlns=""http://www.w3.org/2000/svg""><!--"
            + new string('x', SvgSanitizer.MaximumBytes)
            + "--><rect/></svg>";

        Assert.Equal(SvgRejection.TooLarge, SvgSanitizer.Sanitize(huge).Rejection);
    }

    [Fact(DisplayName = "What was taken out is reported, so the user can be told")]
    public void RemovalsAreReported()
    {
        SvgSanitizeResult result = SvgSanitizer.Sanitize(@"<svg xmlns=""http://www.w3.org/2000/svg"">
            <script>x</script><circle r=""1"" onload=""y""/></svg>");

        Assert.True(result.IsAccepted);
        Assert.Contains("<script>", result.Removed);
        Assert.Contains("@onload", result.Removed);
    }
}
