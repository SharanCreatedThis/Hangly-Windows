//
//  HookConnector.cs
//  Hangly
//
//  The shape of the metal that joins the rope to a charm's hook.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

/// <summary>
/// Where the jump ring — and, on a heavy charm, the end cap above it — that join the rope to a charm's hook
/// (<see cref="CharmHooks"/>) go, in the charm's artwork's unit square: worked out from the hook alone, so it scales with
/// the charm, and so the beads above a charm can make room for it. macOS's <c>HookConnector</c>.
/// </summary>
/// <remarks>
/// <b>The charm is the hero.</b> The hardware is fine jewellery findings, there to be discovered on a closer look, not a
/// keychain's, and kept smaller than what it holds (Sharan's rules, 4 Oct): the jump ring always smaller than the charm's
/// own ring and the cap smaller than the jump ring; the whole of it never taller than the charm's ring; a charm with a
/// clean loop hangs by a jump ring alone, the rope tied on through it, and only a large, heavy charm has a cap; and its
/// size is set by the charm's weight — small 35% of the full-size hardware, medium 50%, large 65%.
/// </remarks>
public readonly record struct HookConnector
{
    /// <summary>How heavy a charm reads: what its hardware has to look able to carry.</summary>
    public enum Weight
    {
        Small,
        Medium,
        Large,
    }

    /// <summary>From the charm's catalogue mass: a feather-light dreamcatcher is small, a bell or a sneaker large.</summary>
    public static Weight WeightOf(double mass) => mass < 3 ? Weight.Small : mass >= 3.85 ? Weight.Large : Weight.Medium;

    /// <summary>The hardware's size against the full-size findings.</summary>
    public static double ScaleOf(Weight weight) => weight switch
    {
        Weight.Small => 0.35,
        Weight.Large => 0.65,
        _ => 0.5,
    };

    /// <summary>The end cap's picture (Assets/Connectors), measured: width over height, its eyelet's opening top and
    /// bottom, and how far into it the rope ends.</summary>
    public static class Cap
    {
        public const double Aspect = 0.474;
        public const double EyeletTop = 0.772;
        public const double EyeletBottom = 0.905;
        public const double RopeEnd = 0.2;
    }

    /// <summary>The jump ring's picture, measured: width over height, and its opening top to bottom and side to side.</summary>
    public static class Ring
    {
        public const double Aspect = 0.6;
        public const double Top = 0.008;
        public const double OpeningTop = 0.187;
        public const double OpeningBottom = 0.82;
        public const double OpeningLeft = 0.317;
        public const double OpeningRight = 0.69;
    }

    /// <summary>The full-size jump ring's width against the charm's longest side, before the weight's scale.</summary>
    public const double SmallestFullRing = 0.075;

    /// <inheritdoc cref="SmallestFullRing"/>
    public const double LargestFullRing = 0.15;

    /// <summary>The jump ring's height at most, against the charm ring's own height: always the smaller ring.</summary>
    public const double RingToCharmRing = 0.8;

    /// <summary>How much wider than the slot in a slotted ring its jump ring is.</summary>
    public const double SlotSpan = 1.6;

    /// <summary>How far past a wide gap's edge, onto the arm, the jump ring hooked over it sits, against its own width.</summary>
    public const double ArmOverlap = 0.15;

    /// <summary>The cap's height against the jump ring's: always the smaller piece.</summary>
    public const double CapToRing = 0.8;

    /// <summary>The smallest cap worth drawing, against the jump ring: where there is less room, the rope is tied on
    /// through the jump ring instead.</summary>
    public const double SmallestCap = 0.3;

    /// <summary>The gap a bead keeps above the hardware, against the charm's longest side.</summary>
    public const double BeadGap = 0.08;

    public HookConnector(CharmArtworkHook hook, Rect body, Weight weight)
    {
        double longest = Math.Max(body.Width, body.Height);
        double bar = Math.Max(0, hook.EyeTop - hook.BarTop);

        // The charm's ring, from the top of its bar to the bottom of its lower wire.
        double charmRing = (hook.EyeBottom - hook.BarTop) + bar;
        double full = Math.Min(
            Math.Max(bar * 1.15 / (Ring.OpeningRight - Ring.OpeningLeft), longest * SmallestFullRing),
            longest * LargestFullRing) / Ring.Aspect;

        // Tall enough to show above the bar it goes round; never as big as the charm's own ring. Across a slotted ring it
        // spans the slot, so it catches the bar either side of it and hangs on the ring rather than in the gap.
        // A gap too wide for that — a ring open like a C — is hooked over the arm beside it instead, as a jump ring on a
        // C-shaped ring hangs from one of its arms; spanning it would take a ring bigger than the charm's own.
        double spanning = hook.SlotWidth * SlotSpan / Ring.Aspect;
        double fitted = Math.Min(Math.Max(full * ScaleOf(weight), bar * 2.2), charmRing * RingToCharmRing);
        double largest = Math.Min(spanning, charmRing);

        // Spanned while the largest ring allowed still lies across the gap.
        bool spans = largest * Ring.Aspect >= hook.SlotWidth * 1.05;
        double height = spans ? Math.Max(fitted, largest) : fitted;
        double width = height * Ring.Aspect;
        double across = spans ? hook.CentreX : hook.CentreX + (hook.SlotWidth / 2) + (width * ArmOverlap);
        RingRect = new Rect(across - (width / 2), hook.EyeTop - (Ring.OpeningBottom * height), width, height);
        Eye = new Vec2(hook.CentreX, hook.EyeTop);

        double top = RingRect.Top;
        CapRect = null;
        if (weight == Weight.Large)
        {
            // Never taller, all of it, than the charm's ring: the cap gives way first.
            double openingTop = RingRect.Top + (Ring.OpeningTop * height);
            double room = charmRing - (RingRect.Top + RingRect.Height - openingTop);
            double capHeight = Math.Min(height * CapToRing, Math.Max(0, room) / Cap.EyeletBottom);
            if (capHeight >= height * SmallestCap)
            {
                double capWidth = capHeight * Cap.Aspect;
                var cap = new Rect(RingRect.MidX - (capWidth / 2), openingTop - (Cap.EyeletBottom * capHeight), capWidth, capHeight);
                CapRect = cap;
                top = cap.Top;
            }
        }

        Top = top;
        Rise = (hook.BarTop - top) + (longest * BeadGap);
    }

    /// <summary>The jump ring's picture, in the unit square.</summary>
    public Rect RingRect { get; }

    /// <summary>
    /// The top of the charm's eye — the underside of its bar — at the eye's middle: where a chain's last link rests,
    /// hooked through the ring with no jump ring between.
    /// </summary>
    public Vec2 Eye { get; }

    /// <summary>Where the rope ends: <see cref="Eye"/> on a chain, <see cref="RopeEnd"/> on a cord.</summary>
    public Vec2 EndFor(bool chained) => chained ? Eye : RopeEnd;

    /// <summary>The end cap's picture, on a heavy charm.</summary>
    public Rect? CapRect { get; }

    /// <summary>The top of the hardware.</summary>
    public double Top { get; }

    /// <summary>How far the hardware reaches above the top of the hook's bar, with the gap a bead above keeps.</summary>
    public double Rise { get; }

    /// <summary>Where the rope ends: inside the cap, or behind the jump ring's top wire — tied on through it.</summary>
    public Vec2 RopeEnd => CapRect is Rect cap
        ? new Vec2(cap.MidX, cap.Top + (cap.Height * Cap.RopeEnd))
        : new Vec2(RingRect.MidX, RingRect.Top + (RingRect.Height * (Ring.Top + Ring.OpeningTop) / 2));

    /// <summary>
    /// How much rope eases from the cord's line into the hardware: several rope widths and more than a charm's radius,
    /// so a ring off the cord's line is reached by a long, gentle curve and never a single bend. macOS's
    /// <c>HookConnectorRenderer.blendLength</c>.
    /// </summary>
    public static double BlendLength(double radius, double cordWidth) => Math.Max(cordWidth * 10, radius * 1.4);

    /// <summary>
    /// The rope from where it leaves the cord to the hardware, after <paramref name="start"/>, in twenty even steps: a
    /// curve that leaves along the cord's own direction and arrives along the charm's hanging axis, so the rope bends
    /// smoothly over its whole length and the hardware hangs beneath its last direction. macOS's
    /// <c>HookConnectorRenderer.blend</c>.
    /// </summary>
    /// <remarks>The cord meets a charm on its centre line and many rings are off it — a sneaker's at its heel, a camera's
    /// at a corner. The rope used to run down the cord and make one straight jump to the ring, which drew as a hinge.</remarks>
    public static IReadOnlyList<Vec2> Blend(Vec2 start, Vec2 leaving, Vec2 end, Vec2 arriving)
    {
        double distance = (end - start).Magnitude;
        if (distance <= 0.5 || leaving.Magnitude <= 1e-9 || arriving.Magnitude <= 1e-9)
        {
            return [end];
        }

        double handle = distance * 0.42;
        Vec2 first = start + (leaving * (handle / leaving.Magnitude));
        Vec2 second = end - (arriving * (handle / arriving.Magnitude));
        var points = new List<Vec2>(20);
        for (int step = 1; step <= 20; step++)
        {
            double t = step / 20.0, u = 1 - t;
            points.Add((start * (u * u * u)) + (first * (3 * u * u * t)) + (second * (3 * u * t * t)) + (end * (t * t * t)));
        }

        return points;
    }
}
