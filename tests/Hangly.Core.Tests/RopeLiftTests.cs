//
//  RopeLiftTests.cs
//  Hangly
//
//  A charm lifted toward the anchor winds its cord in (RopeSimulation.Lift): no slack to fold, and no figure whose rope
//  is lost inside its own outline. macOS's RopeLiftTests.
//

using Hangly.Core.Geometry;
using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

public class RopeLiftTests
{
    private static readonly Vec2 Anchor = new(260, 15);
    private const double Frame120 = 1.0 / 120.0;

    private static RopeSimulation LiftedRope()
    {
        var rope = new RopeSimulation(RopeConfiguration.Default, Anchor);
        rope.Start();
        for (int tick = 0; tick < 600; tick++)
        {
            rope.Step(Frame120);
        }

        Vec2 start = rope.Points[20].Position;
        var target = new Vec2(Anchor.X + 70, Anchor.Y + 90);
        rope.BeginDrag(start);
        for (int tick = 0; tick <= 240; tick++)
        {
            double along = Math.Min(1, tick / 120.0);
            rope.UpdateDrag(start + ((target - start) * along), Vec2.Zero);
            rope.Step(Frame120);
        }

        return rope;
    }

    /// <summary>The sharpest turn the rope makes at any node, in degrees.</summary>
    private static double SharpestBend(RopeSimulation rope)
    {
        double sharpest = 0;
        for (int index = 1; index < rope.Points.Length - 1; index++)
        {
            Vec2 above = rope.Points[index].Position - rope.Points[index - 1].Position;
            Vec2 below = rope.Points[index + 1].Position - rope.Points[index].Position;
            if (above.Magnitude <= 0.5 || below.Magnitude <= 0.5)
            {
                continue;
            }

            double turn = Math.Atan2(below.Y, below.X) - Math.Atan2(above.Y, above.X);
            while (turn > Math.PI)
            {
                turn -= 2 * Math.PI;
            }

            while (turn < -Math.PI)
            {
                turn += 2 * Math.PI;
            }

            sharpest = Math.Max(sharpest, Math.Abs(turn) * 180 / Math.PI);
        }

        return sharpest;
    }

    [Fact(DisplayName = "A charm lifted toward the anchor winds its cord in rather than folding it")]
    public void LiftWindsTheCordIn()
    {
        RopeSimulation rope = LiftedRope();
        Assert.True(rope.LiftReel < 0.6, $"reel {rope.LiftReel}");

        // Folded, the slack turned 45° at a single node (nimbu-mirchi, 5 Oct).
        Assert.True(SharpestBend(rope) < 15, $"bend {SharpestBend(rope)}");
    }

    [Fact(DisplayName = "Let go, the cord pays out in full and the rope is the rope again")]
    public void ReleasePaysTheCordOut()
    {
        RopeSimulation rope = LiftedRope();
        rope.EndDrag();
        for (int tick = 0; tick < 480; tick++)
        {
            rope.Step(Frame120);
        }

        Assert.Equal(1, rope.LiftReel);
        double reach = (rope.Points[20].Position - Anchor).Magnitude;
        Assert.True(reach > RopeConfiguration.Default.SegmentLength * 20 * 0.9, $"reach {reach}");
    }
}
