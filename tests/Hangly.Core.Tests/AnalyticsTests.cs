//
//  AnalyticsTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Analytics;
using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// PRIVACY.md is a published promise, so these are not tests of an implementation
/// detail — each one is a sentence from that document, asserted against what a recording
/// provider actually received.
/// </summary>
public class AnalyticsTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "hangly-analytics-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingAnalyticsProvider provider = new();

    private SettingsStore NewStore()
    {
        Directory.CreateDirectory(directory);
        return new SettingsStore(Path.Combine(directory, "settings.json"));
    }

    /// <summary>A store that already holds these settings, for the identity tests.</summary>
    private SettingsStore NewStore(AppSettings initial)
    {
        Directory.CreateDirectory(directory);
        return new SettingsStore(Path.Combine(directory, "settings.json"), initial);
    }

    private AnalyticsManager NewManager(SettingsStore store, bool hasDestination = true) =>
        new(store, provider, "eu.i.posthog.com", hasDestination, "2.0.0", "1", "10.0.26200");

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "A first launch says so, and says hello, exactly once each")]
    public void FirstLaunchIsReportedOnce()
    {
        SettingsStore store = NewStore();
        NewManager(store).Start();

        Assert.Equal(
            ["app_first_launch", "app_launch"],
            provider.Captured.Select(e => e.Name));
    }

    [Fact(DisplayName = "A later launch is not a first launch")]
    public void SecondLaunchIsOrdinary()
    {
        SettingsStore store = NewStore();
        NewManager(store).Start();
        provider.Captured.ToList().Clear();

        var second = new RecordingAnalyticsProvider();
        new AnalyticsManager(store, second, "eu.i.posthog.com", true, "2.0.0", "1", "10.0.26200").Start();

        Assert.Equal(["app_launch"], second.Captured.Select(e => e.Name));
    }

    /// <summary>
    /// Two app_launch events from one launch is not a rounding error; it is every
    /// per-launch number doubled.
    /// </summary>
    [Fact(DisplayName = "Starting twice does not send twice")]
    public void StartIsIdempotent()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        manager.Start();
        manager.Start();
        manager.Start();

        Assert.Equal(2, provider.Captured.Count);
        Assert.Equal(1, provider.StartCount);
    }

    [Fact(DisplayName = "Nothing is sent while the app simply sits there")]
    public void IdleSendsNothing()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        manager.Start();
        int afterLaunch = provider.Captured.Count;

        // Everything the inspector reads, read repeatedly. None of it is an event.
        for (int i = 0; i < 50; i++)
        {
            _ = manager.IsEnabled;
            _ = manager.MaskedIdentifier;
            _ = manager.Connection;
            _ = manager.SentCount;
            _ = manager.Endpoint;
        }

        Assert.Equal(afterLaunch, provider.Captured.Count);
    }

    [Fact(DisplayName = "Switched off, nothing is captured at all")]
    public void DisabledCapturesNothing()
    {
        SettingsStore store = NewStore();
        store.Update(s => s with { Privacy = s.Privacy with { AnalyticsEnabled = false } });

        AnalyticsManager manager = NewManager(store);
        manager.Start();
        manager.Track(Events.CharmSelected("nazar"));
        manager.Stop();

        Assert.Empty(provider.Captured);
    }

    [Fact(DisplayName = "The launch is still counted when sharing is off")]
    public void LaunchIsCountedRegardless()
    {
        SettingsStore store = NewStore();
        store.Update(s => s with { Privacy = s.Privacy with { AnalyticsEnabled = false } });

        NewManager(store).Start();

        Assert.Equal(1, store.Settings.Milestones.LaunchCount);
        Assert.Empty(provider.Captured);
    }

    [Fact(DisplayName = "Switching off discards the identifier, as the promise says")]
    public void TurningOffForgetsTheIdentifier()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        manager.Start();

        Assert.NotNull(store.Settings.Privacy.AnonymousId);
        Guid first = store.Settings.Privacy.AnonymousId!.Value;

        manager.SetEnabled(false);
        Assert.Null(store.Settings.Privacy.AnonymousId);
        Assert.False(provider.IsEnabled);
        Assert.Equal(0, manager.SentCount);

        // Back on mints a new one, so the two cannot be joined.
        manager.SetEnabled(true);
        Assert.NotNull(store.Settings.Privacy.AnonymousId);
        Assert.NotEqual(first, store.Settings.Privacy.AnonymousId!.Value);
    }

    [Fact(DisplayName = "Switching off stops capture immediately, not eventually")]
    public void TurningOffStopsCaptureAtOnce()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        manager.Start();
        manager.SetEnabled(false);
        int afterOff = provider.Captured.Count;

        manager.Track(Events.CharmSelected("hamsa"));
        manager.Track(Events.AppQuit);

        Assert.Equal(afterOff, provider.Captured.Count);
    }

    [Fact(DisplayName = "The identifier is masked, and is not the identifier")]
    public void IdentifierIsMasked()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        Assert.Null(manager.MaskedIdentifier);

        manager.Start();
        string masked = Assert.IsType<string>(manager.MaskedIdentifier);
        string full = store.Settings.Privacy.AnonymousId!.Value.ToString("D");

        Assert.Contains("••••", masked, StringComparison.Ordinal);
        Assert.DoesNotContain(full, masked, StringComparison.Ordinal);
        Assert.StartsWith(full[..8], masked, StringComparison.Ordinal);
    }

    /// <summary>
    /// "Charms you make. They are reported as the word custom." — PRIVACY.md.
    /// </summary>
    [Fact(DisplayName = "A charm somebody made is reported as the word custom")]
    public void CustomCharmsAreNotNamed()
    {
        Assert.Equal("nazar", Events.NameOf("nazar"));
        Assert.Equal("custom", Events.NameOf("a-file-on-one-persons-disk"));

        AnalyticsEvent selected = Events.CharmSelected("some-imported-thing");
        Assert.Equal(AnalyticsValue.Of("custom"), selected.Properties["charm"]);
    }

    [Fact(DisplayName = "Every event carries what is on the rope, and nothing describing the desktop")]
    public void EventsCarryTheRopeAndNotTheScreen()
    {
        SettingsStore store = NewStore();
        store.UpdateOverlay(o => o with
        {
            CharmIds = ["nazar", "hamsa"],
            RopeStyle = RopeStyle.GoldChain,
            OffsetX = 123,
            OffsetY = -45,
            Anchor = OverlayAnchor.TopTrailing,
            DisplayIndex = 2,
        });

        AnalyticsManager manager = NewManager(store);
        manager.Start();
        AnalyticsEvent sent = provider.Captured[^1];

        Assert.Equal(AnalyticsValue.Of(2), sent.Properties["charm_count"]);
        Assert.Equal(AnalyticsValue.Of("GoldChain"), sent.Properties["rope_style"]);
        Assert.Equal(
            AnalyticsValue.Of((IReadOnlyList<string>)["nazar", "hamsa"]),
            sent.Properties["active_charm_ids"]);

        // Where the charm sits, how large it is, which display it is on: never sent.
        foreach (string forbidden in (string[])
            ["offsetX", "offsetY", "offset_x", "offset_y", "anchor", "display", "displayIndex", "display_index"])
        {
            Assert.DoesNotContain(forbidden, sent.Properties.Keys, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "A setting change reports the setting's name, never its value")]
    public void AppearanceReportsNamesNotValues()
    {
        AnalyticsEvent changed = Events.AppearanceChanged("charm_size");
        Assert.Equal(AnalyticsValue.Of("charm_size"), changed.Properties["setting"]);
        Assert.Single(changed.Properties);
    }

    [Fact(DisplayName = "A dropped file is a type and a bucket, never a name or a size")]
    public void DroppedFilesAreNotDescribed()
    {
        AnalyticsEvent dropped = Events.AirdropFileDropped(".PNG", 5_000_000);

        Assert.Equal(AnalyticsValue.Of("png"), dropped.Properties["fileType"]);
        Assert.Equal(AnalyticsValue.Of("1-10MB"), dropped.Properties["fileSizeBucket"]);
        Assert.Equal(2, dropped.Properties.Count);
    }

    [Theory(DisplayName = "File sizes become buckets")]
    [InlineData(-1, "unknown")]
    [InlineData(999_999, "<1MB")]
    [InlineData(9_999_999, "1-10MB")]
    [InlineData(99_999_999, "10-100MB")]
    [InlineData(999_999_999, "100MB-1GB")]
    [InlineData(1_000_000_000, ">1GB")]
    public void SizeBuckets(long bytes, string expected) =>
        Assert.Equal(expected, Events.FileSizeBucket(bytes));

    [Fact(DisplayName = "With no destination configured the app still runs, and says so")]
    public void NoDestinationIsAValidState()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store, hasDestination: false);
        manager.Start();

        Assert.Equal(AnalyticsConnection.NoDestination, manager.Connection);
        Assert.Equal("none", manager.Endpoint);
    }

    [Fact(DisplayName = "Quitting says goodbye once and flushes")]
    public void StopFlushes()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        manager.Start();
        manager.Stop();

        Assert.Equal("app_quit", provider.Captured[^1].Name);
        Assert.Equal(1, provider.FlushCount);
    }

    /// <summary>
    /// The names are the macOS build's names. Two platforms reporting the same act under
    /// different names produce two datasets that cannot be added together.
    /// </summary>
    [Fact(DisplayName = "The vocabulary is exactly the macOS vocabulary")]
    public void VocabularyMatchesMacOS()
    {
        string[] expected =
        [
            "airdrop_drag_entered", "airdrop_file_dropped", "airdrop_picker_opened",
            "app_first_launch", "app_launch", "app_quit",
            "appearance_changed", "charm_added", "charm_imported", "charm_removed",
            "charm_reordered", "charm_saved", "charm_selected",
            "coffee_copy_upi", "coffee_qr_viewed", "coffee_sheet_opened",
            "collection_charm_selected", "collection_opened",
            "follow_instagram_clicked", "follow_popup_dismissed", "follow_popup_follow_clicked",
            "follow_popup_maybe_later", "follow_popup_shown",
            "rope_count_changed", "rope_style_changed",
        ];

        string[] actual =
        [
            Events.AirdropDragEntered.Name,
            Events.AirdropFileDropped(".png", 1).Name,
            Events.AirdropPickerOpened.Name,
            Events.AppFirstLaunch.Name,
            Events.AppLaunch.Name,
            Events.AppQuit.Name,
            Events.AppearanceChanged("x").Name,
            Events.CharmAdded("nazar").Name,
            Events.CharmImported.Name,
            Events.CharmRemoved("nazar").Name,
            Events.CharmReordered(0, 1).Name,
            Events.CharmSaved.Name,
            Events.CharmSelected("nazar").Name,
            Events.CoffeeCopyUpi("about").Name,
            Events.CoffeeQrViewed("about").Name,
            Events.CoffeeSheetOpened("about").Name,
            Events.CollectionCharmSelected("Marvel", "Spider-Man").Name,
            Events.CollectionOpened("Marvel").Name,
            Events.FollowInstagramClicked.Name,
            Events.FollowPopupDismissed.Name,
            Events.FollowPopupFollowClicked.Name,
            Events.FollowPopupMaybeLater.Name,
            Events.FollowPopupShown.Name,
            Events.RopeCountChanged(2).Name,
            Events.RopeStyleChanged(RopeStyle.Thread).Name,
        ];

        Assert.Equal(expected, actual.Order().ToArray());
    }

    /// <summary>The identity properties, and the promise that nothing else joins them.</summary>
    [Fact(DisplayName = "Every event carries the name, the platform and the architecture")]
    public void IdentityTravelsWithEveryEvent()
    {
        var store = NewStore(new AppSettings
        {
            DisplayName = "Sharan",
            Privacy = new PrivacySettings { AnalyticsEnabled = true },
        });

        AnalyticsManager manager = NewManager(store);
        manager.Start();
        manager.Track(Events.CharmSelected("nazar"));

        AnalyticsEvent last = provider.Captured[^1];
        Assert.Equal("Sharan", ((AnalyticsValue.Text)last.Properties["user_name"]).Value);

        Assert.NotNull(provider.SuperProperties);
        Assert.Equal("windows", ((AnalyticsValue.Text)provider.SuperProperties!["platform"]).Value);
        Assert.True(provider.SuperProperties.ContainsKey("architecture"));
        Assert.True(provider.SuperProperties.ContainsKey("os_version"));
        Assert.True(provider.SuperProperties.ContainsKey("app_version"));

        Assert.NotNull(provider.PersonProperties);
        Assert.Equal("Sharan", ((AnalyticsValue.Text)provider.PersonProperties!["user_name"]).Value);
    }

    /// <summary>A corrected name reaches the project without a relaunch.</summary>
    [Fact(DisplayName = "Renaming yourself renames you on the next event")]
    public void RenamingTakesEffectImmediately()
    {
        var store = NewStore(new AppSettings
        {
            DisplayName = "Old",
            Privacy = new PrivacySettings { AnalyticsEnabled = true },
        });

        AnalyticsManager manager = NewManager(store);
        manager.Start();

        store.Update(settings => settings with { DisplayName = "New" });
        manager.Track(Events.CharmSelected("nazar"));

        Assert.Equal("New", ((AnalyticsValue.Text)provider.Captured[^1].Properties["user_name"]).Value);
        Assert.Equal("New", ((AnalyticsValue.Text)provider.PersonProperties!["user_name"]).Value);
    }

    /// <summary>The name is the only personal thing, and it is never taken from the machine.</summary>
    [Fact(DisplayName = "No event carries anything read off the machine")]
    public void NothingIsTakenFromTheMachine()
    {
        // A name that cannot coincide with this machine's own. Seeding it with something
        // plausible made this pass or fail depending on whose account ran the suite --
        // the first run failed because the tester's Windows account is also called
        // Sharan, and a typed name that happens to match is not a leak.
        var store = NewStore(new AppSettings
        {
            DisplayName = "Zephyr Quill",
            Privacy = new PrivacySettings { AnalyticsEnabled = true },
        });

        AnalyticsManager manager = NewManager(store);
        manager.Start();
        manager.Track(Events.AirdropFileDropped(".png", 4096));
        manager.Track(Events.CharmImported);

        string[] forbidden =
        [
            Environment.UserName,
            Environment.MachineName,
            Environment.UserDomainName,
        ];

        foreach (AnalyticsEvent captured in provider.Captured)
        {
            foreach ((string key, AnalyticsValue value) in captured.Properties)
            {
                if (value is not AnalyticsValue.Text text)
                {
                    continue;
                }

                foreach (string secret in forbidden.Where(s => !string.IsNullOrEmpty(s)))
                {
                    Assert.False(
                        text.Value.Contains(secret, StringComparison.OrdinalIgnoreCase),
                        $"'{key}' carried something read off the machine");
                }

                // And no path separators, which is the shape a file path would have.
                Assert.DoesNotContain(@":\", text.Value, StringComparison.Ordinal);
            }
        }
    }
}
