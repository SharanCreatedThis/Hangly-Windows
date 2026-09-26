using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Xunit;
using Xunit.Abstractions;

namespace Hangly.Core.Tests;

/// <summary>Reduced motion: less movement, for less long, and the same rope at rest.</summary>
/// <remarks>The macOS suite runs the same cases against its own solver.</remarks>
public class RopeMotionTests
{
    private static readonly Vec2 Anchor = new(260, 15);
    private const double Frame120 = 1.0 / 120.0;
    private readonly ITestOutputHelper output;

    public RopeMotionTests(ITestOutputHelper output) => this.output = output;

    private static RopeSimulation Rope(RopeMotion motion)
    {
        var rope = new RopeSimulation(RopeConfiguration.Default, Anchor);
        rope.SetMotion(motion);
        rope.Start();
        return rope;
    }

    /// <summary>Seconds until the solver declares the rope asleep, or the cap.</summary>
    private static double SecondsToSleep(RopeSimulation rope, double cap = 120)
    {
        for (int tick = 0; tick < (int)(cap / Frame120); tick++)
        {
            rope.Step(Frame120);
            if (rope.IsSleeping)
            {
                return tick * Frame120;
            }
        }

        return cap;
    }

    [Theory]
    [InlineData(MotionPreference.FollowSystem, false, RopeMotion.Full)]
    [InlineData(MotionPreference.FollowSystem, true, RopeMotion.Reduced)]
    [InlineData(MotionPreference.Reduced, false, RopeMotion.Reduced)]
    [InlineData(MotionPreference.Reduced, true, RopeMotion.Reduced)]
    [InlineData(MotionPreference.Full, false, RopeMotion.Full)]
    [InlineData(MotionPreference.Full, true, RopeMotion.Full)]
    public void ThePreferenceMeetsTheSystem(MotionPreference preference, bool systemReduces, RopeMotion expected)
    {
        Assert.Equal(expected, RopeMotionTable.Resolve(preference, systemReduces));
    }

    [Fact]
    public void FullMotionIsTheRopeThatShipped()
    {
        foreach (RopeStyle style in RopeStyleTable.All)
        {
            RopeConfiguration before = RopeConfiguration.Default.Applying(style, RopeTimeProfile.Night);
            RopeConfiguration after = RopeConfiguration.Default.Applying(style, RopeTimeProfile.Night, RopeMotion.Full);
            Assert.Equal(before, after);
        }
    }

    [Fact]
    public void ReducedMotionOnlyChangesHowMotionDiesAway()
    {
        RopeConfiguration full = RopeConfiguration.Default.Applying(RopeStyle.Thread);
        RopeConfiguration reduced = RopeConfiguration.Default.Applying(RopeStyle.Thread, motion: RopeMotion.Reduced);

        Assert.Equal((1 - full.Damping) * 4, 1 - reduced.Damping, 12);
        Assert.Equal(0.55, reduced.ThrowScale);

        // What the rope looks like and how it hangs is untouched.
        Assert.Equal(full.Gravity, reduced.Gravity);
        Assert.Equal(full.MaxStretchRatio, reduced.MaxStretchRatio);
        Assert.Equal(full.SegmentCount, reduced.SegmentCount);
        Assert.Equal(full.InitialAngle, reduced.InitialAngle);
    }

    [Fact]
    public void ReducedMotionAppearsHangingStill()
    {
        RopeSimulation reduced = Rope(RopeMotion.Reduced);
        RopeSimulation full = Rope(RopeMotion.Full);

        double reducedSleep = SecondsToSleep(reduced);
        double fullSleep = SecondsToSleep(full);
        output.WriteLine($"launch: full sleeps after {fullSleep:0.0} s, reduced after {reducedSleep:0.0} s");

        Assert.True(reducedSleep < 2, $"reduced rope took {reducedSleep:0.0} s to settle from launch");
        Assert.True(fullSleep > 10, "full motion still greets you with a swing");
    }

    [Fact]
    public void AReducedNudgeSettlesFarSooner()
    {
        RopeSimulation reduced = Rope(RopeMotion.Reduced);
        RopeSimulation full = Rope(RopeMotion.Full);
        SecondsToSleep(reduced);
        SecondsToSleep(full);

        reduced.Push();
        full.Push();
        double reducedSleep = SecondsToSleep(reduced);
        double fullSleep = SecondsToSleep(full);
        output.WriteLine($"nudge: full sleeps after {fullSleep:0.0} s, reduced after {reducedSleep:0.0} s");

        Assert.True(reducedSleep < fullSleep / 2.5, $"reduced {reducedSleep:0.0} s against full {fullSleep:0.0} s");
    }

    [Fact]
    public void AReducedThrowSettlesFarSooner()
    {
        double Thrown(RopeMotion motion)
        {
            RopeSimulation rope = Rope(motion);
            SecondsToSleep(rope);
            Vec2 charm = rope.Points[^1].Position;
            Assert.True(rope.BeginDrag(charm));
            for (int tick = 0; tick < 12; tick++)
            {
                rope.UpdateDrag(new Vec2(charm.X + 40, charm.Y), new Vec2(900, 0));
                rope.Step(Frame120);
            }

            rope.EndDrag();
            return SecondsToSleep(rope);
        }

        double full = Thrown(RopeMotion.Full);
        double reduced = Thrown(RopeMotion.Reduced);
        output.WriteLine($"throw: full sleeps after {full:0.0} s, reduced after {reduced:0.0} s");

        // The macOS suite measured 8.0 s against 19.9 s: seconds, and at least twice as fast.
        Assert.True(reduced < 10, $"reduced {reduced:0.0} s");
        Assert.True(reduced < full / 2, $"reduced {reduced:0.0} s against full {full:0.0} s");
    }

    [Fact]
    public void AtRestBothLookTheSame()
    {
        RopeSimulation reduced = Rope(RopeMotion.Reduced);
        RopeSimulation full = Rope(RopeMotion.Full);
        SecondsToSleep(reduced);
        SecondsToSleep(full);

        double worst = 0;
        for (int index = 0; index < full.Points.Length; index++)
        {
            worst = Math.Max(worst, (full.Points[index].Position - reduced.Points[index].Position).Magnitude);
        }

        output.WriteLine($"rest pose: largest difference {worst:0.00} pt; charm x full {full.Points[^1].Position.X:0.00} reduced {reduced.Points[^1].Position.X:0.00} anchor {Anchor.X}");
        // Within a point and a half: a full-motion rope goes to sleep a hair off centre,
        // wherever the last of its swing ran out (measured 0.95 pt), while the reduced one
        // hangs dead straight. Same rope, same shape.
        Assert.True(worst < 1.5);
    }

    [Fact]
    public void SwitchingIsLiveAndCalmsASwingInPlace()
    {
        RopeSimulation rope = Rope(RopeMotion.Full);
        for (int tick = 0; tick < 120; tick++)
        {
            rope.Step(Frame120);
        }

        Vec2 before = rope.Points[^1].Position;
        rope.SetMotion(RopeMotion.Reduced);

        // Nothing jumps: the charm is exactly where it was, and keeps moving.
        Assert.Equal(before, rope.Points[^1].Position);
        Assert.False(rope.IsSleeping);
        Assert.True(SecondsToSleep(rope) < 20);
    }
}
