//
//  PhysicsAllocationTests.cs
//  Hangly
//
//  The solver allocates nothing per step, with one charm or three: a step that allocates feeds the garbage collector
//  sixty times a second, and its pauses land mid-swing.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

public class PhysicsAllocationTests
{
    private static readonly Vec2 Anchor = new(260, 15);
    private static readonly Size Canvas = new(900, 700);

    [Theory(DisplayName = "Swinging allocates nothing per step, with 1, 2 or 3 charms")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void StepsAllocateNothing(int count)
    {
        RopeConfiguration configuration = RopeConfiguration.Fitted(Canvas);
        var stack = Enumerable.Repeat(CharmMetrics.Default, count).ToArray();
        var rope = new RopeSimulation(configuration, configuration.Anchor(Canvas), charmStack: stack);
        rope.Start();
        var collisions = new List<CharmCollision>();

        // Thrown round hard, so the charms knock and every pass of the solver runs; once round to warm up.
        void Swing(int frames)
        {
            for (int tick = 0; tick < frames; tick++)
            {
                double angle = tick * 0.08;
                rope.UpdateDrag(rope.Anchor + (new Vec2(Math.Sin(angle), Math.Cos(angle)) * 300), new Vec2(2000, 0));
                rope.Step(1.0 / 60.0);
                rope.TakeCollisions(collisions);
                collisions.Clear(); // as the overlay does each frame
            }
        }

        rope.BeginDrag(rope.CharmCenter);
        Swing(240);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Swing(600);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated == 0, $"{count} charm(s): {allocated} bytes over 600 frames ({allocated / 600.0:0} a frame)");
    }
}
