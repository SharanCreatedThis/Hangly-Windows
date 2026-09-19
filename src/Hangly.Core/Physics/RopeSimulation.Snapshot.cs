//
//  RopeSimulation.Snapshot.cs
//  Hangly
//
//  How the rope reports itself to the renderer.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>Read-only geometry derived from the solver's state.</summary>
/// <remarks>
/// Split from the solver so that "how the rope evolves" and "how the rope is measured and
/// drawn" stay separately readable. Nothing here mutates anything; every member is a pure
/// function of the node positions.
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>Radius of the charm on the end of the rope.</summary>
    public double CharmRadius =>
        CharmLayout.Bottom is CharmStackLayout.Slot bottom
            ? bottom.Radius
            : Configuration.TotalLength * CharmMetricsValue.RadiusRatio;

    /// <summary>Where the charm on the end of the rope hangs, which is the last node.</summary>
    public Vec2 CharmCenter => Points.Length > 0 ? Points[^1].Position : Vec2.Zero;

    /// <summary>How the charm hangs: the direction from the knot to the charm's centre.</summary>
    /// <remarks>
    /// Taken from the cord rather than from the final link, so the charm's own loop lines
    /// up with the cord that is drawn into it.
    /// </remarks>
    public double CharmAngle => CharmOrientation;

    /// <summary>Every charm on the rope, measured for drawing.</summary>
    public IReadOnlyList<CharmPlacement> CharmPlacements
    {
        get
        {
            var placements = new CharmPlacement[CharmLayout.Slots.Count];
            for (int slot = 0; slot < CharmLayout.Slots.Count; slot++)
            {
                CharmStackLayout.Slot charm = CharmLayout.Slots[slot];
                ArcSpan span = slot < CharmSpans.Count
                    ? CharmSpans[slot]
                    : new ArcSpan(CordLength, CordLength);

                placements[slot] = new CharmPlacement(
                    Center: PositionOfNode(charm.Node),
                    Radius: charm.Radius,
                    Angle: slot < CharmOrientations.Count ? CharmOrientations[slot] : Math.PI / 2,
                    KnotInset: charm.KnotInset,
                    CordEntry: span.Lower,

                    // The cord never comes back out from under the charm on the end.
                    CordExit: charm.Node == Points.Length - 1 ? span.Lower : span.Upper);
            }

            return placements;
        }
    }

    /// <summary>Longest link as a multiple of its rest length.</summary>
    public double MeasuredMaximumStretch
    {
        get
        {
            if (Configuration.SegmentLength <= Precision.UlpOfOne || Points.Length < 2)
            {
                return 1;
            }

            double longest = 0;
            for (int index = 0; index < Points.Length - 1; index++)
            {
                double distance = Points[index].Position.DistanceTo(Points[index + 1].Position);
                longest = Math.Max(longest, distance / Configuration.SegmentLength);
            }

            return longest;
        }
    }

    public RopeSnapshot Snapshot()
    {
        var points = new Vec2[Points.Length];
        for (int index = 0; index < Points.Length; index++)
        {
            points[index] = Points[index].Position;
        }

        var beads = new BeadPlacement[Beads.Length];
        for (int index = 0; index < Beads.Length; index++)
        {
            beads[index] = new BeadPlacement(
                Beads[index].Position,
                Beads[index].Angle,
                Beads[index].Size,
                Beads[index].Owner);
        }

        return new RopeSnapshot(points, CharmPlacements, beads, MeasuredMaximumStretch, IsDragging);
    }
}
