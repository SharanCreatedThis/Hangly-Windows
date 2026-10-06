//
//  StackBeadTests.cs
//  Hangly
//
//  On a rope of two or three charms, only the top one has beads (Sharan, 6 Oct). macOS's StackBeadTests.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

public class StackBeadTests
{
    [Fact(DisplayName = "Only the top charm of a stack threads its beads")]
    public void OnlyTheTopCharmHasBeads()
    {
        var bead = new CharmBead(new Size(0.2, 0.2), 0.6, 0.1);
        var canvas = new Size(900, 700);
        RopeConfiguration configuration = RopeConfiguration.Fitted(canvas);
        var rope = new RopeSimulation(
            configuration,
            configuration.Anchor(canvas),
            charmStack: [CharmMetrics.Default, CharmMetrics.Default, CharmMetrics.Default]);
        rope.SetBeads([[bead], [bead], [bead]]);
        rope.Start();
        for (int tick = 0; tick < 120; tick++)
        {
            rope.Step(1.0 / 60.0);
        }

        RopeSnapshot snapshot = rope.Snapshot();
        Assert.Single(snapshot.Beads);
        Assert.All(snapshot.Beads, placed => Assert.Equal(0, placed.Owner));
    }
}

public class StackLimitTests
{
    [Theory(DisplayName = "Handed more charms than the rope is built for, the solver hangs what it can and never throws")]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void ClampsTheStack(int count)
    {
        var canvas = new Size(900, 700);
        RopeConfiguration configuration = RopeConfiguration.Fitted(canvas);
        var rope = new RopeSimulation(configuration, configuration.Anchor(canvas), charmStack: [CharmMetrics.Default]);
        rope.Start();
        rope.SetCharmStack([.. Enumerable.Repeat(CharmMetrics.Default, count)]);
        for (int tick = 0; tick < 120; tick++)
        {
            rope.Step(1.0 / 60.0);
        }

        Assert.Equal(CharmStack.MaximumOnRope, rope.CharmLayout.Slots.Count);
    }
}
