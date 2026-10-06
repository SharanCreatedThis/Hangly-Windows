//
//  CharmArtworkSplitter.cs
//  Hangly
//
//  Separates a charm's body from the beads threaded above it.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

/// <summary>A hook at the top of a charm, in the artwork's unit square. macOS's <c>CharmArtworkRegions.Hook</c>.</summary>
/// <param name="BarTop">The top of the bar: where the cord first meets the hook.</param>
/// <param name="EyeTop">The bottom of the bar, which is the top of the eye.</param>
/// <param name="EyeBottom">The bottom of the eye.</param>
/// <param name="CentreX">The hook's own centre line, across: a ring is not always quite on the charm's.</param>
/// <param name="SlotWidth">The width of a slot cut down through the top of the ring, where a cord was once threaded;
/// zero for a whole ring.</param>
public readonly record struct CharmArtworkHook(double BarTop, double EyeTop, double EyeBottom, double CentreX, double SlotWidth = 0);

/// <summary>A charm's artwork divided into the parts that hang independently.</summary>
/// <remarks>
/// Coordinates are in the artwork's fitted unit square, (0, 0) at the top left.
/// </remarks>
/// <param name="Body">The charm itself, including whatever loop or hook it hangs by.</param>
/// <param name="Beads">The beads above the body, ordered from the top down.</param>
/// <param name="KnotY">
/// Where the cord stops, in the same unit square: on the body's centre line, inside the
/// first solid part the cord reaches coming down it. Null means the top of the body,
/// which is where it was always taken to be.
/// </param>
/// <param name="Hook">
/// The hook, ring or loop at the top of the charm that the cord is tied to (<see cref="CharmHooks"/>), if it has one.
/// </param>
/// <param name="AxisY">
/// Where a cord hanging straight down the body's centre line first meets the artwork, measured on that line alone; null
/// when not measured or it never does. macOS's <c>VectorImage.axisInk</c>.
/// </param>
/// <param name="AxisBottom">Where that first ink on the centre line ends.</param>
public readonly record struct CharmArtworkRegions(
    Rect Body,
    IReadOnlyList<Rect> Beads,
    double? KnotY = null,
    CharmArtworkHook? Hook = null,
    double? AxisY = null,
    double? AxisBottom = null,
    double? AxisLastTop = null,
    double? AxisLastBottom = null,
    double? ExitX = null)
{
    /// <summary>
    /// Where the cord comes out from behind the charm to the charm below on a rope of two or three — tucked a little
    /// into the ink there, never past it — as a fraction of the radius from the centre: X across, Y down. On the centre
    /// line unless <see cref="ExitX"/> moves it (the anchor table's exit, off the line where the line ends in a gap).
    /// Null where nothing was measured. macOS's <c>SVGCharm.ropeLeavesOffset</c>.
    /// </summary>
    public Geometry.Vec2? RopeLeavesOffset
    {
        get
        {
            double longest = Math.Max(Body.Width, Body.Height);
            if (AxisLastTop is not double top || AxisLastBottom is not double bottom || longest <= 0)
            {
                return null;
            }

            double tuck = Math.Min((bottom - top) * 0.5, longest * 0.06);
            double midX = Body.Left + (Body.Width / 2), midY = Body.Top + (Body.Height / 2);
            return new Geometry.Vec2(2 * ((ExitX ?? midX) - midX) / longest, 2 * (bottom - tuck - midY) / longest);
        }
    }

    /// <summary>How deep the first ink on the centre line is, as a fraction of the radius: no further than this can the
    /// cord's end be tucked in behind it and stay hidden. macOS's <c>SVGCharm.ropeMeetsDepth</c>.</summary>
    public double? RopeMeetsDepth
    {
        get
        {
            double longest = Math.Max(Body.Width, Body.Height);
            return AxisY is double top && AxisBottom is double bottom && longest > 0 ? 2 * (bottom - top) / longest : null;
        }
    }

    /// <summary><see cref="AxisY"/> as a fraction of the radius back from the centre, as <see cref="CordInset"/> is.</summary>
    public double? RopeMeetsInset
    {
        get
        {
            double longest = Math.Max(Body.Width, Body.Height);
            return AxisY is double y && longest > 0 ? 2 * (Body.Top + (Body.Height / 2) - y) / longest : null;
        }
    }

    /// <summary>
    /// Where the drawn cord ends, as a fraction of the charm's radius measured back along
    /// the final link: the top of the body is its height over its longest side, and a cord
    /// that meets the artwork lower down is proportionally less — below zero when it meets
    /// it past the centre, as a Snitch's cord reaches the ball between its wings.
    /// </summary>
    public double CordInset
    {
        get
        {
            double longest = Math.Max(Body.Width, Body.Height);
            if (longest <= 0)
            {
                return 1;
            }

            double knot = Math.Clamp(KnotY ?? Body.Top, Body.Top, Body.Top + Body.Height);
            return 2 * (Body.Top + (Body.Height / 2) - knot) / longest;
        }
    }

    /// <summary>The top of the body, as a fraction of the radius back from the centre.</summary>
    public double TopInset
    {
        get
        {
            double longest = Math.Max(Body.Width, Body.Height);
            return longest > 0 ? Body.Height / longest : 1;
        }
    }

    /// <summary>
    /// The least a charm hangs below where its cord takes hold, as a fraction of its radius.
    /// The rope ends at the charm's centre and comes down into it from above, so the knot
    /// the physics hangs it from has to be above the centre.
    /// </summary>
    public const double MinimumKnotInset = 0.12;

    /// <summary>
    /// Where the charm hangs from, for the physics: the cord's end, kept above the centre.
    /// Only a charm whose cord reaches past its middle differs, and its drawn cord carries
    /// on behind the artwork to <see cref="CordInset"/>.
    /// </summary>
    public double KnotInset
    {
        get
        {
            double longest = Math.Max(Body.Width, Body.Height);
            if (longest <= 0)
            {
                return 1;
            }

            return Math.Min(Body.Height / longest, Math.Max(CordInset, MinimumKnotInset));
        }
    }

    /// <summary>Points per unit of this coordinate space, for a charm of this radius.</summary>
    public double ScaleForRadius(double radius)
    {
        double longest = Math.Max(Body.Width, Body.Height);
        return longest > 0 ? radius * 2 / longest : 0;
    }
}

/// <summary>Finds the beads in a piece of charm artwork, by looking at its silhouette.</summary>
/// <remarks>
/// Every charm in the collection is drawn as one tall picture: a cord at the top, a few
/// beads threaded onto it, then the charm. The rope needs the beads and the charm as
/// separate sprites, and the artwork must not be edited to get them — so the split is
/// measured from the rendering instead.
///
/// <para>The measurement is a row profile. A row crossed only by the cord is a few per
/// cent of the artwork's width; a row through a bead or the charm is far wider. Runs of
/// wide rows are therefore the solid parts, separated by cord. Which of those runs are
/// beads, and which one begins the charm, is the one judgement a picture cannot make — a
/// thick cord and a fat bead look alike from here — so the catalogue states both per
/// charm. Runs above the body that are not beads are cord furniture and are dropped,
/// because the simulated thread replaces them.</para>
///
/// <para><b>Rasterisation is not done here.</b> This takes an alpha mask and nothing
/// else, which is what keeps it in the model layer, free of Skia and of Win2D, and
/// testable against a bitmap a test can draw for itself.</para>
/// </remarks>
public static class CharmArtworkSplitter
{
    /// <summary>
    /// Analysis resolution. Large enough to separate a bead from the cord, small enough
    /// that the rasterisation is a few milliseconds.
    /// </summary>
    public const int AnalysisPixels = 320;

    /// <summary>
    /// A row no wider than this fraction of the artwork is cord, not substance. The
    /// cords measure 5–7%; the narrowest bead is near 10%.
    /// </summary>
    public const double CordWidthFraction = 0.085;

    /// <summary>
    /// Rows thinner than this fraction of a run's widest row are trimmed from it, so a
    /// bead's bounds hug the bead instead of the cord entering it.
    /// </summary>
    public const double EdgeWidthFraction = 0.25;

    /// <summary>Alpha at or below this counts as transparent.</summary>
    public const byte AlphaThreshold = 8;

    private readonly record struct RowExtent(int MinX, int MaxX, int Width);

    private record struct Run(int First, int Last, int MinX, int MaxX);

    /// <summary>Splits an alpha mask into the charm's body and the beads above it.</summary>
    /// <param name="alpha">
    /// One byte per pixel, row-major, top row first. Square: the same side is the unit
    /// square's divisor, so a non-square mask would scale the two axes differently.
    /// </param>
    /// <param name="side">The mask's width and height.</param>
    /// <param name="contentWidth">
    /// Width of the artwork's own content within the unit square. The cord threshold is
    /// a fraction of what was actually drawn, not of the square it was fitted into, or a
    /// narrow charm would have its cord measured against empty margin.
    /// </param>
    /// <param name="beadCount">How many runs, from the top, are beads.</param>
    /// <param name="bodyRun">Index of the run where the charm itself begins.</param>
    /// <returns>
    /// The split, or <see langword="null"/> when the artwork does not have the parts the
    /// catalogue expects — which the caller reports rather than papering over.
    /// </returns>
    /// <param name="cordDrawn">
    /// The artwork draws a cord of its own above the charm, which the simulated cord
    /// replaces. Without beads and without this, the charm is everything from its first
    /// ink: the narrow tip of an ear, a spike or a sword is part of it, not cord.
    /// </param>
    public static CharmArtworkRegions? Split(
        ReadOnlySpan<byte> alpha,
        int side,
        double contentWidth,
        int beadCount,
        int bodyRun,
        bool cordDrawn = false,
        bool hook = false)
    {
        if (side <= 0 || alpha.Length < side * side || beadCount < 0 || bodyRun < beadCount)
        {
            return null;
        }

        RowExtent[] rows = RowProfile(alpha, side);
        double cordWidth = CordWidthFraction * contentWidth * side;
        List<Run> runs = SolidRuns(rows, cordWidth);
        if (runs.Count <= bodyRun)
        {
            return null;
        }

        var beads = new List<Rect>(beadCount);
        for (int index = 0; index < beadCount; index++)
        {
            beads.Add(UnitRect(Trim(runs[index], rows), side));
        }

        // The body runs from the top of its own solid part to the last ink in the
        // artwork, so a hook or a tassel that thins out stays part of the charm.
        //
        // A charm drawn without beads or a cord of its own starts at its first ink.
        // Measured from its first wide row instead, as a drawn cord needs, the rows
        // narrower than a cord were dropped — and on a figure those are the tips of its
        // ears, its spikes, its sword, or the very loop it hangs by, cut off flat.
        int firstInk = Array.FindIndex(rows, row => row.Width > 0);
        if (firstInk < 0)
        {
            return null;
        }

        bool ownsItsTop = beadCount == 0 && bodyRun == 0 && !cordDrawn;
        int bodyTop = ownsItsTop ? firstInk : runs[bodyRun].First;
        int bodyBottom = -1;
        for (int row = rows.Length - 1; row >= 0; row--)
        {
            if (rows[row].Width > 0)
            {
                bodyBottom = row;
                break;
            }
        }

        if (bodyBottom < bodyTop)
        {
            return null;
        }

        int minX = int.MaxValue;
        int maxX = -1;
        for (int row = bodyTop; row <= bodyBottom; row++)
        {
            if (rows[row].Width <= 0)
            {
                continue;
            }

            minX = Math.Min(minX, rows[row].MinX);
            maxX = Math.Max(maxX, rows[row].MaxX);
        }

        if (maxX < minX)
        {
            return null;
        }

        Rect body = UnitRect(new Run(bodyTop, bodyBottom, minX, maxX), side);
        var regions = new CharmArtworkRegions(body, beads);
        return Attached(regions, alpha, side, side, new Rect(0, 0, 1, 1), bodyTop, (minX + maxX) / 2, hook);
    }

    /// <summary>
    /// <paramref name="regions"/> with where the cord ends, and the hook it is tied to, measured on a sharper picture of
    /// the charm's top: <paramref name="alpha"/> is <paramref name="window"/> of the unit square, <paramref name="width"/>
    /// × <paramref name="height"/> pixels. A bail's eye is a few pixels across at the split's resolution.
    /// </summary>
    public static CharmArtworkRegions Refine(
        CharmArtworkRegions regions, ReadOnlySpan<byte> alpha, int width, int height, Rect window, bool hook)
    {
        if (width <= 0 || height <= 0 || alpha.Length < width * height || window.Width <= 0)
        {
            return regions;
        }

        double scale = width / window.Width;
        int bodyRow = Math.Clamp((int)Math.Round((regions.Body.Top - window.Top) * scale), 0, height - 1);
        return Attached(regions, alpha, width, height, window, bodyRow, width / 2, hook);
    }

    /// <summary>The window of the unit square <see cref="Refine"/> measures: the body, from a little above its top.</summary>
    public static Rect TopWindow(Rect body)
    {
        double top = Math.Max(0, body.Top - (AttachmentDepthFraction * 2));
        return new Rect(body.Left, top, body.Width, body.Top + body.Height - top);
    }

    /// <summary>The most detail <see cref="TopWindow"/> is measured at, in pixels per unit, and the most pixels across.</summary>
    public const double TopPixelsPerUnit = 1600;

    /// <inheritdoc cref="TopPixelsPerUnit"/>
    public const double TopPixelsAcross = 640;

    private static CharmArtworkRegions Attached(
        CharmArtworkRegions regions, ReadOnlySpan<byte> alpha, int width, int height, Rect window, int fromRow, int centre, bool hook)
    {
        double scale = width / window.Width;
        double UnitY(int row) => window.Top + (row / scale);
        Attachment attachment = Attach(alpha, width, height, fromRow, centre, scale, hook);
        return regions with
        {
            KnotY = UnitY(attachment.Row) + (0.5 / scale),
            Hook = attachment.Hook is HookRows rows
                ? new CharmArtworkHook(
                    UnitY(rows.BarTop), UnitY(rows.EyeTop), UnitY(rows.EyeBottom), window.Left + ((rows.Centre + 0.5) / scale),
                    rows.Slot / scale)
                : null,
        };
    }

    /// <summary>
    /// Half the width of the strip the cord arrives through, as a fraction of the analysis
    /// side: where on the centre line the charm first begins.
    /// </summary>
    public const double KnotReachFraction = 0.04;

    /// <summary>
    /// Half the cord's own width, as a fraction of the analysis side: the strip that has
    /// to be solid for the cord to be hidden in it.
    /// </summary>
    public const double KnotBandFraction = 0.012;

    /// <summary>
    /// How deep a solid part must be before the cord stops in it, as a fraction of the
    /// analysis side. Thinner parts — a ring's wall, a bail, a connector — are what the
    /// cord threads through on its way to the charm.
    /// </summary>
    public const double AttachmentDepthFraction = 0.05;

    /// <summary>How far into that part the cord ends, so its rounded end is under the artwork.</summary>
    public const double TuckFraction = 0.025;

    /// <summary>
    /// Alpha above this is ink you can see. Some artwork carries a faint halo past its
    /// edge, enough to count as ink at <see cref="AlphaThreshold"/>; a cord that began the
    /// charm there stopped short of the metal by the halo's width.
    /// </summary>
    public const byte VisibleAlpha = 64;

    /// <summary>
    /// The largest eye a hook has, across or down, as a fraction of the artwork's square. A larger opening is the
    /// inside of a horseshoe or a frame, not a hook's.
    /// </summary>
    public const double EyeFraction = 0.16;

    /// <summary>The smallest: smaller is a chink in the artwork, not an eye.</summary>
    public const double SmallestEyeFraction = 0.006;

    /// <summary>
    /// How far below the charm's first ink a hook's eye may begin: room for a cord the artwork draws running down into
    /// its ring.
    /// </summary>
    public const double HookReachFraction = 0.2;

    /// <summary>How far apart the columns an eye is looked for down are.</summary>
    public const double EyeColumnFraction = 0.006;

    private readonly record struct HookRows(int BarTop, int EyeTop, int EyeBottom, int Centre, int Slot = 0);

    private readonly record struct Attachment(int Row, HookRows? Hook = null);

    /// <summary>Where the cord ends, and where the hook it is tied to is. macOS's <c>CharmArtworkSplitter.attachment</c>.</summary>
    /// <remarks>
    /// <b>A charm with a hook at its top</b> (<see cref="CharmHooks"/>) — a ring, a bail, a loop — has the cord tied to
    /// it: it ends in the bar above the hook's eye, and is joined to it by an end cap and jump ring (<see cref="HookConnector"/>).
    /// <b>Any other charm</b> has the cord run a little way in behind its top, out of sight. Which charms have a hook is
    /// a list, checked by eye against every one: a picture cannot tell a ring from the gap between a figure's arms
    /// reliably enough, and getting it wrong ties the cord to somebody's elbow.
    ///
    /// <para>It used to thread the cord on through every thin part and opening it met — a ring's eye, a bead cap, a
    /// knot's lattice — to the first part solid enough to stop in, so the cord showed through the hook and through
    /// every opening below it, down to the bell, instead of being tied to anything.</para>
    /// </remarks>
    private static Attachment Attach(
        ReadOnlySpan<byte> alpha, int width, int height, int top, int centre, double scale, bool hook)
    {
        int Pixels(double fraction, int floor) =>
            Math.Max(floor, (int)Math.Round(fraction * scale, MidpointRounding.AwayFromZero));
        (int From, int To) Strip(int middle, double fraction)
        {
            int half = Pixels(fraction, 1);
            return (Math.Max(0, middle - half), Math.Min(width - 1, middle + half));
        }

        int bottom = height - 1;
        (int From, int To) reach = Strip(centre, KnotReachFraction);
        (int From, int To) band = Strip(centre, KnotBandFraction);
        int depth = Pixels(AttachmentDepthFraction, 2);
        int tuck = Pixels(TuckFraction, 1);

        static bool Any(ReadOnlySpan<byte> alpha, int width, int row, (int From, int To) columns, byte above)
        {
            for (int x = columns.From; x <= columns.To; x++)
            {
                if (alpha[(row * width) + x] > above)
                {
                    return true;
                }
            }

            return false;
        }

        int first = -1;
        for (int row = top; row <= bottom; row++)
        {
            if (Any(alpha, width, row, reach, VisibleAlpha))
            {
                first = row;
                break;
            }
        }

        if (first < 0)
        {
            return new Attachment(top);
        }

        if (hook && Eye(alpha, width, height, first, centre, scale, depth) is (int eyeTop, int eyeBottom, int eyeCentre, int halfWidth))
        {
            // The bar is the metal just above the eye, up to a bar's depth: what is above that — a cord the artwork
            // draws running down into the ring — is cord, not hook.
            (int From, int To) hookBand = Strip(eyeCentre, KnotBandFraction);
            int barTop = eyeTop;
            while (barTop > Math.Max(first, eyeTop - depth) && Any(alpha, width, barTop - 1, hookBand, VisibleAlpha))
            {
                barTop--;
            }

            barTop = Math.Min(barTop, eyeTop - 1);

            // A ring with a slot cut through the top of it — the classics, drawn for a cord threaded straight through —
            // has a gap across its bar, down the centre or off to one side. Seen down a slot the bar read as a hairline
            // and its jump ring came out too small to cover the slot, which showed as a gap. The bar is measured beside
            // the slot instead, and the jump ring is centred on the slot and spans it. macOS's attachment.
            int slot = 0, ringCentre = eyeCentre;
            if (Slotted(alpha, width, eyeCentre, halfWidth, eyeBottom, first) is (int besideBar, int besideEye))
            {
                int reachAcross = halfWidth + (halfWidth / 2);
                for (int row = besideBar; row < besideEye; row++)
                {
                    if (ClearRun(alpha, width, row, eyeCentre, reachAcross) is not (int gapCentre, int gapWidth))
                    {
                        continue;
                    }

                    bool open = true;
                    for (int above = first; above <= row && open; above++)
                    {
                        open = alpha[(above * width) + gapCentre] <= AlphaThreshold;
                    }

                    if (!open)
                    {
                        continue;
                    }

                    (barTop, eyeTop, slot, ringCentre) = (besideBar, besideEye, gapWidth, gapCentre);
                    break;
                }
            }

            return new Attachment(barTop + ((eyeTop - barTop) / 2), new HookRows(barTop, eyeTop, eyeBottom + 1, ringCentre, slot));
        }

        // No hook: in behind the top of the charm, a little way into it.
        int tucked = first;
        while (tucked < first + tuck && tucked + 1 <= bottom && Any(alpha, width, tucked + 1, band, VisibleAlpha))
        {
            tucked++;
        }

        return new Attachment(tucked);
    }

    /// <summary>The eye of a hook at the top of the artwork — its top and bottom rows and its centre — if there is one.</summary>
    /// <remarks>
    /// Looked for down a few columns either side of the hook's centre line — a cord the artwork draws through its ring
    /// covers the centre itself — as the first clear gap below the top that ink closes off: below it, and close by on
    /// its left and right. An eye's size, no bigger.
    /// </remarks>
    /// <summary>The bar of a ring either side of a slot: the ring's outer top there, and where its eye begins.</summary>
    private static (int BarTop, int EyeTop)? Slotted(ReadOnlySpan<byte> alpha, int width, int centre, int halfWidth, int eyeBottom, int first)
    {
        int barTop = int.MaxValue, eyeTop = int.MaxValue;
        foreach (int side in new[] { -1, 1 })
        {
            int x = centre + (side * (int)Math.Round(halfWidth * 0.6));
            if (x < 0 || x >= width)
            {
                continue;
            }

            int y = first;
            while (y < eyeBottom && alpha[(y * width) + x] <= VisibleAlpha)
            {
                y++;
            }

            int top = y;
            while (y < eyeBottom && alpha[(y * width) + x] > AlphaThreshold)
            {
                y++;
            }

            if (y > top && y < eyeBottom)
            {
                barTop = Math.Min(barTop, top);
                eyeTop = Math.Min(eyeTop, y);
            }
        }

        return barTop == int.MaxValue || eyeTop <= barTop + 1 ? null : (barTop, eyeTop);
    }

    /// <summary>The clear run across <paramref name="row"/> nearest <paramref name="centre"/>, between ink either side.</summary>
    private static (int Centre, int Width)? ClearRun(ReadOnlySpan<byte> alpha, int width, int row, int centre, int within)
    {
        int low = Math.Max(0, centre - within), high = Math.Min(width - 1, centre + within);
        int firstInk = -1, lastInk = -1;
        for (int x = low; x <= high; x++)
        {
            if (alpha[(row * width) + x] > VisibleAlpha)
            {
                if (firstInk < 0)
                {
                    firstInk = x;
                }

                lastInk = x;
            }
        }

        if (firstInk < 0 || lastInk <= firstInk)
        {
            return null;
        }

        (int Centre, int Width)? best = null;
        int at = firstInk;
        while (at <= lastInk)
        {
            if (alpha[(row * width) + at] > AlphaThreshold)
            {
                at++;
                continue;
            }

            int start = at;
            while (at <= lastInk && alpha[(row * width) + at] <= AlphaThreshold)
            {
                at++;
            }

            int runCentre = (start + at - 1) / 2, runWidth = at - start;
            if (runWidth >= 2 && (best is null || Math.Abs(runCentre - centre) < Math.Abs(best.Value.Centre - centre)))
            {
                best = (runCentre, runWidth);
            }
        }

        return best;
    }

    private static (int Top, int Bottom, int Centre, int HalfWidth)? Eye(
        ReadOnlySpan<byte> alpha, int width, int height, int first, int centre, double scale, int depth)
    {
        int Pixels(double fraction, int floor) =>
            Math.Max(floor, (int)Math.Round(fraction * scale, MidpointRounding.AwayFromZero));
        int largest = Pixels(EyeFraction, 2);
        int smallest = Pixels(SmallestEyeFraction, 2);
        int reach = Pixels(HookReachFraction, 1);
        int step = Pixels(EyeColumnFraction, 1);
        int bottom = Math.Min(height - 1, first + reach + largest);

        // The hook's own centre line: the middle of the ink at the very top. Each run of ink that reaches into the strip
        // round the charm's centre line is followed out to its ends: cut at the strip's edge, a ring off to one side —
        // Karuppu's, on the tip of his sword — was measured by the part of it inside, and its jump ring hung off the
        // ring's side (Sharan, 4 Oct).
        int wide = Pixels(KnotReachFraction * 1.5, 1);
        double sum = 0, count = 0;
        for (int row = first; row <= Math.Min(bottom, first + Math.Max(2, depth / 2)); row++)
        {
            int x = 0;
            while (x < width)
            {
                if (alpha[(row * width) + x] <= VisibleAlpha)
                {
                    x++;
                    continue;
                }

                int end = x;
                while (end + 1 < width && alpha[(row * width) + end + 1] > VisibleAlpha)
                {
                    end++;
                }

                if (end >= centre - wide && x <= centre + wide)
                {
                    sum += (x + end) / 2.0 * (end - x + 1);
                    count += end - x + 1;
                }

                x = end + 1;
            }
        }

        int hookCentre = count > 0 ? (int)Math.Round(sum / count) : centre;

        bool Clear(ReadOnlySpan<byte> alpha, int x, int y) => alpha[(y * width) + x] <= AlphaThreshold;
        bool InkWithin(ReadOnlySpan<byte> alpha, int x, int y, int dx, int distance)
        {
            for (int travelled = 1; travelled <= distance; travelled++)
            {
                int px = x + (dx * travelled);
                if (px >= 0 && px < width && !Clear(alpha, px, y))
                {
                    return true;
                }
            }

            return false;
        }

        var found = new List<(int Top, int Bottom, int X)>();
        foreach (int offset in new[] { 0, -1, 1, -2, 2, -3, 3 })
        {
            int x = hookCentre + (offset * step);
            if (x < 0 || x >= width)
            {
                continue;
            }

            // To this column's own first ink — a ring's top is an arc, lower off the centre line — then down that ink
            // to the first gap.
            int y = first;
            while (y <= Math.Min(bottom, first + largest) && Clear(alpha, x, y))
            {
                y++;
            }

            int inkTop = y;
            while (y <= bottom && !Clear(alpha, x, y))
            {
                y++;
            }

            if (inkTop > Math.Min(bottom, first + largest) || y <= inkTop || y > bottom)
            {
                continue;
            }

            int gapTop = y;
            while (y <= bottom && Clear(alpha, x, y))
            {
                y++;
            }

            int length = y - gapTop;
            if (y > bottom || length < smallest || length > largest)
            {
                continue;
            }

            int middle = gapTop + (length / 2);
            if (InkWithin(alpha, x, middle, -1, largest) && InkWithin(alpha, x, middle, 1, largest))
            {
                found.Add((gapTop, y - 1, x));
            }
        }

        if (found.Count == 0)
        {
            return null;
        }

        (int Top, int Bottom, int X) topmost = found.MinBy(eye => eye.Top);
        if (topmost.Top - first > reach)
        {
            return null;
        }

        var eye = found.Where(other => other.Top <= topmost.Bottom && other.Bottom >= topmost.Top).ToList();

        // Its own centre: the middle of the open span across it, halfway down.
        int half = (topmost.Top + topmost.Bottom) / 2;
        int left = topmost.X, right = topmost.X;
        while (left > 0 && topmost.X - left < largest && Clear(alpha, left - 1, half))
        {
            left--;
        }

        while (right < width - 1 && right - topmost.X < largest && Clear(alpha, right + 1, half))
        {
            right++;
        }

        return (eye.Min(e => e.Top), eye.Max(e => e.Bottom), (left + right) / 2, (right - left) / 2);
    }

    /// <summary>Horizontal ink extent of every row, top down.</summary>
    private static RowExtent[] RowProfile(ReadOnlySpan<byte> alpha, int side)
    {
        var rows = new RowExtent[side];
        for (int y = 0; y < side; y++)
        {
            int first = -1;
            int last = -1;
            int offset = y * side;
            for (int x = 0; x < side; x++)
            {
                if (alpha[offset + x] > AlphaThreshold)
                {
                    if (first < 0)
                    {
                        first = x;
                    }

                    last = x;
                }
            }

            rows[y] = first < 0
                ? new RowExtent(0, 0, 0)
                : new RowExtent(first, last, last - first + 1);
        }

        return rows;
    }

    /// <summary>Runs of consecutive rows wider than the cord.</summary>
    private static List<Run> SolidRuns(RowExtent[] rows, double minimumWidth)
    {
        var runs = new List<Run>();
        Run? current = null;

        for (int index = 0; index < rows.Length; index++)
        {
            RowExtent row = rows[index];
            if (row.Width > minimumWidth)
            {
                if (current is Run run)
                {
                    current = new Run(
                        run.First,
                        index,
                        Math.Min(run.MinX, row.MinX),
                        Math.Max(run.MaxX, row.MaxX));
                }
                else
                {
                    current = new Run(index, index, row.MinX, row.MaxX);
                }
            }
            else if (current is Run finished)
            {
                runs.Add(finished);
                current = null;
            }
        }

        if (current is Run last)
        {
            runs.Add(last);
        }

        return runs;
    }

    /// <summary>
    /// Drops the rows at a run's ends where only the cord remains, and re-measures the
    /// horizontal bounds over what is left.
    /// </summary>
    private static Run Trim(Run run, RowExtent[] rows)
    {
        int widest = 0;
        for (int row = run.First; row <= run.Last; row++)
        {
            widest = Math.Max(widest, rows[row].Width);
        }

        double floor = widest * EdgeWidthFraction;
        int first = run.First;
        int last = run.Last;
        while (first < last && rows[first].Width < floor)
        {
            first++;
        }

        while (last > first && rows[last].Width < floor)
        {
            last--;
        }

        int minX = int.MaxValue;
        int maxX = -1;
        for (int row = first; row <= last; row++)
        {
            if (rows[row].Width <= 0)
            {
                continue;
            }

            minX = Math.Min(minX, rows[row].MinX);
            maxX = Math.Max(maxX, rows[row].MaxX);
        }

        return maxX < minX ? run : new Run(first, last, minX, maxX);
    }

    private static Rect UnitRect(Run run, int side) => new(
        (double)run.MinX / side,
        (double)run.First / side,
        (double)(run.MaxX - run.MinX + 1) / side,
        (double)(run.Last - run.First + 1) / side);
}
