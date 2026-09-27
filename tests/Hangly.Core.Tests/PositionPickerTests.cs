using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The miniature desktop: a point on the picture is a point on the display, in the macOS range.</summary>
public class PositionPickerTests
{
    [Fact]
    public void APointOnThePictureIsThePositionAndTheOffsetInPoints()
    {
        (double position, double offsetY) = PositionPicker.FromUnit(0.25, 0.1, areaHeight: 1000);

        Assert.Equal(0.25, position);
        Assert.Equal(100, offsetY);
    }

    [Fact]
    public void ThePictureStopsAtTheMacBandAndTheEdges()
    {
        Assert.Equal((1.0, 340.0), PositionPicker.FromUnit(1.4, 0.9, 1000));
        Assert.Equal((0.0, 0.0), PositionPicker.FromUnit(-0.2, -0.3, 1000));
    }

    [Fact]
    public void TheStoredValuesComeBackToTheSamePoint()
    {
        (double position, double offsetY) = PositionPicker.FromUnit(0.6, 0.2, 900);

        Assert.Equal((0.6, 0.2), PositionPicker.UnitPoint(position, offsetY, 900));
    }

    [Theory]
    [InlineData(5000, 1200)]
    [InlineData(-900, -400)]
    [InlineData(150, 150)]
    public void TheStoredOffsetKeepsToTheMacRange(double stored, double kept)
    {
        Assert.Equal(kept, new OverlaySettings { OffsetY = stored }.Clamped().OffsetY);
    }
}
