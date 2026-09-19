//
//  RopeCurve.cs
//  Hangly
//
//  The drawn cord, measured by arc length.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>A closed interval of distance along the cord.</summary>
public readonly record struct ArcSpan(double Lower, double Upper);

/// <summary>The cord as a curve that can be walked by distance.</summary>
/// <remarks>
/// The rope is drawn as a quadratic spline through the node midpoints rather than as a
/// polyline, so anything that has to sit <em>on</em> the cord — a bead — must be placed
/// on that same curve, not on the underlying chain. This builds the spline, flattens it
/// once, and answers three questions: where is the point this far along, how far along
/// is the point nearest here, and what does the curve look like up to a cut.
///
/// <para>The curve runs the whole chain, down to the charm's centre. The cord that is
/// actually drawn stops short of that, at the knot where the charm's own artwork takes
/// over, which is a cut at a given arc length rather than a shorter curve.</para>
///
/// <para>Flattening rather than solving analytically because the answers are needed a
/// few hundred times a second and need to be cheap, not exact: four samples per segment
/// puts the error far below a pixel at the sizes a charm is drawn.</para>
/// </remarks>
public sealed class RopeCurve
{
    private readonly List<Vec2> samples = [];
    private readonly List<double> cumulative = [];
    private int samplesPerSegment = 4;
    private int nodeCount;

    /// <summary>Total length of the drawn cord, in points.</summary>
    public double Length => cumulative.Count > 0 ? cumulative[^1] : 0;

    public bool IsEmpty => samples.Count < 2;

    /// <summary>Re-measures the curve in place.</summary>
    /// <remarks>
    /// The solver rebuilds this on every fixed step, so it reuses its storage: an
    /// allocation per step, several hundred times a second, is exactly the kind of churn
    /// that shows up as jitter on a 120 Hz overlay — and on .NET it is also what would
    /// put a garbage collection in the middle of a frame.
    /// </remarks>
    /// <param name="points">Node positions, anchor first.</param>
    /// <param name="end">
    /// Where the cord stops, which is the knot on the charm rather than the charm's
    /// centre.
    /// </param>
    /// <param name="samplesPerSegment">Flattening density.</param>
    public void Rebuild(IReadOnlyList<Vec2> points, Vec2 end, int samplesPerSegment = 4)
    {
        samples.Clear();
        cumulative.Clear();
        this.samplesPerSegment = samplesPerSegment;
        nodeCount = points.Count;

        if (points.Count < 2)
        {
            return;
        }

        Vec2 first = points[0];
        samples.Add(first);

        if (points.Count > 2)
        {
            Vec2 start = first;
            for (int index = 1; index < points.Count - 1; index++)
            {
                Vec2 control = points[index];
                Vec2 finish = (points[index] + points[index + 1]) * 0.5;
                for (int step = 1; step <= samplesPerSegment; step++)
                {
                    double fraction = (double)step / samplesPerSegment;
                    samples.Add(Quadratic(start, control, finish, fraction));
                }

                start = finish;
            }
        }

        samples.Add(end);

        cumulative.Add(0);
        double total = 0;
        for (int index = 1; index < samples.Count; index++)
        {
            total += samples[index].DistanceTo(samples[index - 1]);
            cumulative.Add(total);
        }
    }

    private static Vec2 Quadratic(Vec2 start, Vec2 control, Vec2 end, double fraction)
    {
        double inverse = 1 - fraction;
        Vec2 toStart = start * (inverse * inverse);
        Vec2 toControl = control * (2 * inverse * fraction);
        Vec2 toEnd = end * (fraction * fraction);
        return toStart + toControl + toEnd;
    }

    /// <summary>The flattened curve up to <paramref name="arc"/>, as a strokeable polyline.</summary>
    /// <remarks>
    /// The samples are a few points apart, far below the curvature the rope reaches, so
    /// stroking this is indistinguishable from stroking the spline itself — and it is
    /// the same geometry the beads are placed on, which is the point.
    /// </remarks>
    public List<Vec2> Polyline(double arc)
    {
        var result = new List<Vec2>();
        if (IsEmpty)
        {
            return result;
        }

        double cut = Math.Clamp(arc, 0, Length);
        result.Capacity = samples.Count;
        for (int index = 0; index < samples.Count; index++)
        {
            if (cumulative[index] >= cut)
            {
                break;
            }

            result.Add(samples[index]);
        }

        result.Add(PointAtArc(cut));
        return result;
    }

    /// <summary>The flattened curve between two distances along it.</summary>
    /// <remarks>
    /// What draws the cord between two charms. Taking a slice rather than a prefix is
    /// the whole difference between a rope with charms threaded on it and a rope with
    /// charms laid over it.
    /// </remarks>
    public List<Vec2> Polyline(double start, double end)
    {
        var result = new List<Vec2>();
        if (IsEmpty)
        {
            return result;
        }

        double lower = Math.Clamp(start, 0, Length);
        double upper = Math.Clamp(end, lower, Length);
        if (upper <= lower)
        {
            return result;
        }

        result.Add(PointAtArc(lower));
        for (int index = 0; index < samples.Count; index++)
        {
            if (cumulative[index] <= lower)
            {
                continue;
            }

            if (cumulative[index] >= upper)
            {
                break;
            }

            result.Add(samples[index]);
        }

        result.Add(PointAtArc(upper));
        return result;
    }

    /// <summary>Where the curve last crosses into a circle of <paramref name="radius"/>.</summary>
    /// <remarks>
    /// This is where the cord disappears behind the charm: everything nearer the charm's
    /// centre than its own radius is covered by its artwork. Measuring the cut this way
    /// rather than by subtracting a length means the cord meets the charm at the same
    /// place whether the rope is hanging straight or whipping, because a bent tail
    /// covers less cord than a straight one.
    /// </remarks>
    public double ArcEnteringCircle(Vec2 center, double radius)
    {
        if (IsEmpty)
        {
            return 0;
        }

        if (radius <= 0)
        {
            return Length;
        }

        int index = samples.Count - 1;
        while (index > 0)
        {
            double outer = samples[index - 1].DistanceTo(center);
            if (outer < radius)
            {
                index -= 1;
                continue;
            }

            double inner = samples[index].DistanceTo(center);
            double span = outer - inner;
            double fraction = span > Precision.UlpOfOne ? Math.Clamp((outer - radius) / span, 0, 1) : 0;
            return cumulative[index - 1] + ((cumulative[index] - cumulative[index - 1]) * fraction);
        }

        return 0;
    }

    /// <summary>
    /// The stretch of cord one charm covers, as the distances where the curve crosses
    /// into and back out of its circle.
    /// </summary>
    /// <remarks>
    /// The generalisation of <see cref="ArcEnteringCircle"/> to a charm that is not on
    /// the end of the rope. A charm hides the cord it is drawn over, so a string of
    /// three needs four visible pieces of cord and the three gaps between them — and the
    /// gaps have to be <em>measured</em>, not subtracted, for the same reason the single
    /// charm's cut is.
    ///
    /// <para>Searched outward from the node the charm hangs on rather than from the end,
    /// so three charms cost three short local scans instead of three scans of the whole
    /// curve.</para>
    /// </remarks>
    public ArcSpan Span(Vec2 center, double radius, int node)
    {
        if (IsEmpty)
        {
            return new ArcSpan(0, 0);
        }

        int seed = SampleIndexForNode(node);
        if (radius <= 0)
        {
            return new ArcSpan(cumulative[seed], cumulative[seed]);
        }

        double entry = CrossingBackward(seed, center, radius);
        double exit = CrossingForward(seed, center, radius);
        return new ArcSpan(entry, Math.Max(entry, exit));
    }

    /// <summary>Where the curve last crossed into the circle, searching toward the anchor.</summary>
    private double CrossingBackward(int seed, Vec2 center, double radius)
    {
        int index = seed;
        while (index > 0)
        {
            double outer = samples[index - 1].DistanceTo(center);
            if (outer < radius)
            {
                index -= 1;
                continue;
            }

            double inner = samples[index].DistanceTo(center);
            double reach = outer - inner;
            double fraction = reach > Precision.UlpOfOne ? Math.Clamp((outer - radius) / reach, 0, 1) : 0;
            return cumulative[index - 1] + ((cumulative[index] - cumulative[index - 1]) * fraction);
        }

        return 0;
    }

    /// <summary>Where the curve next crosses back out of the circle, searching toward the charm.</summary>
    private double CrossingForward(int seed, Vec2 center, double radius)
    {
        int index = seed;
        while (index < samples.Count - 1)
        {
            double outer = samples[index + 1].DistanceTo(center);
            if (outer < radius)
            {
                index += 1;
                continue;
            }

            double inner = samples[index].DistanceTo(center);
            double reach = outer - inner;
            double fraction = reach > Precision.UlpOfOne ? Math.Clamp((radius - inner) / reach, 0, 1) : 1;
            return cumulative[index] + ((cumulative[index + 1] - cumulative[index]) * fraction);
        }

        return Length;
    }

    /// <summary>The sample that tracks a rope node.</summary>
    /// <remarks>
    /// The curve is a quadratic spline through the node <em>midpoints</em>, with each
    /// node as a control point, so a node is not itself a sample: it is nearest the
    /// middle of its own sub-curve. That middle is what this returns. The anchor and the
    /// charm are the two exceptions, being the curve's own endpoints.
    /// </remarks>
    private int SampleIndexForNode(int node)
    {
        if (nodeCount < 2)
        {
            return 0;
        }

        int clamped = Math.Clamp(node, 0, nodeCount - 1);
        if (clamped == 0)
        {
            return 0;
        }

        if (clamped >= nodeCount - 1)
        {
            return samples.Count - 1;
        }

        int index = 1 + ((clamped - 1) * samplesPerSegment) + (samplesPerSegment / 2);
        return Math.Clamp(index, 0, samples.Count - 1);
    }

    /// <summary>The point this far along the cord, clamped to its ends.</summary>
    public Vec2 PointAtArc(double arc)
    {
        if (IsEmpty)
        {
            return Vec2.Zero;
        }

        double target = Math.Clamp(arc, 0, Length);
        int index = SegmentIndex(target);
        double spanStart = cumulative[index];
        double spanLength = cumulative[index + 1] - spanStart;
        if (spanLength <= Precision.UlpOfOne)
        {
            return samples[index];
        }

        double fraction = (target - spanStart) / spanLength;
        return samples[index] + ((samples[index + 1] - samples[index]) * fraction);
    }

    /// <summary>Direction of travel along the cord at this distance, in radians.</summary>
    public double AngleAtArc(double arc)
    {
        if (IsEmpty)
        {
            return Math.PI / 2;
        }

        int index = SegmentIndex(Math.Clamp(arc, 0, Length));
        Vec2 delta = samples[index + 1] - samples[index];
        return delta.MagnitudeSquared > Precision.UlpOfOne ? Math.Atan2(delta.Y, delta.X) : Math.PI / 2;
    }

    /// <summary>Distance along the cord of the point closest to <paramref name="location"/>.</summary>
    /// <param name="location">The point to measure from.</param>
    /// <param name="near">
    /// Only the cord within <paramref name="window"/> of this distance is searched. A
    /// bead never travels far between steps, and a local search cannot jump the bead to
    /// the far side of a fold the way a global one could.
    /// </param>
    /// <param name="window">Half-width of the search, in points.</param>
    public double ArcNearestTo(Vec2 location, double near, double window)
    {
        if (IsEmpty)
        {
            return 0;
        }

        double lower = Math.Clamp(near - window, 0, Length);
        double upper = Math.Clamp(near + window, 0, Length);

        double best = near;
        double bestDistance = double.PositiveInfinity;
        for (int index = 0; index < samples.Count - 1; index++)
        {
            // Skip segments wholly outside the window.
            if (cumulative[index + 1] < lower || cumulative[index] > upper)
            {
                continue;
            }

            (double arc, double distance) = ClosestPointOnSegment(index, location);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = arc;
            }
        }

        return Math.Clamp(best, lower, upper);
    }

    private (double Arc, double Distance) ClosestPointOnSegment(int index, Vec2 location)
    {
        Vec2 start = samples[index];
        Vec2 end = samples[index + 1];
        Vec2 span = end - start;
        double lengthSquared = span.MagnitudeSquared;
        if (lengthSquared <= Precision.UlpOfOne)
        {
            return (cumulative[index], start.DistanceTo(location));
        }

        Vec2 offset = location - start;
        double fraction = Math.Clamp(((offset.X * span.X) + (offset.Y * span.Y)) / lengthSquared, 0, 1);
        Vec2 projected = start + (span * fraction);
        double arc = cumulative[index] + ((cumulative[index + 1] - cumulative[index]) * fraction);
        return (arc, projected.DistanceTo(location));
    }

    /// <summary>Index of the sample segment containing <paramref name="arc"/>, by binary search.</summary>
    private int SegmentIndex(double arc)
    {
        int low = 0;
        int high = cumulative.Count - 1;
        while (low < high - 1)
        {
            int middle = (low + high) / 2;
            if (cumulative[middle] <= arc)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return Math.Min(low, samples.Count - 2);
    }
}
