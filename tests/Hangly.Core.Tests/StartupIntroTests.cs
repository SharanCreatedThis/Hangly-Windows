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
    private readonly Xunit.Abstractions.ITestOutputHelper output;

    public StartupIntroTests(Xunit.Abstractions.ITestOutputHelper output) => this.output = output;

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
        Assert.Equal(IntroTable.Reel(0), IntroTable.Reel(0.14));
        double previous = IntroTable.Reel(0.15);
        for (int tick = 1; tick <= 80; tick++)
        {
            double reel = IntroTable.Reel(0.15 + (tick / 100.0));
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
    [InlineData(0.05)]
    [InlineData(0.4)]
    [InlineData(1.0)]
    public void EveryThreadEndsOnTheTopEdgeOrOnAStrandNeverInTheAir(double growth)
    {
        var anchor = new Vec2(100, 4);
        Vec2 lean = new Vec2(0.2, 1) / new Vec2(0.2, 1).Magnitude;
        var web = new WebBloom(anchor, anchor + (lean * 33 * growth), 0, 60, growth);
        IReadOnlyList<WebBloom.Thread> strands = web.Strands;
        IReadOnlyList<WebBloom.Thread> threads = web.Threads;
        Assert.Equal(WebPattern.Strands.Count, strands.Count);
        Assert.Equal(strands.Count + WebPattern.Branches.Count + WebPattern.Links.Count, threads.Count);

        // Strands and branches end on the top edge.
        foreach (WebBloom.Thread thread in threads.Take(strands.Count + WebPattern.Branches.Count))
        {
            Assert.Equal(0, thread.End.Y, 9);
        }

        // Branches start on a strand; links start and end on strands.
        for (int index = 0; index < WebPattern.Branches.Count; index++)
        {
            WebPattern.Branch branch = WebPattern.Branches[index];
            Assert.Equal(strands[branch.Strand].At(branch.From), threads[strands.Count + index].Start);
        }

        for (int index = 0; index < WebPattern.Links.Count; index++)
        {
            WebPattern.Link link = WebPattern.Links[index];
            WebBloom.Thread thread = threads[strands.Count + WebPattern.Branches.Count + index];
            Assert.Equal(strands[link.From].At(link.FromT), thread.Start);
            Assert.Equal(strands[link.To].At(link.ToT), thread.End);
        }
    }

    [Fact]
    public void TheWebIsUnevenLikeARealOneAndTheSameEveryTime()
    {
        double[] across = [.. WebPattern.Strands.Select(strand => strand.Across)];
        double[] gaps = [.. across.Zip(across.Skip(1), (a, b) => b - a)];
        Assert.True(gaps.Max() - gaps.Min() > 0.05, "strands unevenly spaced");
        Assert.True(WebPattern.Strands.Select(strand => strand.Width).Distinct().Count() > 5, "strands of different weights");
        Assert.Contains(WebPattern.Strands, strand => Math.Abs(strand.Bow) > 0.02);
        Assert.True(Math.Abs(across[0] + across[^1]) > 0.02, "not a mirror image");
        Assert.True(WebPattern.Links.Count < 4 * (WebPattern.Strands.Count - 1) + 4, "rings have gaps");

        // Pinned, so both builds draw this exact web: the first strand's numbers.
        WebPattern.Strand first = WebPattern.Strands[0];
        output.WriteLine($"first strand {first.Across:R} {first.Bow:R} {first.Width:R}");
        Assert.Equal(-1.044408702114597, first.Across, 12);
        Assert.Equal(0.005039416439831257, first.Bow, 12);
        Assert.Equal(0.9938175584189594, first.Width, 12);
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
        Assert.All(web.Strands, strand => Assert.Equal(0, strand.End.Y, 9));
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
