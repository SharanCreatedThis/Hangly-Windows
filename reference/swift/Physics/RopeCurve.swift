//
//  RopeCurve.swift
//  Hangly
//
//  The drawn cord, measured by arc length.
//

import CoreGraphics
import Foundation

/// The cord as a curve that can be walked by distance.
///
/// The rope is drawn as a quadratic spline through the node midpoints rather than as
/// a polyline, so anything that has to sit *on* the cord — a bead — must be placed on
/// that same curve, not on the underlying chain. This builds the spline, flattens it
/// once, and answers three questions: where is the point this far along, how far
/// along is the point nearest here, and what does the curve look like up to a cut.
///
/// The curve runs the whole chain, down to the charm's centre. The cord that is
/// actually drawn stops short of that, at the knot where the charm's own artwork
/// takes over, which is a cut at a given arc length rather than a shorter curve.
///
/// Flattening rather than solving analytically because the answers are needed a few
/// hundred times a second and need to be cheap, not exact: four samples per segment
/// puts the error far below a pixel at the sizes a charm is drawn.
struct RopeCurve {
    private var samples: [CGPoint] = []
    private var cumulative: [Double] = []
    private var samplesPerSegment = 4
    private var nodeCount = 0

    /// Total length of the drawn cord, in points.
    var length: Double { cumulative.last ?? 0 }

    var isEmpty: Bool { samples.count < 2 }

    init() {}

    /// - Parameters:
    ///   - points: Node positions, anchor first.
    ///   - end: Where the cord stops, which is the knot on the charm rather than
    ///     the charm's centre.
    ///   - samplesPerSegment: Flattening density.
    init(points: [CGPoint], end: CGPoint, samplesPerSegment: Int = 4) {
        rebuild(points: points, end: end, samplesPerSegment: samplesPerSegment)
    }

    /// Re-measures the curve in place.
    ///
    /// The solver rebuilds this on every fixed step, so it reuses its storage: an
    /// allocation per step, several hundred times a second, is exactly the kind of
    /// churn that shows up as jitter on a 120 Hz overlay.
    mutating func rebuild(points: [CGPoint], end: CGPoint, samplesPerSegment: Int = 4) {
        samples.removeAll(keepingCapacity: true)
        cumulative.removeAll(keepingCapacity: true)
        self.samplesPerSegment = samplesPerSegment
        nodeCount = points.count
        guard let first = points.first, points.count >= 2 else { return }

        samples.append(first)
        if points.count > 2 {
            var start = first
            for index in 1..<(points.count - 1) {
                let control = points[index]
                let finish = (points[index] + points[index + 1]) * 0.5
                for step in 1...samplesPerSegment {
                    let fraction = Double(step) / Double(samplesPerSegment)
                    samples.append(Self.quadratic(start: start, control: control, end: finish, at: fraction))
                }
                start = finish
            }
        }
        samples.append(end)

        cumulative.append(0)
        var total = 0.0
        for index in 1..<samples.count {
            total += samples[index].distance(to: samples[index - 1])
            cumulative.append(total)
        }
    }

    private static func quadratic(start: CGPoint, control: CGPoint, end: CGPoint, at fraction: Double) -> CGPoint {
        let inverse = 1 - fraction
        let toStart = start * (inverse * inverse)
        let toControl = control * (2 * inverse * fraction)
        let toEnd = end * (fraction * fraction)
        return toStart + toControl + toEnd
    }

    /// The flattened curve up to `arc`, as a polyline that can be stroked.
    ///
    /// The samples are a few points apart, far below the curvature the rope reaches,
    /// so stroking this is indistinguishable from stroking the spline itself — and
    /// it is the same geometry the beads are placed on, which is the point.
    func polyline(upTo arc: Double) -> [CGPoint] {
        guard !isEmpty else { return [] }
        let cut = arc.clamped(to: 0...length)
        var result: [CGPoint] = []
        result.reserveCapacity(samples.count)
        for (index, sample) in samples.enumerated() {
            if cumulative[index] >= cut { break }
            result.append(sample)
        }
        result.append(point(atArc: cut))
        return result
    }

    /// The flattened curve between two distances along it.
    ///
    /// What draws the cord between two charms. Taking a slice rather than a prefix
    /// is the whole difference between a rope with charms threaded on it and a rope
    /// with charms laid over it.
    func polyline(from start: Double, to end: Double) -> [CGPoint] {
        guard !isEmpty else { return [] }
        let lower = start.clamped(to: 0...length)
        let upper = end.clamped(to: lower...length)
        guard upper > lower else { return [] }

        var result: [CGPoint] = [point(atArc: lower)]
        for (index, sample) in samples.enumerated() {
            guard cumulative[index] > lower else { continue }
            if cumulative[index] >= upper { break }
            result.append(sample)
        }
        result.append(point(atArc: upper))
        return result
    }

    /// Where the curve last crosses into a circle of `radius` around `center`.
    ///
    /// This is where the cord disappears behind the charm: everything nearer the
    /// charm's centre than its own radius is covered by its artwork. Measuring the
    /// cut this way rather than by subtracting a length means the cord meets the
    /// charm at the same place whether the rope is hanging straight or whipping,
    /// because a bent tail covers less cord than a straight one.
    func arc(enteringCircleAround center: CGPoint, radius: Double) -> Double {
        guard !isEmpty else { return 0 }
        guard radius > 0 else { return length }

        var index = samples.count - 1
        while index > 0 {
            let outer = samples[index - 1].distance(to: center)
            guard outer >= radius else {
                index -= 1
                continue
            }
            let inner = samples[index].distance(to: center)
            let span = outer - inner
            let fraction = span > .ulpOfOne ? ((outer - radius) / span).clamped(to: 0...1) : 0
            return cumulative[index - 1] + ((cumulative[index] - cumulative[index - 1]) * fraction)
        }
        return 0
    }

    /// The stretch of cord one charm covers, as the distances where the curve
    /// crosses into and back out of its circle.
    ///
    /// The generalisation of ``arc(enteringCircleAround:radius:)`` to a charm that
    /// is not on the end of the rope. A charm hides the cord it is drawn over, so a
    /// string of three needs four visible pieces of cord and the three gaps between
    /// them — and the gaps have to be *measured*, not subtracted, for the same
    /// reason the single charm's cut is: a bent tail covers less cord than a
    /// straight one, and the cord has to meet the charm's own loop either way.
    ///
    /// Searched outward from the node the charm hangs on rather than from the end,
    /// so three charms cost three short local scans instead of three scans of the
    /// whole curve.
    func span(around center: CGPoint, radius: Double, node: Int) -> ClosedRange<Double> {
        guard !isEmpty else { return 0...0 }
        let seed = sampleIndex(forNode: node)
        guard radius > 0 else { return cumulative[seed]...cumulative[seed] }

        let entry = crossingBackward(from: seed, around: center, radius: radius)
        let exit = crossingForward(from: seed, around: center, radius: radius)
        return entry...max(entry, exit)
    }

    /// Where the curve last crossed into the circle, searching toward the anchor.
    private func crossingBackward(from seed: Int, around center: CGPoint, radius: Double) -> Double {
        var index = seed
        while index > 0 {
            let outer = samples[index - 1].distance(to: center)
            guard outer >= radius else {
                index -= 1
                continue
            }
            let inner = samples[index].distance(to: center)
            let reach = outer - inner
            let fraction = reach > .ulpOfOne ? ((outer - radius) / reach).clamped(to: 0...1) : 0
            return cumulative[index - 1] + ((cumulative[index] - cumulative[index - 1]) * fraction)
        }
        return 0
    }

    /// Where the curve next crosses back out of the circle, searching toward the charm.
    private func crossingForward(from seed: Int, around center: CGPoint, radius: Double) -> Double {
        var index = seed
        while index < samples.count - 1 {
            let outer = samples[index + 1].distance(to: center)
            guard outer >= radius else {
                index += 1
                continue
            }
            let inner = samples[index].distance(to: center)
            let reach = outer - inner
            let fraction = reach > .ulpOfOne ? ((radius - inner) / reach).clamped(to: 0...1) : 1
            return cumulative[index] + ((cumulative[index + 1] - cumulative[index]) * fraction)
        }
        return length
    }

    /// The sample that tracks a rope node.
    ///
    /// The curve is a quadratic spline through the node *midpoints*, with each node
    /// as a control point, so a node is not itself a sample: it is nearest the
    /// middle of its own sub-curve. That middle is what this returns. The anchor and
    /// the charm are the two exceptions, being the curve's own endpoints.
    private func sampleIndex(forNode node: Int) -> Int {
        guard nodeCount >= 2 else { return 0 }
        let clamped = node.clamped(to: 0...(nodeCount - 1))
        if clamped == 0 { return 0 }
        if clamped >= nodeCount - 1 { return samples.count - 1 }
        let index = 1 + ((clamped - 1) * samplesPerSegment) + (samplesPerSegment / 2)
        return index.clamped(to: 0...(samples.count - 1))
    }

    /// The point this far along the cord, clamped to its ends.
    func point(atArc arc: Double) -> CGPoint {
        guard !isEmpty else { return .zero }
        let target = arc.clamped(to: 0...length)
        let index = segmentIndex(for: target)
        let spanStart = cumulative[index]
        let spanLength = cumulative[index + 1] - spanStart
        guard spanLength > .ulpOfOne else { return samples[index] }
        let fraction = (target - spanStart) / spanLength
        return samples[index] + ((samples[index + 1] - samples[index]) * fraction)
    }

    /// Direction of travel along the cord at this distance, in radians.
    func angle(atArc arc: Double) -> Double {
        guard !isEmpty else { return .pi / 2 }
        let index = segmentIndex(for: arc.clamped(to: 0...length))
        let delta = samples[index + 1] - samples[index]
        guard delta.magnitudeSquared > .ulpOfOne else { return .pi / 2 }
        return atan2(delta.y, delta.x)
    }

    /// Distance along the cord of the point closest to `location`.
    ///
    /// - Parameters:
    ///   - near: Only the cord within `window` of this distance is searched. A bead
    ///     never travels far between steps, and a local search cannot jump the bead
    ///     to the far side of a fold the way a global one could.
    ///   - window: Half-width of the search, in points.
    func arc(nearestTo location: CGPoint, near: Double, window: Double) -> Double {
        guard !isEmpty else { return 0 }
        let lower = (near - window).clamped(to: 0...length)
        let upper = (near + window).clamped(to: 0...length)

        var best = near
        var bestDistance = Double.infinity
        for index in 0..<(samples.count - 1) {
            // Skip segments wholly outside the window.
            if cumulative[index + 1] < lower || cumulative[index] > upper { continue }
            let (arc, distance) = closestPoint(onSegment: index, to: location)
            if distance < bestDistance {
                bestDistance = distance
                best = arc
            }
        }
        return best.clamped(to: lower...upper)
    }

    private func closestPoint(onSegment index: Int, to location: CGPoint) -> (arc: Double, distance: Double) {
        let start = samples[index]
        let end = samples[index + 1]
        let span = end - start
        let lengthSquared = span.magnitudeSquared
        guard lengthSquared > .ulpOfOne else {
            return (cumulative[index], start.distance(to: location))
        }
        let offset = location - start
        let fraction = (((offset.x * span.x) + (offset.y * span.y)) / lengthSquared).clamped(to: 0...1)
        let projected = start + (span * fraction)
        let arc = cumulative[index] + ((cumulative[index + 1] - cumulative[index]) * fraction)
        return (arc, projected.distance(to: location))
    }

    /// Index of the sample segment containing `arc`, by binary search.
    private func segmentIndex(for arc: Double) -> Int {
        var low = 0
        var high = cumulative.count - 1
        while low < high - 1 {
            let middle = (low + high) / 2
            if cumulative[middle] <= arc {
                low = middle
            } else {
                high = middle
            }
        }
        return min(low, samples.count - 2)
    }
}
