//
//  RopeSimulationTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Exercises the solver directly.</summary>
/// <remarks>
/// The rope is free of WinUI, Win2D and Win32, so every claim about it can be checked
/// numerically rather than by watching the screen. These are the Swift suite's
/// assertions, with the same tolerances: if the port drifts from the original's
/// behaviour, one of these is what says so.
/// </remarks>
public class RopeSimulationTests
{
    private static readonly Vec2 Anchor = new(260, 15);
    private const double Frame120 = 1.0 / 120.0;

    private static RopeSimulation MakeRope()
    {
        var rope = new RopeSimulation(RopeConfiguration.Default, Anchor);
        rope.Start();
        return rope;
    }

    /// <summary>Advances <paramref name="seconds"/> of simulated time in 120 Hz frames.</summary>
    private static void Run(RopeSimulation rope, double seconds)
    {
        for (int tick = 0; tick < (int)(seconds / Frame120); tick++)
        {
            rope.Step(Frame120);
        }
    }

    [Fact(DisplayName = "Twenty segments means twenty-one nodes")]
    public void SegmentCountMatchesSpecification()
    {
        RopeSimulation rope = MakeRope();

        Assert.Equal(20, rope.Configuration.SegmentCount);
        Assert.Equal(21, rope.Points.Length);
    }

    [Fact(DisplayName = "The first node is pinned and the charm is the heaviest")]
    public void MassDistributionIsCorrect()
    {
        RopeSimulation rope = MakeRope();

        Assert.True(rope.Points[0].IsPinned);
        Assert.Equal(0, rope.Points[0].InverseMass);

        // A heavier charm has a smaller inverse mass than a plain node.
        Assert.True(rope.Points[20].InverseMass < rope.Points[10].InverseMass);
        Assert.Equal(1, rope.Points[10].InverseMass);
    }

    [Fact(DisplayName = "The anchor never moves, however hard the rope is driven")]
    public void AnchorStaysPinned()
    {
        RopeSimulation rope = MakeRope();
        rope.BeginDrag(rope.Points[20].Position);
        rope.UpdateDrag(new Vec2(4000, -4000), new Vec2(9000, -9000));
        Run(rope, 2);

        Assert.Equal(Anchor, rope.Points[0].Position);
    }

    [Fact(DisplayName = "The rope never stretches beyond its limit at rest")]
    public void DoesNotStretchAtRest()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 5);

        Assert.True(rope.MeasuredMaximumStretch <= rope.Configuration.MaxStretchRatio + 1e-9);
    }

    [Fact(DisplayName = "The rope never stretches beyond its limit under a hard flick")]
    public void DoesNotStretchUnderHardFlick()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 1);
        rope.BeginDrag(rope.Points[20].Position);

        // A fast human flick: 3000 points per second, reversing several times a second,
        // and repeatedly yanked well past the rope's reach.
        Vec2 position = rope.Points[20].Position;
        double worstStretch = 0;

        for (int tick = 0; tick < 1200; tick++)
        {
            double direction = (tick / 18) % 2 == 0 ? 1 : -1;
            position += new Vec2(direction * 3000 * Frame120, Math.Sin(tick * 0.05) * 12);
            rope.UpdateDrag(position, new Vec2(direction * 3000, 0));
            rope.Step(Frame120);
            worstStretch = Math.Max(worstStretch, rope.MeasuredMaximumStretch);
        }

        Assert.True(
            worstStretch <= rope.Configuration.MaxStretchRatio + 1e-9,
            $"worst stretch was {worstStretch}");
    }

    [Fact(DisplayName = "Free swinging produces essentially no stretch")]
    public void DoesNotStretchWhileSwinging()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 1);

        double worstStretch = 0;
        for (int tick = 0; tick < 2400; tick++)
        {
            rope.Step(Frame120);
            worstStretch = Math.Max(worstStretch, rope.MeasuredMaximumStretch);
        }

        Assert.True(worstStretch < 1.01, $"worst stretch was {worstStretch}");
    }

    [Fact(DisplayName = "Synthetic torture stays bounded and recovers")]
    public void RecoversFromUnreachableInput()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 1);
        rope.BeginDrag(rope.Points[20].Position);

        // Far beyond anything a pointer can do: nearly seven revolutions a second.
        // Relaxation cannot fully converge inside one frame at this rate, so the
        // guarantee here is that the rope stays bounded rather than exactly at the limit,
        // and snaps back the moment the input stops.
        double worstStretch = 0;
        for (int tick = 0; tick < 600; tick++)
        {
            double angle = tick * 0.35;
            Vec2 target = Anchor + (new Vec2(Math.Cos(angle), Math.Sin(angle)) * 900);
            rope.UpdateDrag(target, new Vec2(6000, 6000));
            rope.Step(Frame120);
            worstStretch = Math.Max(worstStretch, rope.MeasuredMaximumStretch);
        }

        Assert.True(worstStretch < 1.05, $"worst stretch was {worstStretch}");

        rope.EndDrag();
        Run(rope, 1);
        Assert.True(rope.MeasuredMaximumStretch <= rope.Configuration.MaxStretchRatio + 1e-9);
    }

    [Fact(DisplayName = "Gravity swings the rope down below its anchor")]
    public void GravityPullsTheRopeDown()
    {
        RopeSimulation rope = MakeRope();
        double startX = rope.Points[20].Position.X;
        Run(rope, 20);
        Vec2 charm = rope.Points[20].Position;

        // It starts off-vertical by InitialAngle and must end hanging under the anchor.
        Assert.True(Math.Abs(charm.X - Anchor.X) < Math.Abs(startX - Anchor.X) * 0.25);
        Assert.True(charm.Y > Anchor.Y + (rope.Configuration.TotalLength * 0.9));
    }

    [Fact(DisplayName = "Damping brings the rope to rest")]
    public void DampingSettlesTheRope()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 3);
        double movingSpeed = TotalSpeed(rope);

        Run(rope, 25);
        double settledSpeed = TotalSpeed(rope);

        Assert.True(settledSpeed < movingSpeed * 0.1);
    }

    private static double TotalSpeed(RopeSimulation rope)
    {
        double total = 0;
        for (int index = 0; index < rope.Points.Length; index++)
        {
            total += rope.Points[index].Displacement.Magnitude;
        }

        return total;
    }

    [Fact(DisplayName = "Two 120 Hz frames match one 60 Hz frame exactly")]
    public void FixedTimeStepMakesRefreshRateIrrelevant()
    {
        var fast = new RopeSimulation(RopeConfiguration.Default, Anchor);
        var slow = new RopeSimulation(RopeConfiguration.Default, Anchor);
        fast.Start();
        slow.Start();

        for (int tick = 0; tick < 120; tick++)
        {
            fast.Step(1.0 / 120.0);
            fast.Step(1.0 / 120.0);
            slow.Step(1.0 / 60.0);
        }

        for (int index = 0; index < fast.Points.Length; index++)
        {
            Assert.True(Math.Abs(fast.Points[index].Position.X - slow.Points[index].Position.X) < 1e-9);
            Assert.True(Math.Abs(fast.Points[index].Position.Y - slow.Points[index].Position.Y) < 1e-9);
        }
    }

    [Fact(DisplayName = "A long 120 Hz run stays finite and bounded")]
    public void RemainsStableOverALongRun()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 120);

        double reach = rope.Configuration.TotalLength * 3;
        for (int index = 0; index < rope.Points.Length; index++)
        {
            Vec2 position = rope.Points[index].Position;
            Assert.True(double.IsFinite(position.X));
            Assert.True(double.IsFinite(position.Y));
            Assert.True(position.DistanceTo(Anchor) < reach);
        }
    }

    [Fact(DisplayName = "A stalled frame cannot trigger a burst of catch-up steps")]
    public void ClampsOversizedFrames()
    {
        RopeSimulation rope = MakeRope();
        rope.Step(10);

        int ceiling = (int)(rope.Configuration.MaxFrameDuration / rope.Configuration.FixedTimeStep) + 1;
        Assert.True(rope.LastStepCount <= ceiling);
    }
}

/// <summary>Interaction and lifecycle: grabbing, releasing, resting and resizing.</summary>
public class RopeInteractionTests
{
    private static readonly Vec2 Anchor = new(260, 15);
    private const double Frame120 = 1.0 / 120.0;

    private static RopeSimulation MakeRope()
    {
        var rope = new RopeSimulation(RopeConfiguration.Default, Anchor);
        rope.Start();
        return rope;
    }

    private static void Run(RopeSimulation rope, double seconds)
    {
        for (int tick = 0; tick < (int)(seconds / Frame120); tick++)
        {
            rope.Step(Frame120);
        }
    }

    [Fact(DisplayName = "Only the charm can be grabbed")]
    public void GrabIsLimitedToTheCharm()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 3);

        Assert.True(rope.CanGrab(rope.CharmCenter));
        Assert.False(rope.CanGrab(rope.Points[10].Position));
        Assert.False(rope.CanGrab(Anchor));
    }

    [Fact(DisplayName = "A held charm settles exactly on the cursor")]
    public void DraggingMovesTheCharmToTheCursor()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 3);

        var target = new Vec2(Anchor.X + 60, Anchor.Y + 120);
        rope.BeginDrag(rope.CharmCenter);
        for (int tick = 0; tick < 240; tick++)
        {
            rope.UpdateDrag(target, Vec2.Zero);
            rope.Step(Frame120);
        }

        Assert.True(rope.CharmCenter.DistanceTo(target) < 0.5);
    }

    [Fact(DisplayName = "Releasing the charm preserves its momentum")]
    public void ReleasePreservesMomentum()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 3);

        Vec2 position = rope.CharmCenter;
        rope.BeginDrag(position);

        // Drag sideways at a steady 600 points per second for a quarter of a second.
        var velocity = new Vec2(600, 0);
        for (int tick = 0; tick < 30; tick++)
        {
            position += velocity * Frame120;
            rope.UpdateDrag(position, velocity);
            rope.Step(Frame120);
        }

        rope.EndDrag();
        Vec2 released = rope.CharmCenter;
        rope.Step(Frame120);

        // The charm carries on in the direction it was thrown.
        Assert.True(rope.CharmCenter.X > released.X);
    }

    [Fact(DisplayName = "The rope stops simulating once it has settled")]
    public void SleepsWhenSettled()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 120);

        Assert.True(rope.IsSleeping);
        Assert.Equal(0, rope.LastStepCount);
    }

    [Fact(DisplayName = "A settled rope stays exactly where it stopped")]
    public void SleepingRopeDoesNotDrift()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 120);
        Vec2 settled = rope.CharmCenter;

        Run(rope, 10);

        Assert.Equal(settled, rope.CharmCenter);
    }

    [Fact(DisplayName = "Grabbing the charm wakes the rope")]
    public void DraggingWakesTheRope()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 120);
        Assert.True(rope.IsSleeping);

        rope.BeginDrag(rope.CharmCenter);

        Assert.False(rope.IsSleeping);
    }

    [Fact(DisplayName = "Moving the anchor wakes the rope")]
    public void ResizingWakesTheRope()
    {
        RopeSimulation rope = MakeRope();
        Run(rope, 120);
        Assert.True(rope.IsSleeping);

        rope.Resize(new Size(300, 400));

        Assert.False(rope.IsSleeping);
    }

    [Fact(DisplayName = "The rest pose hangs straight down with no motion")]
    public void RestPoseIsStill()
    {
        RopeSimulation rope = MakeRope();
        rope.ResetToHanging();

        for (int index = 0; index < rope.Points.Length; index++)
        {
            Assert.Equal(0, rope.Points[index].Displacement.Magnitude);
            Assert.Equal(Anchor.X, rope.Points[index].Position.X, 9);
        }
    }
}
