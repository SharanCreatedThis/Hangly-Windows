//
//  RopeSimulation.Stack.cs
//  Hangly
//
//  What hangs on the rope, and what changing it costs.
//

using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>The charms threaded on the cord.</summary>
/// <remarks>
/// Separate from the solver because the dependency runs one way: the stack decides which
/// nodes carry weight and where the cord is covered, and the solver then moves a chain
/// that neither knows nor cares how many charms are on it.
///
/// <para>Everything here is applied <em>in place</em>. Adding a charm, swapping one, or
/// growing one as its artwork fades in never rebuilds the rope — every node keeps its
/// position and its history, so the rope sags into its new weight rather than snapping to
/// a new shape.</para>
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>Attaches one charm's physical properties to the final node.</summary>
    public void SetCharmMetrics(CharmMetrics metrics) => SetCharmStack([metrics]);

    /// <summary>Attaches a whole stack, from the anchor down.</summary>
    /// <remarks>
    /// Applied in place rather than by rebuilding, so swapping charms — or adding one —
    /// keeps the rope exactly where it was and lets the change interpolate frame by frame.
    /// Adding a charm therefore makes the rope <em>sag</em> into its new weight rather than
    /// snapping to a new shape, which is the whole reason the metrics are interpolated
    /// rather than switched.
    /// </remarks>
    public void SetCharmStack(IReadOnlyList<CharmMetrics> metrics)
    {
        IReadOnlyList<CharmMetrics> stack = metrics.Count == 0 ? [CharmMetrics.Default] : [.. metrics];
        if (stack.SequenceEqual(CharmStackMetrics))
        {
            return;
        }

        bool countChanged = stack.Count != CharmStackMetrics.Count;
        CharmStackMetrics = stack;
        RefreshLayout();

        // Each charm's radius decides where its cord ends and how large its beads are, so
        // they are re-measured — but keep moving, which is what lets a charm change grow
        // its beads into place instead of dropping new ones in. A change in the *number* of
        // charms moves every attachment, so its beads are placed outright rather than
        // dragged across the rope.
        RebuildBeads(preservingMotion: !countChanged);
        Wake();
    }

    /// <summary>
    /// Re-resolves where the charms hang. Called whenever the stack or the rope's own
    /// geometry changes, and nowhere else.
    /// </summary>
    internal void RefreshLayout()
    {
        var reach = new double[BeadDescriptions.Count];
        for (int index = 0; index < BeadDescriptions.Count; index++)
        {
            reach[index] = CharmBead.ReachOf(BeadDescriptions[index]);
        }

        CharmLayout = CharmStackLayout.Resolve(CharmStackMetrics, Configuration, reach);
    }

    /// <summary>Attaches the beads one charm threads onto its cord.</summary>
    public void SetBeads(IReadOnlyList<CharmBead> descriptions) => SetBeads([descriptions]);

    /// <summary>Attaches the beads every charm on the rope threads onto its cord.</summary>
    /// <remarks>
    /// Beads are described in proportions, so the same description survives a rescale; the
    /// simulation turns them into points against their own charm's radius and hangs them
    /// above it.
    /// </remarks>
    public void SetBeads(IReadOnlyList<IReadOnlyList<CharmBead>> descriptions)
    {
        if (DescriptionsMatch(descriptions, BeadDescriptions))
        {
            return;
        }

        BeadDescriptions = [.. descriptions.Select(group => (IReadOnlyList<CharmBead>)[.. group])];

        // The beads are part of what a charm occupies on the cord, so how much room each
        // charm gets depends on them.
        RefreshLayout();
        RebuildBeads(preservingMotion: false);
        Wake();
    }

    private static bool DescriptionsMatch(
        IReadOnlyList<IReadOnlyList<CharmBead>> left,
        IReadOnlyList<IReadOnlyList<CharmBead>> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!left[index].SequenceEqual(right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
