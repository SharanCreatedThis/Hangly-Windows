//
//  StudioPipeline.cs
//  Hangly
//
//  Creator Studio's stages: isolate, fit, analyse. The same stages and numbers as macOS.
//

using Hangly.Core.Models;

namespace Hangly.Core.Studio;

/// <summary>How the background is taken away. The macOS <c>SubjectRemoval</c>, case for case.</summary>
public abstract record SubjectRemoval
{
    /// <summary>Existing transparency, else the detected subject, else a flat-background fill.</summary>
    public sealed record Automatic : SubjectRemoval;

    /// <summary>The detected subject: every instance (<c>null</c>) or one, numbered from 1.</summary>
    public sealed record DetectedSubject(int? Instance) : SubjectRemoval;

    /// <summary>A flood fill from the corners, for artwork on a plain ground.</summary>
    public sealed record FlatBackground(int Tolerance) : SubjectRemoval;

    /// <summary>The picture as it is.</summary>
    public sealed record KeepOriginal : SubjectRemoval;

    public const int DefaultTolerance = 36;
}

/// <summary>The Studio's controls. Same ranges and defaults as macOS.</summary>
public sealed record StudioAdjustments
{
    public SubjectRemoval Removal { get; init; } = new SubjectRemoval.Automatic();

    public string Name { get; init; } = string.Empty;

    /// <summary>Charm radius as a fraction of the rope.</summary>
    public double SizeRatio { get; init; } = 0.12;

    public double WeightScale { get; init; } = 1.0;

    /// <summary>How much of its square the subject spans.</summary>
    public double Fill { get; init; } = 0.92;

    public static readonly (double Min, double Max) SizeRange = (0.08, 0.16);
    public static readonly (double Min, double Max) WeightRange = (0.5, 2.0);
    public static readonly (double Min, double Max) FillRange = (0.70, 0.98);
    public static readonly (int Min, int Max) ToleranceRange = (8, 96);

    public StudioAdjustments Clamped() => this with
    {
        SizeRatio = Math.Clamp(SizeRatio, SizeRange.Min, SizeRange.Max),
        WeightScale = Math.Clamp(WeightScale, WeightRange.Min, WeightRange.Max),
        Fill = Math.Clamp(Fill, FillRange.Min, FillRange.Max),
        Removal = Removal is SubjectRemoval.FlatBackground flat
            ? new SubjectRemoval.FlatBackground(Math.Clamp(flat.Tolerance, ToleranceRange.Min, ToleranceRange.Max))
            : Removal,
    };
}

/// <summary>A charm as the Studio currently has it: the square picture plus the numbers.</summary>
/// <param name="AnalysedMass">The mass the analysis derived, before the weight was applied.</param>
public sealed record StudioDraft(StudioImage Square, CharmMetrics Metrics, CharmPalette Palette, double AnalysedMass);

/// <summary>A foreground mask: one value per pixel, 0 background to 1 subject.</summary>
public sealed record SubjectMask(int Width, int Height, float[] Values);

/// <summary>Something that can find the subject of a picture. Local, always.</summary>
/// <remarks>
/// The seam future models plug into — people, pets, products, logos, objects — without the
/// Studio changing: each is another implementation, and the Studio asks whichever is
/// available. Returns <c>null</c> when it finds no subject, which is an answer, not an error.
/// </remarks>
public interface ISubjectSegmenter
{
    /// <summary>A short name for the log and the About page.</summary>
    string Name { get; }

    Task<SubjectMask?> SegmentAsync(StudioImage image, CancellationToken cancellation);
}

/// <summary>What a segmenter found: every subject together, then each one alone.</summary>
/// <remarks>
/// The macOS <c>SubjectDetection</c>: index zero is all instances, then one per instance,
/// each cropped to its own extent, so switching subject costs nothing. Vision returns
/// instances directly; a salient-object mask is split into them by connected components.
/// </remarks>
public sealed class SubjectDetection
{
    private readonly IReadOnlyList<StudioImage> cutouts;

    /// <param name="cutouts">Everything first, then each instance when there is more than one.</param>
    /// <param name="instanceCount">How many subjects were found.</param>
    public SubjectDetection(IReadOnlyList<StudioImage> cutouts, int? instanceCount = null)
    {
        this.cutouts = cutouts;
        InstanceCount = instanceCount ?? Math.Max(cutouts.Count - 1, cutouts.Count);
    }

    public static SubjectDetection None { get; } = new([], 0);

    /// <summary>How many subjects: the macOS number, so one subject is 1 and the picker shows from 2.</summary>
    public int InstanceCount { get; }

    public bool HasSubject => cutouts.Count > 0;

    /// <param name="instance">1-based, or null for all of them.</param>
    public StudioImage? Cutout(int? instance)
    {
        if (instance is null || (instance == 1 && InstanceCount == 1))
        {
            return cutouts.Count > 0 ? cutouts[0] : null;
        }

        return instance >= 1 && instance < cutouts.Count ? cutouts[instance.Value] : null;
    }
}

/// <summary>The Studio's pipeline, split into stages so a slider re-runs only what it changes.</summary>
public static class StudioPipeline
{
    public const byte OpaqueThreshold = 8;
    public const double MeaningfulAlphaFraction = 0.02;
    public const int OutputSide = 512;

    /// <summary>A mask value at or above this counts as subject when splitting instances.</summary>
    public const float SubjectThreshold = 0.5f;

    /// <summary>A part smaller than this share of the whole subject is noise, not an instance.</summary>
    public const double MinimumInstanceShare = 0.02;

    /// <summary>Whether a picture already carries a cut-out: more than 2% transparent.</summary>
    public static bool HasMeaningfulAlpha(StudioImage image) =>
        1 - image.Coverage(OpaqueThreshold) > MeaningfulAlphaFraction;

    /// <summary>Turns a mask into cut-outs: all instances, then each one, each cropped to its extent.</summary>
    public static SubjectDetection Detect(StudioImage image, SubjectMask? mask)
    {
        if (mask is null || mask.Width != image.Width || mask.Height != image.Height)
        {
            return SubjectDetection.None;
        }

        StudioImage all = image.Masked(mask.Values);
        if (all.OpaqueBounds(OpaqueThreshold) is not PixelBounds allBounds)
        {
            return SubjectDetection.None;
        }

        var cutouts = new List<StudioImage> { all.Cropped(allBounds) };
        foreach (float[] part in Instances(mask))
        {
            StudioImage one = image.Masked(part);
            if (one.OpaqueBounds(OpaqueThreshold) is PixelBounds bounds)
            {
                cutouts.Add(one.Cropped(bounds));
            }
        }

        // One instance is the same picture as "all"; do not keep it twice.
        int count = Math.Max(1, cutouts.Count - 1);
        if (cutouts.Count == 2)
        {
            cutouts.RemoveAt(1);
        }

        return new SubjectDetection(cutouts, count);
    }

    /// <summary>
    /// Splits a mask into its separate parts, largest first, dropping specks: eight-connected
    /// components over <see cref="SubjectThreshold"/>, each keeping the mask's soft values.
    /// </summary>
    public static IReadOnlyList<float[]> Instances(SubjectMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        int width = mask.Width, height = mask.Height;
        var labels = new int[width * height];
        var sizes = new List<int> { 0 };
        var stack = new Stack<int>();

        for (int start = 0; start < labels.Length; start++)
        {
            if (labels[start] != 0 || mask.Values[start] < SubjectThreshold)
            {
                continue;
            }

            int label = sizes.Count;
            sizes.Add(0);
            labels[start] = label;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int index = stack.Pop();
                sizes[label]++;
                int x = index % width, y = index / width;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        int next = (ny * width) + nx;
                        if (labels[next] == 0 && mask.Values[next] >= SubjectThreshold)
                        {
                            labels[next] = label;
                            stack.Push(next);
                        }
                    }
                }
            }
        }

        int total = sizes.Sum();
        var kept = Enumerable.Range(1, sizes.Count - 1)
            .Where(label => sizes[label] >= total * MinimumInstanceShare)
            .OrderByDescending(label => sizes[label])
            .ToList();

        // Soft edges belong to whichever part they touch: every sub-threshold pixel is
        // carried by the part of its nearest labelled neighbour within one pixel.
        var parts = new List<float[]>();
        foreach (int label in kept)
        {
            var part = new float[labels.Length];
            for (int index = 0; index < labels.Length; index++)
            {
                if (labels[index] == label || (labels[index] == 0 && mask.Values[index] > 0 && Touches(labels, index, width, height, label)))
                {
                    part[index] = mask.Values[index];
                }
            }

            parts.Add(part);
        }

        return parts;
    }

    private static bool Touches(int[] labels, int index, int width, int height, int label)
    {
        int x = index % width, y = index / width;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && labels[(ny * width) + nx] == label)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Applies the chosen removal. The macOS <c>isolate</c>, rule for rule.</summary>
    public static StudioImage Isolate(StudioImage source, SubjectDetection detection, SubjectRemoval removal)
    {
        switch (removal)
        {
            case SubjectRemoval.KeepOriginal:
                return source;
            case SubjectRemoval.FlatBackground flat:
                return FloodFilled(source, flat.Tolerance);
            case SubjectRemoval.DetectedSubject detected:
                return detection.Cutout(detected.Instance) ?? throw new InvalidOperationException("No subject was detected.");
            default:
                if (HasMeaningfulAlpha(source))
                {
                    return source;
                }

                return detection.Cutout(null) ?? FloodFilled(source, SubjectRemoval.DefaultTolerance);
        }
    }

    public static StudioImage FloodFilled(StudioImage source, int tolerance)
    {
        StudioImage copy = source.Copy();
        copy.FloodFillBackground(tolerance);
        copy.FeatherAlphaEdges();
        return copy;
    }

    /// <summary>
    /// The rope-facing numbers for a fitted square. The macOS <c>analyze</c>: mass follows how
    /// much shape there is, measured at the shipped framing so re-framing does not re-weigh;
    /// the knot follows the framing, because the cord has to stop on the artwork.
    /// </summary>
    /// <summary>Where the subject goes in the output square: the macOS <c>fitToSquare</c> rectangle.</summary>
    /// <remarks>
    /// The subject's longest side spans <paramref name="fill"/> of the square, centred, with
    /// its size rounded to whole pixels. Returned in top-down pixels; the caller draws the
    /// source's <paramref name="subject"/> region into it.
    /// </remarks>
    public static (double X, double Y, double Width, double Height) FitRect(PixelBounds subject, double fill, int side = OutputSide)
    {
        double scale = side * Math.Clamp(fill, 0.3, 1) / Math.Max(subject.Width, subject.Height);
        double width = Math.Max(1, Math.Round(subject.Width * scale));
        double height = Math.Max(1, Math.Round(subject.Height * scale));
        return ((side - width) / 2, (side - height) / 2, width, height);
    }

    /// <summary>A fitted square, analysed, with the person's size and weight applied.</summary>
    /// <remarks>The macOS <c>buildDraft</c>: mass is the analysed mass times the weight, within 0.5–9.</remarks>
    public static StudioDraft Draft(StudioImage square, StudioAdjustments adjustments)
    {
        ArgumentNullException.ThrowIfNull(adjustments);
        (CharmMetrics analysed, CharmPalette palette) = Analyze(square);
        var metrics = new CharmMetrics(
            Math.Clamp(analysed.Mass * adjustments.WeightScale, 0.5, 9),
            adjustments.SizeRatio,
            analysed.KnotInset);
        return new StudioDraft(square, metrics, palette, analysed.Mass);
    }

    /// <summary>A display name from a file: the stem, tidied. The macOS rule.</summary>
    /// <remarks>
    /// Leading dots mark a hidden file and are not part of a name; what remains may be
    /// nothing but an extension, in which case the file has no name.
    /// </remarks>
    public static string SuggestedName(string path)
    {
        string file = Path.GetFileName(path ?? string.Empty).TrimStart('.');
        string stem = Path.GetFileNameWithoutExtension(file);
        if (ImageExtensions.Contains(stem.ToLowerInvariant()))
        {
            stem = string.Empty;
        }

        string tidied = stem.Replace('_', ' ').Replace('-', ' ').Trim();
        return tidied.Length == 0 ? "Custom Charm" : tidied;
    }

    private static readonly HashSet<string> ImageExtensions = ["png", "jpg", "jpeg", "webp", "heic", "heif", "svg"];

    public static (CharmMetrics Metrics, CharmPalette Palette) Analyze(StudioImage square, double subjectFill = 0.92)
    {
        ArgumentNullException.ThrowIfNull(square);
        PixelBounds bounds = square.OpaqueBounds(OpaqueThreshold)
            ?? throw new InvalidOperationException("The picture has nothing visible in it.");

        double longest = Math.Max(bounds.Width, bounds.Height);
        double framing = Math.Clamp(longest / square.Width, 0.3, 1);
        double density = square.Coverage(OpaqueThreshold) * (subjectFill * subjectFill) / (framing * framing);
        double mass = Math.Clamp(2.0 + (3.2 * density), 2.0, 4.5);

        // Rows are top-down here, so the top edge is MinY.
        double topFromTop = bounds.MinY / (double)square.Height;
        double knotInset = Math.Clamp((0.5 - topFromTop) / 0.5, 0.3, 1.0);

        CharmColor baseColour = square.AverageColor(OpaqueThreshold) ?? new CharmColor(0.6, 0.6, 0.65);
        var palette = new CharmPalette(
            baseColour,
            baseColour.Scaled(0.72),
            baseColour.Scaled(0.45),
            CharmColor.Interpolate(baseColour, new CharmColor(1, 1, 1), 0.55));
        return (new CharmMetrics(mass, 0.12, knotInset), palette);
    }
}
