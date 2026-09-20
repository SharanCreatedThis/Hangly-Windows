//
//  RopeSimulation.Drag.cs
//  Hangly
//
//  Picking the charm up, moving it and letting it go.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>
/// Dragging, which is input handling rather than physics: it decides what the solver is
/// asked to do with the final node, and the solver decides what the rope does about it.
/// </summary>
public sealed partial class RopeSimulation
{
    /// <summary>Whether a charm is currently held.</summary>
    public bool IsDragging => DragIndex is not null;

    /// <summary>Which charm on the rope is held, counted from the anchor.</summary>
    public int? DraggedCharmSlot
    {
        get
        {
            if (DragIndex is not int node)
            {
                return null;
            }

            for (int index = 0; index < CharmLayout.Slots.Count; index++)
            {
                if (CharmLayout.Slots[index].Node == node)
                {
                    return index;
                }
            }

            return null;
        }
    }

    /// <summary>Grabs whichever charm <paramref name="location"/> lands on.</summary>
    /// <returns><c>true</c> when the drag was accepted.</returns>
    public bool BeginDrag(Vec2 location)
    {
        if (CharmAt(location) is not CharmStackLayout.Slot charm)
        {
            return false;
        }

        DragIndex = charm.Node;
        DragTarget = location;
        DragVelocity = Vec2.Zero;
        Wake();
        return true;
    }

    /// <summary>Whether a grab at <paramref name="location"/> would hit any charm on the rope.</summary>
    public bool CanGrab(Vec2 location) => CharmAt(location) is not null;

    /// <summary>The charm under <paramref name="location"/>, or <c>null</c>.</summary>
    /// <remarks>
    /// Nearest wins where two are close enough to both accept the press, which with the
    /// grab padding can happen in the gap between them. Searched from the bottom up so
    /// that a tie goes to the charm on the end — the one a user reaching for "the charm"
    /// almost always means.
    /// </remarks>
    public CharmStackLayout.Slot? CharmAt(Vec2 location) =>
        CharmIndexAt(location) is int index ? CharmLayout.Slots[index] : null;

    /// <summary>
    /// Which place on the rope is under <paramref name="location"/>, or <c>null</c>.
    /// </summary>
    /// <remarks>
    /// The index rather than the slot, for callers that have to act on the place itself
    /// — dropping a file on the third charm has to replace the third charm, and a
    /// <see cref="CharmStackLayout.Slot"/> knows its node but not its position in the
    /// stack. Same search as <see cref="CharmAt"/>, which is written in terms of this.
    /// </remarks>
    public int? CharmIndexAt(Vec2 location)
    {
        int? best = null;
        double bestDistance = double.PositiveInfinity;

        for (int index = CharmLayout.Slots.Count - 1; index >= 0; index--)
        {
            CharmStackLayout.Slot charm = CharmLayout.Slots[index];
            if (charm.Node < 0 || charm.Node >= Points.Length)
            {
                continue;
            }

            double distance = Points[charm.Node].Position.DistanceTo(location);
            if (distance > charm.Radius + RopeConfiguration.Layout.GrabPadding || distance >= bestDistance)
            {
                continue;
            }

            best = index;
            bestDistance = distance;
        }

        return best;
    }

    /// <param name="location">Where the cursor is, in canvas coordinates.</param>
    /// <param name="velocity">Cursor velocity in points per second.</param>
    public void UpdateDrag(Vec2 location, Vec2 velocity)
    {
        if (DragIndex is null)
        {
            return;
        }

        DragTarget = ReachableTarget(location);
        DragVelocity = velocity.Limited(Configuration.MaximumSpeed);
    }

    /// <summary>Pins the drag target to the circle the rope can actually reach.</summary>
    /// <remarks>
    /// Without this, pulling the cursor past the rope's length holds both ends apart
    /// further than the rope can span. The links have nowhere to go but stretch, and
    /// releasing fires the stored tension back as a snap. Clamping makes the rope go taut
    /// and the charm swing around the anchor instead, which is both what a real cord does
    /// and what keeps the stretch bound honest.
    /// </remarks>
    public Vec2 ReachableTarget(Vec2 location)
    {
        // Only the rope *above* the held node can hold it back, so a charm halfway up the
        // rope reaches half as far. Clamping to the whole rope's length here would let
        // the upper half be pulled straight past its limit and stretch.
        double held = (DragIndex ?? (Points.Length - 1)) * Configuration.SegmentLength;
        double reach = held * Configuration.MaximumReachRatio;
        Vec2 offset = location - Anchor;
        double distance = offset.Magnitude;
        if (distance <= reach || distance <= Precision.UlpOfOne)
        {
            return location;
        }

        return Anchor + ((offset / distance) * reach);
    }

    /// <summary>
    /// Releases the charm. The velocity written during the final step stays in the node's
    /// history, so the rope carries on at the speed it was thrown.
    /// </summary>
    public void EndDrag()
    {
        DragIndex = null;
        DragVelocity = Vec2.Zero;
    }
}
