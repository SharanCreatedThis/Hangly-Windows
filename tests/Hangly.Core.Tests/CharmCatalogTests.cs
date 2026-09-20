//
//  CharmCatalogTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// The catalogue is generated from the Swift, so these do not re-check arithmetic — they
/// check that the generator produced a table the rest of the app can rely on, and that
/// it stays that way when somebody regenerates it.
/// </summary>
public class CharmCatalogTests
{
    /// <summary>Seventy charms ship, which is eleven fewer than macOS.</summary>
    /// <remarks>
    /// The Swift declares eighty-one. The difference is the seasonal pack, cut from this
    /// build along with the eleven charms that only existed to fill it — see STATUS.md.
    /// The generator is where that is expressed, by not reading
    /// <c>SeasonalCharmCatalog.swift</c> at all, so this number and the Swift's are both
    /// right about their own platform.
    /// </remarks>
    [Fact(DisplayName = "Seventy charms ship: the Swift's eighty-one less the seasonal pack")]
    public void CountMatchesTheOriginal() => Assert.Equal(70, CharmCatalog.All.Count);

    [Fact(DisplayName = "No charm is filed under a category the chips do not offer")]
    public void EveryCharmHasAnOfferedCategory()
    {
        var offered = CharmCatalog.Categories.Select(category => category.Id).ToHashSet();
        string[] orphaned =
        [
            .. CharmCatalog.All
                .Where(charm => !offered.Contains(charm.CategoryId))
                .Select(charm => $"{charm.Id} -> {charm.CategoryId}"),
        ];

        Assert.Empty(orphaned);
    }

    [Fact(DisplayName = "Every id is unique, because a settings file names charms by id")]
    public void IdsAreUnique()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<string> duplicates = [.. CharmCatalog.All.Select(charm => charm.Id).Where(id => !seen.Add(id))];
        Assert.Empty(duplicates);
    }

    [Fact(DisplayName = "Every charm names a file, an id and something to call it")]
    public void MetadataIsPresent()
    {
        foreach (CharmCatalogEntry charm in CharmCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(charm.Id));
            Assert.False(string.IsNullOrWhiteSpace(charm.DisplayName));
            Assert.EndsWith(".svg", charm.FileName, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Not style rules. A charm with no mass does not hang, a charm with no radius is a
    /// point, and both are states the solver cannot divide its way out of.
    /// </summary>
    [Fact(DisplayName = "Every charm has a mass and a radius the solver can use")]
    public void PhysicsIsUsable()
    {
        foreach (CharmCatalogEntry charm in CharmCatalog.All)
        {
            Assert.True(charm.Mass > 0, $"{charm.Id} has no mass");
            Assert.True(charm.RadiusRatio > 0, $"{charm.Id} has no radius");
            Assert.True(charm.RadiusRatio < 1, $"{charm.Id} is larger than its own ruler");
        }
    }

    [Fact(DisplayName = "Bead counts are sane, and the body never starts above the beads")]
    public void ArtworkDivisionIsConsistent()
    {
        foreach (CharmCatalogEntry charm in CharmCatalog.All)
        {
            Assert.True(charm.BeadCount >= 0, $"{charm.Id} has a negative bead count");
            Assert.True(
                charm.BodyRun >= charm.BeadCount,
                $"{charm.Id} starts its body at run {charm.BodyRun}, above its {charm.BeadCount} beads");
        }
    }

    [Fact(DisplayName = "Every palette channel is a colour, not a number that escaped")]
    public void PalettesAreInRange()
    {
        foreach (CharmCatalogEntry charm in CharmCatalog.All)
        {
            foreach (CharmColor colour in (CharmColor[])
                [charm.Palette.Primary, charm.Palette.Secondary, charm.Palette.Deep, charm.Palette.Light])
            {
                Assert.InRange(colour.Red, 0, 1);
                Assert.InRange(colour.Green, 0, 1);
                Assert.InRange(colour.Blue, 0, 1);
            }
        }
    }

    /// <summary>
    /// Spot-checks against <c>CollectionCharmCatalog.swift</c>. Three entries from three
    /// different source files, so a generator that silently drops or reorders a list is
    /// caught by more than the count.
    /// </summary>
    [Theory(DisplayName = "Values match the Swift entry they were read from")]
    [InlineData("nazar", "Nazar boncuğu", "Nazar Boncuğu.svg", 2.75, 0.145, 1)]
    [InlineData("nimbuMirchi", "Nimbu-mirchi", "Nimbu-mirchi.svg", 2.85, 0.152, 0)]
    [InlineData("ronaldoJersey", "Ronaldo 7", "Football/Ronaldo 7.svg", 3.02, 0.1652, 0)]
    public void SpotChecksMatchTheSwift(
        string id,
        string displayName,
        string fileName,
        double mass,
        double radiusRatio,
        int beadCount)
    {
        CharmCatalogEntry charm = CharmCatalog.Find(id);
        Assert.Equal(displayName, charm.DisplayName);
        Assert.Equal(fileName, charm.FileName);
        Assert.Equal(mass, charm.Mass);
        Assert.Equal(radiusRatio, charm.RadiusRatio);
        Assert.Equal(beadCount, charm.BeadCount);
    }

    [Fact(DisplayName = "The collection leads and the classics come last, as the menu orders them")]
    public void OrderIsTheCatalogueOwn()
    {
        Assert.Equal("nazar", CharmCatalog.All[0].Id);
        Assert.Equal("circle", CharmCatalog.All[^5].Id);
        Assert.Equal("diamond", CharmCatalog.All[^1].Id);
    }

    [Fact(DisplayName = "An id from a newer build falls back to the bead rather than nothing")]
    public void UnknownIdFallsBack()
    {
        Assert.Equal(CharmCatalog.DefaultId, CharmCatalog.Find("no-such-charm").Id);
        Assert.False(CharmCatalog.Contains("no-such-charm"));
        Assert.True(CharmCatalog.Contains("hamsa"));
    }

    [Fact(DisplayName = "Artwork that could not be read leaves the knot on the bounding circle")]
    public void MissingArtworkFallsBackToTheStatedKnot()
    {
        CharmCatalogEntry charm = CharmCatalog.Find("hamsa");
        Assert.Equal(CharmCatalog.FallbackKnotInset, CharmCatalog.MetricsFor(charm, regions: null).KnotInset);
        Assert.Empty(CharmCatalog.BeadsFor(charm, regions: null));
    }
}
