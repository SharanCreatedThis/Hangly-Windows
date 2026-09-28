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
    /// Held nearly shut for the first 0.15 s while the web bursts across the top edge — the
    /// web is whole before the drop begins — then out to 1.06 of the rope in 0.8 s on an
    /// ease-out, and back to exactly 1 over 0.25 s: the overshoot and the settle.
    /// </remarks>
    public static double Reel(double time)
    {
        const double Closed = 0.03, Over = 1.06;
        if (time < 0.15)
        {
            return Closed;
        }

        if (time < 0.95)
        {
            return Closed + ((Over - Closed) * EaseOut((time - 0.15) / 0.8));
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
    public static double WebSpread(double radius) => Math.Min(95, Math.Max(40, radius * 1.4));

    /// <summary>How far below the top edge the web's centre sits, as a fraction of its spread.</summary>
    public const double WebDepth = 0.55;

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

/// <summary>The web the entrance shoots onto the top edge of the screen: organic, attached, and permanent.</summary>
/// <remarks>
/// <b>Messy on purpose, the same every time.</b> The pattern — where each strand meets the top
/// edge, how it bows, how thick it is, where rings sit and where they are missing — comes from
/// a small seeded generator (<see cref="WebPattern"/>) with integer arithmetic, so it is uneven
/// like a real web and identical at every launch and on both builds.
///
/// <para><b>Everything is attached.</b> Strands run from a small knot on the rope up to the top
/// edge; branch strands leave a strand part-way and also end on the top edge; ring and cross
/// threads start and end on points computed on the strands' own curves. No thread ends in the
/// air, at any stage of the burst: the strands' top ends spread along the edge as the web grows.
/// The rope is the web's middle strand and hangs from its centre.</para>
/// </remarks>
/// <param name="Anchor">Where the rope hangs from.</param>
/// <param name="Hub">The web's centre, on the rope.</param>
/// <param name="Ceiling">The top edge, in canvas points.</param>
/// <param name="Spread">How far along the top edge the web reaches, fully grown.</param>
/// <param name="Growth">How far it has spread, 0 to 1.</param>
public readonly record struct WebBloom(Vec2 Anchor, Vec2 Hub, double Ceiling, double Spread, double Growth)
{
    /// <summary>One thread of the web: a quadratic curve and its width in points.</summary>
    public readonly record struct Thread(Vec2 Start, Vec2 Control, Vec2 End, double Width)
    {
        /// <summary>The point at <paramref name="t"/> (0 to 1) along the curve.</summary>
        public Vec2 At(double t) => (Start * ((1 - t) * (1 - t))) + (Control * (2 * (1 - t) * t)) + (End * (t * t));
    }

    /// <summary>The strands, from the knot on the rope to the top edge, in the order of <see cref="WebPattern.Strands"/>.</summary>
    public IReadOnlyList<Thread> Strands
    {
        get
        {
            var strands = new Thread[WebPattern.Strands.Count];
            for (int index = 0; index < strands.Length; index++)
            {
                WebPattern.Strand strand = WebPattern.Strands[index];
                Vec2 start = Hub + (new Vec2(strand.KnotX, strand.KnotY) * Growth);
                var end = new Vec2(Anchor.X + (Spread * Growth * strand.Across), Ceiling);
                strands[index] = Curve(start, end, strand.Bow, strand.Width);
            }

            return strands;
        }
    }

    /// <summary>Every thread: strands, then branches, rings and cross threads, each ending on the top edge or on a strand.</summary>
    public IReadOnlyList<Thread> Threads
    {
        get
        {
            IReadOnlyList<Thread> strands = Strands;
            var threads = new List<Thread>(strands);
            foreach (WebPattern.Branch branch in WebPattern.Branches)
            {
                Vec2 start = strands[branch.Strand].At(branch.From);
                var end = new Vec2(Anchor.X + (Spread * Growth * branch.Across), Ceiling);
                threads.Add(Curve(start, end, branch.Bow, branch.Width));
            }

            foreach (WebPattern.Link link in WebPattern.Links)
            {
                Vec2 start = strands[link.From].At(link.FromT);
                Vec2 end = strands[link.To].At(link.ToT);
                Vec2 middle = (start + end) / 2;
                double sag = start.DistanceTo(end) * link.Sag;
                threads.Add(new Thread(start, new Vec2(middle.X, middle.Y + sag), end, link.Width));
            }

            return threads;
        }
    }

    /// <summary>A strand from <paramref name="start"/> to <paramref name="end"/>, bowed sideways by <paramref name="bow"/> of its length.</summary>
    private static Thread Curve(Vec2 start, Vec2 end, double bow, double width)
    {
        Vec2 middle = (start + end) / 2;
        Vec2 along = end - start;
        double length = along.Magnitude;
        Vec2 across = length > 1e-9 ? new Vec2(-along.Y / length, along.X / length) : Vec2.Zero;
        return new Thread(start, middle + (across * (bow * length)), end, width);
    }
}

/// <summary>The web's fixed, uneven pattern. Identical to macOS's <c>WebPattern</c>: the same generator, the same numbers.</summary>
public static class WebPattern
{
    /// <summary>A strand: where it meets the top edge (fraction of the spread either side of the rope), its bow, width, and its start's offset in the knot.</summary>
    public readonly record struct Strand(double Across, double Bow, double Width, double KnotX, double KnotY);

    /// <summary>A branch: leaves <see cref="Strand"/> at <see cref="From"/> of its length and ends on the top edge at <see cref="Across"/>.</summary>
    public readonly record struct Branch(int Strand, double From, double Across, double Bow, double Width);

    /// <summary>A ring or cross thread, from one strand's curve to another's, sagging by <see cref="Sag"/> of its length.</summary>
    public readonly record struct Link(int From, double FromT, int To, double ToT, double Sag, double Width);

    public static IReadOnlyList<Strand> Strands { get; }

    public static IReadOnlyList<Branch> Branches { get; }

    public static IReadOnlyList<Link> Links { get; }

    /// <summary>The seed. Any change to it, or to the order numbers are drawn, is a different web.</summary>
    public const uint Seed = 20_260_928;

    static WebPattern()
    {
        uint state = Seed;
        double Next()
        {
            // A plain linear congruential generator in 31 bits, written the same way on macOS,
            // so both builds draw the same sequence to the last bit.
            state = unchecked((state * 1_103_515_245u) + 12_345u) & 0x7FFF_FFFFu;
            return state / 2_147_483_648.0;
        }

        const int Count = 11;
        var strands = new Strand[Count];
        for (int index = 0; index < Count; index++)
        {
            double across = -1 + (2.0 * index / (Count - 1)) + ((Next() - 0.5) * 0.14);
            if (across > 0)
            {
                across *= 0.9; // one side reaches a little less far: a web is not a mirror
            }

            strands[index] = new Strand(
                Across: across,
                Bow: (Next() - 0.5) * 0.12,
                Width: 0.6 + (Next() * 1.0),
                KnotX: (Next() - 0.5) * 3,
                KnotY: (Next() - 0.5) * 2);
        }

        var branches = new Branch[3];
        for (int index = 0; index < branches.Length; index++)
        {
            int strand = 1 + (int)(Next() * (Count - 2));
            double from = 0.35 + (Next() * 0.3);
            double across = Math.Clamp(strands[strand].Across + ((Next() - 0.5) * 0.5), -1.05, 1.05);
            branches[index] = new Branch(strand, from, across, (Next() - 0.5) * 0.1, 0.5 + (Next() * 0.5));
        }

        var links = new List<Link>();
        for (int ring = 0; ring < 4; ring++)
        {
            double at = 0.22 + (ring * 0.2);
            for (int index = 0; index < Count - 1; index++)
            {
                bool hole = Next() < 0.18;
                double fromT = at + ((Next() - 0.5) * 0.12);
                double toT = at + ((Next() - 0.5) * 0.12);
                double sag = 0.03 + (Next() * 0.12);
                double width = 0.5 + (Next() * 0.5);
                if (!hole)
                {
                    links.Add(new Link(index, fromT, index + 1, toT, sag, width));
                }
            }
        }

        for (int index = 0; index < 4; index++)
        {
            int from = (int)(Next() * (Count - 2));
            links.Add(new Link(from, 0.3 + (Next() * 0.5), from + 2, 0.3 + (Next() * 0.5), 0.02 + (Next() * 0.08), 0.4 + (Next() * 0.4)));
        }

        Strands = strands;
        Branches = branches;
        Links = links;
    }
}
