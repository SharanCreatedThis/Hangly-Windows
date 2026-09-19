//
//  RopeSnapshot.cs
//  Hangly
//
//  What the renderer is handed, and nothing more.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>Where one charm is, and how the cord meets it.</summary>
/// <param name="Center">The charm's centre in canvas coordinates.</param>
/// <param name="Radius">Its drawn radius in points.</param>
/// <param name="Angle">Direction from the knot to the centre, in radians.</param>
/// <param name="KnotInset">Where the cord terminates, as a fraction of the radius.</param>
/// <param name="CordEntry">Distance along the cord where the charm covers it.</param>
/// <param name="CordExit">Distance along the cord where it comes back out below.</param>
public readonly record struct CharmPlacement(
    Vec2 Center,
    double Radius,
    double Angle,
    double KnotInset,
    double CordEntry,
    double CordExit);

/// <summary>Where one bead is, and how it sits on the cord.</summary>
public readonly record struct BeadPlacement(Vec2 Position, double Angle, Size Size, int Owner);

/// <summary>One frame of the rope, as the renderer sees it.</summary>
/// <remarks>
/// Immutable and self-contained: nothing here points back into the solver, so a snapshot
/// can outlive the step that produced it. That is what makes moving the simulation off
/// the UI thread a change of ownership rather than a rewrite.
/// </remarks>
public sealed record RopeSnapshot(
    IReadOnlyList<Vec2> Points,
    IReadOnlyList<CharmPlacement> Charms,
    IReadOnlyList<BeadPlacement> Beads,
    double MaximumStretch,
    bool IsDragging);
