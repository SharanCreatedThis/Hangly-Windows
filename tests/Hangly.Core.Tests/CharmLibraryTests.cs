//
//  CharmLibraryTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// Searching, filtering and the two lists the Library remembers. All of it is text and
/// set arithmetic, so none of it needs a window to be checked.
/// </summary>
public class CharmLibraryTests
{
    /// <summary>Nothing imported, so the index is exactly the catalogue.</summary>
    private static readonly CharmIndex Index = new();

    [Fact(DisplayName = "Every charm has the Library facts it is shown with")]
    public void MetadataIsComplete()
    {
        foreach (CharmCatalogEntry charm in CharmCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(charm.CategoryId), $"{charm.Id} has no category");
            Assert.False(string.IsNullOrWhiteSpace(charm.Region), $"{charm.Id} has no region");
            Assert.False(string.IsNullOrWhiteSpace(charm.Description), $"{charm.Id} has no description");
            Assert.NotEmpty(charm.Tags);
        }
    }

    [Fact(DisplayName = "Every charm's category is one the chips offer")]
    public void CategoriesResolve()
    {
        var known = CharmCatalog.Categories.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (CharmCatalogEntry charm in CharmCatalog.All)
        {
            Assert.Contains(charm.CategoryId, known);
        }
    }

    [Fact(DisplayName = "Every category has charms in it, so no chip leads nowhere")]
    public void NoEmptyCategories()
    {
        foreach (CharmCategory category in CharmCatalog.Categories)
        {
            Assert.NotEmpty(CharmSearch.Apply(Index, CharmFilter.Category(category.Id), "", [], []));
        }
    }

    /// <summary>
    /// Half the catalogue is named in languages using marks an English keyboard does not
    /// have. A search that only works for charms with plain names is not a search.
    /// </summary>
    [Theory(DisplayName = "Typing without the accents still finds the charm")]
    [InlineData("pancha", "panchangJie")]
    [InlineData("panchangjie", "panchangJie")]
    [InlineData("boncugu", "nazar")]
    [InlineData("Nazar boncuğu", "nazar")]
    [InlineData("drishti", "drishtiBommai")]
    [InlineData("maneki", "manekiNeko")]
    public void SearchFoldsAccents(string query, string expected)
    {
        IReadOnlyList<CharmCatalogEntry> found = CharmSearch.Apply(Index, CharmFilter.All, query, [], []);
        Assert.Contains(expected, found.Select(charm => charm.Id));
    }

    [Fact(DisplayName = "Search reaches the tags and the region, not only the name")]
    public void SearchReachesEverything()
    {
        Assert.Contains("nazar", CharmSearch.Apply(Index, CharmFilter.All, "glass", [], []).Select(c => c.Id));
        Assert.Contains("nazar", CharmSearch.Apply(Index, CharmFilter.All, "turkey", [], []).Select(c => c.Id));
    }

    [Fact(DisplayName = "Search is case-blind")]
    public void SearchIgnoresCase() => Assert.Equal(
        CharmSearch.Apply(Index, CharmFilter.All, "HAMSA", [], []).Select(c => c.Id),
        CharmSearch.Apply(Index, CharmFilter.All, "hamsa", [], []).Select(c => c.Id));

    [Fact(DisplayName = "An empty query is not a filter")]
    public void EmptyQueryShowsEverything()
    {
        Assert.Equal(70, CharmSearch.Apply(Index, CharmFilter.All, "", [], []).Count);
        Assert.Equal(70, CharmSearch.Apply(Index, CharmFilter.All, "   ", [], []).Count);
    }

    [Fact(DisplayName = "A query nothing matches returns nothing, rather than everything")]
    public void NoMatchesIsEmpty() =>
        Assert.Empty(CharmSearch.Apply(Index, CharmFilter.All, "zzzznotacharm", [], []));

    [Fact(DisplayName = "Favourites show only what was starred")]
    public void FavouritesFilter()
    {
        IReadOnlyList<CharmCatalogEntry> found =
            CharmSearch.Apply(Index, CharmFilter.Favourites, "", ["hamsa", "daruma"], []);

        Assert.Equal(["daruma", "hamsa"], found.Select(c => c.Id).Order());
    }

    /// <summary>Most recent first, because that order is the whole information in the list.</summary>
    [Fact(DisplayName = "Recents keep their own order")]
    public void RecentsKeepOrder()
    {
        IReadOnlyList<CharmCatalogEntry> found =
            CharmSearch.Apply(Index, CharmFilter.Recent, "", [], ["daruma", "nazar", "hamsa"]);

        Assert.Equal(["daruma", "nazar", "hamsa"], found.Select(c => c.Id));
    }

    [Fact(DisplayName = "Search narrows a filter rather than replacing it")]
    public void SearchCombinesWithFilter()
    {
        IReadOnlyList<CharmCatalogEntry> found =
            CharmSearch.Apply(Index, CharmFilter.Favourites, "hamsa", ["hamsa", "daruma"], []);

        CharmCatalogEntry only = Assert.Single(found);
        Assert.Equal("hamsa", only.Id);
    }

    [Fact(DisplayName = "Starring adds, starring again removes")]
    public void FavouritesToggle()
    {
        var library = new LibrarySettings();
        library = library.WithFavouriteToggled("nazar");
        Assert.Equal(["nazar"], library.FavouriteCharmIds);

        library = library.WithFavouriteToggled("hamsa");
        Assert.Equal(["nazar", "hamsa"], library.FavouriteCharmIds);

        library = library.WithFavouriteToggled("nazar");
        Assert.Equal(["hamsa"], library.FavouriteCharmIds);
    }

    [Fact(DisplayName = "Hanging a charm again moves it to the front rather than repeating it")]
    public void RecentsAreUnique()
    {
        var library = new LibrarySettings();
        library = library.WithRecent("nazar").WithRecent("hamsa").WithRecent("nazar");

        Assert.Equal(["nazar", "hamsa"], library.RecentCharmIds);
    }

    [Fact(DisplayName = "Recents stop at their limit")]
    public void RecentsAreCapped()
    {
        var library = new LibrarySettings();
        foreach (CharmCatalogEntry charm in CharmCatalog.All.Take(20))
        {
            library = library.WithRecent(charm.Id);
        }

        Assert.Equal(LibrarySettings.RecentLimit, library.RecentCharmIds.Count);

        // The newest is first, so the twentieth charm added is the one at the front.
        Assert.Equal(CharmCatalog.All[19].Id, library.RecentCharmIds[0]);
    }

    [Fact(DisplayName = "A charm the catalogue no longer knows is dropped on read")]
    public void StaleIdsAreClamped()
    {
        LibrarySettings clamped = new LibrarySettings
        {
            FavouriteCharmIds = ["nazar", "no-such-charm", "nazar"],
            RecentCharmIds = ["hamsa", "also-not-a-charm"],
        }.Clamped();

        Assert.Equal(["nazar"], clamped.FavouriteCharmIds);
        Assert.Equal(["hamsa"], clamped.RecentCharmIds);
    }

    /// <summary>
    /// Two documents holding the same lists are the same document. Without this the
    /// store rewrites the file every time the Library is read.
    /// </summary>
    [Fact(DisplayName = "Library settings compare by value")]
    public void LibrarySettingsCompareByValue()
    {
        var first = new LibrarySettings { FavouriteCharmIds = ["nazar"], RecentCharmIds = ["hamsa"] };
        var second = new LibrarySettings { FavouriteCharmIds = ["nazar"], RecentCharmIds = ["hamsa"] };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, first with { FavouriteCharmIds = ["hamsa"] });
    }

    [Fact(DisplayName = "Favourites and recents survive a round trip through the file")]
    public void LibraryRoundTrips()
    {
        string directory = Path.Combine(Path.GetTempPath(), "hangly-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            store.Update(settings => settings with
            {
                Library = settings.Library.WithFavouriteToggled("nazar").WithRecent("daruma"),
            });

            AppSettings reloaded = new SettingsStore(path).Settings;
            Assert.Equal(["nazar"], reloaded.Library.FavouriteCharmIds);
            Assert.Equal(["daruma"], reloaded.Library.RecentCharmIds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A search box is not a setting. Typing must never reach the document.</summary>
    [Fact(DisplayName = "Searching changes nothing that is stored")]
    public void SearchDoesNotTouchSettings()
    {
        var before = new AppSettings();
        _ = CharmSearch.Apply(Index, CharmFilter.All, "nazar", [], []);
        _ = CharmSearch.Apply(Index, CharmFilter.Category("protection"), "glass", ["hamsa"], ["daruma"]);

        Assert.Equal(before, new AppSettings());
    }
}
