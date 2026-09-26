//
//  RopeBounds.cs
//  Hangly
//
//  The part of the canvas a frame actually paints.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>A conservative rectangle around everything one frame of the rope draws.</summary>
/// <remarks>
/// <b>Why it exists.</b> The overlay's canvas is sized for the whole swing, and at 200% it
/// is 1431×893 pixels; the rope and its charms cover a strip of it. A layered window
/// hands every frame to Windows from system memory, so every frame used to be read back
/// from the GPU in full — 5.9 ms of the 7.9 a frame cost, measured, for pixels that were
/// transparent before and after. Reading back only what changed is most of the saving.
///
/// <para><b>Conservative on purpose.</b> A rectangle too small leaves a trail of the last
/// frame on the desktop; one too large costs a few more bytes. So each part is padded for
/// the widest thing drawn around it: a charm's artwork is a square rotated by the cord,
/// so it reaches √2 of its radius, and its drop shadow and ambient glow reach further
/// still — the glow to <see cref="RopeConfiguration.Layout.CharmHaloExtent"/> radii, the
/// shadow to about 1.8. <see cref="CharmReach"/> covers both with margin.</para>
/// </remarks>
public static class RopeBounds
{
    /// <summary>How far past its centre a charm's pixels can reach, in radii.</summary>
    public const double CharmReach = 2.0;

    /// <summary>Anti-aliasing and rounding, in points.</summary>
    public const double Margin = 4;

    /// <summary>Everything <paramref name="snapshot"/> draws, in canvas points; null if nothing.</summary>
    /// <param name="snapshot">The frame.</param>
    /// <param name="cordWidth">The widest the cord or a bead's outline is drawn, in points.</param>
    public static Rect? Of(RopeSnapshot snapshot, double cordWidth)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        double left = double.PositiveInfinity, top = double.PositiveInfinity;
        double right = double.NegativeInfinity, bottom = double.NegativeInfinity;

        void Include(Vec2 centre, double reach)
        {
            left = Math.Min(left, centre.X - reach);
            top = Math.Min(top, centre.Y - reach);
            right = Math.Max(right, centre.X + reach);
            bottom = Math.Max(bottom, centre.Y + reach);
        }

        double cord = (Math.Max(cordWidth, 0) / 2) + Margin;
        foreach (Vec2 point in snapshot.Points)
        {
            Include(point, cord);
        }

        foreach (CharmPlacement charm in snapshot.Charms)
        {
            Include(charm.Center, (charm.Radius * CharmReach) + Margin);
        }

        foreach (BeadPlacement bead in snapshot.Beads)
        {
            // Rotated with the cord, so its diagonal, plus its own outline.
            double half = Math.Sqrt((bead.Size.Width * bead.Size.Width) + (bead.Size.Height * bead.Size.Height)) / 2;
            Include(bead.Position, half + cord);
        }

        if (double.IsInfinity(left) || double.IsNaN(left) || double.IsNaN(right) || double.IsNaN(top) || double.IsNaN(bottom))
        {
            return null;
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// The furthest anything drawn has moved between two frames, in points: every cord
    /// node, every bead, each charm's centre, and the rim of each charm as it turns.
    /// </summary>
    /// <remarks>
    /// <b>What it is for.</b> A settling rope spends its last stretch moving by less than a
    /// pixel a frame, and every one of those frames used to be drawn and presented. A frame
    /// whose every part is within a quarter of a device pixel of the last one presented is
    /// indistinguishable from it, so it need not be presented at all; the comparison is
    /// always against the last frame actually shown, so the error never accumulates past
    /// that quarter pixel. Infinite when the two frames do not describe the same rope.
    /// </remarks>
    public static double LargestMove(RopeSnapshot shown, RopeSnapshot next)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(next);
        if (shown.Points.Count != next.Points.Count
            || shown.Charms.Count != next.Charms.Count
            || shown.Beads.Count != next.Beads.Count
            || shown.IsDragging != next.IsDragging)
        {
            return double.PositiveInfinity;
        }

        double largest = 0;
        for (int index = 0; index < next.Points.Count; index++)
        {
            largest = Math.Max(largest, (next.Points[index] - shown.Points[index]).Magnitude);
        }

        for (int index = 0; index < next.Beads.Count; index++)
        {
            BeadPlacement a = shown.Beads[index], b = next.Beads[index];
            largest = Math.Max(largest, (b.Position - a.Position).Magnitude);
            largest = Math.Max(largest, Math.Abs(b.Angle - a.Angle) * Math.Max(b.Size.Width, b.Size.Height) / 2);
        }

        for (int index = 0; index < next.Charms.Count; index++)
        {
            CharmPlacement a = shown.Charms[index], b = next.Charms[index];
            if (Math.Abs(a.Radius - b.Radius) > 0)
            {
                return double.PositiveInfinity;
            }

            largest = Math.Max(largest, (b.Center - a.Center).Magnitude);

            // The rim of the artwork, which is a square turned by the cord, moves by the
            // angle times its half-diagonal.
            largest = Math.Max(largest, Math.Abs(b.Angle - a.Angle) * b.Radius * Math.Sqrt(2));
        }

        return largest;
    }

    /// <summary>The smallest rectangle holding both; either may be null.</summary>
    public static Rect? Union(Rect? first, Rect? second)
    {
        if (first is not Rect a)
        {
            return second;
        }

        if (second is not Rect b)
        {
            return a;
        }

        double left = Math.Min(a.Left, b.Left);
        double top = Math.Min(a.Top, b.Top);
        return new Rect(left, top, Math.Max(a.Left + a.Width, b.Left + b.Width) - left, Math.Max(a.Top + a.Height, b.Top + b.Height) - top);
    }
}
