//
//  RopePoint.swift
//  Hangly
//
//  A single Verlet particle.
//

import CoreGraphics

/// One node of the rope.
///
/// Verlet integration stores no explicit velocity. A node's velocity is implied by
/// the gap between where it is and where it was, which is why momentum survives a
/// drag release for free: releasing simply stops writing the position and the gap
/// that the drag left behind becomes the node's velocity.
struct RopePoint: Equatable, Sendable {
    /// Current position in canvas coordinates.
    var position: CGPoint

    /// Position at the end of the previous step.
    var previousPosition: CGPoint

    /// Reciprocal of mass. Zero pins the node in place: constraint corrections
    /// scaled by zero move it not at all, which is how the anchor stays put without
    /// a special case in the solver.
    var inverseMass: Double

    init(position: CGPoint, inverseMass: Double = 1) {
        self.position = position
        self.previousPosition = position
        self.inverseMass = inverseMass
    }

    /// Displacement over the last step. Divide by the step duration for a rate.
    var displacement: CGPoint {
        position - previousPosition
    }

    var isPinned: Bool {
        inverseMass == 0
    }

    /// Sets the implied velocity, in points per second, for a given step length.
    mutating func setVelocity(_ velocity: CGPoint, timeStep: Double) {
        previousPosition = position - (velocity * timeStep)
    }
}

extension RopePoint {
    /// Builds a straight chain of nodes hanging from `anchor` at `angle` from
    /// vertical: the anchor pinned, the charm weighted, everything between free.
    static func chain(
        configuration: RopeConfiguration,
        anchor: CGPoint,
        charmMetrics: CharmMetrics,
        angle: Double
    ) -> [RopePoint] {
        let direction = CGPoint(x: 0, y: 1).rotated(by: angle)
        let lastIndex = configuration.pointCount - 1

        return (0...lastIndex).map { index in
            let inverseMass: Double
            switch index {
            case 0:
                inverseMass = 0
            case lastIndex:
                inverseMass = 1 / max(charmMetrics.mass, 0.0001)
            default:
                inverseMass = 1
            }

            let offset = direction * (Double(index) * configuration.segmentLength)
            return RopePoint(position: anchor + offset, inverseMass: inverseMass)
        }
    }
}
