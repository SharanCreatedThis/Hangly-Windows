using Hangly.Core.Studio;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The arithmetic around the segmentation model, checked without the model.</summary>
public class SegmentationTensorsTests
{
    private static readonly SegmentationModelShape Tiny = new(4, [0.485f, 0.456f, 0.406f], [0.229f, 0.224f, 0.225f], OutputIsLogit: true);

    private static StudioImage Solid(int width, int height, byte r, byte g, byte b, byte a = 255)
    {
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (r, g, b, a);
        }

        return new StudioImage(width, height, pixels);
    }

    [Fact]
    public void AFlatColourBecomesItsNormalisedValueInEveryPlane()
    {
        // Larger than the model and smaller than it: area averaging and bilinear agree on a flat colour.
        foreach (StudioImage image in new[] { Solid(37, 11, 255, 0, 128), Solid(3, 2, 255, 0, 128) })
        {
            float[] tensor = SegmentationTensors.Input(image, Tiny);

            Assert.Equal(3 * 16, tensor.Length);
            Assert.All(tensor[..16], v => Assert.Equal((1f - 0.485f) / 0.229f, v, 4));
            Assert.All(tensor[16..32], v => Assert.Equal((0f - 0.456f) / 0.224f, v, 4));
            Assert.All(tensor[32..], v => Assert.Equal(((128 / 255f) - 0.406f) / 0.225f, v, 4));
        }
    }

    [Fact]
    public void TransparentPixelsAreReadOverWhite()
    {
        float[] tensor = SegmentationTensors.Input(Solid(8, 8, 0, 0, 0, 0), Tiny);

        Assert.Equal((1f - 0.485f) / 0.229f, tensor[0], 4);
    }

    [Fact]
    public void ShrinkingAveragesRatherThanPicksAPixel()
    {
        // A 1-pixel checkerboard at 8× the model: point sampling would give black or white,
        // averaging gives grey.
        var pixels = new byte[32 * 32 * 4];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                byte v = (byte)((x + y) % 2 == 0 ? 255 : 0);
                int i = ((y * 32) + x) * 4;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (v, v, v, 255);
            }
        }

        float[] tensor = SegmentationTensors.Input(new StudioImage(32, 32, pixels), Tiny);

        Assert.All(tensor[..16], v => Assert.Equal((0.5f - 0.485f) / 0.229f, v, 3));
    }

    [Fact]
    public void LogitsBecomeProbabilitiesAtThePictureSize()
    {
        // Left half strongly subject, right half strongly background.
        float[] output = [9, 9, -9, -9, 9, 9, -9, -9, 9, 9, -9, -9, 9, 9, -9, -9];

        SubjectMask mask = SegmentationTensors.Mask(output, Tiny, 40, 10);

        Assert.Equal((40, 10), (mask.Width, mask.Height));
        Assert.True(mask.Values[5] > 0.99f);
        Assert.True(mask.Values[35] < 0.01f);
        Assert.InRange(mask.Values[20], 0.3f, 0.7f); // the edge lands in the middle, not half a pixel off
    }

    [Fact]
    public void ProbabilitiesPassThroughClampedWhenTheModelIsNotLogits()
    {
        float[] output = [1.2f, 0.5f, -0.1f, 0.25f];

        SubjectMask mask = SegmentationTensors.Mask(output, new SegmentationModelShape(2, [0.5f, 0.5f, 0.5f], [1, 1, 1], OutputIsLogit: false), 2, 2);

        Assert.Equal([1f, 0.5f, 0f, 0.25f], mask.Values);
        Assert.False(SegmentationModelShape.IsNet.OutputIsLogit);
    }

    [Fact]
    public void AnOutputOfTheWrongSizeIsRefused() =>
        Assert.Throws<ArgumentException>(() => SegmentationTensors.Mask(new float[10], Tiny, 4, 4));
}
