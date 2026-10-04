//
//  PicturePartsTests.cs
//  Hangly
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Spider-Man and Gwen: a picture taken apart into the figures on its rope. macOS's PicturePartsTests.</summary>
public sealed class PicturePartsTests
{
    [Fact]
    public void ThePictureHangsAsTwoFiguresAndOtherCharmsAsThemselves()
    {
        Assert.Equal(2, PictureParts.FiguresOf("spiderManGwen").Count);
        Assert.Empty(PictureParts.FiguresOf("nazar"));
    }

    [Fact]
    public void EachFigureHangsWhereTheArtworkPutsItByItsOwnRope()
    {
        IReadOnlyList<PictureFigure> figures = PictureParts.FiguresOf("spiderManGwen");
        PictureFigure spiderMan = figures[0], gwen = figures[1];
        Assert.InRange(spiderMan.Metrics.AlongRope!.Value, 0.362, 0.382);
        Assert.Equal(1, gwen.Metrics.AlongRope!.Value, 3);
        Assert.Equal(1, spiderMan.Metrics.KnotInset, 2);
        Assert.Equal(1, gwen.Metrics.KnotInset, 2);
        Assert.Equal(spiderMan.Region.Top, spiderMan.RopeAbove.Top + spiderMan.RopeAbove.Height, 9);
        Assert.Equal(gwen.Region.Top, gwen.RopeAbove.Top + gwen.RopeAbove.Height, 9);
        // The vector's unit square: the artwork 382 × 1910 centred at its longest side.
        Assert.Equal(1906.0 / 1910, gwen.Region.Top + gwen.Region.Height, 3);
        // Centred on the rope it holds (x 172 of 382), so the cord runs into his hand.
        Assert.Equal((172.0 + 764) / 1910, spiderMan.Region.Left + (spiderMan.Region.Width / 2), 3);
    }

    [Fact]
    public void OnTheRopeTheFiguresSitWhereTheArtworkHasThemSizedFromTheRope()
    {
        CharmMetrics[] metrics = [.. PictureParts.FiguresOf("spiderManGwen").Select(figure => figure.Metrics)];
        RopeConfiguration configuration = RopeConfiguration.Default with { SlackBelow = 400 };
        CharmStackLayout layout = CharmStackLayout.Resolve(metrics, configuration);
        Assert.Equal(2, layout.Count);
        double rope = configuration.SegmentLength * configuration.SegmentCount;
        Assert.Equal((int)Math.Round(configuration.SegmentCount * metrics[0].AlongRope!.Value), layout.Slots[0].Node);
        Assert.Equal(configuration.SegmentCount, layout.Slots[1].Node);
        Assert.Equal(rope * metrics[0].RadiusRatio, layout.Slots[0].Radius, 6);
        Assert.Equal(rope * metrics[1].RadiusRatio, layout.Slots[1].Radius, 6);
    }

    [Fact]
    public void ThePicturesSizeScalesTheWholePicture()
    {
        static OverlaySettings With(string id, double size) =>
            new OverlaySettings().WithStack(CharmStackState.Restore([new RopeCharm(id, size)], 1));

        double? small = PictureParts.RopeLength(With("spiderManGwen", 1));
        double? large = PictureParts.RopeLength(With("spiderManGwen", 1.5));
        Assert.NotNull(small);
        Assert.NotNull(large);
        // Half again as large is a rope half again as long: the figures and the distance between them together.
        Assert.Equal(1.5, large!.Value / small!.Value, 6);
        Assert.Null(PictureParts.RopeLength(With("nazar", 1)));
    }

    /// <summary>Straight in a hard swing, and unbuckled when Elastic stretches under a pull sideways and down.</summary>
    [Theory]
    [InlineData(RopePhysics.Standard)]
    [InlineData(RopePhysics.Elastic)]
    public void SpiderManIsARigidBodyBetweenHisHands(RopePhysics physics)
    {
        var rope = new RopeSimulation(style: RopeStyle.Thread, timeProfile: RopeTimeProfileTable.Baseline);
        rope.SetPhysics(physics);
        rope.SetCharmStack([.. PictureParts.FiguresOf("spiderManGwen").Select(figure => figure.Metrics)]);
        rope.Fit(new Size(600, 700), 1, 1);
        rope.Start();
        for (int tick = 0; tick < 240; tick++)
        {
            rope.Step(1.0 / 120);
        }

        rope.ApplyImpulse(new Vec2(1400, 1600), rope.Points.Length - 1);
        CharmStackLayout.Slot spiderMan = rope.CharmLayout.Slots[0];
        Assert.True(spiderMan.HoldsRope);
        (int first, int last) = rope.HeldSpan(spiderMan)!.Value;
        double worst = 0;
        for (int tick = 0; tick < 180; tick++)
        {
            rope.Step(1.0 / 120);
            Vec2 start = rope.Points[first].Position;
            Vec2 chord = rope.Points[last].Position - start;
            double length = chord.Magnitude;
            if (length <= 1)
            {
                continue;
            }

            for (int index = first; index <= last; index++)
            {
                Vec2 offset = rope.Points[index].Position - start;
                worst = Math.Max(worst, Math.Abs((offset.X * chord.Y) - (offset.Y * chord.X)) / length);
            }
        }

        // The rope through him stays straight in a hard swing, so it leaves his lower hand straight down his line.
        Assert.True(worst < rope.Configuration.SegmentLength * 0.25, $"furthest any node strays from his line: {worst}");

        // Drawn midway between his hands, so each hand is on the cord.
        Vec2 middle = (rope.Points[first].Position + rope.Points[last].Position) * 0.5;
        Assert.True((rope.Snapshot().Charms[0].Center - middle).Magnitude < 0.01);
    }

    [Fact]
    public void TheRopeIsDrawnByItsOwnLineSoAFoldStaysWhereTheArtworkHasIt()
    {
        // A rope running from column 20 to column 29 down 300 rows, with a fold hanging off one side for 60 of them:
        // the fold's bands centre 5 pixels off the rope.
        var points = new List<(double Middle, double Centre)>();
        for (double middle = 3; middle < 300; middle += 6)
        {
            double rope = 20 + (9 * middle / 300);
            points.Add((middle, middle is > 30 and < 90 ? rope + 5 : rope));
        }

        var line = new RopeLine(points);
        Assert.Equal(20, line.CentreAt(0)!.Value, 1);
        Assert.Equal(29, line.CentreAt(300)!.Value, 1);
        Assert.Equal(20 + (9 * 60.0 / 300), line.CentreAt(60)!.Value, 1);
        Assert.Null(new RopeLine([]).CentreAt(10));
    }

    [Fact]
    public void AFullRopeWithThePictureOnItHangsEveryOne()
    {
        CharmMetrics nazar = CharmMetrics.Default;
        CharmMetrics[] metrics = [nazar, nazar, .. PictureParts.FiguresOf("spiderManGwen").Select(figure => figure.Metrics)];
        CharmStackLayout layout = CharmStackLayout.Resolve(metrics, RopeConfiguration.Default);
        Assert.Equal(4, layout.Count);
        Assert.Equal(4, layout.Slots.Select(slot => slot.Node).Distinct().Count());
    }
}
