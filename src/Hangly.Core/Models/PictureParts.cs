//
//  PictureParts.cs
//  Hangly
//
//  A picture that hangs by its own rope, taken apart into the figures on it.
//

using Hangly.Core.Geometry;
using Hangly.Core.Settings;

namespace Hangly.Core.Models;

/// <summary>One figure of a picture that hangs by its own rope, ready to hang as a charm of its own.</summary>
/// <param name="Region">The figure, in the vector's unit square (the artwork centred in it at its longest side).</param>
/// <param name="RopeAbove">The artwork's own rope above the figure, down to where the figure takes hold of it: a column of
/// the unit square, wide enough for its knots.</param>
/// <param name="PlainAbove">A plain, knot-free stretch of that rope, repeated above it when the cord is longer than the
/// picture's rope (a small charm size, or Elastic), so the rope is whole at every size without being stretched.</param>
/// <param name="Metrics">Where on the rope it hangs and how large, against the rope's own length.</param>
/// <param name="HalfPixels">Half the figure's longest side, in artwork pixels: drawn at its radius, so a radius over this is
/// the artwork's scale on screen.</param>
/// <param name="LongestPixels">The artwork's longest side, in the same pixels.</param>
public sealed record PictureFigure(Rect Region, Rect RopeAbove, Rect PlainAbove, CharmMetrics Metrics, double HalfPixels, double LongestPixels);

/// <summary>The pictures that hang by their own rope, and how each comes apart. macOS's <c>PictureParts</c>.</summary>
/// <remarks>
/// <b>Why the picture is taken apart.</b> Spider-Man and Gwen is one tall picture: the rope from the top, Spider-Man
/// holding it, the rope again, and Gwen at its end. Drawn whole it could only turn about the anchor like a clock hand: the
/// rope in it could not bend, Elastic could not stretch it, and only the place the solver thought the charm was — near
/// Spider-Man — could be grabbed, never Gwen. As two charms on one cord each figure is a weight of its own: the cord
/// between them bends, swings and stretches like any rope, a flick sends the pair tumbling like a double pendulum, and
/// either figure can be grabbed or thrown. Only the picture's own rope is drawn, laid along the cord in thin bands.
/// </remarks>
public static class PictureParts
{
    /// <param name="Axis">The column the rope runs down where it meets the figure. The figure is centred on it, so the cord's
    /// line runs into the figure's hand exactly where the drawn rope does.</param>
    private sealed record Figure(
        double Left, double Top, double Right, double Bottom, double Axis, double RopeTop, double PlainTop, double PlainBottom, double Mass);

    /// <summary>Half the width of the column the rope is cut from: room for its widest knot.</summary>
    private const double RopeHalfWidth = 24;

    private sealed record Layout(double Width, double Height, IReadOnlyList<Figure> Figures);

    /// <summary>
    /// Measured from the artwork row by row, four rows at a time: the rope is plain down to row 356 and Spider-Man's upper
    /// hand begins at 358, the rope at x 172; it leaves his lower hand at 858, at x 173; Gwen's knot begins at 1426, the
    /// rope at x 182. Plain, knot-free rope runs from 16 to 176 and from 1000 to 1190. The viewBox is 382 × 1910.
    /// </summary>
    private static readonly Layout SpiderManGwen = new(
        382,
        1910,
        [
            new Figure(112, 358, 346, 858, 172, 0, 16, 176, 1.5),
            new Figure(2, 1426, 378, 1906, 182, 858, 1000, 1190, 1.3),
        ]);

    /// <summary>The rope a picture at size one hangs from, against the charm unit: about the default rope.</summary>
    public const double RopeProportion = 0.75;

    /// <summary>
    /// The rope's length while the picture hangs, as a multiple of the shipped rope: the charm size times the picture's
    /// own size on the rope, so a larger picture is the whole picture larger — the rope between Spider-Man and Gwen with
    /// it, as the artwork has it — and never two figures drifting apart on a rope that stayed the same. The rope-length
    /// control does not apply to it: the artwork decides its rope. Null with no picture on the rope.
    /// </summary>
    public static double? RopeLength(OverlaySettings settings)
    {
        foreach (RopeCharm place in settings.Stack.Places)
        {
            if (FiguresOf(place.Id).Count > 0)
            {
                return Math.Clamp(settings.CharmSize * place.Size * RopeProportion, 0.3, 3.0);
            }
        }

        return null;
    }

    /// <summary>The length the canvas is made for: the rope, and Gwen below its end with her shadow.</summary>
    public static double? CanvasRopeLength(OverlaySettings settings) => RopeLength(settings) * 1.3;

    /// <summary>The figures of the catalogue entry <paramref name="id"/>, top first; empty for any other charm.</summary>
    public static IReadOnlyList<PictureFigure> FiguresOf(string id)
    {
        if (id != "spiderManGwen")
        {
            return [];
        }

        Layout layout = SpiderManGwen;
        Figure last = layout.Figures[^1];
        // The rope's length, in artwork pixels: from the anchor to the centre of the figure on its end.
        double rope = (last.Top + last.Bottom) / 2;
        double longest = Math.Max(layout.Width, layout.Height);
        double UnitX(double pixels) => (pixels + ((longest - layout.Width) / 2)) / longest;
        double UnitY(double pixels) => (pixels + ((longest - layout.Height) / 2)) / longest;

        return [.. layout.Figures.Select(figure =>
        {
            // Centred on the rope's column: as wide as the figure reaches on its wider side, both ways.
            double across = Math.Max(figure.Axis - figure.Left, figure.Right - figure.Axis);
            double half = Math.Max(across, (figure.Bottom - figure.Top) / 2);
            double centre = (figure.Top + figure.Bottom) / 2;
            return new PictureFigure(
                new Rect(UnitX(figure.Axis - across), UnitY(figure.Top), across * 2 / longest, (figure.Bottom - figure.Top) / longest),
                new Rect(UnitX(figure.Axis - RopeHalfWidth), UnitY(figure.RopeTop), RopeHalfWidth * 2 / longest, (figure.Top - figure.RopeTop) / longest),
                new Rect(UnitX(figure.Axis - RopeHalfWidth), UnitY(figure.PlainTop), RopeHalfWidth * 2 / longest, (figure.PlainBottom - figure.PlainTop) / longest),
                new CharmMetrics(figure.Mass, half / rope, (centre - figure.Top) / half, centre / rope),
                half,
                longest);
        })];
    }
}
