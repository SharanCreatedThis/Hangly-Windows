//
//  StudioImage.cs
//  Hangly
//
//  Pixels the Studio works on, and the plain loops it runs over them.
//

using Hangly.Core.Models;

namespace Hangly.Core.Studio;

/// <summary>Integer pixel bounds, inclusive, top-down.</summary>
public readonly record struct PixelBounds(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;

    public int Height => MaxY - MinY + 1;
}

/// <summary>8-bit premultiplied RGBA, <b>row zero at the top</b>.</summary>
/// <remarks>
/// The Windows counterpart of the macOS <c>RGBABitmap</c>, and deliberately the same kind
/// of thing: plain loops over an array, easy to read and to test, fast enough that a
/// 2048-pixel square flood-fills in tens of milliseconds. The one difference is the
/// orientation — CoreGraphics stores rows bottom-up, SkiaSharp top-down — and every
/// method that cares says which way is up.
/// </remarks>
public sealed class StudioImage
{
    public StudioImage(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != width * height * 4)
        {
            throw new ArgumentException("pixels must be width × height × 4 bytes");
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Premultiplied RGBA, row-major, top row first.</summary>
    public byte[] Pixels { get; }

    public StudioImage Copy() => new(Width, Height, (byte[])Pixels.Clone());

    public byte Alpha(int x, int y) => Pixels[(((y * Width) + x) * 4) + 3];

    /// <summary>Every pixel more opaque than <paramref name="threshold"/>; null if none.</summary>
    public PixelBounds? OpaqueBounds(byte threshold)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
        {
            int row = y * Width * 4;
            for (int x = 0; x < Width; x++)
            {
                if (Pixels[row + (x * 4) + 3] > threshold)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        return maxX < 0 ? null : new PixelBounds(minX, minY, maxX, maxY);
    }

    /// <summary>Fraction of pixels more opaque than <paramref name="threshold"/>.</summary>
    public double Coverage(byte threshold)
    {
        int count = 0;
        for (int index = 3; index < Pixels.Length; index += 4)
        {
            if (Pixels[index] > threshold)
            {
                count++;
            }
        }

        return count / (double)(Width * Height);
    }

    /// <summary>Alpha-weighted mean colour of the visible pixels, un-premultiplied; null if none.</summary>
    public CharmColor? AverageColor(byte threshold)
    {
        double red = 0, green = 0, blue = 0, weight = 0;
        for (int index = 0; index < Pixels.Length; index += 4)
        {
            double alpha = Pixels[index + 3];
            if (alpha <= threshold)
            {
                continue;
            }

            // Premultiplied channels are colour × alpha / 255, so the alpha-weighted
            // sum of the true colour is the stored channel × 255.
            red += Pixels[index] * 255.0;
            green += Pixels[index + 1] * 255.0;
            blue += Pixels[index + 2] * 255.0;
            weight += alpha;
        }

        return weight > 0 ? new CharmColor(red / weight / 255, green / weight / 255, blue / weight / 255) : null;
    }

    /// <summary>
    /// Clears every pixel reachable from a corner whose colour is within
    /// <paramref name="tolerance"/> (per channel, 0–255) of that corner's colour.
    /// </summary>
    /// <remarks>
    /// The macOS rule exactly: compared against the <em>seed corner</em>, not the
    /// neighbouring pixel, so a soft edge cannot let the fill creep inwards a shade at a
    /// time; an already-transparent corner is not a background.
    /// </remarks>
    public void FloodFillBackground(int tolerance)
    {
        var visited = new bool[Width * Height];
        var queue = new List<int>((Width + Height) * 2);
        (int X, int Y)[] corners = [(0, 0), (Width - 1, 0), (0, Height - 1), (Width - 1, Height - 1)];

        foreach ((int cornerX, int cornerY) in corners)
        {
            int seed = ((cornerY * Width) + cornerX) * 4;
            if (Pixels[seed + 3] <= 250)
            {
                continue;
            }

            int r0 = Pixels[seed], g0 = Pixels[seed + 1], b0 = Pixels[seed + 2];
            int start = (cornerY * Width) + cornerX;
            if (visited[start])
            {
                continue;
            }

            queue.Clear();
            visited[start] = true;
            queue.Add(start);

            for (int head = 0; head < queue.Count; head++)
            {
                int index = queue[head];
                int pixel = index * 4;
                bool matches = Pixels[pixel + 3] > 250
                    && Math.Abs(Pixels[pixel] - r0) <= tolerance
                    && Math.Abs(Pixels[pixel + 1] - g0) <= tolerance
                    && Math.Abs(Pixels[pixel + 2] - b0) <= tolerance;
                if (!matches)
                {
                    continue;
                }

                Pixels[pixel] = Pixels[pixel + 1] = Pixels[pixel + 2] = Pixels[pixel + 3] = 0;

                int x = index % Width, y = index / Width;
                if (x > 0) Enqueue(index - 1);
                if (x < Width - 1) Enqueue(index + 1);
                if (y > 0) Enqueue(index - Width);
                if (y < Height - 1) Enqueue(index + Width);
            }
        }

        void Enqueue(int index)
        {
            if (!visited[index])
            {
                visited[index] = true;
                queue.Add(index);
            }
        }
    }

    /// <summary>Softens hard alpha edges by one pixel without growing the shape.</summary>
    /// <remarks>Each pixel's alpha becomes the lesser of itself and its 3×3 mean, colour rescaled to stay premultiplied.</remarks>
    public void FeatherAlphaEdges()
    {
        if (Width <= 2 || Height <= 2)
        {
            return;
        }

        byte[] original = (byte[])Pixels.Clone();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int index = ((y * Width) + x) * 4;
                int alpha = original[index + 3];
                if (alpha == 0)
                {
                    continue;
                }

                int sum = 0, count = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                        {
                            continue;
                        }

                        sum += original[(((ny * Width) + nx) * 4) + 3];
                        count++;
                    }
                }

                int mean = sum / Math.Max(count, 1);
                if (mean >= alpha)
                {
                    continue;
                }

                double ratio = mean / (double)alpha;
                Pixels[index] = (byte)Math.Round(original[index] * ratio);
                Pixels[index + 1] = (byte)Math.Round(original[index + 1] * ratio);
                Pixels[index + 2] = (byte)Math.Round(original[index + 2] * ratio);
                Pixels[index + 3] = (byte)mean;
            }
        }
    }

    /// <summary>
    /// Keeps the pixels where <paramref name="mask"/> says subject (0–1 per pixel, same size),
    /// scaling each pixel's alpha — and so, premultiplied, its colour — by the mask.
    /// </summary>
    public StudioImage Masked(float[] mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.Length != Width * Height)
        {
            throw new ArgumentException("mask must be one value per pixel");
        }

        byte[] output = (byte[])Pixels.Clone();
        for (int index = 0; index < mask.Length; index++)
        {
            float keep = Math.Clamp(mask[index], 0f, 1f);
            int pixel = index * 4;
            for (int channel = 0; channel < 4; channel++)
            {
                output[pixel + channel] = (byte)Math.Round(output[pixel + channel] * keep);
            }
        }

        return new StudioImage(Width, Height, output);
    }

    /// <summary>The pixels inside <paramref name="bounds"/>, as a new image.</summary>
    public StudioImage Cropped(PixelBounds bounds)
    {
        var output = new byte[bounds.Width * bounds.Height * 4];
        for (int y = 0; y < bounds.Height; y++)
        {
            Array.Copy(Pixels, (((bounds.MinY + y) * Width) + bounds.MinX) * 4, output, y * bounds.Width * 4, bounds.Width * 4);
        }

        return new StudioImage(bounds.Width, bounds.Height, output);
    }
}
