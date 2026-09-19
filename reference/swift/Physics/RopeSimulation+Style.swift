//
//  RopeSimulation+Style.swift
//  Hangly
//
//  Changing what the rope is made of, without stopping it.
//

import Foundation

/// The rope style's grip on the solver.
///
/// Almost all of a style is a set of replacements for numbers `RopeConfiguration`
/// already had, applied by `RopeConfiguration.applying(_:)`. What lives here is the
/// part that has to happen *to a rope that is already moving*.
@MainActor
extension RopeSimulation {
    /// Changes the cord, mid-swing if need be.
    ///
    /// Applied on the spot rather than eased in. The rope is not paused, not reset
    /// and not re-fitted: every node keeps its position *and its history*, so the
    /// velocity Verlet infers from that history is untouched and the swing carries
    /// straight through the change into the new cord's behaviour. Only the numbers
    /// the next step reads are different. The look is what eases — see
    /// `OverlayViewModel.ropeStyleLayers`.
    func setStyle(_ newStyle: RopeStyle) {
        guard newStyle != style else { return }
        style = newStyle
        configuration = configuration.applying(newStyle, at: timeProfile)
        refreshLayout()
        applyMasses()
        wake()
    }

    /// Changes how far the charm hangs.
    ///
    /// Re-fits the rope rather than rebuilding it: the links get longer or shorter
    /// and the solver pulls the chain to suit over the next few frames, so dragging
    /// the slider makes the charm *descend* rather than jump. Nothing about the
    /// charm changes — not its radius, not its beads, not its mass.
    func setRopeLength(_ newLength: Double, in canvasSize: CGSize) {
        guard newLength != ropeLength else { return }
        ropeLength = newLength
        resize(to: canvasSize)
    }

    /// Changes how large the charm is drawn.
    ///
    /// The counterpart of ``setRopeLength(_:in:)`` and the exact complement of it:
    /// the charm grows where it hangs, the rope above it keeps every link the
    /// length it had, and the charm does not move up or down a point.
    func setCharmSize(_ newSize: Double, in canvasSize: CGSize) {
        guard newSize != charmSize else { return }
        charmSize = newSize
        resize(to: canvasSize)
    }

    /// Changes the time of day the rope moves in.
    ///
    /// Applied in place like a style, and for the same reasons — but deliberately
    /// *without* waking the rope. A style change is something a person just asked
    /// for and should see; noon arriving is not. Waking a settled rope to tell it
    /// the afternoon has started would cost a second of simulation, twice a day,
    /// to show nobody anything: the new numbers are read by the next step whenever
    /// that step happens to come.
    func setTimeProfile(_ newProfile: RopeTimeProfile) {
        guard newProfile != timeProfile else { return }
        timeProfile = newProfile
        configuration = configuration.applying(style, at: newProfile)
    }
}
