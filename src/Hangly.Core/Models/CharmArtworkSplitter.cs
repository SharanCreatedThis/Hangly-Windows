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
public readonly record struct CharmArtworkRegions(Rect Body, IReadOnlyList<Rect> Beads)
{
    /// <summary>
    /// Where the cord meets the body, as a fraction of the charm's radius measured back
    /// along the final link. The body is fitted into a square of side twice the radius,
    /// so this is the body's height over its longest side.
    /// </summary>
    public double KnotInset
    {
        get
        {
            double longest = Math.Max(Body.Width, Body.Height);
            return longest > 0 ? Body.Height / longest : 1;
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
    public static CharmArtworkRegions? Split(
        ReadOnlySpan<byte> alpha,
        int side,
        double contentWidth,
        int beadCount,
        int bodyRun)
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
        int bodyTop = runs[bodyRun].First;
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
        return new CharmArtworkRegions(body, beads);
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
