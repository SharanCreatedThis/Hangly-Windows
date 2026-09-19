//
//  Vec2.cs
//  Hangly
//
//  Minimal 2D vector arithmetic for the rope solver.
//

namespace Hangly.Core.Geometry;

/// <summary>
/// The vector type the simulation is written in, standing in for the original's
/// <c>CGPoint</c>.
/// </summary>
/// <remarks>
/// Verlet integration constantly mixes positions and displacements, so both live in
/// one type and no line of the solver pays for a conversion. These operators are the
/// whole of the maths the rope needs.
///
/// A <c>readonly struct</c> rather than a class: the solver allocates nothing per
/// step, which is what keeps a 240 Hz fixed timestep off the garbage collector.
/// </remarks>
public readonly struct Vec2 : IEquatable<Vec2>
{
    public double X { get; }

    public double Y { get; }

    public Vec2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vec2 Zero => new(0, 0);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vec2 operator *(Vec2 v, double scale) => new(v.X * scale, v.Y * scale);

    public static Vec2 operator /(Vec2 v, double divisor) => new(v.X / divisor, v.Y / divisor);

    /// <summary>Euclidean length.</summary>
    public double Magnitude => Math.Sqrt((X * X) + (Y * Y));

    /// <summary>Length without the square root, for comparisons.</summary>
    public double MagnitudeSquared => (X * X) + (Y * Y);

    /// <summary>Unit vector, or zero for a zero-length vector.</summary>
    public Vec2 Normalized
    {
        get
        {
            double length = Magnitude;
            return length > Precision.UlpOfOne ? this / length : Zero;
        }
    }

    public double DistanceTo(Vec2 other) => (other - this).Magnitude;

    /// <summary>The vector rotated counter-clockwise by <paramref name="radians"/>.</summary>
    public Vec2 Rotated(double radians)
    {
        double cosine = Math.Cos(radians);
        double sine = Math.Sin(radians);
        return new Vec2((X * cosine) - (Y * sine), (X * sine) + (Y * cosine));
    }

    /// <summary>Caps the vector's length at <paramref name="maximum"/>, keeping its direction.</summary>
    public Vec2 Limited(double maximum)
    {
        double length = Magnitude;
        if (length <= maximum || length <= Precision.UlpOfOne)
        {
            return this;
        }

        return this * (maximum / length);
    }

    public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);

    public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);

    public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

    public override string ToString() => $"({X:0.####}, {Y:0.####})";
}
