//
//  RopeSimulation+Beads.swift
//  Hangly
//
//  The bead pass: particles that ride the cord.
//

import CoreGraphics
import Foundation

/// Beads threaded on the cord above the charm.
///
/// Kept apart from the solver because the dependency runs one way: the rope is
/// solved first and the beads then ride the cord it produced. Nothing here writes a
/// rope node's position, so no amount of bead behaviour can disturb the rope.
@MainActor
extension RopeSimulation {
    /// Fraction of a bead's distance from its rest place taken back per step. The
    /// knot it is threaded against has a little give, not none.
    static let beadTetherStiffness = 0.05

    /// Separation sweeps per step. Beads start apart and move slowly relative to
    /// one another, so two passes are always enough to resolve a touch.
    static let beadSeparationPasses = 2

    /// Advances the beads threaded on the cord.
    ///
    /// Runs after the rope has been solved, so the cord the beads ride on is this
    /// step's cord and not the last one's. Each bead takes a free Verlet step under
    /// gravity and its own momentum, is projected back onto the cord, pulled toward
    /// the knot that holds it and limited to a short slide either side, and finally
    /// separated from its neighbours. The rope never reads the beads back, so none
    /// of this can perturb the rope's own solver.
    func advanceBeads(timeStep: Double) {
        guard !beads.isEmpty, !curve.isEmpty else { return }

        let gravityStep = CGPoint(x: 0, y: configuration.gravity * timeStep * timeStep)
        let damping = configuration.damping
        let displacementLimit = configuration.maximumSpeed * timeStep

        for index in beads.indices {
            var bead = beads[index]
            let carried = (bead.displacement * damping).limited(to: displacementLimit)
            let predicted = bead.position + carried + gravityStep

            // Measured from the knot of the bead's *own* charm, so a bead sits where
            // its artwork drew it whichever charm on the rope that artwork belongs to.
            let knot = knotArc(ofCharm: bead.owner)
            let rest = (knot - bead.restOffset).clamped(to: 0...curve.length)
            // Search only the cord around where the bead already was: a global
            // search could snap it across a fold in a fast swing.
            let window = bead.slideLimit + bead.spacingRadius + configuration.segmentLength
            var arc = curve.arc(nearestTo: predicted, near: bead.arc, window: window)
            arc += (rest - arc) * Self.beadTetherStiffness
            bead.arc = arc.clamped(to: (rest - bead.slideLimit)...(rest + bead.slideLimit))
            beads[index] = bead
        }

        separateBeads()

        for index in beads.indices {
            var bead = beads[index]
            bead.previousPosition = bead.position
            bead.position = curve.point(atArc: bead.arc)
            bead.angle = curve.angle(atArc: bead.arc)
            beads[index] = bead
        }
    }

    /// Pushes touching beads apart along the cord, and keeps each group clear of
    /// the charm it hangs on and of the charm above it.
    ///
    /// Resolved from the bottom of the rope upwards, because every group has a wall
    /// below it: the charm it belongs to. The lowest bead of a group is placed
    /// against that charm and every bead above gives way in turn. A chain against
    /// one wall settles exactly in a single sweep that way, where splitting each
    /// correction between both beads would leave the last one overlapping whatever
    /// the wall had just pushed it into.
    ///
    /// With several charms the walls simply multiply: group *k* is bounded below by
    /// where the cord meets charm *k*, and above by where the cord came back out of
    /// charm *k-1*. Neither bound can be crossed, so two charms' beads can never
    /// meet however hard the rope is thrown.
    func separateBeads() {
        guard !beads.isEmpty else { return }

        for _ in 0..<Self.beadSeparationPasses {
            // Ceilings from the bottom up: each bead is placed against the charm it
            // belongs to, and every bead above it gives way in turn.
            for index in stride(from: beads.count - 1, through: 0, by: -1) {
                var limit = beadCeiling(index)
                if index + 1 < beads.count, beads[index + 1].owner == beads[index].owner {
                    let gap = beads[index].spacingRadius + beads[index + 1].spacingRadius
                    limit = min(limit, beads[index + 1].arc - gap)
                }
                beads[index].arc = min(beads[index].arc, limit)
            }

            // Then the floors, top down: the anchor for the first group, and the
            // underside of the charm above for every group after it.
            for index in beads.indices {
                var floor = beadFloor(index)
                if index > 0, beads[index - 1].owner == beads[index].owner {
                    let gap = beads[index - 1].spacingRadius + beads[index].spacingRadius
                    floor = max(floor, beads[index - 1].arc + gap)
                }
                // Never past the ceiling. A rope folded hard enough can put the
                // underside of one charm below the top of the next, and when the two
                // walls meet the bead's own charm wins — a bead belongs to the charm
                // it was drawn on, and sinking into that one would be the visible
                // failure where riding a little high is not.
                beads[index].arc = max(beads[index].arc, min(floor, beadCeiling(index)))
            }
        }
    }

    /// How far a charm's bead spacing has to be compressed to fit the cord it has.
    ///
    /// One when the artwork fits as drawn, which is always true of a charm hanging
    /// by itself and usually true of two.
    private func beadSqueeze(forCharm owner: Int, radius: Double, in slot: CharmStackLayout.Slot) -> Double {
        guard beadDescriptions.indices.contains(owner) else { return 1 }
        let group = beadDescriptions[owner]
        guard let outermost = group.max(by: { $0.offset < $1.offset }) else { return 1 }

        let reach = outermost.offset * radius
        let available = slot.beadSpan - (outermost.spacingRatio * radius)
        guard reach > .ulpOfOne, available < reach else { return 1 }
        return max(0, available / reach)
    }

    /// The furthest down the cord a bead may sit: against its own charm's knot.
    private func beadCeiling(_ index: Int) -> Double {
        knotArc(ofCharm: beads[index].owner) - beads[index].spacingRadius
    }

    /// The furthest up the cord a bead may sit: the stretch of cord its own charm
    /// was allotted, measured back from that charm's knot.
    ///
    /// Measured from the charm below rather than from the underside of the charm
    /// above, and that is the difference between beads that behave and beads that
    /// pile up. A charm hides the cord inside its own circle, and a cord that bends
    /// around a charm has *more* of itself inside that circle than a straight one —
    /// so the gap between two charms appears to shrink as the rope swings, though
    /// the cord itself cannot shrink at all. Taking the floor from the live gap
    /// hands the beads that artifact as a squeeze, and a hard throw measured eleven
    /// points of overlap on beads nine points across. Taking it from the charm's own
    /// allotment, which `CharmStackLayout` already sized to hold them, is stable
    /// whatever the rope is doing.
    private func beadFloor(_ index: Int) -> Double {
        let owner = beads[index].owner
        let radius = beads[index].spacingRadius
        guard charmLayout.slots.indices.contains(owner) else { return radius }
        return max(radius, knotArc(ofCharm: owner) - charmLayout.slots[owner].beadSpan)
    }

    /// Re-measures the cord: the curve through the chain, where the charm covers it,
    /// and which way the charm therefore hangs.
    ///
    /// The charm's orientation comes from the cord rather than from the final link.
    /// The two agree on a straight rope and part company on a whipping one, and it
    /// is the cord that has to meet the charm's loop.
    func refreshCord() {
        guard points.count >= 2 else { return }
        curve.rebuild(points: points.map(\.position), end: charmCenter)

        // Every charm hides the stretch of cord it is drawn over, so each is measured
        // in turn and the cord is drawn — and the beads threaded — in the gaps left
        // between them.
        charmSpans.removeAll(keepingCapacity: true)
        charmOrientations.removeAll(keepingCapacity: true)
        for slot in charmLayout.slots {
            let center = position(ofNode: slot.node)
            var span = curve.span(around: center, radius: slot.knotRadius, node: slot.node)

            // A rope folded back on itself can re-enter a charm's circle further
            // down, which would let that charm claim cord belonging to the next one
            // and squeeze its beads out of existence. Each charm keeps the stretch
            // between it and its neighbour: the spans are ordered, always.
            if let previous = charmSpans.last {
                span = max(span.lowerBound, previous.lowerBound)...max(span.upperBound, previous.lowerBound)
                charmSpans[charmSpans.count - 1] = previous.lowerBound...min(previous.upperBound, span.lowerBound)
            }
            charmSpans.append(span)

            // Taken from the cord rather than from the link above it: the two agree
            // on a straight rope and part company on a whipping one, and it is the
            // cord that has to meet the charm's own loop.
            let delta = center - curve.point(atArc: span.lowerBound)
            charmOrientations.append(
                delta.magnitudeSquared > .ulpOfOne ? atan2(delta.y, delta.x) : (.pi / 2)
            )
        }

        cordLength = charmSpans.last?.lowerBound ?? curve.length
        cordEnd = curve.point(atArc: cordLength)
    }

    /// Where a rope node is, or the anchor if the index has gone stale.
    func position(ofNode node: Int) -> CGPoint {
        points.indices.contains(node) ? points[node].position : anchor
    }

    /// Where the cord meets one charm, in distance along the cord.
    func knotArc(ofCharm slot: Int) -> Double {
        charmSpans.indices.contains(slot) ? charmSpans[slot].lowerBound : cordLength
    }

    /// How much of the curve the bottom charm's artwork covers, so the cord stops there.
    var knotDistance: Double {
        charmLayout.bottom.map { $0.knotRadius } ?? (charmRadius * charmMetrics.knotInset)
    }

    /// Re-measures every charm's beads against that charm's current radius.
    /// - Parameter preservingMotion: Keep each bead where it is and let the tether
    ///   carry it to its new place, rather than dropping it there.
    func rebuildBeads(preservingMotion: Bool) {
        refreshCord()
        let slots = charmLayout.slots
        guard !beadDescriptions.isEmpty, !slots.isEmpty, points.count >= 2 else {
            beads = []
            applyMasses()
            return
        }

        let previous = beads
        var rebuilt: [RopeBead] = []
        rebuilt.reserveCapacity(previous.count)

        for (owner, slot) in slots.enumerated() {
            guard beadDescriptions.indices.contains(owner), slot.radius > 0 else { continue }
            let radius = slot.radius
            let knot = curve.isEmpty
                ? (Double(slot.node) * configuration.segmentLength) - slot.knotRadius
                : knotArc(ofCharm: owner)

            // The artwork's spacing is drawn for a charm hanging alone. On a crowded
            // rope the group is squeezed toward its charm until it fits the cord it
            // has, which is a few per cent and invisible — and the alternative is
            // not: rest positions that do not fit leave every bead fighting its
            // tether against the separation pass, for ever.
            let squeeze = beadSqueeze(forCharm: owner, radius: radius, in: slot)

            for description in beadDescriptions[owner] {
                let restOffset = description.offset * radius * squeeze
                let arc = (knot - restOffset).clamped(to: 0...max(curve.length, 0))
                // Matched to the bead that held this place on the same charm, so a
                // charm change grows its beads into place instead of dropping new
                // ones in — and so adding a charm leaves the others' beads alone.
                let existing = preservingMotion
                    ? previous.first { $0.owner == owner && $0.restOffset == restOffset }
                    : nil
                let position = existing?.position ?? curve.point(atArc: arc)
                rebuilt.append(RopeBead(
                    position: position,
                    previousPosition: existing?.previousPosition ?? position,
                    arc: existing?.arc ?? arc,
                    restOffset: restOffset,
                    spacingRadius: description.spacingRatio * radius,
                    size: CGSize(width: description.size.width * radius, height: description.size.height * radius),
                    mass: description.mass,
                    owner: owner,
                    angle: existing?.angle ?? curve.angle(atArc: arc)
                ))
            }
        }

        beads = rebuilt
        applyMasses()
    }

    /// The charm's mass as the solver sees it: what the charm weighs, scaled by what
    /// the cord is made of.
    ///
    /// The rope↔charm link is the one link whose inverse-mass ratio a style can
    /// usefully change, which is why this is the only mass a `RopeStyle` touches;
    /// `RopePhysicsProfile` explains why scaling the rope's own nodes would not.
    var effectiveCharmMass: Double {
        charmLayout.bottom.map(effectiveMass(of:)) ?? max(charmMetrics.mass * style.physics.charmMassScale, 0.0001)
    }

    /// One charm's mass as the solver sees it.
    func effectiveMass(of charm: CharmStackLayout.Slot) -> Double {
        max(charm.mass * style.physics.charmMassScale, 0.0001)
    }

    /// Rebuilds every node's inverse mass: the anchor pinned, a charm's weight on
    /// each node one hangs from, and each bead's weight shared between the two nodes
    /// it hangs between.
    ///
    /// The node on the end *becomes* its charm, because there is no rope below it to
    /// weigh anything; a charm hanging from an interior node adds its weight to the
    /// rope already there. That is the whole of "physics must account for the
    /// combined weight": three charms put three masses on one chain, and the solver
    /// that already balanced one against the cord balances three the same way.
    ///
    /// Applied when the stack or the beads change rather than on every step. A bead
    /// only slides a few points, so where its weight lands does not meaningfully
    /// change as it moves, and a mass that changed under the solver every step would
    /// be a source of instability for no visible gain.
    func applyMasses() {
        guard let last = points.indices.last, last > 0 else { return }

        for index in points.indices {
            setInverseMass(index == 0 ? 0 : 1, at: index)
        }

        var charmLoad = [Double](repeating: 0, count: points.count)
        for charm in charmLayout.slots where points.indices.contains(charm.node) && charm.node > 0 {
            charmLoad[charm.node] += effectiveMass(of: charm)
        }
        for index in 1...last where charmLoad[index] > 0 {
            // The end node *becomes* its charm, because there is no rope below it to
            // weigh anything. An interior node is a piece of rope as well, so it
            // keeps the unit mass it already had and the charm is added to it.
            let rope = index == last ? 0.0 : 1.0
            setInverseMass(1 / max(rope + charmLoad[index], 0.0001), at: index)
        }

        guard !beads.isEmpty, configuration.segmentLength > .ulpOfOne else { return }
        var load = [Double](repeating: 0, count: points.count)
        for bead in beads {
            let position = (bead.arc / configuration.segmentLength).clamped(to: 0...Double(last))
            let lower = Int(position)
            let upper = min(lower + 1, last)
            let fraction = position - Double(lower)
            load[lower] += bead.mass * (1 - fraction)
            load[upper] += bead.mass * fraction
        }

        for index in 1...last where load[index] > 0 {
            let base = 1 / max(points[index].inverseMass, .ulpOfOne)
            setInverseMass(1 / max(base + load[index], 0.0001), at: index)
        }
    }
}
