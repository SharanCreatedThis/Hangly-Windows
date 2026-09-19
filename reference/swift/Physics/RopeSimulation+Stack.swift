//
//  RopeSimulation+Stack.swift
//  Hangly
//
//  What hangs on the rope, and what changing it costs.
//

import CoreGraphics
import Foundation

/// The charms threaded on the cord.
///
/// Separate from the solver because the dependency runs one way: the stack decides
/// which nodes carry weight and where the cord is covered, and the solver then moves
/// a chain that neither knows nor cares how many charms are on it.
///
/// Everything here is applied *in place*. Adding a charm, swapping one, or growing
/// one as its artwork fades in never rebuilds the rope — every node keeps its
/// position and its history, so the rope sags into its new weight rather than
/// snapping to a new shape.
@MainActor
extension RopeSimulation {
    /// Attaches one charm's physical properties to the final node.
    func setCharmMetrics(_ metrics: CharmMetrics) {
        setCharmStack([metrics])
    }

    /// Attaches a whole stack, from the anchor down.
    ///
    /// Applied in place rather than by rebuilding, so swapping charms — or adding
    /// one — keeps the rope exactly where it was and lets the change interpolate
    /// frame by frame. Adding a charm therefore makes the rope *sag* into its new
    /// weight rather than snapping to a new shape, which is the whole reason the
    /// metrics are interpolated rather than switched.
    func setCharmStack(_ metrics: [CharmMetrics]) {
        let stack = metrics.isEmpty ? [CharmMetrics.default] : metrics
        guard stack != charmStack else { return }
        let countChanged = stack.count != charmStack.count
        charmStack = stack
        refreshLayout()

        // Each charm's radius decides where its cord ends and how large its beads
        // are, so they are re-measured — but keep moving, which is what lets a
        // charm change grow its beads into place instead of dropping new ones in.
        // A change in the *number* of charms moves every attachment, so its beads
        // are placed outright rather than dragged across the rope.
        rebuildBeads(preservingMotion: !countChanged)
        wake()
    }

    /// Re-resolves where the charms hang. Called whenever the stack or the rope's
    /// own geometry changes, and nowhere else.
    func refreshLayout() {
        charmLayout = CharmStackLayout.resolve(
            metrics: charmStack,
            beadReach: beadDescriptions.map(CharmBead.reach(of:)),
            configuration: configuration
        )
    }

    /// Attaches the beads one charm threads onto its cord.
    func setBeads(_ descriptions: [CharmBead]) {
        setBeads([descriptions])
    }

    /// Attaches the beads every charm on the rope threads onto its cord.
    ///
    /// Beads are described in proportions, so the same description survives a
    /// rescale; the simulation turns them into points against their own charm's
    /// radius and hangs them above it.
    func setBeads(_ descriptions: [[CharmBead]]) {
        guard descriptions != beadDescriptions else { return }
        beadDescriptions = descriptions
        // The beads are part of what a charm occupies on the cord, so how much room
        // each charm gets depends on them.
        refreshLayout()
        rebuildBeads(preservingMotion: false)
        wake()
    }
}
