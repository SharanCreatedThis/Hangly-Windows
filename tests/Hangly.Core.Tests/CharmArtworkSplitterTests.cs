//
//  CharmArtworkSplitterTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// The splitter against a silhouette the test draws itself: a thin cord down the top,
/// one bead threaded on it, then a wide body. Drawing the input rather than loading an
/// asset is the point — it is the one way to know what the answer should be.
/// </summary>
public class CharmArtworkSplitterTests
{
    private const int Side = CharmArtworkSplitter.AnalysisPixels;

    /// <summary>Content spans x 60–260 of 320, so the cord threshold is measured against that.</summary>
    private const double ContentWidth = 201.0 / Side;

    private static byte[] Artwork()
    {
        var mask = new byte[Side * Side];

        // A cord 6px wide: well under 0.085 × 201 ≈ 17px, so it is never a solid run.
        Fill(mask, 157, 0, 162, 125);

        // One bead, 41px wide.
        Fill(mask, 140, 30, 180, 70);

        // The charm: wider than it is tall, so the knot inset is not trivially 1.
        Fill(mask, 60, 120, 260, 240);
        return mask;
    }

    private static void Fill(byte[] mask, int left, int top, int right, int bottom)
    {
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                mask[(y * Side) + x] = 255;
            }
        }
    }

    private static CharmArtworkRegions Split(int beadCount = 1, int bodyRun = 1) =>
        CharmArtworkSplitter.Split(Artwork(), Side, ContentWidth, beadCount, bodyRun)
        ?? throw new InvalidOperationException("the splitter refused a silhouette it should read");

    [Fact(DisplayName = "The cord is not mistaken for a bead")]
    public void CordIsNotSolid() => Assert.Single(Split().Beads);

    /// <summary>
    /// The one that catches an upside-down read. Row zero has to be the top of the
    /// artwork; if that ever inverts, every charm hangs by its feet.
    /// </summary>
    [Fact(DisplayName = "The bead is found above the body, not below it")]
    public void BeadSitsAboveTheBody()
    {
        CharmArtworkRegions regions = Split();
        Assert.True(regions.Beads[0].Top < regions.Body.Top);
        Assert.True(regions.Beads[0].Bottom <= regions.Body.Top);
    }

    [Fact(DisplayName = "The bead's bounds hug the bead")]
    public void BeadBoundsAreMeasured()
    {
        Rect bead = Split().Beads[0];
        Assert.Equal(140.0 / Side, bead.Left, 6);
        Assert.Equal(30.0 / Side, bead.Top, 6);
        Assert.Equal(41.0 / Side, bead.Width, 6);
        Assert.Equal(41.0 / Side, bead.Height, 6);
    }

    [Fact(DisplayName = "The body runs from its own solid part to the last ink")]
    public void BodyBoundsAreMeasured()
    {
        Rect body = Split().Body;
        Assert.Equal(60.0 / Side, body.Left, 6);
        Assert.Equal(120.0 / Side, body.Top, 6);
        Assert.Equal(201.0 / Side, body.Width, 6);
        Assert.Equal(121.0 / Side, body.Height, 6);
    }

    [Fact(DisplayName = "The knot inset is the body's height over its longest side")]
    public void KnotInsetIsMeasured() =>
        Assert.Equal(121.0 / 201.0, Split().KnotInset, 6);

    [Fact(DisplayName = "Asking for no beads leaves the body where it is")]
    public void ZeroBeadsStillFindsTheBody()
    {
        // bodyRun 1 still: the bead's run is cord furniture now, and is dropped rather
        // than drawn, which is what a thick-corded charm does.
        CharmArtworkRegions regions = Split(beadCount: 0, bodyRun: 1);
        Assert.Empty(regions.Beads);
        Assert.Equal(120.0 / Side, regions.Body.Top, 6);
    }

    [Fact(DisplayName = "Artwork without the parts the catalogue expects is refused, not guessed at")]
    public void MissingRunsReturnNull()
    {
        Assert.Null(CharmArtworkSplitter.Split(Artwork(), Side, ContentWidth, beadCount: 1, bodyRun: 5));
        Assert.Null(CharmArtworkSplitter.Split(Artwork(), Side, ContentWidth, beadCount: -1, bodyRun: 0));
        Assert.Null(CharmArtworkSplitter.Split(Artwork(), Side, ContentWidth, beadCount: 3, bodyRun: 1));
        Assert.Null(CharmArtworkSplitter.Split([], 0, ContentWidth, beadCount: 0, bodyRun: 0));
    }

    [Fact(DisplayName = "A measured bead becomes a bead the rope can carry")]
    public void BeadsBecomePhysics()
    {
        CharmCatalogEntry charm = CharmCatalog.Find("nazar");
        IReadOnlyList<CharmBead> beads = CharmCatalog.BeadsFor(charm, Split());

        CharmBead bead = Assert.Single(beads);

        // Sizes are multiples of the charm's radius, and the body is two radii across.
        double scale = 2 / (201.0 / Side);
        Assert.Equal(41.0 / Side * scale, bead.Size.Width, 6);

        // Above the knot, so it rides the cord rather than the charm.
        Assert.True(bead.Offset > 0);
        Assert.True(bead.Mass >= CharmCatalog.MinimumBeadMass);
    }

    [Fact(DisplayName = "The knot comes from the artwork once it has been measured")]
    public void MetricsTakeTheMeasuredKnot()
    {
        CharmCatalogEntry charm = CharmCatalog.Find("nazar");
        CharmMetrics metrics = CharmCatalog.MetricsFor(charm, Split());

        Assert.Equal(charm.Mass, metrics.Mass);
        Assert.Equal(charm.RadiusRatio, metrics.RadiusRatio);
        Assert.Equal(121.0 / 201.0, metrics.KnotInset, 6);
        Assert.NotEqual(CharmCatalog.FallbackKnotInset, metrics.KnotInset);
    }
}
