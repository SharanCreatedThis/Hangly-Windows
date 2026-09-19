//
//  CharmStackLayout.swift
//  Hangly
//
//  Where the charms sit on the rope, and how large they may be.
//

import CoreGraphics
import Foundation

/// The resolved geometry of a stack of charms: which node each hangs from, and the
/// radius it is allowed once its neighbours have had their say.
///
/// Pure arithmetic over `CharmMetrics` and a `RopeConfiguration`, deliberately
/// separate from the solver. "Three charms never overlap and never fall off the
/// bottom of the canvas" is a claim about *this* function, and it can be checked
/// against every charm in the catalogue in a test that runs no physics at all.
struct CharmStackLayout: Equatable, Sendable {
    /// One charm's place on the rope.
    struct Slot: Equatable, Sendable {
        /// Index of the rope node the charm is centred on.
        var node: Int

        /// Drawn radius in points, after scaling and capping.
        var radius: Double

        /// Where the cord meets it, as a fraction of the radius.
        var knotInset: Double

        /// Mass contributed at ``node``.
        var mass: Double

        /// Cord available above this charm's knot for the beads it threads, in
        /// points, measured on a rope hanging straight.
        var beadSpan: Double

        /// Radius of the circle the cord disappears behind.
        var knotRadius: Double { radius * knotInset }
    }

    /// Ordered from the anchor down; the last hangs on the end of the rope.
    var slots: [Slot]

    var count: Int { slots.count }

    /// The charm on the end of the rope, which is the one everything that predates
    /// stacks means by "the charm".
    var bottom: Slot? { slots.last }

    /// Resolves a stack against the rope it hangs on.
    ///
    /// Walked from the anchor down, each charm taking what the rope has left. What a
    /// charm occupies is not its own circle but its circle *and the beads threaded
    /// above it* — a daruma's beads reach a full radius past its knot — so a stack
    /// sized on radius alone leaves the lowest charm's beads nowhere to go and they
    /// end up inside the charm above. Measured, not guessed: three charms at the
    /// scale that looked right left the bottom one's beads needing thirty-seven
    /// points of cord and holding twenty-five.
    ///
    /// - Parameter beadReach: How far each charm's beads extend past its knot, in
    ///   multiples of its own radius. Zero for a charm that threads none.
    static func resolve(
        metrics: [CharmMetrics],
        beadReach: [Double] = [],
        configuration: RopeConfiguration
    ) -> CharmStackLayout {
        guard !metrics.isEmpty else { return CharmStackLayout(slots: []) }

        let nodes = RopeConfiguration.Layout.attachments(
            forCharmCount: metrics.count,
            segmentCount: configuration.segmentCount
        )
        let scale = RopeConfiguration.Layout.charmScale(forCharmCount: metrics.count)
        let segment = configuration.segmentLength
        // The charm's own ruler, which is not the rope's length: see
        // `RopeConfiguration.charmReference`. Lengthening the rope moves the charms
        // apart without changing how big any of them is.
        let reference = configuration.charmReference

        var slots: [Slot] = []
        slots.reserveCapacity(metrics.count)

        // How much cord the charm above has already taken below its own centre.
        var consumed = 0.0

        for index in metrics.indices {
            let charm = metrics[index]
            let reach = beadReach.indices.contains(index) ? max(0, beadReach[index]) : 0
            let gapAbove = Double(nodes[index] - (index == 0 ? 0 : nodes[index - 1])) * segment
            let above = gapAbove - consumed

            // Everything this charm needs above its centre — its own artwork up to
            // the knot, then its beads — has to fit in what is left.
            var ceiling = above / max(charm.knotInset + reach, .ulpOfOne)

            // Then the halves: no charm may take more than its share of the rope on
            // either side, so no two neighbours can sum past what is between them
            // whatever their artwork asks for. Above the first charm the "neighbour"
            // is the anchor, which keeps it from reaching up into the menu bar.
            ceiling = min(ceiling, RopeConfiguration.Layout.charmClearance * gapAbove)
            if index == metrics.count - 1 {
                // Whatever the canvas actually has left below the rope, rather than
                // a fraction of the rope: the canvas grows for a large charm and
                // that extra room is exactly what the halo is meant to use.
                ceiling = min(ceiling, configuration.charmHeadroom / RopeConfiguration.Layout.charmHaloExtent)
            } else {
                let below = Double(nodes[index + 1] - nodes[index]) * segment
                ceiling = min(ceiling, RopeConfiguration.Layout.charmClearance * below)
            }

            let radius = max(0, min(reference * charm.radiusRatio * scale, ceiling))
            consumed = radius * charm.knotInset
            slots.append(Slot(
                node: nodes[index],
                radius: radius,
                knotInset: charm.knotInset,
                mass: charm.mass,
                beadSpan: max(0, above - consumed)
            ))
        }
        return CharmStackLayout(slots: slots)
    }
}

extension CharmBead {
    /// How far a charm's beads reach past its knot, in multiples of its radius.
    static func reach(of beads: [CharmBead]) -> Double {
        beads.map { $0.offset + $0.spacingRatio }.max() ?? 0
    }
}

extension RopeConfiguration.Layout {
    /// Which nodes the charms hang from, from the anchor down.
    ///
    /// The last charm always takes the end node, so a single charm is attached
    /// exactly where it has always been and nothing about the one-charm rope
    /// changes. The others divide the rope as evenly as whole nodes allow: two
    /// charms take nodes 10 and 20, three take 6, 13 and 20. Rounding down rather
    /// than to nearest is what puts the spare node at the *top*, which is the gap a
    /// short cord is least noticeable in.
    static func attachments(forCharmCount count: Int, segmentCount: Int) -> [Int] {
        let charms = max(1, min(count, CharmStack.maximumCount))
        return (1...charms).map { index in
            guard index < charms else { return segmentCount }
            let node = (Double(segmentCount) * Double(index) / Double(charms)).rounded(.down)
            // At least one node clear of its neighbours, however the arithmetic falls.
            return max(index, min(segmentCount - (charms - index), Int(node)))
        }
    }

    /// How much a charm shrinks when it has company.
    ///
    /// Three charms at full size would crowd the rope, and a string of charms reads
    /// better with smaller ones anyway — a single charm is a statement, three are a
    /// string. One charm is untouched, so this changes nothing about the rope as it
    /// shipped.
    static func charmScale(forCharmCount count: Int) -> Double {
        switch max(1, min(count, CharmStack.maximumCount)) {
        case 1: 1.0
        case 2: 0.92
        default: 0.82
        }
    }

    /// The largest a charm may be as a fraction of the rope between it and its
    /// nearest neighbour.
    ///
    /// Below a half, so two adjacent charms can never sum past ninety per cent of
    /// the rope between them and a tenth of it is always left showing as cord. This
    /// is the guarantee rather than the tuning: ``charmScale(forCharmCount:)`` keeps
    /// the shipped charms clear of it, and this catches anything the Studio or a
    /// future asset asks for that would not be.
    static let charmClearance = 0.45

    /// How far a charm's ambient halo reaches past its own radius. Read by
    /// `CharmRenderer.drawAmbientGlow`, which is what makes it true.
    static let charmHaloExtent = 1.7
}
