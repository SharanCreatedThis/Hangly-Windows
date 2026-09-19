//
//  SettingsStore.cs
//  Hangly
//
//  The one write path for everything a person has chosen.
//

namespace Hangly.Core.Settings;

/// <summary>Holds the settings document and persists every change to it.</summary>
/// <remarks>
/// <b>One write path.</b> Every settings change — a tray menu toggle, a slider, a
/// programmatic correction at launch — goes through <see cref="Update"/> and therefore
/// through one setter. There is no <c>Save()</c> to forget. The equality guard means a
/// no-op assignment writes nothing, which matters because the overlay's own reactions
/// are driven off <see cref="Changed"/>.
///
/// <para>The file path is injected rather than discovered, which is what lets the tests
/// run the real store against a throwaway directory instead of the user's profile.</para>
///
/// <para>The macOS original keeps this document in <c>UserDefaults</c>. Windows has no
/// equivalent worth using — the registry is the wrong shape for a nested document and
/// roaming app data is the wrong lifetime — so it is one JSON file under
/// <c>%LOCALAPPDATA%</c>, written atomically through a temporary file so that a crash
/// mid-write cannot leave a half-document behind.</para>
/// </remarks>
public sealed class SettingsStore
{
    private readonly string path;
    private AppSettings storage;

    public SettingsStore(string path, AppSettings? initial = null)
    {
        this.path = path;
        storage = initial ?? Load(path, out _);
    }

    /// <summary>Raised after a change has been persisted, with the settled value.</summary>
    public event Action<AppSettings>? Changed;

    /// <summary>Whether the document on disk was unreadable and had to be replaced.</summary>
    public bool WasRecovered { get; private set; }

    /// <summary>The default location: one file per user, alongside the app's own data.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hangly",
        "settings.json");

    public AppSettings Settings
    {
        get => storage;
        private set
        {
            if (value == storage)
            {
                return;
            }

            storage = value;
            Persist(value);
            Changed?.Invoke(value);
        }
    }

    /// <summary>Applies a change and persists it. The only way to write a setting.</summary>
    public void Update(Func<AppSettings, AppSettings> change) => Settings = change(storage).Clamped();

    /// <summary>Applies a change to the overlay half of the document.</summary>
    public void UpdateOverlay(Func<OverlaySettings, OverlaySettings> change) =>
        Update(settings => settings with { Overlay = change(settings.Overlay) });

    /// <summary>Returns everything to how it shipped.</summary>
    public void Reset() => Settings = new AppSettings();

    private static AppSettings Load(string path, out bool wasRecovered)
    {
        wasRecovered = false;
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            return AppSettings.FromJson(File.ReadAllText(path), out wasRecovered);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable file is not a reason to refuse to launch. The rope is an
            // ornament; it hangs on the defaults and says so.
            wasRecovered = true;
            return new AppSettings();
        }
    }

    private void Persist(AppSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Written beside the real file and moved over it, so a crash or a full disk
            // mid-write leaves the previous document intact rather than a truncated one.
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, settings.ToJson());
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Losing a preference is worse than a dropped write, but crashing the app a
            // person is not looking at is worse than both.
            WasRecovered = true;
        }
    }
}
