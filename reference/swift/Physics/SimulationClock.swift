//
//  SimulationClock.swift
//  Hangly
//
//  Display-synchronised tick source for the simulation.
//

import AppKit
import Foundation
import QuartzCore

/// Drives the simulation from the display's own refresh signal.
///
/// A `Timer` would drift against the compositor and produce visible judder. A
/// `CADisplayLink` fires once per frame on the display actually showing the overlay,
/// so every simulated frame corresponds to a frame the user sees.
///
/// `preferredFrameRateRange` is set explicitly because ProMotion displays otherwise
/// settle at a lower rate for a mostly-static window. Asking for 120 is what makes
/// the rope run at 120 rather than being throttled to 60.
@MainActor
final class SimulationClock: NSObject {
    /// Called once per display frame with the time since the previous frame.
    var onTick: (@MainActor (TimeInterval) -> Void)?

    /// Smoothed frames per second, for the debug read-out.
    private(set) var framesPerSecond: Double = 0

    private var displayLink: CADisplayLink?
    private var lastTimestamp: CFTimeInterval?
    private var isThrottled = false

    var isRunning: Bool {
        displayLink != nil
    }

    /// Full display rate, used while the rope is moving.
    private static let activeRange = CAFrameRateRange(minimum: 60, maximum: 120, preferred: 120)

    /// Idle rate, used once the rope has settled. The link keeps running at a low
    /// rate rather than stopping, because it is also what polls the cursor for a
    /// grab. Thirty per second keeps that responsive at a fraction of the cost.
    private static let idleRange = CAFrameRateRange(minimum: 10, maximum: 30, preferred: 30)

    /// - Parameter view: The view whose display drives the link. Using the view's
    ///   own display link means the rope follows the refresh rate of the screen the
    ///   overlay is actually on, not the primary one.
    func start(in view: NSView) {
        guard displayLink == nil else { return }

        let link = view.displayLink(target: self, selector: #selector(handleTick))
        link.preferredFrameRateRange = Self.activeRange
        link.add(to: .main, forMode: .common)

        lastTimestamp = nil
        isThrottled = false
        displayLink = link
    }

    /// Switches between the full display rate and the idle rate.
    func setThrottled(_ throttled: Bool) {
        guard throttled != isThrottled, let displayLink else { return }
        isThrottled = throttled
        displayLink.preferredFrameRateRange = throttled ? Self.idleRange : Self.activeRange
    }

    func stop() {
        displayLink?.invalidate()
        displayLink = nil
        lastTimestamp = nil
        isThrottled = false
        framesPerSecond = 0
    }

    @objc
    private func handleTick(_ link: CADisplayLink) {
        let now = link.timestamp
        defer { lastTimestamp = now }

        // The first tick has no predecessor to measure against.
        guard let previous = lastTimestamp else { return }

        let delta = now - previous
        guard delta > 0 else { return }

        updateFrameRate(delta: delta)
        onTick?(delta)
    }

    private func updateFrameRate(delta: TimeInterval) {
        let instantaneous = 1 / delta
        framesPerSecond = framesPerSecond == 0
            ? instantaneous
            : (framesPerSecond * 0.9) + (instantaneous * 0.1)
    }
}
