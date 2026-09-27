//
//  StartupIntro.cs
//  Hangly
//
//  The Spider-Man entrance: a web blooms at the anchor and the charm drops on its strand.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

/// <summary>When the entrance plays, and how it moves. Identical numbers to macOS's <c>IntroTable</c>, pinned by the same tests.</summary>
/// <remarks>
/// <b>Not an animation layered over the rope.</b> The rope <em>is</em> the animation: its
/// links start almost too short to see and are reeled out to their length, so the charm
/// drops on its own cord under the solver's own gravity and damping, overshoots a little
/// as the reel runs past full, and settles as it comes back. Physics is in charge from the
/// first frame, so at the end there is nothing to hand over. It replaces the launch swing
/// for that one launch, and leaves nothing behind.
/// </remarks>
public static class IntroTable
{
    /// <summary>From the first frame to the last trace of the web, in seconds.</summary>
    public const double Duration = 1.45;

    /// <summary>Reel length as a fraction of the rope, over time.</summary>
    /// <remarks>
    /// Held nearly shut for the first 0.1 s while the web blooms, then out to 1.06 of the
    /// rope in 0.85 s on an ease-out, and back to exactly 1 over 0.25 s — the overshoot and
    /// the settle.
    /// </remarks>
    public static double Reel(double time)
    {
        const double Closed = 0.03, Over = 1.06;
        if (time < 0.1)
        {
            return Closed;
        }

        if (time < 0.95)
        {
            return Closed + ((Over - Closed) * EaseOut((time - 0.1) / 0.85));
        }

        if (time < 1.2)
        {
            return Over + ((1 - Over) * EaseInOut((time - 0.95) / 0.25));
        }

        return 1;
    }

    /// <summary>How far the web has spread, 0 to 1: out in the first 0.15 s.</summary>
    public static double BloomGrowth(double time) => EaseOut(Math.Clamp(time / 0.15, 0, 1));

    /// <summary>How visible the web is: whole until the charm has landed, then gone by the end.</summary>
    public static double BloomOpacity(double time) => time < 1.0 ? 1 : Math.Max(0, 1 - ((time - 1.0) / (Duration - 1.0)));

    /// <summary>The web's reach from the anchor, in points, for a charm of <paramref name="radius"/>.</summary>
    public static double BloomSize(double radius) => Math.Min(70, Math.Max(28, radius * 0.9));

    /// <summary>Whether the entrance plays at this launch: setting on, a Spider-Man charm anywhere on the rope, motion not reduced.</summary>
    public static bool Plays(bool enabled, IEnumerable<string> charmIds, bool reducesMotion) =>
        enabled && !reducesMotion && charmIds.Any(IsSpiderMan);

    /// <summary>The two charms the entrance belongs to.</summary>
    public static bool IsSpiderMan(string charmId) => charmId is "spiderMan" or "spiderManSwinging";

    private static double EaseOut(double progress)
    {
        double remaining = 1 - progress;
        return 1 - (remaining * remaining * remaining);
    }

    private static double EaseInOut(double progress) =>
        progress < 0.5 ? 2 * progress * progress : 1 - (Math.Pow((-2 * progress) + 2, 2) / 2);
}

/// <summary>The web at the anchor while the entrance plays, drawn from fixed numbers — the same on both builds, at every launch.</summary>
public readonly record struct WebBloom(Vec2 Anchor, double Size, double Growth, double Opacity)
{
    /// <summary>Spoke directions in degrees from +x, y down: a fan hanging from the anchor. Uneven on purpose.</summary>
    public static readonly double[] SpokeAngles = [8, 30, 52, 76, 98, 121, 146, 170];

    /// <summary>Each spoke's length as a fraction of <see cref="Size"/>. The short ones are the torn edges.</summary>
    public static readonly double[] SpokeLengths = [0.62, 0.95, 0.8, 1.0, 0.88, 0.97, 0.7, 0.5];

    /// <summary>The rings' distances from the anchor as fractions of <see cref="Size"/>; a ring spans two spokes only when both reach it.</summary>
    public static readonly double[] Rings = [0.34, 0.6, 0.84];

    /// <summary>How far each ring segment sags towards the anchor between spokes, as a fraction of the ring's radius.</summary>
    public const double Sag = 0.1;

    /// <summary>One stretch of a ring between two spokes: a quadratic curve.</summary>
    public readonly record struct RingSegment(Vec2 Start, Vec2 Control, Vec2 End);

    /// <summary>The spokes' outer ends at the current growth; each runs from <see cref="Anchor"/>.</summary>
    public IReadOnlyList<Vec2> SpokeEnds
    {
        get
        {
            var ends = new Vec2[SpokeAngles.Length];
            for (int index = 0; index < ends.Length; index++)
            {
                ends[index] = Point(SpokeAngles[index], Size * SpokeLengths[index] * Growth);
            }

            return ends;
        }
    }

    /// <summary>The ring segments, sagging towards the anchor between spokes.</summary>
    public IReadOnlyList<RingSegment> RingSegments
    {
        get
        {
            var segments = new List<RingSegment>();
            foreach (double ring in Rings)
            {
                double radius = Size * ring * Growth;
                for (int index = 0; index < SpokeAngles.Length - 1; index++)
                {
                    if (SpokeLengths[index] < ring || SpokeLengths[index + 1] < ring)
                    {
                        continue;
                    }

                    double middle = (SpokeAngles[index] + SpokeAngles[index + 1]) / 2;
                    segments.Add(new RingSegment(
                        Point(SpokeAngles[index], radius),
                        Point(middle, radius * (1 - Sag)),
                        Point(SpokeAngles[index + 1], radius)));
                }
            }

            return segments;
        }
    }

    private Vec2 Point(double degrees, double distance)
    {
        double radians = degrees * Math.PI / 180;
        return new Vec2(Anchor.X + (Math.Cos(radians) * distance), Anchor.Y + (Math.Sin(radians) * distance));
    }
}
