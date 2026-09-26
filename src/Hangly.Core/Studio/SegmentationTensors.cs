//
//  SegmentationTensors.cs
//  Hangly
//
//  The arithmetic either side of a segmentation model: pixels in, a mask out.
//

namespace Hangly.Core.Studio;

/// <summary>How a model wants its input, as it was trained.</summary>
/// <param name="Side">The square the picture is resized to.</param>
/// <param name="Mean">Per-channel mean subtracted after scaling to 0–1, in RGB order.</param>
/// <param name="Deviation">Per-channel divisor applied after the mean.</param>
/// <param name="OutputIsLogit">Whether the output needs a sigmoid to become 0–1.</param>
public sealed record SegmentationModelShape(int Side, float[] Mean, float[] Deviation, bool OutputIsLogit)
{
    /// <summary>BiRefNet: ImageNet statistics at 1024², logits out.</summary>
    public static SegmentationModelShape BiRefNet { get; } = new(
        1024,
        [0.485f, 0.456f, 0.406f],
        [0.229f, 0.224f, 0.225f],
        OutputIsLogit: true);

    /// <summary>IS-Net (DIS): mean 0.5 and no scaling at 1024², probabilities out.</summary>
    public static SegmentationModelShape IsNet { get; } = new(
        1024,
        [0.5f, 0.5f, 0.5f],
        [1f, 1f, 1f],
        OutputIsLogit: false);
}

/// <summary>Picture to tensor, and tensor back to a mask the size of the picture.</summary>
/// <remarks>
/// Kept in Core, apart from the runtime that executes the model, so it is tested on its own
/// and a future model reuses it with a different <see cref="SegmentationModelShape"/>.
/// </remarks>
public static class SegmentationTensors
{
    /// <summary>The model's input: planar RGB, <c>1 × 3 × side × side</c>, normalised.</summary>
    /// <remarks>
    /// Shrinking averages every source pixel under each output pixel, so a 4000-pixel photo
    /// does not alias into the model; enlarging is bilinear. Transparent pixels are read
    /// over white, the backdrop the Studio shows them on.
    /// </remarks>
    public static float[] Input(StudioImage image, SegmentationModelShape shape)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(shape);
        int side = shape.Side;
        int plane = side * side;
        var tensor = new float[3 * plane];
        double scaleX = image.Width / (double)side;
        double scaleY = image.Height / (double)side;

        Span<double> sum = stackalloc double[3];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                SamplePixel(image, x, y, scaleX, scaleY, sum);
                int at = (y * side) + x;
                for (int channel = 0; channel < 3; channel++)
                {
                    tensor[(channel * plane) + at] = (float)((sum[channel] - shape.Mean[channel]) / shape.Deviation[channel]);
                }
            }
        }

        return tensor;
    }

    /// <summary>The model's <c>side × side</c> output as a mask the size of <paramref name="image"/>.</summary>
    public static SubjectMask Mask(ReadOnlySpan<float> output, SegmentationModelShape shape, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(shape);
        int side = shape.Side;
        if (output.Length != side * side)
        {
            throw new ArgumentException("output must be one value per model pixel");
        }

        var square = new float[side * side];
        for (int index = 0; index < square.Length; index++)
        {
            float value = output[index];
            square[index] = shape.OutputIsLogit ? 1f / (1f + MathF.Exp(-value)) : Math.Clamp(value, 0f, 1f);
        }

        // Bilinear, sampling at pixel centres, which is what keeps the edge from shifting
        // half a pixel between the model's grid and the picture's.
        var values = new float[width * height];
        double scaleX = side / (double)width;
        double scaleY = side / (double)height;
        for (int y = 0; y < height; y++)
        {
            double sourceY = Math.Clamp(((y + 0.5) * scaleY) - 0.5, 0, side - 1);
            int y0 = (int)sourceY;
            int y1 = Math.Min(y0 + 1, side - 1);
            float fy = (float)(sourceY - y0);
            for (int x = 0; x < width; x++)
            {
                double sourceX = Math.Clamp(((x + 0.5) * scaleX) - 0.5, 0, side - 1);
                int x0 = (int)sourceX;
                int x1 = Math.Min(x0 + 1, side - 1);
                float fx = (float)(sourceX - x0);
                float top = square[(y0 * side) + x0] + ((square[(y0 * side) + x1] - square[(y0 * side) + x0]) * fx);
                float bottom = square[(y1 * side) + x0] + ((square[(y1 * side) + x1] - square[(y1 * side) + x0]) * fx);
                values[(y * width) + x] = top + ((bottom - top) * fy);
            }
        }

        return new SubjectMask(width, height, values);
    }

    /// <summary>The 0–1 RGB colour under output pixel (x, y).</summary>
    private static void SamplePixel(StudioImage image, int x, int y, double scaleX, double scaleY, Span<double> rgb)
    {
        rgb.Clear();
        if (scaleX > 1 || scaleY > 1)
        {
            // Area average over the footprint, which is at least one source pixel wide.
            int left = (int)(x * scaleX), right = Math.Max(left + 1, (int)Math.Ceiling((x + 1) * scaleX));
            int top = (int)(y * scaleY), bottom = Math.Max(top + 1, (int)Math.Ceiling((y + 1) * scaleY));
            right = Math.Min(right, image.Width);
            bottom = Math.Min(bottom, image.Height);
            int count = 0;
            for (int sy = top; sy < bottom; sy++)
            {
                for (int sx = left; sx < right; sx++)
                {
                    AddOverWhite(image, sx, sy, 1, rgb);
                    count++;
                }
            }

            for (int channel = 0; channel < 3; channel++)
            {
                rgb[channel] /= count;
            }

            return;
        }

        double fx = Math.Clamp(((x + 0.5) * scaleX) - 0.5, 0, image.Width - 1);
        double fy = Math.Clamp(((y + 0.5) * scaleY) - 0.5, 0, image.Height - 1);
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = Math.Min(x0 + 1, image.Width - 1), y1 = Math.Min(y0 + 1, image.Height - 1);
        double dx = fx - x0, dy = fy - y0;
        AddOverWhite(image, x0, y0, (1 - dx) * (1 - dy), rgb);
        AddOverWhite(image, x1, y0, dx * (1 - dy), rgb);
        AddOverWhite(image, x0, y1, (1 - dx) * dy, rgb);
        AddOverWhite(image, x1, y1, dx * dy, rgb);
    }

    /// <summary>Adds a premultiplied pixel composited over white, weighted.</summary>
    private static void AddOverWhite(StudioImage image, int x, int y, double weight, Span<double> rgb)
    {
        int at = ((y * image.Width) + x) * 4;
        double clear = 1 - (image.Pixels[at + 3] / 255.0);
        for (int channel = 0; channel < 3; channel++)
        {
            rgb[channel] += ((image.Pixels[at + channel] / 255.0) + clear) * weight;
        }
    }
}
