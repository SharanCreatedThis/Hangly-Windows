//
//  CustomCharmStore.cs
//  Hangly
//
//  On-disk home for imported charms.
//

using System.Text.Json;
using Hangly.Core.Settings;

namespace Hangly.Core.Import;

/// <summary>Persists imported charms as files plus a JSON manifest.</summary>
/// <remarks>
/// Layout, mirroring the macOS store:
///
/// <code>
/// %APPDATA%\Hangly\Charms\
///     manifest.json      every entry's name, metrics and palette
///     &lt;guid&gt;.svg         the sanitised drawing
/// </code>
///
/// <para><b>Beside the settings, not beside the program.</b> Velopack installs to
/// <c>%LOCALAPPDATA%\Hangly</c> and clears it on an install-over — which was measured,
/// not assumed, when it deleted a settings file during the packaging spike. A charm
/// somebody made is exactly the thing that must not be destroyed by an update, so it
/// lives where the settings do.</para>
///
/// <para>The manifest is the source of truth, with two repairs at load that the macOS
/// store also makes: an entry whose file has gone is dropped, so a stale reference can
/// never leave the rope bare; and a file with no entry is re-registered, so a manifest
/// lost to a half-written save does not orphan the drawings beside it.</para>
/// </remarks>
public sealed class CustomCharmStore
{
    private const string ManifestName = "manifest.json";

    private readonly List<CustomCharmEntry> entries = [];

    public CustomCharmStore(string directory)
    {
        Directory = directory;
        Load();
    }

    /// <summary>Imported charms, oldest first.</summary>
    public IReadOnlyList<CustomCharmEntry> Entries => entries;

    public string Directory { get; }

    /// <summary>Entries dropped at load because their drawing was gone.</summary>
    public IReadOnlyList<Guid> Pruned { get; private set; } = [];

    /// <summary>Drawings found on disk with no manifest entry, re-registered at load.</summary>
    public int Recovered { get; private set; }

    /// <summary>Where imports live for a real installation.</summary>
    public static string DefaultDirectory => Path.Combine(
        Path.GetDirectoryName(SettingsStore.DefaultPath)!,
        "Charms");

    public string PathFor(CustomCharmEntry entry) => Path.Combine(Directory, entry.ImageFileName);

    public CustomCharmEntry? Find(Guid id) => entries.FirstOrDefault(entry => entry.Id == id);

    /// <summary>Writes the drawing and records the entry.</summary>
    public CustomCharmEntry Add(string markup, string name, Hangly.Core.Models.CharmMetrics metrics, Hangly.Core.Models.CharmPalette palette)
    {
        System.IO.Directory.CreateDirectory(Directory);

        var id = Guid.NewGuid();
        string fileName = id.ToString("D", System.Globalization.CultureInfo.InvariantCulture) + ".svg";
        File.WriteAllText(Path.Combine(Directory, fileName), markup, System.Text.Encoding.UTF8);

        var entry = new CustomCharmEntry
        {
            Id = id,
            Name = name,

            // Whole seconds: the manifest stores the time without fractions, and an entry
            // must compare equal to itself after a reload.
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds()),
            ImageFileName = fileName,
            Metrics = metrics,
            Palette = palette,
        };

        entries.Add(entry);
        Save();
        return entry;
    }

    /// <summary>Removes the entry and its drawing. Removing something absent is not an error.</summary>
    public void Remove(Guid id)
    {
        CustomCharmEntry? entry = Find(id);
        if (entry is null)
        {
            return;
        }

        entries.Remove(entry);

        string path = PathFor(entry);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        Save();
    }

    /// <summary>Renames an import, keeping everything else about it.</summary>
    public void Rename(Guid id, string name)
    {
        int index = entries.FindIndex(entry => entry.Id == id);
        if (index < 0)
        {
            return;
        }

        entries[index] = entries[index] with { Name = name };
        Save();
    }

    private void Load()
    {
        entries.Clear();
        Pruned = [];
        Recovered = 0;

        if (!System.IO.Directory.Exists(Directory))
        {
            return;
        }

        string manifest = Path.Combine(Directory, ManifestName);
        if (File.Exists(manifest))
        {
            try
            {
                entries.AddRange(
                    JsonSerializer.Deserialize<List<CustomCharmEntry>>(
                        File.ReadAllText(manifest),
                        AppSettings.JsonOptions) ?? []);
            }
            catch (Exception)
            {
                // A corrupt manifest is recovered from rather than fatal, exactly as a
                // corrupt settings document is: the drawings are still on disk, and the
                // sweep below re-registers them.
                entries.Clear();
            }
        }

        var present = new HashSet<string>(
            System.IO.Directory.EnumerateFiles(Directory, "*.svg").Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        // Dropped: the manifest names a drawing that is not there.
        List<Guid> pruned = [.. entries
            .Where(entry => !present.Contains(entry.ImageFileName))
            .Select(entry => entry.Id)];

        entries.RemoveAll(entry => pruned.Contains(entry.Id));
        Pruned = pruned;

        // Recovered: a drawing is there that the manifest does not name.
        var known = entries.Select(entry => entry.ImageFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string file in present.Where(file => !known.Contains(file)).Order(StringComparer.Ordinal))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file), out Guid id))
            {
                continue;
            }

            entries.Add(new CustomCharmEntry
            {
                Id = id,
                Name = "Recovered charm",
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds()),
                ImageFileName = file,
                Metrics = Hangly.Core.Models.CharmMetrics.Default,
                Palette = Hangly.Core.Models.CharmCatalog.Find(Hangly.Core.Models.CharmCatalog.DefaultId).Palette,
            });

            Recovered++;
        }

        entries.Sort((left, right) => left.CreatedAt.CompareTo(right.CreatedAt));

        if (pruned.Count > 0 || Recovered > 0)
        {
            Save();
        }
    }

    private void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        string manifest = Path.Combine(Directory, ManifestName);
        string temporary = manifest + ".tmp";

        // Written through a temporary file and moved over the real one, so a crash
        // mid-write cannot leave a half-manifest — the same discipline the settings
        // document uses.
        File.WriteAllText(temporary, JsonSerializer.Serialize(entries, AppSettings.JsonOptions));
        File.Move(temporary, manifest, overwrite: true);
    }
}
