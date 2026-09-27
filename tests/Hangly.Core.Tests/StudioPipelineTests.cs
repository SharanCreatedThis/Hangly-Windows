using Hangly.Core.Studio;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The Studio's stages, with the macOS pipeline's rules.</summary>
public class StudioPipelineTests
{
    /// <summary>A white square with a red disc in the middle, opaque throughout.</summary>
    private static StudioImage DiscOnWhite(int side = 64, int radius = 20)
    {
        var pixels = new byte[side * side * 4];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                int i = ((y * side) + x) * 4;
                bool inside = Math.Pow(x - (side / 2.0), 2) + Math.Pow(y - (side / 2.0), 2) <= radius * radius;
                pixels[i] = 255;
                pixels[i + 1] = inside ? (byte)30 : (byte)255;
                pixels[i + 2] = inside ? (byte)30 : (byte)255;
                pixels[i + 3] = 255;
            }
        }

        return new StudioImage(side, side, pixels);
    }

    private static SubjectMask Mask(int width, int height, Func<int, int, float> value)
    {
        var values = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                values[(y * width) + x] = value(x, y);
            }
        }

        return new SubjectMask(width, height, values);
    }

    [Fact]
    public void AFlatBackgroundIsFilledAwayAndTheSubjectKept()
    {
        StudioImage cut = StudioPipeline.FloodFilled(DiscOnWhite(), SubjectRemoval.DefaultTolerance);

        Assert.Equal(0, cut.Alpha(0, 0));
        Assert.Equal(0, cut.Alpha(63, 63));
        Assert.Equal(255, cut.Alpha(32, 32));
        Assert.True(StudioPipeline.HasMeaningfulAlpha(cut));
        Assert.False(StudioPipeline.HasMeaningfulAlpha(DiscOnWhite()));
    }

    [Fact]
    public void FeatheringSoftensTheEdgeWithoutGrowingIt()
    {
        StudioImage hard = DiscOnWhite();
        hard.FloodFillBackground(SubjectRemoval.DefaultTolerance);
        PixelBounds before = hard.OpaqueBounds(StudioPipeline.OpaqueThreshold)!.Value;

        hard.FeatherAlphaEdges();
        PixelBounds after = hard.OpaqueBounds(StudioPipeline.OpaqueThreshold)!.Value;

        Assert.True(after.Width <= before.Width && after.Height <= before.Height);
        Assert.InRange(hard.Alpha(before.MinX, 32), 1, 254);
    }

    [Fact]
    public void TwoSeparateSubjectsAreTwoInstancesAndASpeckIsNone()
    {
        SubjectMask mask = Mask(100, 40, (x, y) =>
            (x is >= 5 and < 40 && y is >= 5 and < 35) ? 1f   // left, 35×30
            : (x is >= 60 and < 90 && y is >= 5 and < 30) ? 1f // right, 30×25
            : (x == 50 && y == 20) ? 1f                        // one-pixel speck
            : 0f);

        IReadOnlyList<float[]> parts = StudioPipeline.Instances(mask);

        Assert.Equal(2, parts.Count);
        Assert.Equal(1f, parts[0][(20 * 100) + 20]); // the larger, left one first
        Assert.Equal(0f, parts[0][(20 * 100) + 70]);
        Assert.Equal(1f, parts[1][(20 * 100) + 70]);
    }

    [Fact]
    public void OneSubjectIsNotOfferedTwice()
    {
        StudioImage image = DiscOnWhite();
        SubjectDetection detection = StudioPipeline.Detect(image, Mask(64, 64, (x, y) => image.Pixels[(((y * 64) + x) * 4) + 1] < 100 ? 1f : 0f));

        Assert.Equal(1, detection.InstanceCount);
        Assert.Same(detection.Cutout(null), detection.Cutout(1));
        Assert.Null(detection.Cutout(2));
        Assert.False(SubjectDetection.None.HasSubject);
    }

    [Fact]
    public void TwoSubjectsGiveAllThenEachCroppedToItself()
    {
        var image = new StudioImage(100, 40, Enumerable.Repeat((byte)200, 100 * 40 * 4).ToArray());
        SubjectDetection detection = StudioPipeline.Detect(image, Mask(100, 40, (x, y) =>
            (x is >= 5 and < 40 && y is >= 5 and < 35) || (x is >= 60 and < 90 && y is >= 5 and < 30) ? 1f : 0f));

        Assert.Equal(2, detection.InstanceCount);
        Assert.Equal((85, 30), (detection.Cutout(null)!.Width, detection.Cutout(null)!.Height));
        Assert.Equal((35, 30), (detection.Cutout(1)!.Width, detection.Cutout(1)!.Height));
        Assert.Equal((30, 25), (detection.Cutout(2)!.Width, detection.Cutout(2)!.Height));
    }

    [Fact]
    public void AutomaticKeepsACutOutThenUsesTheSubjectThenFloods()
    {
        StudioImage opaque = DiscOnWhite();
        StudioImage alreadyCut = StudioPipeline.FloodFilled(opaque, 36);
        SubjectDetection found = new([alreadyCut]);

        Assert.Same(alreadyCut, StudioPipeline.Isolate(alreadyCut, SubjectDetection.None, new SubjectRemoval.Automatic()));
        Assert.Same(alreadyCut, StudioPipeline.Isolate(opaque, found, new SubjectRemoval.Automatic()));
        Assert.Equal(0, StudioPipeline.Isolate(opaque, SubjectDetection.None, new SubjectRemoval.Automatic()).Alpha(0, 0));
        Assert.Same(opaque, StudioPipeline.Isolate(opaque, found, new SubjectRemoval.KeepOriginal()));
        Assert.Throws<InvalidOperationException>(() => StudioPipeline.Isolate(opaque, SubjectDetection.None, new SubjectRemoval.DetectedSubject(1)));
    }

    [Fact]
    public void AnalysisFollowsTheMacFormula()
    {
        // A bar 92% of the side wide and half that tall, framed as shipped: its longest side
        // is the 92% framing, so density is its coverage, 0.4232, and mass is
        // 2 + 3.2 × 0.4232. (A solid square always reaches fill² and hits the 4.5 ceiling.)
        // The knot sits at its top edge.
        const int side = 500;
        const int span = 460;
        const int start = (side - span) / 2;
        var pixels = new byte[side * side * 4];
        for (int y = start; y < start + (span / 2); y++)
        {
            for (int x = start; x < start + span; x++)
            {
                int i = ((y * side) + x) * 4;
                pixels[i] = 200;
                pixels[i + 1] = 100;
                pixels[i + 2] = 50;
                pixels[i + 3] = 255;
            }
        }

        (Hangly.Core.Models.CharmMetrics metrics, Hangly.Core.Models.CharmPalette palette) = StudioPipeline.Analyze(new StudioImage(side, side, pixels));

        Assert.Equal(2 + (3.2 * 0.4232), metrics.Mass, 3);
        Assert.Equal((0.5 - (20.0 / 500)) / 0.5, metrics.KnotInset, 3);
        Assert.Equal(200 / 255.0, palette.Primary.Red, 3);
    }

    [Fact]
    public void AdjustmentsClampToTheMacRanges()
    {
        StudioAdjustments wild = new StudioAdjustments { SizeRatio = 1, WeightScale = 9, Fill = 0.1, Removal = new SubjectRemoval.FlatBackground(500) }.Clamped();

        Assert.Equal(0.16, wild.SizeRatio);
        Assert.Equal(2.0, wild.WeightScale);
        Assert.Equal(0.70, wild.Fill);
        Assert.Equal(new SubjectRemoval.FlatBackground(96), wild.Removal);
    }

    [Fact]
    public void TheSubjectIsFittedByItsLongestSideAndCentred()
    {
        (double x, double y, double width, double height) = StudioPipeline.FitRect(new PixelBounds(10, 20, 209, 119), 0.92);

        Assert.Equal((471, 236), (width, height)); // 200×100 scaled to 0.92 of 512
        Assert.Equal(((512 - 471) / 2.0, (512 - 236) / 2.0), (x, y));
    }

    [Fact]
    public void WeightScalesTheAnalysedMassWithinItsLimitsAndSizeIsTheRatio()
    {
        var pixels = new byte[100 * 100 * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 3] = 255;
        }

        StudioDraft draft = StudioPipeline.Draft(new StudioImage(100, 100, pixels), new StudioAdjustments { WeightScale = 2, SizeRatio = 0.1 });

        Assert.Equal(4.5, draft.AnalysedMass);
        Assert.Equal(9.0, draft.Metrics.Mass);
        Assert.Equal(0.1, draft.Metrics.RadiusRatio);
    }

    [Theory]
    [InlineData(@"C:\Pictures\my_dog-photo.JPG", "my dog photo")]
    [InlineData("/tmp/.hidden.png", "hidden")]
    [InlineData("/tmp/.png", "Custom Charm")]
    [InlineData("/tmp/heic.heic", "Custom Charm")]
    [InlineData("", "Custom Charm")]
    public void NamesFollowTheMacRule(string path, string expected) =>
        Assert.Equal(expected, StudioPipeline.SuggestedName(path.Replace('\\', Path.DirectorySeparatorChar)));

    [Fact]
    public void UndoAndRedoWalkTheHistoryAndANewChangeDropsTheRedoBranch()
    {
        var stack = new UndoStack<int>();
        stack.Record(1);
        stack.Record(2);

        Assert.True(stack.TryUndo(3, out int back) && back == 2);
        Assert.True(stack.TryRedo(2, out int forward) && forward == 3);
        Assert.True(stack.TryUndo(3, out _));
        stack.Record(2);
        Assert.False(stack.CanRedo);
        Assert.False(new UndoStack<int>().TryUndo(5, out int unchanged));
        Assert.Equal(5, unchanged);
    }
}
