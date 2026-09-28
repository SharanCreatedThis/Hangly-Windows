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
/// for that one launch. The web it leaves stays, attached to the top edge, for as long as a
/// Spider-Man charm is on the rope.
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

    /// <summary>How far the web reaches along the top edge either side of the rope, in points, for a charm of <paramref name="radius"/>.</summary>
    public static double WebSpread(double radius) => Math.Min(80, Math.Max(36, radius * 1.2));

    /// <summary>How far below the top edge the web's centre sits, as a fraction of its spread.</summary>
    public const double WebDepth = 0.6;

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

/// <summary>The web the entrance spins: a funnel from the top edge of the screen down to the rope.</summary>
/// <remarks>
/// <b>Everything is attached.</b> Every strand runs from the web's centre, on the rope, up to a
/// point on the top edge; every ring runs from one strand to the next. No thread ends in the
/// air, at any stage of the bloom: the strands' outer ends start on the top edge beside the
/// rope and spread along it as the web grows, and the centre moves down the rope. The rope is
/// the web's middle strand. Drawn from fixed numbers — the same on both builds, at every launch.
/// </remarks>
/// <param name="Anchor">Where the rope hangs from.</param>
/// <param name="Hub">The web's centre, on the rope.</param>
/// <param name="Ceiling">The top edge, in canvas points.</param>
/// <param name="Spread">How far along the top edge the web reaches either side, fully grown.</param>
/// <param name="Growth">How far it has spread, 0 to 1.</param>
public readonly record struct WebBloom(Vec2 Anchor, Vec2 Hub, double Ceiling, double Spread, double Growth)
{
    /// <summary>Where each strand meets the top edge, as a fraction of <see cref="Spread"/> either side of the rope.</summary>
    public static readonly double[] StrandEnds = [-1.0, -0.7, -0.42, -0.16, 0.16, 0.42, 0.7, 1.0];

    /// <summary>The rings' places along each strand, from the centre out.</summary>
    public static readonly double[] Rings = [0.3, 0.55, 0.8];

    /// <summary>A small, fixed unevenness per strand, so the rings are spun rather than printed.</summary>
    public static readonly double[] Wobble = [0.02, -0.03, 0.01, -0.02, 0.03, -0.01, 0.02, -0.02];

    /// <summary>How far each ring thread sags between two strands, as a fraction of its length.</summary>
    public const double Sag = 0.08;

    /// <summary>One stretch of a ring between two strands: a quadratic curve.</summary>
    public readonly record struct RingSegment(Vec2 Start, Vec2 Control, Vec2 End);

    /// <summary>Where each strand meets the top edge; each runs from <see cref="Hub"/>.</summary>
    public IReadOnlyList<Vec2> SpokeEnds
    {
        get
        {
            var ends = new Vec2[StrandEnds.Length];
            for (int index = 0; index < ends.Length; index++)
            {
                ends[index] = new Vec2(Anchor.X + (Spread * Growth * StrandEnds[index]), Ceiling);
            }

            return ends;
        }
    }

    /// <summary>The ring threads, each from one strand to the next, sagging a little.</summary>
    public IReadOnlyList<RingSegment> RingSegments
    {
        get
        {
            IReadOnlyList<Vec2> ends = SpokeEnds;
            var segments = new List<RingSegment>();
            foreach (double ring in Rings)
            {
                for (int index = 0; index < ends.Count - 1; index++)
                {
                    Vec2 start = Along(ends[index], ring * (1 + Wobble[index]));
                    Vec2 end = Along(ends[index + 1], ring * (1 + Wobble[index + 1]));
                    Vec2 middle = (start + end) / 2;
                    double sag = start.DistanceTo(end) * Sag;
                    segments.Add(new RingSegment(start, new Vec2(middle.X, middle.Y + sag), end));
                }
            }

            return segments;
        }
    }

    /// <summary>The point <paramref name="fraction"/> of the way from the centre to <paramref name="end"/>.</summary>
    public Vec2 Along(Vec2 end, double fraction) => Hub + ((end - Hub) * fraction);
}
