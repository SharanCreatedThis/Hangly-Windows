using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Xunit;
using Xunit.Abstractions;

namespace Hangly.Core.Tests;

/// <summary>Rope → Elastic. The macOS suite runs the same cases against its own solver.</summary>
public sealed class ElasticTests
{
    private const double Frame120 = 1.0 / 120.0;
    private readonly ITestOutputHelper output;

    public ElasticTests(ITestOutputHelper output) => this.output = output;

    private static RopeSimulation SettledRope(RopePhysics physics, RopeMotion motion = RopeMotion.Full, RopeStyle style = RopeStyle.Thread)
    {
        var rope = new RopeSimulation(RopeConfiguration.Default, new Vec2(260, 15));
        rope.SetStyle(style);
        rope.SetPhysics(physics);
        rope.SetMotion(RopeMotion.Reduced);
        rope.Start();
        for (int tick = 0; tick < 120 * 60 && !rope.IsSleeping; tick++)
        {
            rope.Step(Frame120);
        }

        Assert.True(rope.IsSleeping);
        rope.SetMotion(motion);
        return rope;
    }

    private sealed record Pull(double Held, double Slackest, int Rebounds, double Sleep, double Fastest, double FromRest, IReadOnlyList<double> Peaks);

    /// <summary>Pulls the charm down and a little aside, past the rope's length, for a second; lets go; watches.</summary>
    private static Pull PullAndRelease(RopeSimulation rope)
    {
        CharmStackLayout.Slot slot = rope.CharmLayout.Slots[^1];
        Vec2 rest = rope.Points[slot.Node].Position;
        double length = rest.DistanceTo(rope.Anchor);
        Vec2 target = rope.Anchor + ((rest - rope.Anchor) * 1.4) + new Vec2(length * 0.15, 0);
        rope.BeginDrag(rest);
        for (int tick = 0; tick < 120; tick++)
        {
            rope.UpdateDrag(target, Vec2.Zero);
            rope.Step(Frame120);
        }

        double held = rope.Points[slot.Node].Position.DistanceTo(rope.Anchor) / length;
        rope.EndDrag();

        double slackest = double.PositiveInfinity, fastest = 0, previous = held;
        var peaks = new List<double>();
        int ticks = 0;
        bool shortening = true;
        for (; ticks < 120 * 300 && !rope.IsSleeping; ticks++)
        {
            rope.Step(Frame120);
            double now = rope.Points[slot.Node].Position.DistanceTo(rope.Anchor) / length;
            Assert.True(double.IsFinite(now));
            slackest = Math.Min(slackest, now);
            fastest = Math.Max(fastest, rope.Points[slot.Node].Displacement.Magnitude / rope.Configuration.FixedTimeStep);
            if (shortening && now > previous)
            {
                shortening = false;
            }
            else if (!shortening && now < previous)
            {
                // The top of a stretch: a peak, if the cord is visibly stretched there.
                if (previous > 1.03)
                {
                    peaks.Add(previous);
                }

                shortening = true;
            }

            previous = now;
        }

        return new Pull(held, slackest, peaks.Count, ticks / 120.0, fastest, rope.Points[slot.Node].Position.DistanceTo(rest), peaks);
    }

    [Fact]
    public void TwoModesInTheSameOrderAndWordsAsMacOSStandardByDefault()
    {
        Assert.Equal(["Standard", "Elastic"], Enum.GetValues<RopePhysics>().Select(ElasticTable.TitleOf));
        Assert.Equal(RopePhysics.Standard, new OverlaySettings().RopePhysics);
        var settings = new AppSettings { Overlay = new OverlaySettings { RopePhysics = RopePhysics.Elastic } };
        Assert.Equal(RopePhysics.Elastic, AppSettings.FromJson(settings.ToJson(), out _).Overlay.RopePhysics);
    }

    [Theory]
    [InlineData(RopeStyle.Thread)]
    [InlineData(RopeStyle.GoldChain)]
    [InlineData(RopeStyle.Neon)]
    public void AtRestAnElasticRopeHangsExactlyWhereAStandardOneDoes(RopeStyle style)
    {
        RopeSimulation standard = SettledRope(RopePhysics.Standard, style: style);
        RopeSimulation elastic = SettledRope(RopePhysics.Elastic, style: style);
        CharmStackLayout.Slot slot = standard.CharmLayout.Slots[^1];
        double apart = standard.Points[slot.Node].Position.DistanceTo(elastic.Points[slot.Node].Position);
        output.WriteLine($"{style}: {apart:0.000} pt apart at rest");
        // Under a pixel: what the solver's convergence tolerance leaves, measured 0.30 to 0.52.
        Assert.True(apart < 1, $"{apart:0.000} pt");
    }

    [Fact]
    public void StandardIsTheCordThatShippedAPullStopsAtItsLength()
    {
        Pull pull = PullAndRelease(SettledRope(RopePhysics.Standard));
        Assert.True(pull.Held < 1.001, $"held at {pull.Held:0.000}");
    }

    [Fact]
    public void APullStretchesTheCordItReboundsAndSettlesWhereItHung()
    {
        Pull pull = PullAndRelease(SettledRope(RopePhysics.Elastic));
        output.WriteLine($"held {pull.Held:0.000} slackest {pull.Slackest:0.000} rebounds {pull.Rebounds} sleep {pull.Sleep:0.0}s fastest {pull.Fastest:0} pt/s; {pull.FromRest:0.00} pt from rest");

        Assert.InRange(pull.Held, 1.12, ElasticTable.Ceiling + 0.001);
        Assert.True(pull.Rebounds >= 2, $"{pull.Rebounds} rebounds");
        Assert.True(pull.Slackest < 0.97, "overshoots past where it hangs");
        Assert.True(pull.Sleep < 60, $"asleep after {pull.Sleep:0.0} s");
        Assert.True(pull.FromRest < 2, $"{pull.FromRest:0.00} pt from rest");
    }

    [Fact]
    public void NoReboundOutgrowsTheFirstAndTheBounceIsOverInAFew()
    {
        // After the bounce the charm swings on — the pull was a little to one side — and the
        // cord gives a hair at the bottom of each swing, which is the tail of small, level
        // peaks measured here (1.031, 1.033, 1.034 ...). What must never happen is a peak
        // growing past an earlier bounce: that would be energy from nowhere.
        Pull pull = PullAndRelease(SettledRope(RopePhysics.Elastic));
        output.WriteLine(string.Join(" ", pull.Peaks.Select(peak => peak.ToString("0.0000"))));
        Assert.NotEmpty(pull.Peaks);
        Assert.All(pull.Peaks, peak => Assert.True(peak <= pull.Peaks[0] + 1e-9));
        Assert.True(pull.Peaks[0] < pull.Held);
        // A rubber band bounces a handful of times, not forever.
        Assert.True(pull.Peaks.Count(peak => peak > 1.05) <= 8);
    }

    [Fact]
    public void ReducedMotionStillStretchesButReboundsLess()
    {
        Pull full = PullAndRelease(SettledRope(RopePhysics.Elastic, RopeMotion.Full));
        Pull reduced = PullAndRelease(SettledRope(RopePhysics.Elastic, RopeMotion.Reduced));
        Assert.True(reduced.Held > 1.12);
        Assert.True(reduced.Rebounds < full.Rebounds, $"{reduced.Rebounds} vs {full.Rebounds}");
        Assert.True(reduced.Sleep < full.Sleep);
    }

    [Fact]
    public void TheSamePullGivesTheSameRopeEveryTime()
    {
        RopeSimulation first = SettledRope(RopePhysics.Elastic);
        RopeSimulation second = SettledRope(RopePhysics.Elastic);
        PullAndRelease(first);
        PullAndRelease(second);
        Assert.Equal(first.Snapshot().Points, second.Snapshot().Points);
    }
}
