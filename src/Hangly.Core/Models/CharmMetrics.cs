//
//  CharmMetrics.cs
//  Hangly
//
//  What a charm weighs and how large it is, as the solver sees it.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

/// <summary>The physical half of a charm: what the rope has to carry.</summary>
/// <param name="Mass">
/// Mass relative to a plain rope node. Heavier charms swing with more authority and
/// pull the rope straighter.
/// </param>
/// <param name="RadiusRatio">Bounding radius as a fraction of the charm ruler.</param>
/// <param name="KnotInset">
/// Where the cord terminates, as a fraction of the bounding radius measured back along
/// the final link. One puts the knot on the bounding circle.
/// </param>
public readonly record struct CharmMetrics(double Mass, double RadiusRatio, double KnotInset)
{
    /// <summary>The shipped default, matching the plain bead.</summary>
    public static CharmMetrics Default { get; } = new(2.6, 0.126, 0.90);

    /// <summary>Linear blend, used to make a charm change resize smoothly instead of popping.</summary>
    /// <summary>This charm at a different size.</summary>
    /// <remarks>
    /// Radius and mass both move, and they move together: a charm drawn twice the size
    /// that swung with the same authority as before would read as a picture of a charm
    /// rather than an object on a string.
    ///
    /// <para><b>Linear rather than cubed</b>, which is not what physics would say and is
    /// deliberate — the rope is tuned for how heavy a charm <em>looks</em>, and a cubed
    /// mass makes a large charm behave like a wrecking ball long before it looks like
    /// one. Transcribed from the macOS <c>CharmMetrics.scaled(by:)</c>.</para>
    ///
    /// <para><see cref="KnotInset"/> is a proportion of the radius and so is already
    /// correct at any size; scaling it too would move the cord off the artwork.</para>
    /// </remarks>
    public CharmMetrics Scaled(double size) => size.Equals(1)
        ? this
        : new CharmMetrics(Mass * size, RadiusRatio * size, KnotInset);

    public static CharmMetrics Interpolate(CharmMetrics start, CharmMetrics end, double progress)
    {
        double clamped = Math.Clamp(progress, 0, 1);
        return new CharmMetrics(
            start.Mass + ((end.Mass - start.Mass) * clamped),
            start.RadiusRatio + ((end.RadiusRatio - start.RadiusRatio) * clamped),
            start.KnotInset + ((end.KnotInset - start.KnotInset) * clamped));
    }
}

/// <summary>A bead as the charm describes it, in proportions rather than points.</summary>
/// <remarks>
/// Everything is a fraction of the charm's radius, so a bead keeps its place and its
/// size when the overlay is rescaled or the charm changes: the simulation converts to
/// points each time it is given a radius.
/// </remarks>
/// <param name="Size">Size relative to the charm's radius.</param>
/// <param name="Offset">
/// Distance from the knot up along the cord, relative to the charm's radius.
/// </param>
/// <param name="Mass">Mass relative to a plain rope node.</param>
public readonly record struct CharmBead(Size Size, double Offset, double Mass)
{
    /// <summary>
    /// Extent along the cord, which is what decides whether two beads collide.
    /// </summary>
    public double SpacingRatio => Size.Height / 2;

    /// <summary>
    /// How far a charm's beads reach past its knot, in multiples of its radius.
    /// </summary>
    public static double ReachOf(IReadOnlyList<CharmBead> beads)
    {
        double reach = 0;
        foreach (CharmBead bead in beads)
        {
            reach = Math.Max(reach, bead.Offset + bead.SpacingRatio);
        }

        return reach;
    }
}

/// <summary>Limits on how many charms one cord carries.</summary>
public static class CharmStack
{
    /// <summary>One rope, at most three charms.</summary>
    public const int MaximumCount = 3;
}
