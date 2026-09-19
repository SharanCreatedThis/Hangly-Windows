//
//  RopeConfiguration.swift
//  Hangly
//
//  Tunable constants for the rope solver.
//

import CoreGraphics
import Foundation

/// Every number the rope solver depends on, in one value type.
///
/// Separating the tuning from the solver keeps `RopeSimulation` free of magic
/// numbers and lets tests drive the solver into deliberately hostile configurations
/// without touching shipped behaviour.
///
/// Units are points and seconds throughout, matching the SwiftUI canvas the rope is
/// drawn into. The canvas has its origin at the top left with `y` increasing
/// downward, so gravity is a **positive** `y` acceleration.
struct RopeConfiguration: Equatable, Sendable {
    /// Number of links. Twenty is the shipped value.
    var segmentCount: Int

    /// Rest length of one link, in points.
    var segmentLength: Double

    /// Downward acceleration in points per second squared.
    var gravity: Double

    /// Fraction of velocity carried into the next step. Below 1 this is air drag:
    /// the rope loses energy and eventually settles instead of swinging forever.
    var damping: Double

    /// Gauss-Seidel relaxation passes per step. More passes means a stiffer rope.
    ///
    /// Corrections propagate roughly one link per pass, so this must exceed
    /// `segmentCount` for a disturbance at the charm to reach the anchor within a
    /// single step. Below that, yanking one end leaves the far end unaware and the
    /// links in between absorb the difference by stretching.
    var constraintIterations: Int

    /// Inequality-projection sweeps applied after relaxation, enforcing the
    /// stretch ceiling on any link relaxation left over its limit.
    var stretchPasses: Int

    /// Relaxation stops early once no link moved further than this in a whole pass.
    /// Both solver loops are capped rather than fixed: a settled rope converges in a
    /// pass or two and exits, so the large caps above cost nothing except in the
    /// rare frame that actually needs them.
    var convergenceTolerance: Double

    /// Hard ceiling on how far a link may exceed its rest length, as a ratio.
    /// Applied after relaxation, so the rope cannot visibly stretch even if the
    /// solver has not fully converged.
    var maxStretchRatio: Double

    /// Physics advances in fixed slices of this length regardless of the display's
    /// refresh rate. This is what makes behaviour identical at 60, 120 and ProMotion's
    /// variable rates, and what keeps Verlet stable.
    var fixedTimeStep: Double

    /// Upper bound on the real time consumed by one frame. Prevents a stall or a
    /// wake-from-sleep from triggering hundreds of catch-up steps at once.
    var maxFrameDuration: Double

    /// Speed ceiling in points per second, applied per node. A safety rail: no
    /// legitimate interaction reaches it, but it makes divergence impossible.
    var maximumSpeed: Double

    /// How far the charm may be dragged from the anchor, as a fraction of the
    /// rope's total length. Below one, so a fully extended rope keeps a little
    /// slack for the solver to work with and never reads as a rigid bar.
    var maximumReachRatio: Double

    /// Node speed, in points per second, below which the rope counts as still.
    var restSpeed: Double

    /// Consecutive still frames before the solver stops working. The rope is a
    /// background ornament, so once it has settled it must stop costing anything;
    /// without this the overlay would redraw at the display rate forever.
    var framesBeforeSleep: Int

    /// Angle from vertical the rope is released at on first appearance, in radians.
    /// A small offset means the rope visibly swings into place instead of being
    /// motionless until touched.
    var initialAngle: Double

    /// Points of cord that charm radii are measured against.
    ///
    /// Deliberately *not* ``totalLength``. Charm sizes are written as fractions of
    /// a rope, and for as long as there was one slider called "Size" that was the
    /// same thing. It is not any more: lengthening the rope must not fatten the
    /// charm, and enlarging the charm must not lower it. This is the fixed ruler
    /// both are measured with, set once from the canvas and left alone by both
    /// controls.
    ///
    /// Zero means "never fitted to a canvas" — `default`, and the configurations
    /// tests build by hand — in which case the rope's own length is the ruler,
    /// exactly as it was before the two could move apart.
    var charmUnit: Double = 0

    /// How large the charm is drawn, as the person asked for it.
    var charmSizeScale: Double = 1

    /// Points between the end of the rope and the bottom of the canvas. What stops
    /// the lowest charm being clipped by the edge of the window.
    ///
    /// Zero carries the same meaning as it does for ``charmUnit``.
    var slackBelow: Double = 0

    /// Where the rope is pinned, in points below the top of the canvas.
    ///
    /// Held in points rather than taken as a fraction of the canvas, because the
    /// canvas now grows to make room for a long rope or a large charm and the
    /// charm must go on hanging from the same place on the display when it does.
    var anchorHeight: Double = 0

    /// Number of nodes, which is one more than the number of links.
    var pointCount: Int {
        segmentCount + 1
    }

    /// Total rest length of the rope.
    var totalLength: Double {
        Double(segmentCount) * segmentLength
    }

    /// The ruler charm radii are measured with, after the person's charm size.
    var charmReference: Double {
        (charmUnit > 0 ? charmUnit : totalLength) * charmSizeScale
    }

    /// Room below the rope for the lowest charm and its halo, in points.
    var charmHeadroom: Double {
        guard slackBelow <= 0 else { return slackBelow }
        return totalLength * Layout.tailFraction / Layout.lengthFraction
    }

    /// Where the rope is pinned in a canvas of this size.
    func anchor(in size: CGSize) -> CGPoint {
        guard anchorHeight > 0 else { return Layout.anchor(in: size) }
        return CGPoint(x: size.width / 2, y: anchorHeight)
    }

    static let `default` = RopeConfiguration(
        segmentCount: 20,
        segmentLength: 11,
        gravity: 2000,
        damping: 0.999,
        constraintIterations: 256,
        stretchPasses: 256,
        convergenceTolerance: 0.05,
        maxStretchRatio: 1.02,
        fixedTimeStep: 1.0 / 240.0,
        maxFrameDuration: 0.1,
        maximumSpeed: 6000,
        maximumReachRatio: 0.98,
        restSpeed: 4.0,
        framesBeforeSleep: 60,
        initialAngle: 0.38
    )

    /// Fits the rope to a canvas, keeping the shipped proportions at any scale.
    ///
    /// The canvas is not the ruler. It used to be: every length here was a fraction
    /// of the window's height, so growing the window to make room for a bigger charm
    /// also lengthened the rope, and lengthening the rope also fattened the charm.
    /// What the canvas gives is a *unit* — the height the shipped window would have
    /// had for this rope — and both controls are then measured against that instead
    /// of against each other.
    ///
    /// - Parameters:
    ///   - size: The overlay canvas size in points.
    ///   - style: The cord the charm hangs on. Taken here rather than applied
    ///     afterwards because the overlay re-fits on every resize, and a style left
    ///     out of the fit would be silently reset to thread the first time the
    ///     window changed size.
    ///   - profile: The time of day the rope is moving in, applied for the same
    ///     reason and with the same consequence if it were left out.
    ///   - charmSize: How large the charm is drawn. Changes nothing about the rope.
    ///   - ropeLength: How far the charm hangs, as a multiple of the shipped rope.
    ///     Changes nothing about the charm.
    static func fitted(
        to size: CGSize,
        style: RopeStyle = .default,
        profile: RopeTimeProfile = .baseline,
        charmSize: Double = 1,
        ropeLength: Double = 1
    ) -> RopeConfiguration {
        var configuration = RopeConfiguration.default
        let room = Layout.canvasScale(charmSize: charmSize, ropeLength: ropeLength)
        // The height the shipped canvas would have had. Every proportion below is
        // taken against this rather than against the canvas actually handed over,
        // which is what keeps the two controls from reading each other.
        let unit = max(40, size.height / room.height)

        configuration.charmUnit = unit * Layout.lengthFraction
        configuration.charmSizeScale = charmSize
        configuration.segmentLength = configuration.charmUnit * ropeLength / Double(configuration.segmentCount)
        configuration.anchorHeight = unit * Layout.anchorFraction
        configuration.slackBelow = max(0, size.height - configuration.anchorHeight - configuration.totalLength)
        return configuration.applying(style, at: profile)
    }

    /// Returns this configuration with a style's solver values applied.
    ///
    /// Every styled value is written from `RopeConfiguration.default` rather than
    /// from the receiver, so this is idempotent and order-independent: applying
    /// leather and then thread gives thread, not a rope that remembers being leather.
    /// Geometry — segment count, length, timestep, sleep — is a property of the
    /// canvas and is left exactly as it was.
    func applying(_ style: RopeStyle, at profile: RopeTimeProfile = .baseline) -> RopeConfiguration {
        let physics = style.physics
        let defaults = RopeConfiguration.default
        var configuration = self
        configuration.gravity = defaults.gravity * physics.gravityScale
        configuration.damping = physics.damping
        configuration.maxStretchRatio = physics.maxStretchRatio
        configuration.constraintIterations = physics.constraintIterations
        configuration.stretchPasses = physics.stretchPasses

        // The time of day goes on last and scales what the style decided, so a
        // leather rope stays the quick one and night is the quicker version of
        // whichever rope you are on. Written from the defaults for the same reason
        // the style is: so that applying morning and then afternoon gives the
        // afternoon, not a rope that remembers the morning.
        let time = profile.physics
        configuration.damping = 1 - ((1 - configuration.damping) * time.energyLossScale)
        configuration.initialAngle = defaults.initialAngle * time.releaseAngleScale
        configuration.restSpeed = defaults.restSpeed * time.restSpeedScale
        return configuration
    }

    /// Proportions shared by the solver and the renderer.
    enum Layout {
        /// Rope length as a fraction of canvas height.
        static let lengthFraction = 0.69

        /// What is left of the shipped canvas below the end of the rope: the room
        /// the lowest charm and its halo hang in.
        static var tailFraction: Double { 1 - anchorFraction - lengthFraction }

        /// How much larger than the shipped canvas the overlay has to be to hold a
        /// rope this long with charms this big.
        ///
        /// The canvas used to be scaled by the one "Size" slider, which is why that
        /// slider moved everything at once. Now it grows by exactly what has been
        /// asked for and no more: a longer rope needs more room below the anchor, a
        /// larger charm needs more room below the rope and more to either side, and
        /// neither is any reason to change the other. At the shipped values this
        /// returns exactly 1×1, so the overlay is the window it always was.
        static func canvasScale(charmSize: Double, ropeLength: Double) -> CGSize {
            let height = anchorFraction + (lengthFraction * ropeLength) + (tailFraction * charmSize)
            return CGSize(width: max(1, charmSize), height: max(1, height))
        }

        /// Anchor height as a fraction of canvas height.
        ///
        /// Small rather than zero: the cord's own drawing starts exactly here, and
        /// a sliver keeps antialiasing at the top of a top-inset-zero window from
        /// reading as a hard clip.
        static let anchorFraction = 0.01

        /// Extra radius around the charm that still accepts a grab.
        static let grabPadding = 10.0

        /// Anchor point for a canvas of the given size.
        static func anchor(in size: CGSize) -> CGPoint {
            CGPoint(x: size.width / 2, y: size.height * anchorFraction)
        }
    }
}
