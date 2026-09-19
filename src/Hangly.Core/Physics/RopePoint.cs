//
//  RopePoint.cs
//  Hangly
//
//  A single Verlet particle.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>One node of the rope.</summary>
/// <remarks>
/// Verlet integration stores no explicit velocity. A node's velocity is implied by the
/// gap between where it is and where it was, which is why momentum survives a drag
/// release for free: releasing simply stops writing the position, and the gap the drag
/// left behind becomes the node's velocity.
///
/// <para>A mutable struct, held by the solver in an array rather than a
/// <c>List&lt;T&gt;</c>. That is not an incidental choice: indexing an array of structs
/// yields a reference to the element, so <c>points[i].Position += …</c> writes through
/// to storage, where the same line against a <c>List&lt;T&gt;</c> would not compile —
/// and the idiom is used on nearly every line of the constraint passes.</para>
/// </remarks>
public struct RopePoint : IEquatable<RopePoint>
{
    /// <summary>Current position in canvas coordinates.</summary>
    public Vec2 Position;

    /// <summary>Position at the end of the previous step.</summary>
    public Vec2 PreviousPosition;

    /// <summary>
    /// Reciprocal of mass. Zero pins the node in place: constraint corrections scaled
    /// by zero move it not at all, which is how the anchor stays put without a special
    /// case in the solver.
    /// </summary>
    public double InverseMass;

    public RopePoint(Vec2 position, double inverseMass = 1)
    {
        Position = position;
        PreviousPosition = position;
        InverseMass = inverseMass;
    }

    /// <summary>Displacement over the last step. Divide by the step duration for a rate.</summary>
    public readonly Vec2 Displacement => Position - PreviousPosition;

    public readonly bool IsPinned => InverseMass == 0;

    /// <summary>Sets the implied velocity, in points per second, for a given step length.</summary>
    public void SetVelocity(Vec2 velocity, double timeStep) =>
        PreviousPosition = Position - (velocity * timeStep);

    /// <summary>
    /// Builds a straight chain of nodes hanging from <paramref name="anchor"/> at
    /// <paramref name="angle"/> from vertical: the anchor pinned, the charm weighted,
    /// everything between free.
    /// </summary>
    public static RopePoint[] Chain(
        RopeConfiguration configuration,
        Vec2 anchor,
        CharmMetrics charmMetrics,
        double angle)
    {
        Vec2 direction = new Vec2(0, 1).Rotated(angle);
        int lastIndex = configuration.PointCount - 1;
        var chain = new RopePoint[configuration.PointCount];

        for (int index = 0; index <= lastIndex; index++)
        {
            double inverseMass = index switch
            {
                0 => 0,
                _ when index == lastIndex => 1 / Math.Max(charmMetrics.Mass, 0.0001),
                _ => 1,
            };

            Vec2 offset = direction * (index * configuration.SegmentLength);
            chain[index] = new RopePoint(anchor + offset, inverseMass);
        }

        return chain;
    }

    public readonly bool Equals(RopePoint other) =>
        Position == other.Position
        && PreviousPosition == other.PreviousPosition
        && InverseMass.Equals(other.InverseMass);

    public override readonly bool Equals(object? obj) => obj is RopePoint other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Position, PreviousPosition, InverseMass);

    public static bool operator ==(RopePoint a, RopePoint b) => a.Equals(b);

    public static bool operator !=(RopePoint a, RopePoint b) => !a.Equals(b);
}
