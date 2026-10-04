//
//  HookConnectorTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The end cap and jump ring that join the rope to a charm's hook. macOS's <c>HookConnectorTests</c>.</summary>
public sealed class HookConnectorTests
{
    private static readonly CharmArtworkHook Hook = new(0.05, 0.07, 0.12, 0.5);
    private static readonly Rect Body = new(0.2, 0.05, 0.6, 0.9);

    [Fact(DisplayName = "Each link hangs on the next: the charm's eye on the jump ring, the jump ring on the cap's eyelet")]
    public void LinksHangOnEachOther()
    {
        var connector = new HookConnector(Hook, Body, HookConnector.Weight.Large);
        Rect ring = connector.RingRect;
        Rect cap = connector.CapRect ?? throw new InvalidOperationException("a large charm has a cap");
        Assert.Equal(Hook.EyeTop, ring.Top + (HookConnector.Ring.OpeningBottom * ring.Height), 9);
        Assert.Equal(ring.Top + (HookConnector.Ring.OpeningTop * ring.Height), cap.Top + (HookConnector.Cap.EyeletBottom * cap.Height), 9);
        Assert.Equal(Hook.CentreX, ring.MidX, 9);
        Assert.Equal(Hook.CentreX, cap.MidX, 9);
        Assert.True(cap.Top < ring.Top);
        Assert.InRange(connector.RopeEnd.Y, cap.Top, cap.Top + cap.Height);
        Assert.True(connector.Rise > Hook.BarTop - cap.Top, "beads keep a gap above the cap");
    }

    [Fact(DisplayName = "The charm is the hero: smaller than its ring, a cap smaller than the jump ring, never taller than the ring")]
    public void SmallerThanTheCharm()
    {
        double charmRing = (Hook.EyeBottom - Hook.BarTop) + (Hook.EyeTop - Hook.BarTop);
        foreach (HookConnector.Weight weight in Enum.GetValues<HookConnector.Weight>())
        {
            var connector = new HookConnector(Hook, Body, weight);
            Assert.True(connector.RingRect.Height <= (charmRing * HookConnector.RingToCharmRing) + 1e-9, $"{weight} ring");
            Assert.True(connector.RingRect.Top + connector.RingRect.Height - connector.Top <= charmRing + 1e-9, $"{weight} taller than the ring");
            if (connector.CapRect is Rect cap)
            {
                Assert.True(cap.Height < connector.RingRect.Height, "the cap is the smaller piece");
            }
        }

        Assert.Null(new HookConnector(Hook, Body, HookConnector.Weight.Small).CapRect);
        Assert.Null(new HookConnector(Hook, Body, HookConnector.Weight.Medium).CapRect);
        Assert.Equal(HookConnector.Weight.Small, HookConnector.WeightOf(2.5));
        Assert.Equal(HookConnector.Weight.Medium, HookConnector.WeightOf(3.3));
        Assert.Equal(HookConnector.Weight.Large, HookConnector.WeightOf(4.0));
    }

    [Fact(DisplayName = "Beads above a charm with a hook hang clear of its connector")]
    public void BeadsClearTheConnector()
    {
        CharmCatalogEntry entry = CharmCatalog.All.First(entry => entry.BeadCount > 0);
        var regions = new CharmArtworkRegions(Body, [new Rect(0.45, 0.0, 0.1, 0.04)], Hook.BarTop, Hook);
        var plain = new CharmArtworkRegions(Body, [new Rect(0.45, 0.0, 0.1, 0.04)]);
        double rise = new HookConnector(Hook, Body, HookConnector.WeightOf(entry.Mass)).Rise * 2 / Math.Max(Body.Width, Body.Height);
        Assert.Equal(CharmCatalog.BeadsFor(entry, plain)[0].Offset + rise, CharmCatalog.BeadsFor(entry, regions)[0].Offset, 9);
    }

    [Fact(DisplayName = "Lifted to the top and dragged round, a charm stays upright on a slack cord and never spins in a step")]
    public void StaysUprightWhenLifted()
    {
        foreach (string id in new[] { "airJordan1Chicago", "camera", "ironManHelmet", "nazar" })
        {
            CharmCatalogEntry entry = CharmCatalog.All.First(entry => entry.Id == id);
            var rope = new Physics.RopeSimulation(style: RopeStyle.Thread, timeProfile: RopeTimeProfileTable.Baseline);
            rope.SetCharmStack([CharmCatalog.MetricsFor(entry, null)]);
            rope.Fit(new Size(900, 600), 1, 1);
            rope.Start();
            for (int tick = 0; tick < 240; tick++)
            {
                rope.Step(1.0 / 120);
            }

            Vec2 start = rope.Snapshot().Charms[0].Center;
            rope.BeginDrag(start);
            var targets = new List<Vec2>();
            for (int step = 0; step < 120; step++)
            {
                targets.Add(new Vec2(start.X, start.Y + ((rope.Anchor.Y + 20 - start.Y) * step / 120)));
            }

            targets.AddRange(Enumerable.Repeat(new Vec2(start.X, rope.Anchor.Y + 20), 120));
            foreach (double ring in new[] { 0.3, 0.95 })
            {
                for (int step = 0; step < 240; step++)
                {
                    double angle = step / 240.0 * 2 * Math.PI;
                    targets.Add(rope.Anchor + new Vec2(ring * 300 * Math.Sin(angle), ring * 300 * Math.Cos(angle)));
                }
            }

            double last = rope.Snapshot().Charms[0].Angle, worstStep = 0, worstSlack = 0;
            foreach (Vec2 target in targets)
            {
                rope.UpdateDrag(target, new Vec2(0, 0));
                rope.Step(1.0 / 120);
                Physics.RopeSnapshot snapshot = rope.Snapshot();
                double angle = snapshot.Charms[0].Angle;
                worstStep = Math.Max(worstStep, Math.Abs(Physics.RopeSimulation.ShortestTurn(last, angle)));
                last = angle;
                double path = 0;
                for (int node = 1; node < snapshot.Points.Count; node++)
                {
                    path += (snapshot.Points[node] - snapshot.Points[node - 1]).Magnitude;
                }

                double tightness = (snapshot.Points[^1] - snapshot.Points[0]).Magnitude / Math.Max(path, 0.001);
                if (tightness < Physics.RopeSimulation.SlackTightness - 0.05)
                {
                    worstSlack = Math.Max(worstSlack, Math.Abs(Physics.RopeSimulation.ShortestTurn(Math.PI / 2, angle)));
                }
            }

            Assert.True(worstStep <= Physics.RopeSimulation.MaximumTurnPerStep + 1e-9, $"{id} spun {worstStep} in a step");
            Assert.True(worstSlack < 0.2, $"{id} turned {(int)(worstSlack * 180 / Math.PI)}° on a slack cord");
        }
    }

    [Fact(DisplayName = "Lifted past the top, a held charm stops just under its anchor and the cord stays on the screen")]
    public void NeverLiftedAboveTheAnchor()
    {
        CharmCatalogEntry entry = CharmCatalog.All.First(entry => entry.Id == "captainAmericaShield");
        var rope = new Physics.RopeSimulation(style: RopeStyle.Thread, timeProfile: RopeTimeProfileTable.Baseline);
        rope.SetCharmStack([CharmCatalog.MetricsFor(entry, null)]);
        rope.Fit(new Size(900, 440), 1, 1);
        rope.Start();
        for (int tick = 0; tick < 240; tick++)
        {
            rope.Step(1.0 / 120);
        }

        Vec2 start = rope.Snapshot().Charms[0].Center;
        rope.BeginDrag(start);
        double highest = double.MaxValue;
        var goal = new Vec2(rope.Anchor.X + 40, rope.Anchor.Y - 60);
        for (int step = 0; step < 360; step++)
        {
            double f = Math.Min(1, step / 90.0);
            rope.UpdateDrag(start + ((goal - start) * f), new Vec2(0, 0));
            rope.Step(1.0 / 120);
            highest = Math.Min(highest, rope.Snapshot().Charms[0].Center.Y);
        }

        Assert.True(highest >= rope.Anchor.Y + rope.CharmLayout.Slots[0].KnotRadius - 1, $"the charm rose to {highest}, above its anchor");
    }

    [Fact(DisplayName = "The rope eases into an off-centre ring: no corner between neighbouring segments, and it arrives along the axis")]
    public void RopeEasesIntoTheRing()
    {
        const double radius = 60;
        double length = HookConnector.BlendLength(radius, 3);
        double worst = 0;
        for (double side = -0.8; side <= 0.8001; side += 0.1)
        {
            for (double skew = -0.3; skew <= 0.3001; skew += 0.15)
            {
                var start = new Vec2(0, 0);
                var end = new Vec2(side * radius, length);
                var leaving = new Vec2(Math.Cos((Math.PI / 2) + skew), Math.Sin((Math.PI / 2) + skew));
                List<Vec2> points = [start, .. HookConnector.Blend(start, leaving, end, new Vec2(0, 1))];
                for (int index = 1; index < points.Count - 1; index++)
                {
                    double a = Math.Atan2(points[index].Y - points[index - 1].Y, points[index].X - points[index - 1].X);
                    double b = Math.Atan2(points[index + 1].Y - points[index].Y, points[index + 1].X - points[index].X);
                    worst = Math.Max(worst, Math.Abs(Physics.RopeSimulation.ShortestTurn(a, b)));
                }

                double last = Math.Atan2(end.Y - points[^2].Y, end.X - points[^2].X);
                Assert.True(Math.Abs(Physics.RopeSimulation.ShortestTurn(last, Math.PI / 2)) < 0.15, $"arrives off the axis by {last}");
            }
        }

        Assert.True(worst < 0.25, $"a {(int)(worst * 180 / Math.PI)}° corner in the rope");
    }
}
