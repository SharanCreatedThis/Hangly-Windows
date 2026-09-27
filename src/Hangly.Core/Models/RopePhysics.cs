//
//  RopePhysics.cs
//  Hangly
//
//  Standard or Elastic: whether the cord gives when it is pulled.
//

namespace Hangly.Core.Models;

/// <summary>Appearance → Motion → Rope. The same two choices, in the same order and words, as macOS.</summary>
public enum RopePhysics
{
    /// <summary>The cord as it has always been: it barely stretches.</summary>
    Standard,

    /// <summary>The cord gives under a pull, rebounds and settles, like a good bungee.</summary>
    Elastic,
}

/// <summary>The numbers behind Elastic. Identical to macOS's <c>ElasticTable</c>, pinned by the same tests.</summary>
/// <remarks>
/// <b>Rigid at rest, springy under load.</b> Every link is still solved rigidly up to the
/// small give its rope style already has (the style's stretch ceiling). Only a link pulled
/// past that — by a drag past the rope's length, or a hard throw — enters the elastic range,
/// where it is a compliant constraint (XPBD: the stiffness does not depend on the timestep or
/// on how many passes run) up to <see cref="Ceiling"/>. So a hanging rope carries its weight
/// exactly as Standard does, hangs at the same length and sleeps by the same rule, and only
/// a real pull stretches it. The rebound is the spring giving back what the pull put in, and
/// the rope's own damping — four times stronger under reduced motion — takes it away.
/// </remarks>
public static class ElasticTable
{
    /// <summary>The furthest a link can stretch, as a multiple of its length.</summary>
    /// <remarks>
    /// 1.5, for a rubber-band cord (asked for after C6(a)'s 1.2 read as too stiff). Swept at
    /// the same compliance: 1.2 held a strong pull at 1.17 and rebounded to 1.11; 1.5 holds it
    /// at 1.47, rebounds to 1.24 and bounces five to eight times at up to 700 pt/s; 1.6
    /// stretches further but bounces less. The canvas grows with it, in Elastic only.
    /// </remarks>
    public const double Ceiling = 1.5;

    /// <summary>XPBD compliance of a link in its elastic range: the inverse of its stiffness.</summary>
    public const double Compliance = 1e-4;

    public static string TitleOf(RopePhysics physics) => physics == RopePhysics.Elastic ? "Elastic" : "Standard";

    /// <summary>
    /// Extra canvas height, as a fraction of the shipped canvas, that an elastic rope needs to
    /// stretch into: the rope's own share of the height, times how much further it can reach.
    /// </summary>
    public static double CanvasAllowance(double lengthFraction, double ropeLength) =>
        lengthFraction * ropeLength * (Ceiling - 1);
}
