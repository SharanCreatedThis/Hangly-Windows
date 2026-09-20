//
//  CustomCharmTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Import;
using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// Imported charms: the identity that lets the settings document recognise one, the
/// store that keeps them, and the sanitiser that decides what is allowed in.
/// </summary>
public class CustomCharmTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "hangly-custom-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static CharmMetrics Metrics => new(3.0, 0.12, 0.8);

    private static CharmPalette Palette => CharmCatalog.Find("nazar").Palette;

    // MARK: - Identity

    [Fact(DisplayName = "A custom id is recognisable without knowing which imports exist")]
    public void CustomIdsAreRecognisable()
    {
        string id = CharmId.ForCustom(Guid.NewGuid());
        Assert.True(CharmId.IsCustom(id));
        Assert.True(CharmId.IsWellFormed(id));
        Assert.False(CharmCatalog.Contains(id));
    }

    [Theory(DisplayName = "Anything that is not a charm id is not well-formed")]
    [InlineData("custom:")]
    [InlineData("custom:not-a-guid")]
    [InlineData("nonsense")]
    [InlineData("")]
    public void MalformedIdsAreRejected(string id)
    {
        Assert.False(CharmId.IsCustom(id));
        Assert.False(CharmId.IsWellFormed(id));
    }

    [Fact(DisplayName = "A built-in id is well-formed and is not custom")]
    public void BuiltInIdsAreNotCustom()
    {
        Assert.True(CharmId.IsWellFormed("nazar"));
        Assert.False(CharmId.IsCustom("nazar"));
    }

    /// <summary>
    /// This is the bug the identity exists to prevent: clamping used to replace any id
    /// the catalogue did not know, which would have thrown an imported charm off the rope
    /// on the next read.
    /// </summary>
    [Fact(DisplayName = "An imported charm survives being clamped, on the rope and in the Library")]
    public void ImportsSurviveClamping()
    {
        string id = CharmId.ForCustom(Guid.NewGuid());

        OverlaySettings overlay = new OverlaySettings { CharmIds = [id] }.Clamped();
        Assert.Equal([id], overlay.CharmIds);

        LibrarySettings library = new LibrarySettings
        {
            FavouriteCharmIds = [id],
            RecentCharmIds = [id],
        }.Clamped();

        Assert.Equal([id], library.FavouriteCharmIds);
        Assert.Equal([id], library.RecentCharmIds);
    }

    [Fact(DisplayName = "An import is reported to analytics as the word custom, never by name")]
    public void ImportsAreAnonymousInAnalytics()
    {
        string id = CharmId.ForCustom(Guid.NewGuid());
        Assert.Equal("custom", Hangly.Core.Analytics.Events.NameOf(id));
        Assert.Equal(
            Hangly.Core.Analytics.AnalyticsValue.Of("custom"),
            Hangly.Core.Analytics.Events.CharmSelected(id).Properties["charm"]);
    }

    // MARK: - Index

    [Fact(DisplayName = "The index is the catalogue until something is imported")]
    public void EmptyIndexIsTheCatalogue()
    {
        var index = new CharmIndex();
        Assert.Equal(70, index.All.Count);
        Assert.DoesNotContain(index.Categories, category => category.Id == CharmIndex.CustomCategoryId);
    }

    [Fact(DisplayName = "An import joins the index, and brings its category with it")]
    public void ImportsJoinTheIndex()
    {
        var store = new CustomCharmStore(directory);
        CustomCharmEntry entry = store.Add("<svg/>", "My charm", Metrics, Palette);
        var index = new CharmIndex([entry.AsCatalogEntry(store.PathFor(entry)!)]);

        Assert.Equal(71, index.All.Count);
        Assert.True(index.Contains(entry.CharmId));
        Assert.Equal("My charm", index.Find(entry.CharmId).DisplayName);
        Assert.Contains(index.Categories, category => category.Id == CharmIndex.CustomCategoryId);
    }

    [Fact(DisplayName = "An import whose drawing has gone falls back to the bead rather than nothing")]
    public void MissingImportsFallBack()
    {
        var index = new CharmIndex();
        Assert.Equal(CharmCatalog.DefaultId, index.Find(CharmId.ForCustom(Guid.NewGuid())).Id);
    }

    [Fact(DisplayName = "An import is searchable, favouritable and can be recent")]
    public void ImportsBehaveLikeAnyCharm()
    {
        var store = new CustomCharmStore(directory);
        CustomCharmEntry entry = store.Add("<svg/>", "Seahorse", Metrics, Palette);
        var index = new CharmIndex([entry.AsCatalogEntry(store.PathFor(entry)!)]);

        Assert.Contains(entry.CharmId, CharmSearch
            .Apply(index, CharmFilter.All, "seahorse", [], []).Select(c => c.Id));

        Assert.Contains(entry.CharmId, CharmSearch
            .Apply(index, CharmFilter.Category(CharmIndex.CustomCategoryId), "", [], []).Select(c => c.Id));

        Assert.Contains(entry.CharmId, CharmSearch
            .Apply(index, CharmFilter.Favourites, "", [entry.CharmId], []).Select(c => c.Id));

        Assert.Contains(entry.CharmId, CharmSearch
            .Apply(index, CharmFilter.Recent, "", [], [entry.CharmId]).Select(c => c.Id));
    }

    [Fact(DisplayName = "An import has no beads, so nothing is drawn twice")]
    public void ImportsDeclareNoBeads()
    {
        var store = new CustomCharmStore(directory);
        CustomCharmEntry entry = store.Add("<svg/>", "Thing", Metrics, Palette);
        CharmCatalogEntry projected = entry.AsCatalogEntry(store.PathFor(entry)!);

        Assert.Equal(0, projected.BeadCount);
        Assert.Equal(0, projected.BodyRun);
        Assert.Empty(CharmCatalog.BeadsFor(projected, regions: null));
    }

    // MARK: - Store

    [Fact(DisplayName = "An import survives a restart")]
    public void ImportsPersist()
    {
        var store = new CustomCharmStore(directory);
        CustomCharmEntry entry = store.Add("<svg>drawing</svg>", "Kept", Metrics, Palette);

        var reopened = new CustomCharmStore(directory);
        CustomCharmEntry reloaded = Assert.Single(reopened.Entries);
        Assert.Equal(entry, reloaded);
        Assert.Equal("<svg>drawing</svg>", File.ReadAllText(reopened.PathFor(reloaded)!));
    }

    [Fact(DisplayName = "Deleting takes the drawing with it")]
    public void DeleteRemovesTheFile()
    {
        var store = new CustomCharmStore(directory);
        CustomCharmEntry entry = store.Add("<svg/>", "Gone", Metrics, Palette);
        string path = store.PathFor(entry)!;

        store.Remove(entry.Id);

        Assert.Empty(store.Entries);
        Assert.False(File.Exists(path));
        Assert.Empty(new CustomCharmStore(directory).Entries);
    }

    [Fact(DisplayName = "Deleting something that is not there is not an error")]
    public void DeletingNothingIsFine()
    {
        var store = new CustomCharmStore(directory);
        store.Remove(Guid.NewGuid());
        Assert.Empty(store.Entries);
    }

    [Fact(DisplayName = "An entry whose drawing has gone is dropped at load")]
    public void MissingDrawingsArePruned()
    {
        var store = new CustomCharmStore(directory);
        CustomCharmEntry entry = store.Add("<svg/>", "Doomed", Metrics, Palette);
        File.Delete(store.PathFor(entry)!);

        var reopened = new CustomCharmStore(directory);
        Assert.Empty(reopened.Entries);
        Assert.Equal([entry.Id], reopened.Pruned);
    }

    [Fact(DisplayName = "A drawing with no entry is re-registered rather than orphaned")]
    public void OrphanDrawingsAreRecovered()
    {
        var store = new CustomCharmStore(directory);
        store.Add("<svg/>", "Known", Metrics, Palette);

        var stray = Guid.NewGuid();
        File.WriteAllText(Path.Combine(directory, stray.ToString("D") + ".svg"), "<svg/>");

        var reopened = new CustomCharmStore(directory);
        Assert.Equal(2, reopened.Entries.Count);
        Assert.Equal(1, reopened.Recovered);
        Assert.Contains(reopened.Entries, entry => entry.Id == stray);
    }

    [Fact(DisplayName = "A corrupt manifest is recovered from, not fatal")]
    public void CorruptManifestRecovers()
    {
        var store = new CustomCharmStore(directory);
        store.Add("<svg/>", "Survivor", Metrics, Palette);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), "{{{ broken");

        var reopened = new CustomCharmStore(directory);
        Assert.Single(reopened.Entries);
        Assert.Equal(1, reopened.Recovered);
    }

    [Fact(DisplayName = "Imports live beside the settings, not inside the installation")]
    public void ImportsLiveWhereUpdatesCannotReachThem()
    {
        string expected = Path.GetDirectoryName(SettingsStore.DefaultPath)!;
        Assert.Equal(Path.Combine(expected, "Charms"), CustomCharmStore.DefaultDirectory);
        Assert.StartsWith(expected, CustomCharmStore.DefaultDirectory, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Renaming keeps everything else")]
    public void RenameKeepsTheRest()
    {
        var store = new CustomCharmStore(directory);
        CustomCharmEntry entry = store.Add("<svg/>", "Before", Metrics, Palette);
        store.Rename(entry.Id, "After");

        CustomCharmEntry renamed = Assert.Single(new CustomCharmStore(directory).Entries);
        Assert.Equal("After", renamed.Name);
        Assert.Equal(entry.Id, renamed.Id);
        Assert.Equal(entry.ImageFileName, renamed.ImageFileName);
    }
}
