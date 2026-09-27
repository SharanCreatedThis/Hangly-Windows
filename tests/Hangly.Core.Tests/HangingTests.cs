using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Hanging a charm is one act with one outcome, whichever button did it.</summary>
public class HangingTests
{
    private static AppSettings Rope(params string[] ids) => new AppSettings() with
    {
        Overlay = new OverlaySettings().WithStack(CharmStackState.Of(ids)),
    };

    [Fact]
    public void HangingChangesThePlaceRemembersAndCountsInOneWrite()
    {
        AppSettings after = Hanging.Hang(Rope("nazar", "daruma"), 1, "hamsa");

        Assert.Equal(["nazar", "hamsa"], after.Overlay.Stack.Ids);
        Assert.Equal("hamsa", after.Library.RecentCharmIds[0]);
        Assert.Equal(1, after.Milestones.CharmsHung);
    }

    [Fact]
    public void ChoosingTheCharmAlreadyThereIsNotAHangButStillMovesItUpRecent()
    {
        AppSettings before = Rope("nazar") with { Library = new LibrarySettings { RecentCharmIds = ["daruma", "nazar"] } };

        AppSettings after = Hanging.Hang(before, 0, "nazar");

        Assert.Equal(0, after.Milestones.CharmsHung);
        Assert.Equal(["nazar", "daruma"], after.Library.RecentCharmIds);
    }

    [Fact]
    public void APlaceKeepsItsSizeWhenItsCharmChanges()
    {
        AppSettings before = Rope("nazar", "daruma");
        before = before with { Overlay = before.Overlay.WithStack(before.Overlay.Stack.WithSize(1, 1.4)) };

        AppSettings after = Hanging.Hang(before, 1, "hamsa");

        Assert.Equal(1.4, after.Overlay.Stack.SizeAt(1));
    }

    [Fact]
    public void AnOutOfRangePlaceIsClampedRatherThanIgnored()
    {
        AppSettings after = Hanging.Hang(Rope("nazar", "daruma"), 9, "hamsa");

        Assert.Equal(["nazar", "hamsa"], after.Overlay.Stack.Ids);
    }

    [Fact]
    public void TheTrayHangsOneCharmAloneAndCountsOnlyAChange()
    {
        AppSettings once = Hanging.HangAlone(Rope("nazar", "daruma"), "hamsa");
        AppSettings again = Hanging.HangAlone(once, "hamsa");

        Assert.Equal(["hamsa"], once.Overlay.Stack.Ids);
        Assert.Equal("hamsa", once.Library.RecentCharmIds[0]);
        Assert.Equal(1, once.Milestones.CharmsHung);
        Assert.Equal(1, again.Milestones.CharmsHung);
    }

    [Fact]
    public void ACollectionCharmBringsItsCordOnlyOverTheShippedRope()
    {
        string dc = CharmCatalog.All.First(entry => entry.CategoryId == "dc").Id;
        AppSettings shipped = Rope("nazar") with { Overlay = Rope("nazar").Overlay with { RopeStyle = RopeStyleTable.FirstRun } };
        AppSettings chosen = shipped with { Overlay = shipped.Overlay with { RopeStyle = RopeStyle.Leather } };

        Assert.Equal(RopeStyle.MidnightCord, Hanging.Hang(shipped, 0, dc).Overlay.RopeStyle);
        Assert.Equal(RopeStyle.Leather, Hanging.Hang(chosen, 0, dc).Overlay.RopeStyle);

        // A charm from no collection, and the tray's "that one alone", leave the rope alone.
        Assert.Equal(RopeStyleTable.FirstRun, Hanging.Hang(shipped, 0, "daruma").Overlay.RopeStyle);
        Assert.Equal(RopeStyleTable.FirstRun, Hanging.HangAlone(shipped, dc).Overlay.RopeStyle);
    }

    [Fact]
    public void EveryCollectionHasTheMacCord()
    {
        Assert.All(CharmCatalog.Collections, collection => Assert.NotNull(CollectionRopes.For(collection.Id)));
        Assert.Equal(RopeStyle.SpiderThread, CollectionRopes.For("marvel"));
        Assert.Equal(RopeStyle.Neon, CollectionRopes.For("strangerThings"));
    }

    [Fact]
    public void FavouriteRopesToggleAndSurviveARoundTrip()
    {
        LibrarySettings starred = new LibrarySettings().WithFavouriteRopeToggled(RopeStyle.Neon).WithFavouriteRopeToggled(RopeStyle.Leather);
        LibrarySettings unstarred = starred.WithFavouriteRopeToggled(RopeStyle.Neon);

        Assert.Equal([RopeStyle.Neon, RopeStyle.Leather], starred.FavouriteRopes);
        Assert.Equal([RopeStyle.Leather], unstarred.FavouriteRopes);

        var settings = new AppSettings { Library = starred };
        string json = System.Text.Json.JsonSerializer.Serialize(settings, AppSettings.JsonOptions);
        Assert.Contains("\"favouriteRopes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Neon\"", json, StringComparison.Ordinal);
        Assert.Equal(starred, System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions)!.Library);
    }
}
