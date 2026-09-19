//
//  PhysicsSimulating.swift
//  Hangly
//
//  Phase 2 seam. No simulation is implemented in Phase 1.
//

import Foundation

/// Contract for the simulation that will animate the overlay in Phase 2.
///
/// Phase 1 intentionally ships **no** rope physics. This protocol exists so the
/// boundary is settled before any implementation lands: the overlay view model will
/// own a `PhysicsSimulating`, a display-link ticker will drive `step(deltaTime:)`,
/// and the renderer will read back positions. Fixing the shape now means the Phase 2
/// work is additive rather than a refactor.
///
/// Main-actor isolated because the values it produces are consumed directly by the
/// SwiftUI render pass. A future implementation that simulates off the main actor
/// should publish snapshots across the boundary rather than relaxing this.
@MainActor
protocol PhysicsSimulating: AnyObject {
    /// Whether the simulation is currently advancing.
    var isRunning: Bool { get }

    /// Begins advancing the simulation.
    func start()

    /// Suspends the simulation without discarding its state.
    func stop()

    /// Advances the simulation.
    /// - Parameter deltaTime: Elapsed time in seconds since the previous step.
    func step(deltaTime: TimeInterval)

    /// Returns the simulation to its rest state.
    func reset()
}
