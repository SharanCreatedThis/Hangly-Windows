using Hangly.Core.Geometry;
using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

public class RopeBoundsTests
{
    private static RopeSnapshot Frame(Vec2 charmAt, double radius = 20) => new(
        [new Vec2(100, 0), new Vec2(100, 50), charmAt],
        [new CharmPlacement(charmAt, radius, Math.PI / 2, 0.9, 40, 60)],
        [new BeadPlacement(new Vec2(100, 30), Math.PI / 2, new Size(6, 10), 0)],
        1.02,
        false);

    [Fact]
    public void CoversTheCharmWithItsGlowAndShadow()
    {
        Rect bounds = RopeBounds.Of(Frame(new Vec2(100, 100)), cordWidth: 2)!.Value;

        double reach = (20 * RopeBounds.CharmReach) + RopeBounds.Margin;
        Assert.True(bounds.Left <= 100 - reach);
        Assert.True(bounds.Right >= 100 + reach);
        Assert.True(bounds.Bottom >= 100 + reach);

        // Wider than the glow, which is the widest thing a charm draws.
        Assert.True(RopeBounds.CharmReach > RopeConfiguration.Layout.CharmHaloExtent);
    }

    [Fact]
    public void CoversTheAnchor()
    {
        Rect bounds = RopeBounds.Of(Frame(new Vec2(100, 100)), cordWidth: 2)!.Value;
        Assert.True(bounds.Top <= 0 - 1);
    }

    [Fact]
    public void IsMuchSmallerThanTheCanvasForOneCharm()
    {
        // The canvas at the shipped size is several hundred points on a side.
        Rect bounds = RopeBounds.Of(Frame(new Vec2(100, 100)), cordWidth: 2)!.Value;
        Assert.True(bounds.Width < 120);
    }

    [Fact]
    public void AnEmptyFrameHasNoBounds()
    {
        Assert.Null(RopeBounds.Of(new RopeSnapshot([], [], [], 1, false), 2));
    }

    [Fact]
    public void AMovedCharmIsCoveredWhereItWasAndWhereItIs()
    {
        Rect? before = RopeBounds.Of(Frame(new Vec2(60, 100)), 2);
        Rect? after = RopeBounds.Of(Frame(new Vec2(160, 100)), 2);
        Rect union = RopeBounds.Union(before, after)!.Value;

        Assert.True(union.Left <= before!.Value.Left);
        Assert.True(union.Right >= after!.Value.Right);
        Assert.Equal(after, RopeBounds.Union(null, after));
        Assert.Equal(before, RopeBounds.Union(before, null));
    }

    [Fact]
    public void AnIdenticalFrameHasNotMoved()
    {
        RopeSnapshot frame = Frame(new Vec2(100, 100));
        Assert.Equal(0, RopeBounds.LargestMove(frame, Frame(new Vec2(100, 100))));
    }

    [Fact]
    public void TheCharmMovingIsTheMove()
    {
        Assert.Equal(0.1, RopeBounds.LargestMove(Frame(new Vec2(100, 100)), Frame(new Vec2(100.1, 100))), 6);
    }

    [Fact]
    public void TurningCountsAtTheRim()
    {
        RopeSnapshot a = Frame(new Vec2(100, 100), radius: 20);
        RopeSnapshot b = a with { Charms = [a.Charms[0] with { Angle = a.Charms[0].Angle + 0.01 }] };

        // A hundredth of a radian barely moves the centre, but moves the corner of a
        // 20-point charm by 0.28 of a point.
        Assert.Equal(0.01 * 20 * Math.Sqrt(2), RopeBounds.LargestMove(a, b), 6);
    }

    [Fact]
    public void ADifferentRopeIsAlwaysAMove()
    {
        RopeSnapshot a = Frame(new Vec2(100, 100));
        Assert.Equal(double.PositiveInfinity, RopeBounds.LargestMove(a, a with { Beads = [] }));
        Assert.Equal(double.PositiveInfinity, RopeBounds.LargestMove(a, a with { IsDragging = true }));
        Assert.Equal(double.PositiveInfinity, RopeBounds.LargestMove(a, Frame(new Vec2(100, 100), radius: 21)));
    }
}
