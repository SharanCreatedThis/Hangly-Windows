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
        Assert.Equal(RopeStyleTable.FirstRun, settings.Overlay.RopeStyle);
    }

    /// <summary>
    /// What somebody meets on a brand-new install, pinned.
    /// </summary>
    /// <remarks>
    /// These are a product decision rather than a physical constant, and the reason they
    /// are asserted is that they are invisible from inside the code: nothing fails if one
    /// of them drifts, and nobody notices until an install looks wrong. Changing one is
    /// fine; changing one by accident is what this is here to stop.
    /// </remarks>
    [Fact(DisplayName = "A new install hangs a spider on a spider thread, near the right")]
    public void FirstInstallDefaults()
    {
        AppSettings settings = AppSettings.FromJson("{}", out _);
        OverlaySettings overlay = settings.Overlay;

        Assert.Equal(["spiderMan"], overlay.CharmIds);
        Assert.Equal(RopeStyle.SpiderThread, overlay.RopeStyle);
        Assert.Equal(OverlayAnchor.TopTrailing, overlay.Anchor);
        Assert.Equal(1.0, overlay.RopeLength);
        Assert.Equal(1.8, overlay.CharmSize);

        // From an empty document rather than from no document: a file that says "{}" has
        // been written by something, so the position still migrates from the anchor.
        // AppSettings.Defaults is where the new-install 0.89 lives.
        Assert.Null(overlay.HorizontalPosition);
        Assert.Equal(0.85, AppSettings.Defaults.Overlay.Position);

        // The fallback is a different question from the first-run charm, and stays the
        // plain bead: a deleted import must not silently become somebody else's charm.
        Assert.Equal("circle", CharmCatalog.DefaultId);

        // Clamping must not reject what the app ships with.
        Assert.Equal(overlay, overlay.Clamped());
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
        Assert.Equal(1.8, settings.Overlay.CharmSize);
        Assert.True(settings.Overlay.IsEnabled);
    }

    [Fact(DisplayName = "Keys from a newer build are ignored, not fatal")]
    public void UnknownKeysAreIgnored()
    {
        AppSettings settings = AppSettings.FromJson(
            """{"overlay":{"opacity":0.4,"unknownSetting":"storm"},"futureThing":42}""",
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
                DisplayId = @"\\?\DISPLAY#GSM7714#5&4",
                DisplayName = "LG ULTRAWIDE",
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

        // Defaults rather than `new AppSettings()`: a reset is a new install, and a new
        // install gets a position rather than inheriting one from the old anchor.
        Assert.Equal(AppSettings.Defaults, store.Settings);
    }

    /// <summary>
    /// The Appearance page's "Restore defaults" writes this, and it has to be the whole
    /// rope. It used to keep the charms, which meant three charms survived a restore and
    /// the button had not done what it said.
    /// </summary>
    [Fact(DisplayName = "Restoring defaults puts the charms back too, not just the cord")]
    public void RestoringDefaultsResetsTheWholeRope()
    {
        var store = new SettingsStore(Path_);
        store.UpdateOverlay(overlay => overlay with
        {
            CharmIds = ["hamsa", "daruma", "nazar"],
            CharmCount = 3,
            RopeStyle = RopeStyle.Neon,
            CharmSize = 0.6,
            RopeLength = 1.9,
            HorizontalPosition = 0.1,
        });

        store.UpdateOverlay(_ => AppSettings.Defaults.Overlay);

        Assert.Equal(AppSettings.Defaults.Overlay, store.Settings.Overlay);
        Assert.Equal(["spiderMan"], store.Settings.Overlay.CharmIds);
        Assert.Equal(1, store.Settings.Overlay.Stack.Count);
        Assert.Equal(0.85, store.Settings.Overlay.Position);
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
        Assert.Equal(AppSettings.Defaults, new SettingsStore(Path_).Settings);

    /// <summary>
    /// The generated record equality compared the charm list by reference, so two
    /// documents naming the same charms were unequal and the store's "did anything
    /// change" guard never held. Every read raised a change and rewrote the file.
    /// </summary>
    [Fact(DisplayName = "Two documents naming the same charms are the same document")]
    public void CharmListsCompareByValue()
    {
        var first = new OverlaySettings { CharmIds = ["nazar", "hamsa"] };
        var second = new OverlaySettings { CharmIds = ["nazar", "hamsa"] };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, first with { CharmIds = ["hamsa", "nazar"] });
    }

    [Fact(DisplayName = "An unknown charm becomes the bead rather than an empty rope")]
    public void UnknownCharmsAreReplaced()
    {
        OverlaySettings clamped = new OverlaySettings { CharmIds = ["nazar", "not-a-charm"] }.Clamped();
        Assert.Equal(["nazar", CharmCatalog.DefaultId], clamped.CharmIds);

        Assert.Equal([CharmCatalog.DefaultId], new OverlaySettings { CharmIds = [] }.Clamped().CharmIds);
    }

    [Fact(DisplayName = "A cord carries no more charms than the stack allows")]
    public void CharmCountIsCapped()
    {
        OverlaySettings clamped = new OverlaySettings
        {
            CharmIds = ["nazar", "hamsa", "daruma", "ghanta"],
        }.Clamped();

        Assert.Equal(CharmStack.MaximumCount, clamped.CharmIds.Count);
    }

    [Fact(DisplayName = "Charms survive a round trip through the file")]
    public void CharmsRoundTrip()
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsStore(Path_);
        store.UpdateOverlay(overlay => overlay with { CharmIds = ["hamsa", "daruma"] });

        Assert.Equal(["hamsa", "daruma"], new SettingsStore(Path_).Settings.Overlay.CharmIds);
    }
}

/// <summary>
/// Where the charm hangs, and what happens to a document written before it was a number.
/// </summary>
public class OverlayPositionTests
{
    /// <summary>
    /// Inset from the edge, not on it. The fraction places the rope rather than the
    /// window, so 1 hangs the charm half off the display — which is not what anybody
    /// meant by "Top Right", and is what a laptop showed on its first launch after
    /// updating.
    /// </summary>
    [Theory(DisplayName = "An old anchor becomes the position it meant, inset from the edge")]
    [InlineData("TopLeading", 0.15)]
    [InlineData("TopCenter", 0.5)]
    [InlineData("TopTrailing", 0.85)]
    public void AnchorsMigrate(string anchor, double expected)
    {
        AppSettings settings = AppSettings.FromJson(
            $"{{\"overlay\":{{\"anchor\":\"{anchor}\"}}}}",
            out bool recovered);

        Assert.False(recovered);
        Assert.Equal(expected, settings.Overlay.Position);

        // The anchor is still there, so a file taken back to an older build still works.
        Assert.Equal(anchor, settings.Overlay.Anchor.ToString());
    }

    [Fact(DisplayName = "A stored position wins over the anchor it replaced")]
    public void StoredPositionWins()
    {
        AppSettings settings = AppSettings.FromJson(
            """{"overlay":{"anchor":"TopLeading","horizontalPosition":0.82}}""",
            out _);

        Assert.Equal(0.82, settings.Overlay.Position);
    }

    [Fact(DisplayName = "A position out of range is brought back, not discarded")]
    public void PositionIsClamped()
    {
        OverlaySettings low = new OverlaySettings { HorizontalPosition = -3 }.Clamped();
        OverlaySettings high = new OverlaySettings { HorizontalPosition = 9 }.Clamped();

        Assert.Equal(0, low.Position);
        Assert.Equal(1, high.Position);
    }

    [Fact(DisplayName = "A document with neither still hangs where the default says")]
    public void DefaultIsTheDefaultAnchor()
    {
        AppSettings settings = AppSettings.FromJson("{}", out _);

        // Top right inset from the edge. A brand-new install gets 0.85 from
        // AppSettings.Defaults; an empty document is a file somebody wrote, so it takes
        // the same number by way of the anchor rather than by way of the defaults.
        Assert.Equal(0.85, settings.Overlay.Position);
        Assert.Null(settings.Overlay.HorizontalPosition);
    }
}
