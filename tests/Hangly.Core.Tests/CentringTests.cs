using Hangly.Core.Geometry;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Every Hangly window opens in the middle of the display under the pointer. macOS: <c>WindowCenteringTests</c>.</summary>
public sealed class CentringTests
{
    [Fact]
    public void CentredInTheWorkAreaOfWhicheverDisplayItIs()
    {
        Assert.Equal((400, 140), Centring.Origin(0, 0, 1920, 1040, 1120, 760));

        // A second display to the left of the main one, and a little lower.
        (int x, int y) = Centring.Origin(-2560, 120, 2560, 1400, 1680, 1200);
        Assert.Equal((-2120, 220), (x, y));
    }

    [Fact]
    public void AWindowBiggerThanTheDisplayKeepsItsTitleBarOnScreen() =>
        Assert.Equal((0, 0), Centring.Origin(0, 0, 800, 500, 1120, 800));
}
