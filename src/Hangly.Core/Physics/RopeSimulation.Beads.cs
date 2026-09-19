//
//  RopeSimulation.Beads.cs
//  Hangly
//
//  The bead pass: particles that ride the cord.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>Beads threaded on the cord above the charm.</summary>
/// <remarks>
/// Kept apart from the solver because the dependency runs one way: the rope is solved
/// first and the beads then ride the cord it produced. Nothing here writes a rope node's
/// position, so no amount of bead behaviour can disturb the rope.
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>
    /// Fraction of a bead's distance from its rest place taken back per step. The knot it
    /// is threaded against has a little give, not none.
    /// </summary>
    public const double BeadTetherStiffness = 0.05;

    /// <summary>
    /// Separation sweeps per step. Beads start apart and move slowly relative to one
    /// another, so two passes are always enough to resolve a touch.
    /// </summary>
    public const int BeadSeparationPasses = 2;

    /// <summary>Advances the beads threaded on the cord.</summary>
    /// <remarks>
    /// Runs after the rope has been solved, so the cord the beads ride on is this step's
    /// cord and not the last one's. Each bead takes a free Verlet step under gravity and
    /// its own momentum, is projected back onto the cord, pulled toward the knot that
    /// holds it and limited to a short slide either side, and finally separated from its
    /// neighbours. The rope never reads the beads back, so none of this can perturb the
    /// rope's own solver.
    /// </remarks>
    internal void AdvanceBeads(double timeStep)
    {
        if (Beads.Length == 0 || Curve.IsEmpty)
        {
            return;
        }

        var gravityStep = new Vec2(0, Configuration.Gravity * timeStep * timeStep);
        double damping = Configuration.Damping;
        double displacementLimit = Configuration.MaximumSpeed * timeStep;

        for (int index = 0; index < Beads.Length; index++)
        {
            Vec2 carried = (Beads[index].Displacement * damping).Limited(displacementLimit);
            Vec2 predicted = Beads[index].Position + carried + gravityStep;

            // Measured from the knot of the bead's *own* charm, so a bead sits where its
            // artwork drew it whichever charm on the rope that artwork belongs to.
            double knot = KnotArc(Beads[index].Owner);
            double rest = Math.Clamp(knot - Beads[index].RestOffset, 0, Curve.Length);

            // Search only the cord around where the bead already was: a global search
            // could snap it across a fold in a fast swing.
            double window = Beads[index].SlideLimit + Beads[index].SpacingRadius + Configuration.SegmentLength;
            double arc = Curve.ArcNearestTo(predicted, Beads[index].Arc, window);
            arc += (rest - arc) * BeadTetherStiffness;
            Beads[index].Arc = Math.Clamp(
                arc,
                rest - Beads[index].SlideLimit,
                rest + Beads[index].SlideLimit);
        }

        SeparateBeads();

        for (int index = 0; index < Beads.Length; index++)
        {
            Beads[index].PreviousPosition = Beads[index].Position;
            Beads[index].Position = Curve.PointAtArc(Beads[index].Arc);
            Beads[index].Angle = Curve.AngleAtArc(Beads[index].Arc);
        }
    }

    /// <summary>
    /// Pushes touching beads apart along the cord, and keeps each group clear of the charm
    /// it hangs on and of the charm above it.
    /// </summary>
    /// <remarks>
    /// Resolved from the bottom of the rope upwards, because every group has a wall below
    /// it: the charm it belongs to. The lowest bead of a group is placed against that
    /// charm and every bead above gives way in turn. A chain against one wall settles
    /// exactly in a single sweep that way, where splitting each correction between both
    /// beads would leave the last one overlapping whatever the wall had just pushed it
    /// into.
    ///
    /// <para>With several charms the walls simply multiply: group <em>k</em> is bounded
    /// below by where the cord meets charm <em>k</em>, and above by where the cord came
    /// back out of charm <em>k-1</em>. Neither bound can be crossed, so two charms' beads
    /// can never meet however hard the rope is thrown.</para>
    /// </remarks>
    internal void SeparateBeads()
    {
        if (Beads.Length == 0)
        {
            return;
        }

        for (int pass = 0; pass < BeadSeparationPasses; pass++)
        {
            // Ceilings from the bottom up: each bead is placed against the charm it
            // belongs to, and every bead above it gives way in turn.
            for (int index = Beads.Length - 1; index >= 0; index--)
            {
                double limit = BeadCeiling(index);
                if (index + 1 < Beads.Length && Beads[index + 1].Owner == Beads[index].Owner)
                {
                    double gap = Beads[index].SpacingRadius + Beads[index + 1].SpacingRadius;
                    limit = Math.Min(limit, Beads[index + 1].Arc - gap);
                }

                Beads[index].Arc = Math.Min(Beads[index].Arc, limit);
            }

            // Then the floors, top down: the anchor for the first group, and the underside
            // of the charm above for every group after it.
            for (int index = 0; index < Beads.Length; index++)
            {
                double floor = BeadFloor(index);
                if (index > 0 && Beads[index - 1].Owner == Beads[index].Owner)
                {
                    double gap = Beads[index - 1].SpacingRadius + Beads[index].SpacingRadius;
                    floor = Math.Max(floor, Beads[index - 1].Arc + gap);
                }

                // Never past the ceiling. A rope folded hard enough can put the underside
                // of one charm below the top of the next, and when the two walls meet the
                // bead's own charm wins — a bead belongs to the charm it was drawn on, and
                // sinking into that one would be the visible failure where riding a little
                // high is not.
                Beads[index].Arc = Math.Max(Beads[index].Arc, Math.Min(floor, BeadCeiling(index)));
            }
        }
    }

    /// <summary>How far a charm's bead spacing has to be compressed to fit the cord it has.</summary>
    /// <remarks>
    /// One when the artwork fits as drawn, which is always true of a charm hanging by
    /// itself and usually true of two.
    /// </remarks>
    private double BeadSqueeze(int owner, double radius, CharmStackLayout.Slot slot)
    {
        if (owner < 0 || owner >= BeadDescriptions.Count)
        {
            return 1;
        }

        IReadOnlyList<CharmBead> group = BeadDescriptions[owner];
        if (group.Count == 0)
        {
            return 1;
        }

        CharmBead outermost = group[0];
        foreach (CharmBead bead in group)
        {
            if (bead.Offset > outermost.Offset)
            {
                outermost = bead;
            }
        }

        double reach = outermost.Offset * radius;
        double available = slot.BeadSpan - (outermost.SpacingRatio * radius);
        if (reach <= Precision.UlpOfOne || available >= reach)
        {
            return 1;
        }

        return Math.Max(0, available / reach);
    }

    /// <summary>The furthest down the cord a bead may sit: against its own charm's knot.</summary>
    private double BeadCeiling(int index) =>
        KnotArc(Beads[index].Owner) - Beads[index].SpacingRadius;

    /// <summary>
    /// The furthest up the cord a bead may sit: the stretch of cord its own charm was
    /// allotted, measured back from that charm's knot.
    /// </summary>
    /// <remarks>
    /// Measured from the charm below rather than from the underside of the charm above,
    /// and that is the difference between beads that behave and beads that pile up. A
    /// charm hides the cord inside its own circle, and a cord that bends around a charm
    /// has <em>more</em> of itself inside that circle than a straight one — so the gap
    /// between two charms appears to shrink as the rope swings, though the cord itself
    /// cannot shrink at all. Taking the floor from the live gap hands the beads that
    /// artifact as a squeeze, and a hard throw measured eleven points of overlap on beads
    /// nine points across. Taking it from the charm's own allotment, which
    /// <see cref="CharmStackLayout"/> already sized to hold them, is stable whatever the
    /// rope is doing.
    /// </remarks>
    private double BeadFloor(int index)
    {
        int owner = Beads[index].Owner;
        double radius = Beads[index].SpacingRadius;
        if (owner < 0 || owner >= CharmLayout.Slots.Count)
        {
            return radius;
        }

        return Math.Max(radius, KnotArc(owner) - CharmLayout.Slots[owner].BeadSpan);
    }

    /// <summary>
    /// Re-measures the cord: the curve through the chain, where the charm covers it, and
    /// which way the charm therefore hangs.
    /// </summary>
    /// <remarks>
    /// The charm's orientation comes from the cord rather than from the final link. The
    /// two agree on a straight rope and part company on a whipping one, and it is the cord
    /// that has to meet the charm's loop.
    /// </remarks>
    internal void RefreshCord()
    {
        if (Points.Length < 2)
        {
            return;
        }

        var positions = new Vec2[Points.Length];
        for (int index = 0; index < Points.Length; index++)
        {
            positions[index] = Points[index].Position;
        }

        Curve.Rebuild(positions, CharmCenter);

        // Every charm hides the stretch of cord it is drawn over, so each is measured in
        // turn and the cord is drawn — and the beads threaded — in the gaps left between
        // them.
        CharmSpans.Clear();
        CharmOrientations.Clear();

        foreach (CharmStackLayout.Slot slot in CharmLayout.Slots)
        {
            Vec2 center = PositionOfNode(slot.Node);
            ArcSpan span = Curve.Span(center, slot.KnotRadius, slot.Node);

            // A rope folded back on itself can re-enter a charm's circle further down,
            // which would let that charm claim cord belonging to the next one and squeeze
            // its beads out of existence. Each charm keeps the stretch between it and its
            // neighbour: the spans are ordered, always.
            if (CharmSpans.Count > 0)
            {
                ArcSpan previous = CharmSpans[^1];
                span = new ArcSpan(
                    Math.Max(span.Lower, previous.Lower),
                    Math.Max(span.Upper, previous.Lower));
                CharmSpans[^1] = new ArcSpan(previous.Lower, Math.Min(previous.Upper, span.Lower));
            }

            CharmSpans.Add(span);

            // Taken from the cord rather than from the link above it: the two agree on a
            // straight rope and part company on a whipping one, and it is the cord that
            // has to meet the charm's own loop.
            Vec2 delta = center - Curve.PointAtArc(span.Lower);
            CharmOrientations.Add(
                delta.MagnitudeSquared > Precision.UlpOfOne ? Math.Atan2(delta.Y, delta.X) : Math.PI / 2);
        }

        CordLength = CharmSpans.Count > 0 ? CharmSpans[^1].Lower : Curve.Length;
        CordEnd = Curve.PointAtArc(CordLength);
    }

    /// <summary>Where a rope node is, or the anchor if the index has gone stale.</summary>
    public Vec2 PositionOfNode(int node) =>
        node >= 0 && node < Points.Length ? Points[node].Position : Anchor;

    /// <summary>Where the cord meets one charm, in distance along the cord.</summary>
    public double KnotArc(int slot) =>
        slot >= 0 && slot < CharmSpans.Count ? CharmSpans[slot].Lower : CordLength;

    /// <summary>How much of the curve the bottom charm's artwork covers, so the cord stops there.</summary>
    public double KnotDistance =>
        CharmLayout.Bottom is CharmStackLayout.Slot bottom
            ? bottom.KnotRadius
            : CharmRadius * CharmMetricsValue.KnotInset;

    /// <summary>Re-measures every charm's beads against that charm's current radius.</summary>
    /// <param name="preservingMotion">
    /// Keep each bead where it is and let the tether carry it to its new place, rather
    /// than dropping it there.
    /// </param>
    internal void RebuildBeads(bool preservingMotion)
    {
        RefreshCord();
        IReadOnlyList<CharmStackLayout.Slot> slots = CharmLayout.Slots;

        if (BeadDescriptions.Count == 0 || slots.Count == 0 || Points.Length < 2)
        {
            Beads = [];
            ApplyMasses();
            return;
        }

        RopeBead[] previous = Beads;
        var rebuilt = new List<RopeBead>(previous.Length);

        for (int owner = 0; owner < slots.Count; owner++)
        {
            CharmStackLayout.Slot slot = slots[owner];
            if (owner >= BeadDescriptions.Count || slot.Radius <= 0)
            {
                continue;
            }

            double radius = slot.Radius;
            double knot = Curve.IsEmpty
                ? (slot.Node * Configuration.SegmentLength) - slot.KnotRadius
                : KnotArc(owner);

            // The artwork's spacing is drawn for a charm hanging alone. On a crowded rope
            // the group is squeezed toward its charm until it fits the cord it has, which
            // is a few per cent and invisible — and the alternative is not: rest positions
            // that do not fit leave every bead fighting its tether against the separation
            // pass, for ever.
            double squeeze = BeadSqueeze(owner, radius, slot);

            foreach (CharmBead description in BeadDescriptions[owner])
            {
                double restOffset = description.Offset * radius * squeeze;
                double arc = Math.Clamp(knot - restOffset, 0, Math.Max(Curve.Length, 0));

                // Matched to the bead that held this place on the same charm, so a charm
                // change grows its beads into place instead of dropping new ones in — and
                // so adding a charm leaves the others' beads alone.
                RopeBead? existing = null;
                if (preservingMotion)
                {
                    foreach (RopeBead candidate in previous)
                    {
                        if (candidate.Owner == owner && candidate.RestOffset.Equals(restOffset))
                        {
                            existing = candidate;
                            break;
                        }
                    }
                }

                Vec2 position = existing?.Position ?? Curve.PointAtArc(arc);
                rebuilt.Add(new RopeBead
                {
                    Position = position,
                    PreviousPosition = existing?.PreviousPosition ?? position,
                    Arc = existing?.Arc ?? arc,
                    RestOffset = restOffset,
                    SpacingRadius = description.SpacingRatio * radius,
                    Size = new Size(description.Size.Width * radius, description.Size.Height * radius),
                    Mass = description.Mass,
                    Owner = owner,
                    Angle = existing?.Angle ?? Curve.AngleAtArc(arc),
                });
            }
        }

        Beads = [.. rebuilt];
        ApplyMasses();
    }

    /// <summary>
    /// The charm's mass as the solver sees it: what the charm weighs, scaled by what the
    /// cord is made of.
    /// </summary>
    /// <remarks>
    /// The rope↔charm link is the one link whose inverse-mass ratio a style can usefully
    /// change, which is why this is the only mass a <see cref="RopeStyle"/> touches;
    /// <see cref="RopePhysicsProfile"/> explains why scaling the rope's own nodes would
    /// not.
    /// </remarks>
    public double EffectiveCharmMass =>
        CharmLayout.Bottom is CharmStackLayout.Slot bottom
            ? EffectiveMassOf(bottom)
            : Math.Max(CharmMetricsValue.Mass * RopeStyleTable.PhysicsOf(Style).CharmMassScale, 0.0001);

    /// <summary>One charm's mass as the solver sees it.</summary>
    public double EffectiveMassOf(CharmStackLayout.Slot charm) =>
        Math.Max(charm.Mass * RopeStyleTable.PhysicsOf(Style).CharmMassScale, 0.0001);

    /// <summary>
    /// Rebuilds every node's inverse mass: the anchor pinned, a charm's weight on each node
    /// one hangs from, and each bead's weight shared between the two nodes it hangs between.
    /// </summary>
    /// <remarks>
    /// The node on the end <em>becomes</em> its charm, because there is no rope below it to
    /// weigh anything; a charm hanging from an interior node adds its weight to the rope
    /// already there. That is the whole of "physics must account for the combined weight":
    /// three charms put three masses on one chain, and the solver that already balanced one
    /// against the cord balances three the same way.
    ///
    /// <para>Applied when the stack or the beads change rather than on every step. A bead
    /// only slides a few points, so where its weight lands does not meaningfully change as
    /// it moves, and a mass that changed under the solver every step would be a source of
    /// instability for no visible gain.</para>
    /// </remarks>
    internal void ApplyMasses()
    {
        int last = Points.Length - 1;
        if (last <= 0)
        {
            return;
        }

        for (int index = 0; index < Points.Length; index++)
        {
            SetInverseMass(index == 0 ? 0 : 1, index);
        }

        var charmLoad = new double[Points.Length];
        foreach (CharmStackLayout.Slot charm in CharmLayout.Slots)
        {
            if (charm.Node > 0 && charm.Node < Points.Length)
            {
                charmLoad[charm.Node] += EffectiveMassOf(charm);
            }
        }

        for (int index = 1; index <= last; index++)
        {
            if (charmLoad[index] <= 0)
            {
                continue;
            }

            // The end node *becomes* its charm, because there is no rope below it to weigh
            // anything. An interior node is a piece of rope as well, so it keeps the unit
            // mass it already had and the charm is added to it.
            double rope = index == last ? 0.0 : 1.0;
            SetInverseMass(1 / Math.Max(rope + charmLoad[index], 0.0001), index);
        }

        if (Beads.Length == 0 || Configuration.SegmentLength <= Precision.UlpOfOne)
        {
            return;
        }

        var load = new double[Points.Length];
        foreach (RopeBead bead in Beads)
        {
            double position = Math.Clamp(bead.Arc / Configuration.SegmentLength, 0, last);
            int lower = (int)position;
            int upper = Math.Min(lower + 1, last);
            double fraction = position - lower;
            load[lower] += bead.Mass * (1 - fraction);
            load[upper] += bead.Mass * fraction;
        }

        for (int index = 1; index <= last; index++)
        {
            if (load[index] <= 0)
            {
                continue;
            }

            double baseMass = 1 / Math.Max(Points[index].InverseMass, Precision.UlpOfOne);
            SetInverseMass(1 / Math.Max(baseMass + load[index], 0.0001), index);
        }
    }
}
