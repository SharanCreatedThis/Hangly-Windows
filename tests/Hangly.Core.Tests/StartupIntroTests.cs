using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The Spider-Man entrance. The macOS suite runs the same cases against its own solver.</summary>
public sealed class StartupIntroTests
{
    private const double Frame120 = 1.0 / 120.0;

    [Fact]
    public void OnlyWithTheSettingOnASpiderManCharmAnywhereOnTheRopeAndMotionNotReduced()
    {
        Assert.True(IntroTable.Plays(true, ["spiderMan"], false));
        Assert.True(IntroTable.Plays(true, ["spiderManSwinging"], false));
        Assert.True(IntroTable.Plays(true, ["nazar", "spiderMan", "nazar"], false));
        Assert.False(IntroTable.Plays(false, ["spiderMan"], false));
        Assert.False(IntroTable.Plays(true, ["nazar"], false));
        Assert.False(IntroTable.Plays(true, ["spiderMan"], true));
        Assert.False(IntroTable.Plays(true, [], false));
    }

    [Fact]
    public void OnByDefaultAndKeptAcrossASave()
    {
        Assert.True(new OverlaySettings().StartupAnimation);
        var settings = new AppSettings { Overlay = new OverlaySettings { StartupAnimation = false } };
        Assert.False(AppSettings.FromJson(settings.ToJson(), out _).Overlay.StartupAnimation);
        Assert.True(AppSettings.FromJson("""{ "overlay": { "isEnabled": true } }""", out _).Overlay.StartupAnimation);
    }

    [Fact]
    public void AboutASecondAndAHalfOutOnAnEaseALittlePastFullBackToExactlyFull()
    {
        Assert.InRange(IntroTable.Duration, 1, 1.5);
        Assert.True(IntroTable.Reel(0) < 0.05);
        double previous = IntroTable.Reel(0.1);
        for (int tick = 1; tick <= 85; tick++)
        {
            double reel = IntroTable.Reel(0.1 + (tick / 100.0));
            Assert.True(reel >= previous);
            previous = reel;
        }

        Assert.Equal(1.06, IntroTable.Reel(0.95), 9);
        Assert.Equal(1, IntroTable.Reel(1.2));
        Assert.Equal(1, IntroTable.Reel(IntroTable.Duration));
        Assert.Equal(0, IntroTable.BloomGrowth(0));
        Assert.Equal(1, IntroTable.BloomGrowth(0.2));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void EveryStrandEndsOnTheTopEdgeAndEveryRingEndsOnAStrand(double growth)
    {
        var anchor = new Vec2(100, 4);
        var hub = anchor + (new Vec2(0.2, 1) / new Vec2(0.2, 1).Magnitude * 36 * growth);
        var web = new WebBloom(anchor, hub, 0, 60, growth);
        IReadOnlyList<Vec2> ends = web.SpokeEnds;
        Assert.Equal(8, ends.Count);
        Assert.All(ends, end => Assert.Equal(0, end.Y));
        Assert.Equal(WebBloom.Rings.Length * (ends.Count - 1), web.RingSegments.Count);

        foreach (WebBloom.RingSegment segment in web.RingSegments)
        {
            Assert.Contains(ends, end => OnStrand(segment.Start, web.Hub, end));
            Assert.Contains(ends, end => OnStrand(segment.End, web.Hub, end));
            Assert.True(segment.Control.Y >= Math.Min(segment.Start.Y, segment.End.Y), "sags down, never up");
        }

        static bool OnStrand(Vec2 point, Vec2 from, Vec2 to)
        {
            Vec2 strand = to - from;
            double length = strand.Magnitude;
            if (length < 1e-9)
            {
                return point.DistanceTo(from) < 1e-9;
            }

            double cross = Math.Abs((strand.X * (point.Y - from.Y)) - (strand.Y * (point.X - from.X))) / length;
            double along = (((point.X - from.X) * strand.X) + ((point.Y - from.Y) * strand.Y)) / (length * length);
            return cross < 1e-9 && along is >= 0 and <= 1;
        }
    }

    private static RopeSimulation Rope(int charms)
    {
        var rope = new RopeSimulation(RopeConfiguration.Default, new Vec2(260, 15));
        if (charms > 1)
        {
            rope.SetCharmStack([.. Enumerable.Repeat(CharmMetrics.Default, charms)]);
            rope.SetBeads([.. Enumerable.Repeat<IReadOnlyList<CharmBead>>([], charms)]);
        }

        rope.SetMotion(RopeMotion.Full);
        rope.Start();
        return rope;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void TheCharmStartsAtTheAnchorDropsOnTheRopeAndEndsAsThePlainRope(int charms)
    {
        RopeSimulation still = Rope(charms);
        still.ResetToHanging();
        CharmStackLayout.Slot slot = still.CharmLayout.Slots[^1];
        Vec2 rest = still.Points[slot.Node].Position;

        RopeSimulation intro = Rope(charms);
        intro.BeginIntro();
        Assert.True(intro.Points[slot.Node].Position.DistanceTo(intro.Anchor) < rest.DistanceTo(intro.Anchor) * 0.1);
        Assert.NotNull(intro.Snapshot().Bloom);
        Assert.Equal(0, intro.Snapshot().Bloom!.Value.Growth);

        double lowest = 0;
        int steps = 0;
        while (intro.IntroElapsed is not null && steps < 600)
        {
            intro.Step(Frame120);
            Assert.False(intro.IsSleeping);
            Vec2 position = intro.Points[slot.Node].Position;
            Assert.True(double.IsFinite(position.X) && double.IsFinite(position.Y));
            lowest = Math.Max(lowest, position.Y);
            steps++;
        }

        Assert.True(steps * Frame120 <= IntroTable.Duration + 0.02);
        Assert.Equal(1, intro.ReelFraction);
        // The web stays: whole, attached, with no fade — until Spider-Man leaves the rope.
        WebBloom web = intro.Snapshot().Bloom!.Value;
        Assert.Equal(1, web.Growth);
        Assert.All(web.SpokeEnds, end => Assert.Equal(0, end.Y));
        Assert.True(web.Hub.Y > intro.Anchor.Y);
        for (int tick = 0; tick < 120 * 5; tick++)
        {
            intro.Step(Frame120);
        }

        Assert.Equal(1, intro.Snapshot().Bloom!.Value.Growth);
        intro.DetachWeb();
        Assert.Null(intro.Snapshot().Bloom);
        double length = rest.Y - intro.Anchor.Y;
        Assert.True(lowest > rest.Y);
        Assert.True(lowest - rest.Y < 0.2 * length);
        Assert.True(intro.Points[slot.Node].Position.DistanceTo(rest) < 0.15 * length);
    }
}
