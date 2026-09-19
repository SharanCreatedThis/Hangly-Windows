//
//  RopeSimulation.swift
//  Hangly
//
//  Verlet rope with distance-constraint relaxation.
//

import CoreGraphics
import Foundation

/// A hanging rope simulated with Verlet integration and position-based constraints.
///
/// The step order is deliberate and is what makes the rope both stable and stiff:
///
/// 1. **Pin the anchor.** Done first so a moved anchor drags the rope this step
///    rather than next, which is what makes a window resize look physical.
/// 2. **Integrate.** Each free node moves by its own damped displacement plus
///    gravity. No forces, no velocity array: Verlet infers velocity from history.
/// 3. **Drive the held node.** A dragged node is written directly, with its history
///    set so that its implied velocity matches the cursor's.
/// 4. **Relax constraints.** Gauss-Seidel passes pull each link back to its rest
///    length. Later passes see the corrections of earlier ones, so convergence is
///    fast.
/// 5. **Clamp stretch.** A final hard pass guarantees no link exceeds its limit even
///    if relaxation has not fully converged.
///
/// Time advances in fixed slices. The display's refresh rate only decides how often
/// `step(deltaTime:)` is called, never how far the physics moves per slice, so the
/// rope behaves identically at 60 Hz and 120 Hz.
@MainActor
final class RopeSimulation: PhysicsSimulating {
    /// The rope's nodes, anchor first. Written by the integrator here and by the
    /// projection passes in `RopeSimulation+Constraints`; `setInverseMass(_:at:)`
    /// is the only way anything outside the solver may touch one.
    var points: [RopePoint] = []

    private(set) var anchor: CGPoint

    // The two below are written by `RopeSimulation+Style` as well as here, which is
    // the same arrangement the bead and stack passes have: private to the type in
    // spirit, internal only because Swift cannot say so across a type's own files.

    var configuration: RopeConfiguration

    /// The cord the charm hangs on. Held here as well as in the configuration
    /// because a resize re-fits the configuration from scratch, and the style has to
    /// survive that.
    var style: RopeStyle

    /// The time of day the rope is moving in, held for the same reason.
    var timeProfile: RopeTimeProfile

    /// How far the charm hangs, as a multiple of the shipped rope. Held here for
    /// the same reason as the other two: a resize re-fits from scratch.
    var ropeLength: Double = 1

    /// How large the charm is drawn, held for the same reason. Separate from
    /// ``ropeLength`` in every sense: neither reads the other.
    var charmSize: Double = 1

    /// Mass and size of every charm on the rope, from the anchor down. Maintained
    /// by `RopeSimulation+Stack`, which is the only thing that should write it.
    var charmStack: [CharmMetrics]

    /// Where those charms hang and how large they are allowed to be, resolved
    /// against the rope. Held rather than recomputed because the renderer, the
    /// bead pass and the mass pass all read it several times a step.
    var charmLayout: CharmStackLayout

    /// The charm on the end of the rope. Everything that predates stacks means
    /// this one, and for a single charm it is the whole stack.
    var charmMetrics: CharmMetrics {
        charmStack.last ?? .default
    }

    // The four below are maintained by `RopeSimulation+Beads`, which is the only
    // thing that should write them. They are not `private(set)` only because Swift
    // has no way to say "private to this type across its own files".

    /// The beads threaded on the cord above the charm, nearest the anchor first.
    var beads: [RopeBead] = []

    /// Where the drawn cord stops, which is where the charm's artwork takes over.
    var cordEnd: CGPoint = .zero

    /// Length of the drawn cord, in points. Beads are threaded along it.
    var cordLength: Double = 0

    /// Direction from the knot to each charm's centre, which is how each hangs.
    var charmOrientations: [Double] = []

    /// The stretch of cord each charm covers, from where the cord meets it to
    /// where the cord comes back out below it.
    var charmSpans: [ClosedRange<Double>] = []

    /// How the charm on the end of the rope hangs.
    var charmOrientation: Double {
        charmOrientations.last ?? (.pi / 2)
    }

    private(set) var isRunning = false

    /// Fixed steps consumed by the most recent frame. Surfaced in debug mode.
    private(set) var lastStepCount = 0

    /// Whether the rope has settled and stopped simulating. Cleared by `wake()`.
    private(set) var isSleeping = false

    private var stillFrames = 0

    private var accumulator: TimeInterval = 0
    // Written by `RopeSimulation+Drag`, read by the solver.
    var dragIndex: Int?
    var dragTarget: CGPoint = .zero
    var dragVelocity: CGPoint = .zero

    /// What each charm on the rope says its beads are, in proportions, from the
    /// anchor down. One entry per charm, and an empty entry for a charm with none.
    var beadDescriptions: [[CharmBead]] = []

    /// The drawn cord, rebuilt each step and reused rather than reallocated.
    var curve = RopeCurve()

    /// - Note: The style is applied to the configuration here, so `configuration`
    ///   and `style` can never disagree. That invariant is the whole defence against
    ///   the obvious bug in this feature: the overlay re-fits the rope on every
    ///   resize, and anything that rebuilds the configuration without the style
    ///   silently puts the charm back on thread.
    init(
        configuration: RopeConfiguration = .default,
        anchor: CGPoint = .zero,
        charmMetrics: CharmMetrics = .default,
        charmStack: [CharmMetrics]? = nil,
        style: RopeStyle = .default,
        timeProfile: RopeTimeProfile = .baseline
    ) {
        let resolved = configuration.applying(style, at: timeProfile)
        let stack = charmStack.flatMap { $0.isEmpty ? nil : $0 } ?? [charmMetrics]
        self.configuration = resolved
        self.anchor = anchor
        self.charmStack = stack
        self.charmLayout = CharmStackLayout.resolve(metrics: stack, configuration: resolved)
        self.style = style
        self.timeProfile = timeProfile
        reset()
    }

    // MARK: - PhysicsSimulating

    func start() {
        guard !isRunning else { return }
        if points.isEmpty { reset() }
        accumulator = 0
        isRunning = true
        wake()
    }

    func stop() {
        isRunning = false
        accumulator = 0
    }

    func step(deltaTime: TimeInterval) {
        guard isRunning, deltaTime > 0, !isSleeping else {
            lastStepCount = 0
            return
        }

        // Clamping the accumulator stops a stall or a wake from sleep turning into a
        // burst of catch-up steps, which would look like the rope teleporting.
        accumulator = min(accumulator + deltaTime, configuration.maxFrameDuration)

        let timeStep = configuration.fixedTimeStep
        var taken = 0
        while accumulator >= timeStep {
            advance(timeStep: timeStep)
            accumulator -= timeStep
            taken += 1
        }
        lastStepCount = taken
        updateSleepState()
    }

    /// Wakes the rope so the next `step` does work again.
    func wake() {
        isSleeping = false
        stillFrames = 0
    }

    /// Applies a one-shot velocity impulse to the node at `index`, as if something
    /// hit it from outside. The rope carries the momentum from there.
    ///
    /// Uses the same Verlet trick as a drag release: the gap between position and
    /// previous position *is* the velocity, so writing `previousPosition` backward
    /// by the desired displacement gives the node exactly that much speed on the
    /// next step.
    func applyImpulse(_ velocity: CGPoint, at index: Int) {
        guard points.indices.contains(index), points[index].inverseMass > 0 else { return }
        let timeStep = configuration.fixedTimeStep
        points[index].previousPosition = points[index].position - (velocity * timeStep)
        wake()
    }

    /// A settled rope is indistinguishable from a still image, so stop drawing one.
    private func updateSleepState() {
        guard dragIndex == nil else {
            stillFrames = 0
            return
        }

        let speedLimit = configuration.restSpeed * configuration.fixedTimeStep
        let moving = points.contains { $0.displacement.magnitude > speedLimit }
            || beads.contains { $0.displacement.magnitude > speedLimit }
        if moving {
            stillFrames = 0
            return
        }

        stillFrames += 1
        if stillFrames >= configuration.framesBeforeSleep {
            isSleeping = true
        }
    }

    func reset() {
        reset(angle: configuration.initialAngle)
    }

    /// Rebuilds the rope hanging straight down with no motion, for users who have
    /// asked for reduced motion: it then moves only when they move it.
    func resetToHanging() {
        reset(angle: 0)
    }

    private func reset(angle: Double) {
        points = RopePoint.chain(configuration: configuration, anchor: anchor, charmMetrics: charmMetrics, angle: angle)
        accumulator = 0
        dragIndex = nil
        dragVelocity = .zero
        lastStepCount = 0
        rebuildBeads(preservingMotion: false)
        wake()
    }

    // MARK: - Geometry changes

    /// Re-fits the rope to a new canvas without discarding its motion, so changing
    /// the overlay scale makes the rope swing rather than snap.
    func resize(to canvasSize: CGSize) {
        let fitted = RopeConfiguration.fitted(
            to: canvasSize,
            style: style,
            profile: timeProfile,
            charmSize: charmSize,
            ropeLength: ropeLength
        )
        let needsRebuild = points.count != fitted.pointCount

        configuration = fitted
        anchor = fitted.anchor(in: canvasSize)
        refreshLayout()

        if needsRebuild {
            reset()
        } else {
            // The anchor moved, so the rope has somewhere to swing to.
            rebuildBeads(preservingMotion: true)
            wake()
        }
    }

    // MARK: - Solver

    private func advance(timeStep: Double) {
        enforceAnchor()
        integrate(timeStep: timeStep)
        driveDraggedPoint(timeStep: timeStep)

        // Relax until converged, or until the pass budget runs out. Written as a
        // `while` because the exit condition is the point: a `for ... where` would
        // filter iterations rather than end the loop, quietly running the full
        // budget on every step.
        var relaxations = 0
        var residual = Double.infinity
        while relaxations < configuration.constraintIterations,
              residual >= configuration.convergenceTolerance {
            residual = max(solveDistanceConstraints(), separateCharms())
            relaxations += 1
        }

        enforceMaximumStretch()
        refreshCord()
        advanceBeads(timeStep: timeStep)
    }

    private func enforceAnchor() {
        guard !points.isEmpty else { return }
        points[0].position = anchor
        points[0].previousPosition = anchor
    }

    private func integrate(timeStep: Double) {
        let gravityStep = CGPoint(x: 0, y: configuration.gravity * timeStep * timeStep)
        let damping = configuration.damping
        let displacementLimit = configuration.maximumSpeed * timeStep

        for index in points.indices where index != dragIndex {
            guard points[index].inverseMass > 0 else { continue }

            var point = points[index]
            let carried = (point.displacement * damping).limited(to: displacementLimit)
            point.previousPosition = point.position
            point.position += carried + gravityStep
            points[index] = point
        }
    }

    /// Moves the held node toward the cursor, at a finite rate.
    ///
    /// The rate limit matters more than it looks. A held node that teleports leaves
    /// the rest of the chain an unreachable configuration to solve in one step, and
    /// relaxation cannot redistribute that far in the passes available, so links
    /// visibly stretch for a frame. A real cursor cannot teleport, so following at a
    /// bounded speed is the faithful model as well as the stable one. At human
    /// pointer speeds the limit is never reached and tracking is exact.
    private func driveDraggedPoint(timeStep: Double) {
        guard let dragIndex else { return }

        let current = points[dragIndex].position
        let travelLimit = configuration.maximumSpeed * timeStep
        points[dragIndex].position = current + (dragTarget - current).limited(to: travelLimit)
        points[dragIndex].setVelocity(dragVelocity, timeStep: timeStep)
    }

    /// Sets one node's inverse mass.
    ///
    /// The only way anything outside the solver may touch a node, and it exists so
    /// that bead loading can live beside the beads rather than here.
    func setInverseMass(_ value: Double, at index: Int) {
        guard points.indices.contains(index) else { return }
        points[index].inverseMass = value
    }
}
