//
//  SettingsTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// The settings document: what a file written by another build does to this one.
/// </summary>
public class SettingsCodingTests
{
    [Fact(DisplayName = "An empty document yields the defaults")]
    public void EmptyDocumentIsDefaults()
    {
        AppSettings settings = AppSettings.FromJson("{}", out bool recovered);

        Assert.False(recovered);
        Assert.Equal(new AppSettings(), settings);
        Assert.Equal(RopeStyleTable.Shipped, settings.Overlay.RopeStyle);
    }

    [Fact(DisplayName = "A partial document keeps the defaults for what it omits")]
    public void PartialDocumentFallsBackFieldByField()
    {
        AppSettings settings = AppSettings.FromJson(
            """{"overlay":{"opacity":0.5}}""",
            out bool recovered);

        Assert.False(recovered);
        Assert.Equal(0.5, settings.Overlay.Opacity);

        // A new field in a future release must not discard every existing preference.
        Assert.Equal(1.0, settings.Overlay.CharmSize);
        Assert.True(settings.Overlay.IsEnabled);
    }

    [Fact(DisplayName = "Keys from a newer build are ignored, not fatal")]
    public void UnknownKeysAreIgnored()
    {
        AppSettings settings = AppSettings.FromJson(
            """{"overlay":{"opacity":0.4,"weatherMood":"storm"},"futureThing":42}""",
            out bool recovered);

        Assert.False(recovered);
        Assert.Equal(0.4, settings.Overlay.Opacity);
    }

    [Fact(DisplayName = "Out-of-range values are clamped rather than honoured")]
    public void ValuesAreClamped()
    {
        AppSettings settings = AppSettings.FromJson(
            """{"overlay":{"opacity":99,"charmSize":-4,"ropeLength":1000}}""",
            out _);

        Assert.Equal(1.0, settings.Overlay.Opacity);
        Assert.Equal(0.5, settings.Overlay.CharmSize);
        Assert.Equal(2.0, settings.Overlay.RopeLength);
    }

    [Fact(DisplayName = "A corrupt document is replaced rather than blocking launch")]
    public void CorruptDocumentRecovers()
    {
        AppSettings settings = AppSettings.FromJson("{ not json at all", out bool recovered);

        Assert.True(recovered);
        Assert.Equal(new AppSettings(), settings);
    }

    [Fact(DisplayName = "A round trip is lossless")]
    public void RoundTripIsLossless()
    {
        var original = new AppSettings
        {
            LaunchAtLogin = true,
            HasSeenWelcome = true,
            Overlay = new OverlaySettings
            {
                Opacity = 0.75,
                CharmSize = 1.25,
                RopeLength = 0.8,
                Anchor = OverlayAnchor.TopTrailing,
                RopeStyle = RopeStyle.GoldChain,
                OffsetX = -120,
                DisplayIndex = 2,
            },
        };

        AppSettings decoded = AppSettings.FromJson(original.ToJson(), out bool recovered);

        Assert.False(recovered);
        Assert.Equal(original, decoded);
    }

    [Fact(DisplayName = "Enums are written by name, not by ordinal")]
    public void EnumsSurviveAReorderedTable()
    {
        string json = new AppSettings
        {
            Overlay = new OverlaySettings { RopeStyle = RopeStyle.MidnightCord },
        }.ToJson();

        // Inserting a style in the middle of the enum must not re-point an existing
        // user's cord at a different one.
        Assert.Contains("MidnightCord", json, StringComparison.Ordinal);
    }
}

/// <summary>The real store against a throwaway directory.</summary>
public class SettingsStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "hangly-tests-" + Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(directory, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "A change survives a new store over the same file")]
    public void ChangesPersist()
    {
        var store = new SettingsStore(Path_);
        store.UpdateOverlay(overlay => overlay with { Opacity = 0.6 });

        Assert.Equal(0.6, new SettingsStore(Path_).Settings.Overlay.Opacity);
    }

    [Fact(DisplayName = "A redundant write notifies nobody")]
    public void RedundantWritesAreGuarded()
    {
        var store = new SettingsStore(Path_);
        int notifications = 0;
        store.Changed += _ => notifications += 1;

        store.UpdateOverlay(overlay => overlay with { Opacity = 0.6 });
        store.UpdateOverlay(overlay => overlay with { Opacity = 0.6 });

        Assert.Equal(1, notifications);
    }

    [Fact(DisplayName = "Reset returns everything to how it shipped")]
    public void ResetRestoresDefaults()
    {
        var store = new SettingsStore(Path_);
        store.UpdateOverlay(overlay => overlay with { Opacity = 0.3, RopeStyle = RopeStyle.Neon });

        store.Reset();

        Assert.Equal(new AppSettings(), store.Settings);
    }

    [Fact(DisplayName = "A corrupt file on disk is recovered from, not fatal")]
    public void CorruptFileRecovers()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path_, "{{{ broken");

        var store = new SettingsStore(Path_);

        Assert.Equal(new AppSettings(), store.Settings);
    }

    [Fact(DisplayName = "A missing file is not an error")]
    public void MissingFileIsDefaults() =>
        Assert.Equal(new AppSettings(), new SettingsStore(Path_).Settings);
}
