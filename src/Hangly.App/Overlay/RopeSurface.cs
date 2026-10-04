//
//  RopeSurface.cs
//  Hangly
//
//  A rope's photographed surface, laid along the cord.
//

using Hangly.App.Services;
using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using SkiaSharp;

namespace Hangly.App.Overlay;

/// <summary>
/// Draws a rope style's picture (Assets/Ropes) along the cord: a real twist, braid or chain where the cord used to be a
/// lit rod with strokes across it, which read as a flat 2D line (Sharan, 4 Oct). macOS's <c>RopeSurface</c>.
/// </summary>
/// <remarks>
/// <b>One seamless tile per style</b>, cut from a photographic render of a straight length of that rope, shrunk to 64
/// pixels across and made to repeat with no seam. It is stacked into one long strip, and laid along the cord's own
/// curve in as few pieces as its shape allows: wherever the cord runs straight — the whole of it, at rest — one piece,
/// so there is no join to see; where it bends, a corner wherever it turns by more than shows, each piece turned to it.
/// Measured from the cord's top, so the pattern holds still on the cord as it moves.
///
/// <para><b>Never stretched, never shifted.</b> Each piece is the strip's rows for exactly its stretch of cord, at the
/// cord's scale, and begins half a point early on the rows that really come before it, so the piece above meets it with
/// no gap. The first version stretched each short band by that half point instead, and every join showed: a chain's
/// link stepped sideways and repeated a sliver of itself (Sharan, 4 Oct).</para>
///
/// <para>Every style has one, neon included — a twisted tube lit from inside, its halo still drawn round it. A style
/// without a picture, or whose picture is missing, is drawn as it was: a lit rod with its texture stroked along it.</para>
/// </remarks>
internal static class RopeSurface
{
    /// <summary>A style's tile stacked into a strip; the tile's height and the rope's own width across it, in pixels.</summary>
    private sealed record Tile(CanvasBitmap Strip, double Height, double RopeWidth, EndLink? Link);

    /// <summary>
    /// Where a chain's links are in its tile, measured once: how the chain hangs a charm by its own last link. macOS's
    /// <c>RopeSurface.EndLink</c>.
    /// </summary>
    /// <remarks>
    /// A chain's links alternate face-on and edge-on, and each edge-on link hangs on the top wire of the face-on link
    /// below it. Hooked through a charm's ring, the chain's last link is an edge-on one and the charm's ring takes the
    /// place of the face-on link that would come next. So the chain is laid from that end, stopped where its last link
    /// begins, and that link is drawn over the charm, across its bar, its tip showing inside the ring.
    /// </remarks>
    /// <param name="EyeRow">The row laid on the underside of the charm's ring bar, <see cref="LinkThroughRing"/> of the
    /// way up the link from its tip.</param>
    /// <param name="LinkTop">The first row below the face-on link above: from here the last link is drawn alone.</param>
    /// <param name="LinkBottom">The edge-on link's tip.</param>
    /// <param name="HalfWidth">Half the edge-on link's width.</param>
    private sealed record EndLink(double EyeRow, double LinkTop, double LinkBottom, double HalfWidth);

    /// <summary>
    /// How much of a chain's last link reaches below the underside of a charm's ring bar: with the bar's own thickness,
    /// about half the link lies across or inside the ring (Sharan's rule, 4 Oct: 40–60%), and its tip shows in the ring.
    /// </summary>
    private const double LinkThroughRing = 0.3;

    /// <summary>The strip's length in pixels: the longest straight piece drawn at once, plus a repeat to start in.</summary>
    private const int StripPixels = 4096;

    /// <summary>How far a piece may run from straight before the cord is split there, in points.</summary>
    private const double Straightness = 0.25;

    /// <summary>
    /// How far each piece reaches back over the one above, in points, on the rows that really come before it; at least a
    /// quarter of the rope's width.
    /// </summary>
    private const double Overlap = 0.5;

    private static readonly Dictionary<RopeStyle, Tile?> tiles = [];
    private static CanvasDevice? device;

    /// <summary>Whether <paramref name="style"/> is drawn from a picture.</summary>
    public static bool Has(ICanvasResourceCreator creator, RopeStyle style) => TileFor(creator, style) is not null;

    /// <summary>
    /// Draws <paramref name="style"/>'s rope along <paramref name="path"/>, <paramref name="width"/> wide; a chain whose
    /// path ends <paramref name="hooked"/> through a charm's ring is laid from that end and stopped where its last link
    /// begins (<see cref="DrawEndLink"/>).
    /// </summary>
    public static void Draw(CanvasDrawingSession session, CanvasGeometry path, RopeStyle style, double width, bool hooked = false)
    {
        if (width <= 0.3 || TileFor(session, style) is not Tile tile)
        {
            return;
        }

        double length = path.ComputePathLength();
        if (length <= 0.1)
        {
            return;
        }

        // Points per tile pixel: the rope in the picture drawn at the cord's width.
        double scale = width / tile.RopeWidth;
        double longest = (tile.Strip.SizeInPixels.Height - tile.Height - 2) * scale;

        // The cord's curve, sampled every half a rope's width, then cut to the corners it really has.
        double step = Math.Max(0.5, width / 2);
        var samples = new List<System.Numerics.Vector2>();
        for (double arc = 0; arc < length; arc += step)
        {
            samples.Add(path.ComputePointOnPath((float)arc));
        }

        samples.Add(path.ComputePointOnPath((float)length));
        List<System.Numerics.Vector2> corners = StraightRuns(samples);

        double total = 0;
        for (int index = 1; index < corners.Count; index++)
        {
            total += System.Numerics.Vector2.Distance(corners[index - 1], corners[index]);
        }

        double phase = 0, stop = total;
        if (hooked && tile.Link is EndLink link)
        {
            phase = link.EyeRow - (total / scale);
            stop = Math.Max(0, total - ((link.EyeRow - link.LinkTop) * scale));
        }

        System.Numerics.Matrix3x2 previous = session.Transform;
        double along = 0;
        for (int index = 1; index < corners.Count && along < stop; index++)
        {
            System.Numerics.Vector2 head = corners[index - 1], tail = corners[index];
            double span = System.Numerics.Vector2.Distance(head, tail);
            if (span <= 0.01)
            {
                continue;
            }

            double drawn = Math.Min(span, stop - along);

            // A run longer than the strip is drawn in pieces of it.
            int pieces = Math.Max(1, (int)Math.Ceiling(drawn / longest));
            for (int piece = 0; piece < pieces; piece++)
            {
                double start = drawn * piece / pieces, end = drawn * (piece + 1) / pieces;
                DrawPiece(session, tile, along + start, phase, end - start, scale, width,
                    System.Numerics.Vector2.Lerp(head, tail, (float)(start / span)), tail - head, previous);
            }

            along += span;
        }

        session.Transform = previous;
    }

    /// <summary>
    /// The corners of the cord once every run straight to within <see cref="Straightness"/> is one line: a cord at rest is
    /// a single run; a swinging one keeps a corner wherever it bends (Douglas–Peucker).
    /// </summary>
    private static List<System.Numerics.Vector2> StraightRuns(List<System.Numerics.Vector2> points)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        bool[] keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var pending = new Stack<(int First, int Last)>();
        pending.Push((0, points.Count - 1));
        while (pending.Count > 0)
        {
            (int first, int last) = pending.Pop();
            if (last <= first + 1)
            {
                continue;
            }

            System.Numerics.Vector2 from = points[first], to = points[last];
            double chord = Math.Max(System.Numerics.Vector2.Distance(from, to), 0.0001);
            int farthest = first;
            double distance = 0;
            for (int index = first + 1; index < last; index++)
            {
                System.Numerics.Vector2 point = points[index];
                double off = Math.Abs(((to.X - from.X) * (from.Y - point.Y)) - ((from.X - point.X) * (to.Y - from.Y))) / chord;
                if (off > distance)
                {
                    distance = off;
                    farthest = index;
                }
            }

            if (distance > Straightness)
            {
                keep[farthest] = true;
                pending.Push((first, farthest));
                pending.Push((farthest, last));
            }
        }

        var corners = new List<System.Numerics.Vector2>();
        for (int index = 0; index < points.Count; index++)
        {
            if (keep[index])
            {
                corners.Add(points[index]);
            }
        }

        return corners;
    }

    /// <summary>
    /// One straight piece, <paramref name="length"/> long from <paramref name="origin"/>, starting <paramref name="arc"/>
    /// along the cord: the strip's rows for that stretch, at the cord's scale, ending exactly where the piece does — the
    /// cord's last piece ends inside a cap, or behind a jump ring's wire.
    /// </summary>
    private static void DrawPiece(
        CanvasDrawingSession session, Tile tile, double arc, double phase, double length, double scale, double ropeWidth,
        System.Numerics.Vector2 origin, System.Numerics.Vector2 direction, System.Numerics.Matrix3x2 previous)
    {
        // Reaching back over the piece above on the rows that really come before this one, except at the cord's top —
        // further on a thick rope, whose outer edge opens wider where two pieces meet at an angle.
        double back = Math.Min(Math.Max(Overlap, ropeWidth * 0.25), arc);
        float angle = MathF.Atan2(direction.Y, direction.X) - (MathF.PI / 2);
        session.Transform = System.Numerics.Matrix3x2.CreateRotation(angle)
            * System.Numerics.Matrix3x2.CreateTranslation(origin)
            * previous;

        double width = tile.Strip.SizeInPixels.Width;
        double row = (((arc - back) / scale) + phase) % tile.Height;
        if (row < 0)
        {
            row += tile.Height;
        }
        session.DrawImage(
            tile.Strip,
            new Windows.Foundation.Rect(-width * scale / 2, -back, width * scale, length + back),
            new Windows.Foundation.Rect(0, row, width, (length + back) / scale),
            1f,
            CanvasImageInterpolation.HighQualityCubic);
    }

    /// <summary>
    /// A chain's last link, hooked through a charm's ring at <paramref name="eye"/> (the underside of the ring's bar),
    /// hanging at <paramref name="direction"/>: drawn over the charm, across the bar, its tip inside the ring.
    /// </summary>
    public static void DrawEndLink(CanvasDrawingSession session, RopeStyle style, Vec2 eye, double direction, double width)
    {
        if (width <= 0.3 || TileFor(session, style) is not Tile tile || tile.Link is not EndLink link)
        {
            return;
        }

        double scale = width / tile.RopeWidth;
        double half = link.HalfWidth * scale;
        System.Numerics.Matrix3x2 previous = session.Transform;
        session.Transform = System.Numerics.Matrix3x2.CreateRotation((float)(direction - (Math.PI / 2)))
            * System.Numerics.Matrix3x2.CreateTranslation((float)eye.X, (float)eye.Y)
            * previous;

        // Just the link, from two rows up over the chain above (the same rows of the same picture, so no line shows where
        // it stops) to its tip, as wide as the link and rounded at the tip as the link is.
        double fromRow = link.LinkTop - 2;
        double top = (fromRow - link.EyeRow) * scale, bottom = (link.LinkBottom - link.EyeRow) * scale;
        using CanvasGeometry tip = CanvasGeometry.CreateRoundedRectangle(
            session, (float)-half, (float)(top - half), (float)(half * 2), (float)(bottom - top + half), (float)half, (float)half);
        double centre = tile.Strip.SizeInPixels.Width / 2.0;
        using (session.CreateLayer(1f, tip))
        {
            session.DrawImage(
                tile.Strip,
                new Windows.Foundation.Rect(-half, top, half * 2, bottom - top),
                new Windows.Foundation.Rect(centre - link.HalfWidth, fromRow, link.HalfWidth * 2, link.LinkBottom - fromRow),
                1f,
                CanvasImageInterpolation.HighQualityCubic);
        }

        session.Transform = previous;
    }

    /// <summary>A style's tile, decoded once per graphics device; null for a style with no picture.</summary>
    private static Tile? TileFor(ICanvasResourceCreator creator, RopeStyle style)
    {
        if (!ReferenceEquals(device, creator.Device))
        {
            foreach (Tile? stale in tiles.Values)
            {
                stale?.Strip.Dispose();
            }

            tiles.Clear();
            device = creator.Device;
        }

        if (tiles.TryGetValue(style, out Tile? cached))
        {
            return cached;
        }

        Tile? tile = null;
        string name = style.ToString();
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Ropes", $"{char.ToLowerInvariant(name[0])}{name[1..]}.png");
        if (File.Exists(path))
        {
            try
            {
                using SKBitmap? decoded = SKBitmap.Decode(path);
                if (decoded is not null)
                {
                    using SKBitmap converted = decoded.Copy(SKColorType.Bgra8888)
                        ?? throw new InvalidOperationException("could not convert");
                    byte[] pixels = new byte[converted.ByteCount];
                    System.Runtime.InteropServices.Marshal.Copy(converted.GetPixels(), pixels, 0, pixels.Length);
                    double ropeWidth = RopeWidth(pixels, converted.Width, converted.Height, converted.RowBytes);
                    if (ropeWidth > 1)
                    {
                        // The tile repeated down a strip — it repeats with no seam, so neither does this.
                        int count = (int)Math.Ceiling((double)StripPixels / converted.Height);
                        byte[] strip = new byte[pixels.Length * count];
                        for (int index = 0; index < count; index++)
                        {
                            Buffer.BlockCopy(pixels, 0, strip, index * pixels.Length, pixels.Length);
                        }

                        tile = new Tile(
                            CanvasBitmap.CreateFromBytes(
                                creator, strip, converted.Width, converted.Height * count,
                                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 96,
                                CanvasAlphaMode.Premultiplied),
                            converted.Height,
                            ropeWidth,
                            style.IsChain() ? EndLinkOf(pixels, converted.Width, converted.Height, converted.RowBytes, ropeWidth) : null);
                    }
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Log($"rope '{name}' could not be loaded: {exception.Message}");
            }
        }

        tiles[style] = tile;
        return tile;
    }

    /// <summary>
    /// Where a chain tile's links are (<see cref="EndLink"/>), read from its alpha over two repeats: the face-on links from
    /// columns through their arms, their openings from columns just beside the edge-on link, the edge-on link's tip from
    /// the middle column. macOS's <c>RopeSurface.endLink</c>.
    /// </summary>
    private static EndLink? EndLinkOf(byte[] pixels, int width, int height, int rowBytes, double ropeWidth)
    {
        byte Alpha(int column, int row) => pixels[((row % height) * rowBytes) + (column * 4) + 3];
        int centre = width / 2;
        int arm = (int)Math.Round(ropeWidth * 0.38), beside = Math.Max(1, (int)Math.Round(ropeWidth * 0.085));
        if (centre - arm < 0 || centre + arm >= width)
        {
            return null;
        }

        List<(int First, int Last)> Runs(Func<int, bool> test)
        {
            var found = new List<(int First, int Last)>();
            int? start = null;
            for (int row = 0; row < height * 2; row++)
            {
                if (test(row))
                {
                    start ??= row;
                }
                else if (start is int first)
                {
                    if (row - first > 2)
                    {
                        found.Add((first, row - 1));
                    }

                    start = null;
                }
            }

            return found;
        }

        List<(int First, int Last)> faces = Runs(row => Alpha(centre - arm, row) > 128 && Alpha(centre + arm, row) > 128);
        List<(int First, int Last)> openings = Runs(row => Alpha(centre - beside, row) < 60 && Alpha(centre + beside, row) < 60);
        int openingIndex = openings.FindIndex(run => run.First >= height / 2);
        if (openingIndex < 0)
        {
            return null;
        }

        (int First, int Last) opening = openings[openingIndex];
        int below = faces.FindIndex(run => run.First <= opening.First && opening.First <= run.Last);
        if (below <= 0)
        {
            return null;
        }

        (int First, int Last) above = faces[below - 1];
        int middle = (above.Last + faces[below].First) / 2;
        int left = centre, right = centre;
        while (left > 0 && Alpha(left - 1, middle) > 60)
        {
            left--;
        }

        while (right < width - 1 && Alpha(right + 1, middle) > 60)
        {
            right++;
        }

        int tip = -1;
        for (int row = opening.First; row < height * 2; row++)
        {
            if (Alpha(centre, row) < 60)
            {
                tip = row;
                break;
            }
        }

        if (tip < 0)
        {
            return null;
        }

        double top = above.Last + 1, bottom = tip + 1;
        return new EndLink(bottom - ((bottom - top) * LinkThroughRing), top, bottom, Math.Max(centre - left, right - centre) - 0.5);
    }

    /// <summary>How wide the rope is in its tile: the average, over the rows, of the run of pixels it covers.</summary>
    private static double RopeWidth(byte[] pixels, int width, int height, int rowBytes)
    {
        double total = 0;
        for (int row = 0; row < height; row++)
        {
            int first = -1, last = -1;
            for (int column = 0; column < width; column++)
            {
                if (pixels[(row * rowBytes) + (column * 4) + 3] > 128)
                {
                    if (first < 0)
                    {
                        first = column;
                    }

                    last = column;
                }
            }

            if (first >= 0)
            {
                total += last - first + 1;
            }
        }

        return height > 0 ? total / height : 0;
    }
}
