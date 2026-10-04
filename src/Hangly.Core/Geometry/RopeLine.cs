//
//  RopeLine.cs
//  Hangly
//
//  The straight line a stretch of drawn rope runs down.
//

namespace Hangly.Core.Geometry;

/// <summary>
/// The straight line a stretch of a picture's drawn rope runs down, fitted through its bands' centroids with the folds
/// and knots left out: least squares, then again without the bands that stand more than a couple of pixels off it.
/// macOS's <c>PictureRopeRenderer.RopeLine</c>.
/// </summary>
/// <remarks>
/// Every band of the rope is placed against this one line, so the rope is drawn as the artwork has it — the fold
/// hanging from Spider-Man's lower hand, the twists above his upper one, the knots — and only the line bends with the
/// cord. Centring each band on its own centroid, as it first did, slid every band holding a fold or a knot sideways by
/// half of it: the fold under his hand opened out and came away from it.
/// </remarks>
public sealed class RopeLine
{
    private readonly double intercept;
    private readonly double slope;
    private readonly bool isFitted;

    /// <param name="points">Each band's middle row and the column of its centroid, in artwork pixels.</param>
    public RopeLine(IReadOnlyList<(double Middle, double Centre)> points)
    {
        List<(double Middle, double Centre)> kept = [.. points];
        for (int pass = 0; pass < 4 && kept.Count >= 2; pass++)
        {
            if (!Fit(kept, out double nextIntercept, out double nextSlope))
            {
                break;
            }

            (intercept, slope, isFitted) = (nextIntercept, nextSlope, true);
            double[] residuals = [.. kept.Select(point => Math.Abs(point.Centre - (intercept + (slope * point.Middle))))];
            double typical = residuals.Order().ElementAt(residuals.Length / 2);
            double limit = Math.Max(2, typical * 2.5);
            List<(double Middle, double Centre)> next = [.. kept.Where((_, index) => residuals[index] <= limit)];
            if (next.Count == kept.Count)
            {
                break;
            }

            kept = next;
        }
    }

    /// <summary>Where the line crosses the row <paramref name="middle"/>; null when there was nothing to fit.</summary>
    public double? CentreAt(double middle) => isFitted ? intercept + (slope * middle) : null;

    private static bool Fit(List<(double Middle, double Centre)> points, out double intercept, out double slope)
    {
        double meanX = points.Average(point => point.Middle);
        double meanY = points.Average(point => point.Centre);
        double spread = points.Sum(point => (point.Middle - meanX) * (point.Middle - meanX));
        if (spread <= 0)
        {
            (intercept, slope) = (0, 0);
            return false;
        }

        slope = points.Sum(point => (point.Middle - meanX) * (point.Centre - meanY)) / spread;
        intercept = meanY - (slope * meanX);
        return true;
    }
}
