//
//  CharmArtworkSplitter.cs
//  Hangly
//
//  Separates a charm's body from the beads threaded above it.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

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
public readonly record struct CharmArtworkRegions(Rect Body, IReadOnlyList<Rect> Beads, double? KnotY = null)
{
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
        bool cordDrawn = false)
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
        int knot = AttachmentRow(alpha, side, bodyTop, bodyBottom, (minX + maxX) / 2);
        return new CharmArtworkRegions(body, beads, (knot + 0.5) / side);
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
    /// The largest opening the cord passes through: the hole in a hook. A larger one is
    /// the inside of a horseshoe or a frame, and the cord stops above it.
    /// </summary>
    public const double HoleFraction = 0.08;

    /// <summary>
    /// The most open space the cord crosses in all: a ring or two. A dream catcher's web
    /// is many small openings that add up to far more, and the cord stops in its hoop.
    /// </summary>
    public const double OpenFraction = 0.12;

    /// <summary>Alpha at or above this is solid enough to hide the cord behind.</summary>
    public const byte SolidAlpha = 200;

    /// <summary>
    /// Alpha above this is ink you can see. Some artwork carries a faint halo past its
    /// edge, enough to count as ink at <see cref="AlphaThreshold"/>; a cord that began the
    /// charm there stopped short of the metal by the halo's width.
    /// </summary>
    public const byte VisibleAlpha = 64;

    /// <summary>The row where the cord ends: where it attaches to the charm.</summary>
    /// <remarks>
    /// The cord comes straight down the body's centre line. It meets the charm at the first
    /// visible ink — a ring's top, a figure's head — and carries on through anything thin:
    /// a ring's wall and the hole inside it, a bail, a second ring. It ends a little way
    /// into the first part solid enough to be what it is tied to, so it passes through a
    /// hook the way a real one is threaded, and on a charm without one it runs behind the
    /// artwork rather than stopping at its edge; the renderer takes the charm's silhouette
    /// out of the rope, so only what shows through a hook is seen. It stops in the hook
    /// above an opening too big to thread. macOS's <c>CharmArtworkSplitter.attachmentRow</c>.
    /// </remarks>
    private static int AttachmentRow(ReadOnlySpan<byte> alpha, int side, int top, int bottom, int centre)
    {
        (int From, int To) Strip(double fraction)
        {
            int half = Math.Max(1, (int)Math.Round(fraction * AnalysisPixels, MidpointRounding.AwayFromZero));
            return (Math.Max(0, centre - half), Math.Min(side - 1, centre + half));
        }

        static int Count(double fraction, int floor) =>
            Math.Max(floor, (int)Math.Round(fraction * AnalysisPixels, MidpointRounding.AwayFromZero));

        (int From, int To) reach = Strip(KnotReachFraction);
        (int From, int To) band = Strip(KnotBandFraction);
        int depth = Count(AttachmentDepthFraction, 2);
        int tuck = Count(TuckFraction, 1);
        int hole = Count(HoleFraction, 1);
        int open = Count(OpenFraction, 1);

        static bool Any(ReadOnlySpan<byte> alpha, int side, int row, (int From, int To) columns, byte above)
        {
            for (int x = columns.From; x <= columns.To; x++)
            {
                if (alpha[(row * side) + x] > above)
                {
                    return true;
                }
            }

            return false;
        }

        static bool All(ReadOnlySpan<byte> alpha, int side, int row, (int From, int To) columns, byte atLeast)
        {
            for (int x = columns.From; x <= columns.To; x++)
            {
                if (alpha[(row * side) + x] < atLeast)
                {
                    return false;
                }
            }

            return true;
        }

        int first = -1;
        for (int row = top; row <= bottom; row++)
        {
            if (Any(alpha, side, row, reach, VisibleAlpha))
            {
                first = row;
                break;
            }
        }

        if (first < 0)
        {
            return top;
        }

        // Runs of solid rows from where the charm begins; the first deep one is what the
        // cord is tied to. Thin ones are threaded through, and so are the holes between
        // them — as long as they are a hook's, not a frame's.
        int longestStart = -1;
        int longestLength = 0;
        int lastInk = first;
        int gap = 0;
        int crossed = 0;
        int current = first;
        while (current <= bottom)
        {
            if (!Any(alpha, side, current, band, AlphaThreshold))
            {
                gap++;
                crossed++;
                if (gap > hole || crossed > open)
                {
                    break;
                }

                current++;
                continue;
            }

            gap = 0;
            lastInk = current;
            if (!All(alpha, side, current, band, SolidAlpha))
            {
                current++;
                continue;
            }

            int start = current;
            while (current <= bottom && All(alpha, side, current, band, SolidAlpha))
            {
                current++;
            }

            int length = current - start;
            if (length >= depth)
            {
                return start + tuck;
            }

            if (length > longestLength)
            {
                longestStart = start;
                longestLength = length;
            }

            lastInk = current - 1;
        }

        // Nothing deep enough before an opening too big to thread: the most solid part
        // passed on the way — the hook itself — or the last ink if none.
        return longestStart < 0 ? lastInk : longestStart + (longestLength / 2);
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
