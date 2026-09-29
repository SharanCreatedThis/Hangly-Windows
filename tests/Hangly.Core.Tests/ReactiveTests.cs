using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Interaction → Reactive. The macOS suite runs the same cases against its own solver.</summary>
public sealed class ReactiveTests
{
    private static readonly Vec2 Charm = new(100, 300);
    private const double Radius = 40;
    private const double Grab = Radius + RopeConfiguration.Layout.GrabPadding;
    private const double Frame120 = 1.0 / 120.0;
    private readonly Xunit.Abstractions.ITestOutputHelper output;

    public ReactiveTests(Xunit.Abstractions.ITestOutputHelper output) => this.output = output;

    [Fact]
    public void TwoModesInTheSameOrderAndWordsAsMacOSNormalByDefault()
    {
        Assert.Equal(["Normal", "Reactive"], Enum.GetValues<InteractionMode>().Select(ReactiveTable.TitleOf));
        Assert.Equal(InteractionMode.Normal, new OverlaySettings().Interaction);
        var settings = new AppSettings { Overlay = new OverlaySettings { Interaction = InteractionMode.Reactive } };
        Assert.Equal(InteractionMode.Reactive, AppSettings.FromJson(settings.ToJson(), out _).Overlay.Interaction);
    }

    [Fact]
    public void AStillPointerASlowReachAndAPointerMovingAwayPushNothing()
    {
        Vec2 beside = Charm - new Vec2(70, 0);
        Assert.Null(ReactiveTable.Push(Charm, Radius, Grab, beside, Vec2.Zero));
        Assert.Null(ReactiveTable.Push(Charm, Radius, Grab, beside, new Vec2(ReactiveTable.ApproachThreshold - 1, 0)));
        Assert.Null(ReactiveTable.Push(Charm, Radius, Grab, beside, new Vec2(-2000, 0)));
    }

    [Fact]
    public void InsideTheGrabRadiusAndBeyondTheReachThereIsNoPush()
    {
        Assert.Null(ReactiveTable.Push(Charm, Radius, Grab, Charm - new Vec2(Grab - 1, 0), new Vec2(3000, 0)));
        Assert.Null(ReactiveTable.Push(Charm, Radius, Grab, Charm - new Vec2(Radius * ReactiveTable.Reach, 0), new Vec2(3000, 0)));
    }

    [Fact]
    public void AFastApproachPushesAwayFromThePointerNeverFasterThanTheCap()
    {
        Vec2 push = ReactiveTable.Push(Charm, Radius, Grab, Charm - new Vec2(60, 0), new Vec2(5000, 0))!.Value;
        Assert.True(push.X > 0);
        Assert.Equal(0, push.Y, 9);
        Assert.True(push.Magnitude <= ReactiveTable.MaximumPush);

        // Stronger closer in.
        double near = ReactiveTable.Push(Charm, Radius, Grab, Charm - new Vec2(55, 0), new Vec2(800, 0))!.Value.Magnitude;
        double far = ReactiveTable.Push(Charm, Radius, Grab, Charm - new Vec2(90, 0), new Vec2(800, 0))!.Value.Magnitude;
        Assert.True(near > far);
    }

    private static RopeSimulation SettledRope()
    {
        var rope = new RopeSimulation(RopeConfiguration.Default, new Vec2(260, 15));
        rope.SetMotion(RopeMotion.Reduced);
        rope.Start();
        for (int tick = 0; tick < 120 * 30 && !rope.IsSleeping; tick++)
        {
            rope.Step(Frame120);
        }

        Assert.True(rope.IsSleeping);
        rope.SetMotion(RopeMotion.Full);
        return rope;
    }

    [Fact]
    public void ThePushWakesTheRopeTheCharmMovesAwayAndSettlesBackWhereItHung()
    {
        RopeSimulation rope = SettledRope();
        CharmStackLayout.Slot charm = rope.CharmLayout.Slots[^1];
        Vec2 rest = rope.Points[charm.Node].Position;
        Vec2 cursor = rest - new Vec2(charm.Radius + 20, 0);

        Assert.True(rope.Repel(cursor, new Vec2(1500, 0)));
        Assert.False(rope.IsSleeping);

        double furthest = 0;
        double fastest = 0;
        int ticks = 0;
        for (; ticks < 120 * 300 && !rope.IsSleeping; ticks++)
        {
            rope.Step(Frame120);
            furthest = Math.Max(furthest, rope.Points[charm.Node].Position.X - rest.X);
            fastest = Math.Max(fastest, rope.Points[charm.Node].Displacement.Magnitude / rope.Configuration.FixedTimeStep);
        }

        output.WriteLine($"moved {furthest:0.0} pt, fastest {fastest:0} pt/s, asleep after {ticks / 120.0:0.0} s, {rope.Points[charm.Node].Position.DistanceTo(rest):0.00} pt from rest");
        Assert.True(furthest > 25, $"moved {furthest:0.0} pt");
        Assert.True(fastest <= ReactiveTable.MaximumPush + 1, $"fastest {fastest:0} pt/s");
        Assert.True(rope.IsSleeping);
        // Back where it hung, to within what the sleep rule leaves after any small swing:
        // the rope stops being drawn once it is slower than RestSpeed, about a point short
        // of dead centre — measured 1.2 pt here, the same as after a flick.
        Assert.True(rope.Points[charm.Node].Position.DistanceTo(rest) < 2);
    }

    [Fact]
    public void RepeatedPollsWhileThePointerKeepsComingDoNotBuildUp()
    {
        RopeSimulation rope = SettledRope();
        CharmStackLayout.Slot charm = rope.CharmLayout.Slots[^1];
        Vec2 rest = rope.Points[charm.Node].Position;
        for (int poll = 0; poll < 30; poll++)
        {
            rope.Repel(rope.Points[charm.Node].Position - new Vec2(charm.Radius + 20, 0), new Vec2(5000, 0));
            rope.Step(Frame120);
            double speed = rope.Points[charm.Node].Displacement.Magnitude / rope.Configuration.FixedTimeStep;
            Assert.True(speed <= ReactiveTable.MaximumPush + 1, $"poll {poll}: {speed:0} pt/s");
        }
    }

    [Fact]
    public void ReducedMotionTurnsItOffEntirely()
    {
        RopeSimulation rope = SettledRope();
        rope.SetMotion(RopeMotion.Reduced);
        CharmStackLayout.Slot charm = rope.CharmLayout.Slots[^1];
        Vec2 cursor = rope.Points[charm.Node].Position - new Vec2(charm.Radius + 20, 0);
        Assert.False(rope.Repel(cursor, new Vec2(5000, 0)));
        Assert.True(rope.IsSleeping);
    }
}
