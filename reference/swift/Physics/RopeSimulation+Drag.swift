//
//  RopeSimulation+Drag.swift
//  Hangly
//
//  Picking the charm up, moving it and letting it go.
//

import CoreGraphics
import Foundation

/// Dragging, which is input handling rather than physics: it decides what the
/// solver is asked to do with the final node, and the solver decides what the rope
/// does about it.
@MainActor
extension RopeSimulation {
    /// Whether a charm is currently held.
    var isDragging: Bool {
        dragIndex != nil
    }

    /// Which charm on the rope is held, counted from the anchor.
    var draggedCharmSlot: Int? {
        guard let dragIndex else { return nil }
        return charmLayout.slots.firstIndex { $0.node == dragIndex }
    }

    /// Grabs whichever charm `location` lands on.
    /// - Returns: `true` when the drag was accepted.
    @discardableResult
    func beginDrag(at location: CGPoint) -> Bool {
        guard let charm = charm(at: location) else { return false }

        dragIndex = charm.node
        dragTarget = location
        dragVelocity = .zero
        wake()
        return true
    }

    /// Whether a grab at `location` would hit any charm on the rope.
    func canGrab(at location: CGPoint) -> Bool {
        charm(at: location) != nil
    }

    /// The charm under `location`, or `nil`.
    ///
    /// Nearest wins where two are close enough to both accept the press, which with
    /// the grab padding can happen in the gap between them. Searched from the bottom
    /// up so that a tie goes to the charm on the end — the one a user reaching for
    /// "the charm" almost always means.
    func charm(at location: CGPoint) -> CharmStackLayout.Slot? {
        var best: CharmStackLayout.Slot?
        var bestDistance = Double.infinity
        for charm in charmLayout.slots.reversed() where points.indices.contains(charm.node) {
            let distance = points[charm.node].position.distance(to: location)
            guard distance <= charm.radius + RopeConfiguration.Layout.grabPadding,
                  distance < bestDistance else { continue }
            best = charm
            bestDistance = distance
        }
        return best
    }

    /// - Parameter velocity: Cursor velocity in points per second.
    func updateDrag(to location: CGPoint, velocity: CGPoint) {
        guard dragIndex != nil else { return }
        dragTarget = reachableTarget(for: location)
        dragVelocity = velocity.limited(to: configuration.maximumSpeed)
    }

    /// Pins the drag target to the circle the rope can actually reach.
    ///
    /// Without this, pulling the cursor past the rope's length holds both ends
    /// apart further than the rope can span. The links have nowhere to go but
    /// stretch, and releasing fires the stored tension back as a snap. Clamping
    /// makes the rope go taut and the charm swing around the anchor instead, which
    /// is both what a real cord does and what keeps the stretch bound honest.
    func reachableTarget(for location: CGPoint) -> CGPoint {
        // Only the rope *above* the held node can hold it back, so a charm halfway
        // up the rope reaches half as far. Clamping to the whole rope's length here
        // would let the upper half be pulled straight past its limit and stretch.
        let held = Double(dragIndex ?? (points.count - 1)) * configuration.segmentLength
        let reach = held * configuration.maximumReachRatio
        let offset = location - anchor
        let distance = offset.magnitude
        guard distance > reach, distance > .ulpOfOne else { return location }
        return anchor + ((offset / distance) * reach)
    }

    /// Releases the charm. The velocity written during the final step stays in the
    /// node's history, so the rope carries on at the speed it was thrown.
    func endDrag() {
        dragIndex = nil
        dragVelocity = .zero
    }
}
