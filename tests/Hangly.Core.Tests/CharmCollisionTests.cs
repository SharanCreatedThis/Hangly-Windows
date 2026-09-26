using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Xunit;
using Xunit.Abstractions;

namespace Hangly.Core.Tests;

/// <summary>Collisions: when two charms on one cord meet, once, with how hard.</summary>
public class CharmCollisionTests
{
    private const double Frame120 = 1.0 / 120.0;
    private readonly ITestOutputHelper output;

    public CharmCollisionTests(ITestOutputHelper output) => this.output = output;

    private static RopeSimulation Hang(int charmCount)
    {
        var rope = new RopeSimulation(style: RopeStyle.Thread, timeProfile: RopeTimeProfileTable.Baseline);
        var metrics = new List<CharmMetrics>();
        var beads = new List<IReadOnlyList<CharmBead>>();
        // The default charm three times: the macOS suite uses the same numbers.
        for (int index = 0; index < charmCount; index++)
        {
            metrics.Add(CharmMetrics.Default);
            beads.Add([]);
        }

        rope.SetCharmStack(metrics);
        rope.SetBeads(beads);
        rope.Fit(new Size(420, 420), 1, 1);
        rope.Start();
        return rope;
    }

    private static List<CharmCollision> Run(RopeSimulation rope, double seconds)
    {
        var all = new List<CharmCollision>();
        for (int tick = 0; tick < seconds * 120; tick++)
        {
            rope.Step(Frame120);
            rope.TakeCollisions(all);
        }

        return all;
    }

    /// <summary>Grabs the bottom charm and flings it up and over, which folds the cord.</summary>
    private static void Fling(RopeSimulation rope)
    {
        Vec2 charm = rope.Points[^1].Position;
        Assert.True(rope.BeginDrag(charm));
        Vec2 target = rope.Anchor + new Vec2(-60, 20);
        for (int tick = 0; tick < 18; tick++)
        {
            rope.UpdateDrag(target, new Vec2(-2500, -2500));
            rope.Step(Frame120);
        }

        rope.EndDrag();
    }

    [Fact]
    public void AFoldedThrowMakesCharmsMeet()
    {
        RopeSimulation rope = Hang(3);
        Run(rope, 3);
        Fling(rope);
        List<CharmCollision> hits = Run(rope, 8);
        output.WriteLine("collisions: " + string.Join(", ", hits.Select(hit => $"{hit.First}-{hit.Second} @ {hit.Speed:0} pt/s")));

        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.True(hit.First < hit.Second && hit.Speed > 0));
    }

    [Fact]
    public void AHangingRopeMakesNone()
    {
        RopeSimulation rope = Hang(3);
        rope.ResetToHanging();
        Assert.Empty(Run(rope, 20));
    }

    [Fact]
    public void OneCharmHasNothingToMeet()
    {
        RopeSimulation rope = Hang(1);
        Fling(rope);
        Assert.Empty(Run(rope, 8));
    }

    [Fact]
    public void NobodyListeningCannotGrowTheBuffer()
    {
        RopeSimulation rope = Hang(3);
        for (int round = 0; round < 20; round++)
        {
            Fling(rope);
            for (int tick = 0; tick < 240; tick++)
            {
                rope.Step(Frame120);
            }
        }

        var taken = new List<CharmCollision>();
        rope.TakeCollisions(taken);
        Assert.True(taken.Count <= 8);
    }
}
