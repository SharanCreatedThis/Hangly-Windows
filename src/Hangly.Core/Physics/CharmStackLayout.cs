//
//  CharmStackLayout.cs
//  Hangly
//
//  Where the charms sit on the rope, and how large they may be.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>
/// The resolved geometry of a stack of charms: which node each hangs from, and the
/// radius it is allowed once its neighbours have had their say.
/// </summary>
/// <remarks>
/// Pure arithmetic over <see cref="CharmMetrics"/> and a <see cref="RopeConfiguration"/>,
/// deliberately separate from the solver. "Three charms never overlap and never fall off
/// the bottom of the canvas" is a claim about <em>this</em> function, and it can be
/// checked against every charm in the catalogue in a test that runs no physics at all.
/// </remarks>
public sealed class CharmStackLayout
{
    /// <summary>One charm's place on the rope.</summary>
    /// <param name="Node">Index of the rope node the charm is centred on.</param>
    /// <param name="Radius">Drawn radius in points, after scaling and capping.</param>
    /// <param name="KnotInset">Where the cord meets it, as a fraction of the radius.</param>
    /// <param name="Mass">Mass contributed at <paramref name="Node"/>.</param>
    /// <param name="BeadSpan">
    /// Cord available above this charm's knot for the beads it threads, in points,
    /// measured on a rope hanging straight.
    /// </param>
    public readonly record struct Slot(
        int Node,
        double Radius,
        double KnotInset,
        double Mass,
        double BeadSpan)
    {
        /// <summary>Radius of the circle the cord disappears behind.</summary>
        public double KnotRadius => Radius * KnotInset;
    }

    private CharmStackLayout(IReadOnlyList<Slot> slots) => Slots = slots;

    /// <summary>Ordered from the anchor down; the last hangs on the end of the rope.</summary>
    public IReadOnlyList<Slot> Slots { get; }

    public int Count => Slots.Count;

    /// <summary>
    /// The charm on the end of the rope, which is the one everything that predates
    /// stacks means by "the charm".
    /// </summary>
    public Slot? Bottom => Slots.Count > 0 ? Slots[^1] : null;

    public static CharmStackLayout Empty { get; } = new([]);

    /// <summary>Resolves a stack against the rope it hangs on.</summary>
    /// <remarks>
    /// Walked from the anchor down, each charm taking what the rope has left. What a
    /// charm occupies is not its own circle but its circle <em>and the beads threaded
    /// above it</em> — a daruma's beads reach a full radius past its knot — so a stack
    /// sized on radius alone leaves the lowest charm's beads nowhere to go and they end
    /// up inside the charm above. Measured, not guessed: three charms at the scale that
    /// looked right left the bottom one's beads needing thirty-seven points of cord and
    /// holding twenty-five.
    /// </remarks>
    /// <param name="beadReach">
    /// How far each charm's beads extend past its knot, in multiples of its own radius.
    /// Zero for a charm that threads none.
    /// </param>
    public static CharmStackLayout Resolve(
        IReadOnlyList<CharmMetrics> metrics,
        RopeConfiguration configuration,
        IReadOnlyList<double>? beadReach = null)
    {
        if (metrics.Count == 0)
        {
            return Empty;
        }

        int[] nodes = RopeConfiguration.Layout.Attachments(metrics.Count, configuration.SegmentCount);
        double scale = RopeConfiguration.Layout.CharmScale(metrics.Count);
        double segment = configuration.SegmentLength;

        // The charm's own ruler, which is not the rope's length: see
        // RopeConfiguration.CharmReference. Lengthening the rope moves the charms apart
        // without changing how big any of them is.
        double reference = configuration.CharmReference;

        var slots = new List<Slot>(metrics.Count);

        // How much cord the charm above has already taken below its own centre.
        double consumed = 0;

        for (int index = 0; index < metrics.Count; index++)
        {
            CharmMetrics charm = metrics[index];
            double reach = beadReach is not null && index < beadReach.Count ? Math.Max(0, beadReach[index]) : 0;
            double gapAbove = (nodes[index] - (index == 0 ? 0 : nodes[index - 1])) * segment;
            double above = gapAbove - consumed;

            // Everything this charm needs above its centre — its own artwork up to the
            // knot, then its beads — has to fit in what is left.
            double ceiling = above / Math.Max(charm.KnotInset + reach, Precision.UlpOfOne);

            // Then the halves: no charm may take more than its share of the rope on
            // either side, so no two neighbours can sum past what is between them
            // whatever their artwork asks for. Above the first charm the "neighbour" is
            // the anchor, which keeps it from reaching up into the taskbar.
            ceiling = Math.Min(ceiling, RopeConfiguration.Layout.CharmClearance * gapAbove);

            if (index == metrics.Count - 1)
            {
                // Whatever the canvas actually has left below the rope, rather than a
                // fraction of the rope: the canvas grows for a large charm and that
                // extra room is exactly what the halo is meant to use.
                ceiling = Math.Min(ceiling, configuration.CharmHeadroom / RopeConfiguration.Layout.CharmHaloExtent);
            }
            else
            {
                double below = (nodes[index + 1] - nodes[index]) * segment;
                ceiling = Math.Min(ceiling, RopeConfiguration.Layout.CharmClearance * below);
            }

            double radius = Math.Max(0, Math.Min(reference * charm.RadiusRatio * scale, ceiling));
            consumed = radius * charm.KnotInset;
            slots.Add(new Slot(
                Node: nodes[index],
                Radius: radius,
                KnotInset: charm.KnotInset,
                Mass: charm.Mass,
                BeadSpan: Math.Max(0, above - consumed)));
        }

        return new CharmStackLayout(slots);
    }
}
