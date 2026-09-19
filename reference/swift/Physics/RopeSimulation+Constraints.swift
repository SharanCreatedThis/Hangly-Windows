//
//  RopeSimulation+Constraints.swift
//  Hangly
//
//  The projection passes: what the rope is not allowed to do.
//

import CoreGraphics
import Foundation

/// Everything the solver enforces after it has integrated.
///
/// Position-based dynamics in the plainest form: the integrator moves nodes wherever
/// gravity and momentum take them, and these passes move them back until the rope's
/// rules hold again. Every one of them is idempotent on a rope that already obeys it,
/// which is what lets them be run to convergence and what makes a settled rope free.
@MainActor
extension RopeSimulation {
    /// One relaxation pass.
    /// - Returns: The largest correction applied, so the caller can stop early.
    @discardableResult
    func solveDistanceConstraints() -> Double {
        let restLength = configuration.segmentLength
        var largestCorrection = 0.0
        for index in 0..<(points.count - 1) {
            let correction = solveLink(from: index, to: index + 1, restLength: restLength)
            largestCorrection = max(largestCorrection, correction)
        }
        return largestCorrection
    }

    /// - Returns: The magnitude of the correction applied to this link.
    @discardableResult
    fileprivate func solveLink(from indexA: Int, to indexB: Int, restLength: Double) -> Double {
        let inverseA = effectiveInverseMass(at: indexA)
        let inverseB = effectiveInverseMass(at: indexB)
        let totalInverseMass = inverseA + inverseB
        guard totalInverseMass > 0 else { return 0 }

        let delta = points[indexB].position - points[indexA].position
        let distance = delta.magnitude
        guard distance > .ulpOfOne else { return 0 }

        // Split the error between the two nodes in proportion to their mobility.
        let correction = delta * ((distance - restLength) / distance / totalInverseMass)
        points[indexA].position += correction * inverseA
        points[indexB].position -= correction * inverseB
        return max((correction * inverseA).magnitude, (correction * inverseB).magnitude)
    }

    /// Keeps two charms on one cord from passing through each other.
    ///
    /// A minimum-distance constraint between the nodes the charms hang from, solved
    /// in the same relaxation as the links so that the rope gives way rather than
    /// the charms. Spacing the attachments along the cord is not enough on its own:
    /// a hard throw folds the rope, and a fold brings two attachment nodes far
    /// closer together than the cord between them — measured at up to eighty points
    /// of overlap before this existed.
    ///
    /// Every pair, not just neighbours, because a deep fold can bring the top charm
    /// down onto the bottom one with the middle charm nowhere near either.
    ///
    /// One-sided, like the stretch ceiling: charms already far enough apart are left
    /// alone, so this costs nothing on a hanging rope and cannot perturb one.
    /// - Returns: The largest correction applied, so relaxation can stop early.
    @discardableResult
    func separateCharms() -> Double {
        let slots = charmLayout.slots
        guard slots.count > 1 else { return 0 }

        var largestCorrection = 0.0
        for first in 0..<(slots.count - 1) {
            for second in (first + 1)..<slots.count {
                let correction = separate(
                    slots[first].node,
                    from: slots[second].node,
                    minimum: slots[first].radius + slots[second].radius
                )
                largestCorrection = max(largestCorrection, correction)
            }
        }
        return largestCorrection
    }

    /// Pushes two nodes apart until they are `minimum` apart, sharing the correction
    /// between them in proportion to their mobility exactly as the link solver does.
    /// - Returns: The magnitude of the correction applied.
    fileprivate func separate(_ lower: Int, from upper: Int, minimum: Double) -> Double {
        guard points.indices.contains(lower), points.indices.contains(upper) else { return 0 }
        let inverseLower = effectiveInverseMass(at: lower)
        let inverseUpper = effectiveInverseMass(at: upper)
        let totalInverseMass = inverseLower + inverseUpper
        guard totalInverseMass > 0 else { return 0 }

        let delta = points[upper].position - points[lower].position
        let distance = delta.magnitude
        guard distance < minimum else { return 0 }

        // Two charms exactly on top of one another have no direction to separate
        // along, so they take the cord's: the later one hangs below.
        let direction = distance > .ulpOfOne ? delta / distance : CGPoint(x: 0, y: 1)
        let correction = direction * ((distance - minimum) / totalInverseMass)
        points[lower].position += correction * inverseLower
        points[upper].position -= correction * inverseUpper
        return max((correction * inverseLower).magnitude, (correction * inverseUpper).magnitude)
    }

    /// The hard guarantee behind "never stretches unrealistically".
    ///
    /// Relaxation targets the rest length and is iterative, so it can leave a link
    /// long after a violent frame. This pass enforces the ceiling as a one-sided
    /// constraint: links inside the limit are untouched, and links over it are
    /// pulled back.
    ///
    /// An earlier version snapped the offending node straight onto the limit. That
    /// oscillated rather than converged, because a chain pinned at both ends had
    /// each sweep undo the last one's work. Splitting the correction between the two
    /// ends, exactly as the distance solver does, converges instead.
    func enforceMaximumStretch() {
        let limit = configuration.segmentLength * configuration.maxStretchRatio

        for _ in 0..<configuration.stretchPasses {
            var corrected = false
            for index in 0..<(points.count - 1) where clampLink(at: index, limit: limit) {
                corrected = true
            }

            // Converged: every link is inside the limit.
            if !corrected { return }
        }
    }

    /// Pulls one over-long link back to `limit`, sharing the correction between its
    /// ends in proportion to their mobility.
    /// - Returns: Whether the link was over its limit.
    fileprivate func clampLink(at index: Int, limit: Double) -> Bool {
        let lower = index
        let upper = index + 1

        let inverseLower = effectiveInverseMass(at: lower)
        let inverseUpper = effectiveInverseMass(at: upper)
        let totalInverseMass = inverseLower + inverseUpper
        guard totalInverseMass > 0 else { return false }

        let delta = points[upper].position - points[lower].position
        let distance = delta.magnitude
        guard distance > limit, distance > .ulpOfOne else { return false }

        let correction = delta * ((distance - limit) / distance / totalInverseMass)
        points[lower].position += correction * inverseLower
        points[upper].position -= correction * inverseUpper
        return true
    }

    /// A held node is immovable for the solver, exactly like the anchor.
    func effectiveInverseMass(at index: Int) -> Double {
        index == dragIndex ? 0 : points[index].inverseMass
    }
}
