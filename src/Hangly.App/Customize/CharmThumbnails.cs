//
//  CharmThumbnails.cs
//  Hangly
//
//  Charm artwork, small, for choosing by.
//

using Hangly.App.Overlay;
using Hangly.Core.Models;
using SkiaSharp;
using Svg.Skia;

namespace Hangly.App.Customize;

/// <summary>Rasterises each charm once, to a file, for the picker to show.</summary>
/// <remarks>
/// Deliberately not the overlay's <see cref="CharmArtworkCache"/>. That one produces
/// Win2D bitmaps at whatever size the current frame needs and throws them away when the
/// charm resizes; this one wants eighty-one images at one fixed size, held for as long as
/// the window is open, and handed to XAML rather than to a drawing session.
///
/// <para>Written to disk rather than kept in memory, and reused across launches. Eighty-one
/// rasterisations is about a second of work — fine once, rude every time the window
/// opens — and a PNG on disk is something XAML can load by URI without any of the
/// in-memory stream plumbing a <c>BitmapImage</c> otherwise needs.</para>
///
/// <para>The whole artwork is drawn, cord and beads included, which is right here: the
/// picker is showing what the charm <i>is</i>, not how it will hang.</para>
/// </remarks>
public static class CharmThumbnails
{
    /// <summary>Rendered at twice the display size so the grid stays sharp at 200%.</summary>
    public const int Pixels = 256;

    private static readonly Lock Gate = new();

    /// <summary>Where the rendered thumbnails live.</summary>
    public static string Directory => Path.Combine(
        Path.GetTempPath(),
        "Hangly",
        "thumbnails",
        Pixels.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// The PNG for this charm, rendering it first if it is not already there.
    /// </summary>
    /// <returns>A path, or <see langword="null"/> when the artwork could not be drawn.</returns>
    /// <summary>Deletes cached thumbnails for charms that no longer exist.</summary>
    /// <remarks>
    /// The cache is keyed by charm id and nothing ever looks up an id that is gone, so a
    /// stale file is inert rather than harmful. It still accumulates: an audit found
    /// fourteen — the eleven seasonal charms cut from v1, and three imports that had been
    /// deleted — and the import ones would have kept arriving for as long as people tried
    /// charms and changed their minds.
    ///
    /// <para>Swallows its own failures. This is a tidy-up of a temporary directory, and
    /// there is no version of "could not delete a cached thumbnail" that a person needs to
    /// hear about or that should stop the Library opening.</para>
    /// </remarks>
    public static void Prune(IReadOnlyCollection<string> liveIds)
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return;
            }

            var live = liveIds.Select(SafeName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int removed = 0;
            foreach (string file in System.IO.Directory.EnumerateFiles(Directory, "*.png"))
            {
                if (!live.Contains(Path.GetFileNameWithoutExtension(file)))
                {
                    File.Delete(file);
                    removed++;
                }
            }

            Services.Diagnostics.Log($"pruned {removed} stale thumbnail(s) of {live.Count} live");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Services.Diagnostics.Log($"could not prune thumbnails: {exception.GetType().Name}");
        }
    }

    public static string? PathFor(CharmCatalogEntry entry)
    {
        string target = Path.Combine(Directory, SafeName(entry.Id) + ".png");
        if (File.Exists(target))
        {
            return target;
        }

        lock (Gate)
        {
            if (File.Exists(target))
            {
                return target;
            }

            try
            {
                return Render(entry, target) ? target : null;
            }
            catch (Exception exception)
            {
                Services.Diagnostics.Failure($"thumbnail for {entry.Id}", exception);
                return null;
            }
        }
    }

    private static bool Render(CharmCatalogEntry entry, string target)
    {
        string source = Path.Combine(CharmArtworkCache.DefaultDirectory, entry.FileName);
        if (!File.Exists(source))
        {
            return false;
        }

        using var document = new SKSvg();
        document.Load(source);
        if (document.Picture is null)
        {
            return false;
        }

        SKRect bounds = document.Picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        using var surface = SKSurface.Create(new SKImageInfo(
            Pixels,
            Pixels,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));

        float scale = Math.Min(Pixels / bounds.Width, Pixels / bounds.Height);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(
            (Pixels - (bounds.Width * scale)) / 2,
            (Pixels - (bounds.Height * scale)) / 2);
        canvas.Scale(scale);
        canvas.DrawPicture(document.Picture);
        canvas.Flush();

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream file = File.Create(target);
        data.SaveTo(file);
        return true;
    }

    /// <summary>Charm ids are camelCase words, but a file name should not have to trust that.</summary>
    private static string SafeName(string id)
    {
        Span<char> buffer = stackalloc char[id.Length];
        for (int index = 0; index < id.Length; index++)
        {
            buffer[index] = char.IsLetterOrDigit(id[index]) ? id[index] : '_';
        }

        return new string(buffer);
    }
}
