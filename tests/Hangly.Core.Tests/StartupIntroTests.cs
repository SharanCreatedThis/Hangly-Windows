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
        Assert.Equal(0, IntroTable.BloomOpacity(IntroTable.Duration));
        Assert.Equal(0, IntroTable.BloomGrowth(0));
        Assert.Equal(1, IntroTable.BloomGrowth(0.2));
    }

    [Fact]
    public void TheWebHangsFromTheAnchorAndNeverReachesAboveIt()
    {
        var bloom = new WebBloom(new Vec2(100, 5), 60, 1, 1);
        Assert.Equal(8, bloom.SpokeEnds.Count);
        Assert.NotEmpty(bloom.RingSegments);
        foreach (Vec2 end in bloom.SpokeEnds)
        {
            Assert.True(end.Y >= bloom.Anchor.Y);
            Assert.True(end.DistanceTo(bloom.Anchor) <= bloom.Size + 1e-9);
        }

        foreach (WebBloom.RingSegment segment in bloom.RingSegments)
        {
            Assert.True(segment.Start.Y >= bloom.Anchor.Y);
            Assert.True(segment.Control.Y >= bloom.Anchor.Y);
            Assert.True(segment.End.Y >= bloom.Anchor.Y);
        }

        var unborn = new WebBloom(new Vec2(100, 5), 60, 0, 1);
        Assert.All(unborn.SpokeEnds, end => Assert.Equal(unborn.Anchor, end));
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
        Assert.Null(intro.Snapshot().Bloom);
        double length = rest.Y - intro.Anchor.Y;
        Assert.True(lowest > rest.Y);
        Assert.True(lowest - rest.Y < 0.2 * length);
        Assert.True(intro.Points[slot.Node].Position.DistanceTo(rest) < 0.15 * length);
    }
}
