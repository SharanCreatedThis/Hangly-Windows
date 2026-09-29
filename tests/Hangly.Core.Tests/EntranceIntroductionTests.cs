using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The Spider-Man introduction: the shipped look for two launches, then theirs. The same cases as macOS's <c>EntranceIntroductionTests</c>.</summary>
public sealed class EntranceIntroductionTests
{
    private static AppSettings ExistingUser(double volume = 0.3, bool sound = true) => new()
    {
        HasSeenWelcome = true,
        DisplayName = "Sharan",
        SoundEffectsEnabled = sound,
        SoundVolume = volume,
        Milestones = new MilestoneSettings { LaunchCount = 42 },
        Library = new LibrarySettings { FavouriteCharmIds = ["hamsa"] },
        Overlay = new OverlaySettings
        {
            Slots = [new RopeCharm("hamsa"), new RopeCharm("nazar", 0.8)],
            CharmCount = 2,
            CharmIds = ["hamsa", "nazar"],
            RopeStyle = RopeStyle.GoldChain,
            Glow = GlowLevel.Off,
            CharmSize = 0.7,
            RopeLength = 1.3,
            Opacity = 0.6,
            HorizontalPosition = 0.2,
            OffsetY = 40,
        },
    };

    /// <summary>One launch, as the app's bootstrap runs it: advance, apply, then complete once the overlay is up.</summary>
    private static AppSettings Launch(AppSettings settings)
    {
        (settings, _) = EntranceIntroduction.Advance(settings);
        (settings, _) = EntranceIntroduction.Apply(settings);
        return EntranceIntroduction.Complete(settings);
    }

    private static void AssertShippedLook(AppSettings settings)
    {
        OverlaySettings shipped = AppSettings.Defaults.Overlay;
        Assert.Equal(["spiderMan"], settings.Overlay.Stack.Ids);
        Assert.Equal(RopeStyle.SpiderThread, settings.Overlay.RopeStyle);
        Assert.Equal(shipped.Stack.Places[^1].Size, settings.Overlay.Stack.Places[^1].Size);
        Assert.Equal(shipped.HorizontalPosition, settings.Overlay.HorizontalPosition);
        Assert.Equal(shipped.OffsetY, settings.Overlay.OffsetY);
        Assert.Equal(shipped.CharmSize, settings.Overlay.CharmSize);
        Assert.Equal(shipped.RopeLength, settings.Overlay.RopeLength);
        Assert.Equal(shipped.Opacity, settings.Overlay.Opacity);
        Assert.Equal(GlowLevel.Soft, settings.Overlay.Glow);
        Assert.True(settings.Overlay.StartupAnimation);
        Assert.True(settings.SoundEffectsEnabled);
        Assert.Equal(0.5, settings.SoundVolume);
    }

    [Fact]
    public void TheFirstTwoLaunchesAfterTheUpdateUseTheShippedLookAndSound()
    {
        AppSettings first = Launch(ExistingUser());
        AssertShippedLook(first);
        AppSettings second = Launch(first);
        AssertShippedLook(second);
        Assert.Equal(2, second.Milestones.EntranceShowcase!.Launch);
    }

    [Fact]
    public void TheThirdLaunchGivesTheirWholeSetupBack()
    {
        AppSettings before = ExistingUser();
        AppSettings third = Launch(Launch(Launch(before)));

        Assert.Null(third.Milestones.EntranceShowcase);
        Assert.Equal(["hamsa", "nazar"], third.Overlay.Stack.Ids);
        Assert.Equal([1.0, 0.8], third.Overlay.Stack.Places.Select(place => place.Size));
        Assert.Equal(before.Overlay.Stack.StoredSlots, third.Overlay.Stack.StoredSlots);
        Assert.Equal(RopeStyle.GoldChain, third.Overlay.RopeStyle);
        Assert.Equal(0.2, third.Overlay.HorizontalPosition);
        Assert.Equal(40, third.Overlay.OffsetY);
        Assert.Equal(0.7, third.Overlay.CharmSize);
        Assert.Equal(1.3, third.Overlay.RopeLength);
        Assert.Equal(0.6, third.Overlay.Opacity);
        Assert.Equal(GlowLevel.Off, third.Overlay.Glow);
        Assert.Equal(0.3, third.SoundVolume);
        // Never touched at all.
        Assert.Equal("Sharan", third.DisplayName);
        Assert.Equal(["hamsa"], third.Library.FavouriteCharmIds);
    }

    [Fact]
    public void WhatTheyChangeWhileItIsLentIsTheirsAndStays()
    {
        AppSettings second = Launch(Launch(ExistingUser()));
        second = second with
        {
            Overlay = second.Overlay.WithStack(CharmStackState.Of(["hamsa"])) with { Glow = GlowLevel.Strong, Opacity = 0.9 },
        };

        AppSettings third = Launch(second);

        Assert.Equal(["hamsa"], third.Overlay.Stack.Ids);
        Assert.Equal(GlowLevel.Strong, third.Overlay.Glow);
        Assert.Equal(0.9, third.Overlay.Opacity);
        Assert.Equal(0.7, third.Overlay.CharmSize);
        Assert.Equal(0.2, third.Overlay.HorizontalPosition);
    }

    [Fact]
    public void TheOldDefaultVolumeIsNotGivenBackButAChosenOneAndSoundOffAre()
    {
        Assert.Equal(0.5, Launch(Launch(Launch(ExistingUser(volume: EntranceIntroduction.LegacyDefaultVolume)))).SoundVolume);
        Assert.Equal(0.3, Launch(Launch(Launch(ExistingUser(volume: 0.3)))).SoundVolume);
        Assert.False(Launch(Launch(Launch(ExistingUser(sound: false)))).SoundEffectsEnabled);
    }

    [Fact]
    public void ANewInstallIsLeftAlone()
    {
        AppSettings fresh = AppSettings.Defaults;
        (AppSettings after, bool introduced) = EntranceIntroduction.Apply(fresh);
        Assert.False(introduced);
        Assert.Null(after.Milestones.EntranceShowcase);
    }

    [Fact]
    public void OnceOverNoLaterLaunchOrUpdateLendsAgain()
    {
        AppSettings settings = Launch(Launch(Launch(ExistingUser())));
        settings = settings with { Overlay = settings.Overlay.WithStack(CharmStackState.Of(["hamsa"])) };
        for (int launch = 0; launch < 3; launch++)
        {
            settings = Launch(settings);
        }

        Assert.Equal(["hamsa"], settings.Overlay.Stack.Ids);
        Assert.Null(settings.Milestones.EntranceShowcase);
    }

    [Fact]
    public void ALaunchThatFailsBeforeCompletingDoesNotLendTwice()
    {
        (AppSettings lent, bool introduced) = EntranceIntroduction.Apply(ExistingUser());
        Assert.True(introduced);
        (AppSettings again, bool twice) = EntranceIntroduction.Apply(lent);
        Assert.False(twice);
        Assert.Equal(lent.Milestones.EntranceShowcase, again.Milestones.EntranceShowcase);
        Assert.Equal(0.7, again.Milestones.EntranceShowcase!.Before.CharmSize);
    }

    [Fact]
    public void TheLentLookSurvivesBeingSavedAndRead()
    {
        AppSettings lent = Launch(ExistingUser());
        AppSettings read = AppSettings.FromJson(lent.ToJson(), out bool recovered);
        Assert.False(recovered);
        Assert.Equal(lent.Milestones.EntranceShowcase, read.Milestones.EntranceShowcase);
        Assert.Equal(0.7, Launch(Launch(read)).Overlay.CharmSize);
        Assert.Equal(RopeStyle.GoldChain, Launch(Launch(read)).Overlay.RopeStyle);
    }

    private static SettingsStore StoreWith(AppSettings settings, string path)
    {
        var store = new SettingsStore(path);
        store.Update(_ => settings);
        return store;
    }

    [Fact]
    public void ACharmOrRopeChosenWhileTheLookIsLentIsKeptEvenSpiderMan()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hangly-showcase-{Guid.NewGuid():N}.json");
        try
        {
            SettingsStore store = StoreWith(Launch(ExistingUser()), path);
            store.UpdateOverlay(overlay => overlay.WithStack(CharmStackState.Of(["spiderMan", "spiderManSwinging"])));
            Assert.True(store.Settings.Milestones.EntranceShowcase?.CharmChosen);

            AppSettings third = Launch(Launch(store.Settings));
            Assert.Equal(["spiderMan", "spiderManSwinging"], third.Overlay.Stack.Ids);
            Assert.Equal(RopeStyle.SpiderThread, third.Overlay.RopeStyle);
            Assert.Equal(0.7, third.Overlay.CharmSize);
            Assert.Equal(GlowLevel.Off, third.Overlay.Glow);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ChangingOnlyTheRopeStyleWhileLentKeepsTheRopeAsItIs()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hangly-showcase-{Guid.NewGuid():N}.json");
        try
        {
            SettingsStore store = StoreWith(Launch(ExistingUser()), path);
            store.UpdateOverlay(overlay => overlay with { RopeStyle = RopeStyle.Leather });
            AppSettings third = Launch(Launch(store.Settings));
            Assert.Equal(RopeStyle.Leather, third.Overlay.RopeStyle);
            Assert.Equal(["spiderMan"], third.Overlay.Stack.Ids);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OtherChangesWhileLentAreNotACharmChoice()
    {
        string path = Path.Combine(Path.GetTempPath(), $"hangly-showcase-{Guid.NewGuid():N}.json");
        try
        {
            SettingsStore store = StoreWith(Launch(ExistingUser()), path);
            store.UpdateOverlay(overlay => overlay with { Glow = GlowLevel.Strong });
            store.Update(settings => settings with { DisplayName = "Someone" });
            Assert.False(store.Settings.Milestones.EntranceShowcase?.CharmChosen);
            Assert.Equal(["hamsa", "nazar"], Launch(Launch(store.Settings)).Overlay.Stack.Ids);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AShowcase210BeganWithNoRopeStillReadsAndEndsThe210Way()
    {
        AppSettings lent = Launch(ExistingUser());
        EntranceShowcase showcase = lent.Milestones.EntranceShowcase!;
        // What 2.1.0 wrote: no rope in either snapshot, no CharmChosen.
        string json = lent.ToJson();
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var written = node["milestones"]!["entranceShowcase"]!.AsObject();
        written["before"]!.AsObject().Remove("rope");
        written["lent"]!.AsObject().Remove("rope");
        written.Remove("charmChosen");
        AppSettings read = AppSettings.FromJson(node.ToJsonString(), out bool recovered);
        Assert.False(recovered);
        Assert.Null(read.Milestones.EntranceShowcase!.Before.Rope);
        Assert.False(read.Milestones.EntranceShowcase.CharmChosen);

        AppSettings third = Launch(Launch(read));
        Assert.Null(third.Milestones.EntranceShowcase);
        Assert.Equal(["spiderMan"], third.Overlay.Stack.Ids);
        Assert.Equal(0.8, third.Overlay.Stack.Places[^1].Size);
        Assert.Equal(0.7, third.Overlay.CharmSize);
        Assert.NotNull(showcase.Before.Rope);
    }

    [Fact]
    public void ASnapshotThatNoLongerReadsBackGivesNoRopeBackAndTheRestStill()
    {
        AppSettings lent = Launch(ExistingUser());
        EntranceShowcase showcase = lent.Milestones.EntranceShowcase!;
        ShowcaseRope unreadable = showcase.Before.Rope! with
        {
            StoredSlots = [new RopeCharm("a-charm-this-build-does-not-have"), .. showcase.Before.Rope!.StoredSlots.Skip(1)],
        };
        lent = lent with { Milestones = lent.Milestones with { EntranceShowcase = showcase with { Before = showcase.Before with { Rope = unreadable } } } };

        AppSettings third = Launch(Launch(lent));
        Assert.Equal(["spiderMan"], third.Overlay.Stack.Ids);
        Assert.Equal(0.7, third.Overlay.CharmSize);
        Assert.Null(third.Milestones.EntranceShowcase);
    }
}
