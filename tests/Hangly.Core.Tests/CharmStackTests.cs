//
//  CharmStackTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// Where the charms hang and how large they may be — checked with no physics at all,
/// because it is pure arithmetic over the metrics and the configuration.
/// </summary>
public class CharmStackTests
{
    private static readonly Size Canvas = new(220, 360);

    [Theory(DisplayName = "The last charm always takes the end node")]
    [InlineData(1, new[] { 20 })]
    [InlineData(2, new[] { 10, 20 })]
    [InlineData(3, new[] { 6, 13, 20 })]
    public void AttachmentsDivideTheRope(int count, int[] expected) =>
        Assert.Equal(expected, RopeConfiguration.Layout.Attachments(count, 20));

    [Fact(DisplayName = "A single charm hangs exactly where it always did")]
    public void OneCharmIsUnchanged()
    {
        RopeConfiguration configuration = RopeConfiguration.Fitted(Canvas);
        CharmStackLayout layout = CharmStackLayout.Resolve([CharmMetrics.Default], configuration);

        Assert.Equal(1, layout.Count);
        Assert.Equal(20, layout.Slots[0].Node);

        // The per-count scale leaves one charm untouched, so its radius is the ruler
        // times its own ratio and nothing else.
        Assert.Equal(1.0, RopeConfiguration.Layout.CharmScale(1));
        Assert.Equal(
            configuration.CharmReference * CharmMetrics.Default.RadiusRatio,
            layout.Slots[0].Radius,
            9);
    }

    [Fact(DisplayName = "No charm may take more than its share of the rope either side")]
    public void NeighboursCannotOverlap()
    {
        RopeConfiguration configuration = RopeConfiguration.Fitted(Canvas);

        // Deliberately larger than anything in the catalogue: the clearance rule is the
        // guarantee, not the everyday path, and it has to hold for artwork nobody has
        // drawn yet.
        var greedy = new CharmMetrics(3.0, 0.9, 0.9);
        CharmStackLayout layout = CharmStackLayout.Resolve([greedy, greedy, greedy], configuration);

        for (int index = 0; index < layout.Count - 1; index++)
        {
            double gap = (layout.Slots[index + 1].Node - layout.Slots[index].Node)
                * configuration.SegmentLength;
            double occupied = layout.Slots[index].Radius + layout.Slots[index + 1].Radius;

            // Two neighbours can never sum past ninety per cent of the rope between them,
            // so a tenth of it is always left showing as cord.
            Assert.True(occupied <= gap * 0.9 + 1e-9, $"charms {index} and {index + 1} overlap");
        }
    }

    [Fact(DisplayName = "The bottom charm's halo stays inside the canvas")]
    public void BottomCharmIsNotClipped()
    {
        RopeConfiguration configuration = RopeConfiguration.Fitted(Canvas);
        var greedy = new CharmMetrics(3.0, 0.9, 0.9);
        CharmStackLayout layout = CharmStackLayout.Resolve([greedy], configuration);

        double halo = layout.Slots[^1].Radius * RopeConfiguration.Layout.CharmHaloExtent;

        Assert.True(halo <= configuration.CharmHeadroom + 1e-9);
    }

    [Fact(DisplayName = "Charms shrink when they have company")]
    public void CompanyShrinksCharms()
    {
        Assert.Equal(1.0, RopeConfiguration.Layout.CharmScale(1));
        Assert.Equal(0.92, RopeConfiguration.Layout.CharmScale(2));
        Assert.Equal(0.82, RopeConfiguration.Layout.CharmScale(3));

        // A count past the maximum is clamped rather than rejected.
        Assert.Equal(0.82, RopeConfiguration.Layout.CharmScale(9));
    }

    [Fact(DisplayName = "Lengthening the rope does not fatten the charm")]
    public void RopeLengthAndCharmSizeAreIndependent()
    {
        RopeConfiguration shipped = RopeConfiguration.Fitted(Canvas);
        Size longerCanvas = new(
            Canvas.Width,
            Canvas.Height * RopeConfiguration.Layout.CanvasScale(1, 1.5).Height);
        RopeConfiguration longer = RopeConfiguration.Fitted(longerCanvas, ropeLength: 1.5);

        // The rope gets longer; the ruler the charm is measured with does not move.
        Assert.True(longer.TotalLength > shipped.TotalLength * 1.4);
        Assert.Equal(shipped.CharmReference, longer.CharmReference, 6);
    }

    [Fact(DisplayName = "Enlarging the charm does not lower it")]
    public void CharmSizeDoesNotMoveTheRope()
    {
        RopeConfiguration shipped = RopeConfiguration.Fitted(Canvas);
        Size biggerCanvas = new(
            Canvas.Width * RopeConfiguration.Layout.CanvasScale(1.5, 1).Width,
            Canvas.Height * RopeConfiguration.Layout.CanvasScale(1.5, 1).Height);
        RopeConfiguration bigger = RopeConfiguration.Fitted(biggerCanvas, charmSize: 1.5);

        // The charm grows where it hangs: every link keeps the length it had.
        Assert.Equal(shipped.SegmentLength, bigger.SegmentLength, 6);
        Assert.True(bigger.CharmReference > shipped.CharmReference * 1.4);
    }

    [Fact(DisplayName = "Three charms on one rope never pass through each other")]
    public void CharmsCannotPassThroughEachOther()
    {
        var rope = new RopeSimulation(
            RopeConfiguration.Fitted(Canvas),
            RopeConfiguration.Fitted(Canvas).Anchor(Canvas),
            charmStack: [CharmMetrics.Default, CharmMetrics.Default, CharmMetrics.Default]);
        rope.Start();

        // A hard throw folds the rope, and a fold brings two attachment nodes far closer
        // together than the cord between them.
        rope.BeginDrag(rope.CharmCenter);
        double worstOverlap = 0;

        for (int tick = 0; tick < 900; tick++)
        {
            double angle = tick * 0.3;
            Vec2 target = rope.Anchor + (new Vec2(Math.Cos(angle), Math.Sin(angle)) * 400);
            rope.UpdateDrag(target, new Vec2(4000, 4000));
            rope.Step(1.0 / 120.0);

            IReadOnlyList<CharmStackLayout.Slot> slots = rope.CharmLayout.Slots;
            for (int first = 0; first < slots.Count - 1; first++)
            {
                for (int second = first + 1; second < slots.Count; second++)
                {
                    double distance = rope.PositionOfNode(slots[first].Node)
                        .DistanceTo(rope.PositionOfNode(slots[second].Node));
                    double minimum = slots[first].Radius + slots[second].Radius;
                    worstOverlap = Math.Max(worstOverlap, minimum - distance);
                }
            }
        }

        // The separation pass is one-sided and solved in the same relaxation as the
        // links, so the rope gives way rather than the charms.
        Assert.True(worstOverlap < 1e-6, $"charms overlapped by {worstOverlap} points");
    }
}
