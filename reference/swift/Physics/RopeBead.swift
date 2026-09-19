//
//  RopeBead.swift
//  Hangly
//
//  Beads threaded onto the cord above the charm.
//

import CoreGraphics
import Foundation

/// A bead as the charm describes it, in proportions rather than points.
///
/// Everything is a fraction of the charm's radius so a bead keeps its place and its
/// size when the overlay is rescaled or the charm changes: the simulation converts
/// to points each time it is given a radius.
struct CharmBead: Equatable, Sendable {
    /// Size relative to the charm's radius.
    var size: CGSize

    /// Distance from the knot up along the cord, relative to the charm's radius.
    var offset: Double

    /// Mass relative to a plain rope node.
    var mass: Double

    /// Extent along the cord, which is what decides whether two beads collide.
    var spacingRatio: Double { size.height / 2 }
}

/// A bead being simulated.
///
/// A bead is a Verlet particle exactly like a rope node — it carries its position
/// and its previous position, and gravity and damping act on it the same way — with
/// one extra constraint: it lives on the cord. Each step the particle is integrated
/// freely, then projected back onto the curve, which is what makes it lag behind a
/// whipping rope and catch up afterwards instead of being glued to a fixed point.
///
/// The knot it is threaded against keeps it from sliding away: the projected
/// position is pulled back toward its rest place on the cord and hard-limited to a
/// short travel either side, so a bead slides during motion and never migrates.
struct RopeBead: Equatable, Sendable {
    /// Current world position.
    var position: CGPoint

    /// Position at the end of the previous step.
    var previousPosition: CGPoint

    /// Distance along the cord, from the anchor.
    var arc: Double

    /// Where the bead rests, measured back from the knot.
    var restOffset: Double

    /// Half the bead's extent along the cord, in points.
    var spacingRadius: Double

    /// Drawn size in points.
    var size: CGSize

    /// Mass relative to a plain rope node.
    var mass: Double

    /// Which charm on the rope this bead belongs to, counted from the anchor.
    ///
    /// Beads hang above their own charm and are held clear of the charm above them,
    /// so with three charms on one cord the beads fall into three groups that can
    /// never stray into each other's stretch of rope.
    var owner: Int = 0

    /// Orientation of the cord where the bead sits, in radians.
    var angle: Double

    /// Displacement over the last step, which is the bead's implied velocity.
    var displacement: CGPoint {
        position - previousPosition
    }

    /// How far the bead may travel from its rest place, in points. Proportional to
    /// the bead, so a large bead slides further than a small one and neither can
    /// wander into its neighbour.
    var slideLimit: Double {
        spacingRadius * 0.6
    }
}
