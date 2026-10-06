//
//  RopeSimulation.Lift.cs
//  Hangly
//
//  A charm lifted toward the anchor winds its cord in, as a badge reel does. macOS's RopeSimulation+Lift.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>Lifting a charm winds its slack in at the anchor.</summary>
/// <remarks>
/// A charm lifted toward the anchor used to leave its whole cord to hang: the slack folded below it in a sharp V
/// (nimbu-mirchi, Sharan, 5 Oct), and a figure lifted close in had the anchor inside its own outline, so none of its rope
/// was left to draw — Spider-Man's web line vanished. Now the cord above the held charm is wound in to just longer than
/// the distance it spans, so it hangs nearly straight from the anchor to the charm, never faster than a cursor can move.
/// Let go, it pays out only as far as the charm pulls it, so the charm drops on a taut cord and swings on as before once
/// the cord is all out.
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>How much longer than the distance it spans the held cord is kept: a little give, so it sags as a cord
    /// does rather than standing like a rod.</summary>
    public const double LiftSlack = 1.02;

    /// <summary>The shortest the wound links get, against their length: never quite to nothing, so the cord keeps a
    /// direction at the anchor.</summary>
    public const double LiftShortest = 0.04;

    /// <summary>The cord above a held charm, wound in, as a fraction of its length; 1 when nothing is wound.</summary>
    public double LiftReel { get; private set; } = 1;

    /// <summary>The node the cord is wound in above, kept after the charm is let go until the cord has paid out.</summary>
    private int? liftNode;

    /// <summary>Which links the lift winds in: those above the held node, less any a figure holds in its hands.</summary>
    private bool[] liftLinks = [];

    /// <summary>Winds the cord above the held charm in or out for this step. Run after the held node has moved and before
    /// the links are solved, so the links are solved at the length the cord has now.</summary>
    private void UpdateLift(double timeStep)
    {
        if (DragIndex is int dragged)
        {
            liftNode = dragged;
        }

        if (liftNode is not int node || node <= 0 || node >= Points.Length || Configuration.SegmentLength <= 0)
        {
            EndLift();
            return;
        }

        // Only the top charm, by the cord above it. Below it the cord runs between charms, where the stack keeps them
        // apart: wound in above a lower charm held up, the top one was pinned under the anchor and the held one pushed
        // through it (34 points, three charms thrown round).
        if (CharmLayout.Slots.Count > 0 && node != CharmLayout.Slots[0].Node)
        {
            EndLift();
            return;
        }

        HashSet<int> held = HeldLinks();
        int links = Math.Max(0, Points.Length - 1);
        if (liftLinks.Length != links)
        {
            liftLinks = new bool[links];
        }

        int wound = 0;
        for (int index = 0; index < links; index++)
        {
            liftLinks[index] = index < node && !held.Contains(index);
            wound += liftLinks[index] ? 1 : 0;
        }

        if (wound == 0)
        {
            EndLift();
            return;
        }

        double unit = Configuration.SegmentLength * ReelFraction;
        double fixedLength = (node - wound) * unit;
        double distance = (Points[node].Position - Anchor).Magnitude;
        bool holding = DragIndex is not null;

        // Held, a little give; let go, exactly as far as the charm has pulled it, so it never runs out ahead of the charm
        // and folds again.
        double spanned = holding ? distance * LiftSlack : distance;
        double wanted = Math.Clamp((spanned - fixedLength) / (wound * unit), LiftShortest, 1);

        if (!holding)
        {
            LiftReel = Math.Max(LiftReel, wanted);
        }
        else if (wanted < LiftReel)
        {
            // Wound in no faster than anything on the rope may move.
            double most = Configuration.MaximumSpeed * timeStep / (wound * unit);
            LiftReel = Math.Max(wanted, LiftReel - most);
        }
        else
        {
            LiftReel = wanted;
        }

        if (LiftReel >= 1 && !holding)
        {
            EndLift();
        }
    }

    /// <summary>The rest length of one link: shortened by the entrance's reel, and by the lift's if it winds this link.</summary>
    internal double RestLength(int link)
    {
        double unit = Configuration.SegmentLength * ReelFraction;
        return LiftReel < 1 && link >= 0 && link < liftLinks.Length && liftLinks[link] ? unit * LiftReel : unit;
    }

    /// <summary>How far below the anchor a held charm's centre may come: far enough for its cord to meet it, and for the
    /// beads above it to fit on the cord between.</summary>
    private double LiftClearance(int node)
    {
        IReadOnlyList<CharmStackLayout.Slot> slots = CharmLayout.Slots;
        for (int slot = 0; slot < slots.Count; slot++)
        {
            if (slots[slot].Node != node)
            {
                continue;
            }

            double beadReach = 0;
            foreach (RopeBead bead in Beads)
            {
                if (bead.Owner == slot)
                {
                    beadReach = Math.Max(beadReach, bead.RestOffset + bead.SpacingRadius);
                }
            }

            return slots[slot].KnotRadius + beadReach;
        }

        return 0;
    }

    private void EndLift()
    {
        LiftReel = 1;
        liftNode = null;
    }
}
