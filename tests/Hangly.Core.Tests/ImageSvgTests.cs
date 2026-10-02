//
//  ImageSvgTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Import;
using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// The reader that replaced the SVG library on Windows: everything Hangly ships or makes is
/// read, and nothing else is.
/// </summary>
public class ImageSvgTests
{
    /// <summary>One transparent pixel, as a PNG.</summary>
    private const string Pixel =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static string Wrapper(string inner = "", string root = "viewBox=\"0 0 10 20\"") =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" {root}>"
        + (inner.Length > 0 ? inner : $"<image width=\"10\" height=\"20\" xlink:href=\"data:image/png;base64,{Pixel}\"/>")
        + "</svg>";

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    [Fact(DisplayName = "Every charm Hangly ships is read")]
    public void EveryShippedCharmIsRead()
    {
        string charms = Path.Combine(RepositoryRoot(), "src", "Hangly.App", "Assets", "Charms");
        foreach (CharmCatalogEntry entry in CharmCatalog.All)
        {
            ImageSvgDocument? document = ImageSvg.Load(Path.Combine(charms, entry.FileName));
            Assert.True(document is not null, $"{entry.Id} ({entry.FileName}) was not read");
            Assert.NotEmpty(document!.Layers);
        }
    }

    [Fact(DisplayName = "Create's and a photograph's wrapper survive the sanitiser and are read")]
    public void CreatedCharmsAreRead()
    {
        // StudioRaster.ToSvg and RasterCharmSource write exactly this, and the importer
        // sanitises it before storing it.
        SvgSanitizeResult cleaned = SvgSanitizer.Sanitize(Wrapper());
        Assert.True(cleaned.IsAccepted);
        ImageSvgDocument? document = ImageSvg.Parse(cleaned.Markup!);
        Assert.NotNull(document);
        Assert.Equal(new Geometry.Rect(0, 0, 10, 20), document!.ViewBox);
        Assert.Equal(new Geometry.Rect(0, 0, 10, 20), Assert.Single(document.Layers).Box);
    }

    [Theory(DisplayName = "A vector drawing, or anything this could get wrong, is refused")]
    [InlineData("<path d=\"M0 0L10 10\"/>")]
    [InlineData("<rect width=\"10\" height=\"10\"/>")]
    [InlineData("<image width=\"10\" height=\"20\" style=\"opacity:.5\" href=\"data:image/png;base64,iVBORw0KGgo=\"/>")]
    [InlineData("<image width=\"10\" height=\"20\" href=\"https://example.com/a.png\"/>")]
    [InlineData("<image width=\"10\" height=\"20\" href=\"file:///C:/Windows/win.ini\"/>")]
    [InlineData("<image width=\"10\" height=\"20\" href=\"data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=\"/>")]
    [InlineData("<g transform=\"skewX(30)\"><image width=\"10\" height=\"20\" href=\"data:image/png;base64,iVBORw0KGgo=\"/></g>")]
    [InlineData("<g opacity=\"0.5\"><image width=\"10\" height=\"20\" href=\"data:image/png;base64,iVBORw0KGgo=\"/></g>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<text>hello</text>")]
    public void OtherDrawingsAreRefused(string inner) => Assert.Null(ImageSvg.Parse(Wrapper(inner)));

    [Fact(DisplayName = "No viewBox, no pictures, a DTD or broken XML is refused")]
    public void MalformedIsRefused()
    {
        Assert.Null(ImageSvg.Parse(Wrapper(root: "width=\"10\" height=\"20\"")));
        Assert.Null(ImageSvg.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><g/></svg>"));
        Assert.Null(ImageSvg.Parse("<!DOCTYPE svg [<!ENTITY x \"y\">]>" + Wrapper()));
        Assert.Null(ImageSvg.Parse("<svg viewBox=\"0 0 10 10\"><image"));
        Assert.Null(ImageSvg.Parse(string.Empty));
    }

    [Fact(DisplayName = "Transforms compose outermost first, as SVG applies them")]
    public void TransformsCompose()
    {
        Affine translate = ImageSvg.ParseTransform("translate(10 20)")!.Value;
        Assert.Equal(new Affine(1, 0, 0, 1, 10, 20), translate);

        // Turned a quarter about (5, 5): the point (10, 5) lands on (5, 10).
        Affine turn = ImageSvg.ParseTransform("rotate(90 5 5)")!.Value;
        Assert.Equal(5, (turn.A * 10) + (turn.C * 5) + turn.E, 6);
        Assert.Equal(10, (turn.B * 10) + (turn.D * 5) + turn.F, 6);

        // Scaled then moved: x' = 2x + 3.
        Affine both = ImageSvg.ParseTransform("translate(3) scale(2)")!.Value;
        Assert.Equal(2, both.A, 6);
        Assert.Equal(3, both.E, 6);

        Assert.Equal(Affine.Identity, ImageSvg.ParseTransform(null));
        Assert.Null(ImageSvg.ParseTransform("translate(1 2) nonsense"));
    }
}
