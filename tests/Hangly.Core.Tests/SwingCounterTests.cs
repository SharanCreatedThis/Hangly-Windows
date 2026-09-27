using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>"Swings survived": one crossing of the vertical is one swing. Same cases as macOS.</summary>
public class SwingCounterTests
{
    private static int Count(params double[] offsets)
    {
        var counter = new SwingCounter();
        return offsets.Count(offset => counter.Observe(offset, ropeLength: 100));
    }

    [Fact]
    public void EachCrossingIsOneSwing() => Assert.Equal(3, Count(10, -10, 10, -10));

    [Fact]
    public void ThePassThroughTheDeadBandIsNotASwingOfItsOwn() => Assert.Equal(1, Count(10, 1, 0, -1, -10));

    [Fact]
    public void RestingNearCentreNeverCounts() => Assert.Equal(0, Count(1.9, -1.9, 1.5, -0.5, 0));

    [Fact]
    public void TheFirstSideSeenIsAStartNotASwing() => Assert.Equal(0, Count(-10, -12, -8));
}
