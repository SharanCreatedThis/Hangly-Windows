//
//  RopeBead.cs
//  Hangly
//
//  A bead being simulated on the cord.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>A bead riding the cord.</summary>
/// <remarks>
/// A bead is a Verlet particle exactly like a rope node — it carries its position and
/// its previous position, and gravity and damping act on it the same way — with one
/// extra constraint: it lives on the cord. Each step the particle is integrated freely,
/// then projected back onto the curve, which is what makes it lag behind a whipping
/// rope and catch up afterwards instead of being glued to a fixed point.
///
/// <para>The knot it is threaded against keeps it from sliding away: the projected
/// position is pulled back toward its rest place on the cord and hard-limited to a short
/// travel either side, so a bead slides during motion and never migrates.</para>
/// </remarks>
public struct RopeBead
{
    /// <summary>Current world position.</summary>
    public Vec2 Position;

    /// <summary>Position at the end of the previous step.</summary>
    public Vec2 PreviousPosition;

    /// <summary>Distance along the cord, from the anchor.</summary>
    public double Arc;

    /// <summary>Where the bead rests, measured back from the knot.</summary>
    public double RestOffset;

    /// <summary>Half the bead's extent along the cord, in points.</summary>
    public double SpacingRadius;

    /// <summary>Drawn size in points.</summary>
    public Size Size;

    /// <summary>Mass relative to a plain rope node.</summary>
    public double Mass;

    /// <summary>Which charm on the rope this bead belongs to, counted from the anchor.</summary>
    /// <remarks>
    /// Beads hang above their own charm and are held clear of the charm above them, so
    /// with three charms on one cord the beads fall into three groups that can never
    /// stray into each other's stretch of rope.
    /// </remarks>
    public int Owner;

    /// <summary>Orientation of the cord where the bead sits, in radians.</summary>
    public double Angle;

    /// <summary>Displacement over the last step, which is the bead's implied velocity.</summary>
    public readonly Vec2 Displacement => Position - PreviousPosition;

    /// <summary>
    /// How far the bead may travel from its rest place, in points. Proportional to the
    /// bead, so a large bead slides further than a small one and neither can wander into
    /// its neighbour.
    /// </summary>
    public readonly double SlideLimit => SpacingRadius * 0.6;
}
