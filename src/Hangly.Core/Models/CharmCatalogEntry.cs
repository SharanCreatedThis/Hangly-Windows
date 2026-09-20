//
//  CharmCatalogEntry.cs
//  Hangly
//
//  One charm, as the catalogue states it.
//

namespace Hangly.Core.Models;

/// <summary>A built-in charm's identity, physics, palette and artwork division.</summary>
/// <remarks>
/// Everything here is data read out of the Swift catalogue by
/// <c>tools/generate-catalogue.py</c>. What is <b>not</b> here is the knot inset and the
/// beads: both are measured from the artwork by <see cref="CharmArtworkSplitter"/>
/// rather than stated, because the artwork is where the designer put them and a number
/// typed beside it is a number that can disagree with the picture.
/// </remarks>
/// <param name="Id">
/// The macOS <c>CharmKind</c> raw value, unchanged. It is what a settings file and an
/// analytics event name the charm by, so the two platforms have to agree on it exactly.
/// </param>
/// <param name="FileName">
/// Path under <c>Assets/Charms</c>, carrying its pack subdirectory where it has one.
/// </param>
/// <param name="BeadCount">How many solid parts of the artwork, from the top, are beads.</param>
/// <param name="BodyRun">
/// Which solid part the charm itself begins at. Equal to <paramref name="BeadCount"/>
/// unless the artwork's own cord is thick enough to read as a part of its own, in which
/// case the parts between are dropped and the simulated cord replaces them.
/// </param>
/// <param name="CategoryId">
/// Which of <see cref="CharmCatalog.Categories"/> this charm belongs to.
/// </param>
/// <param name="Region">Where the charm comes from, as the Library says it.</param>
/// <param name="Tags">
/// Words the Library searches in addition to the name — the material, the colour, the
/// place. They are what makes "glass" find the nazar.
/// </param>
public sealed record CharmCatalogEntry(
    string Id,
    string DisplayName,
    string FileName,
    double Mass,
    double RadiusRatio,
    CharmPalette Palette,
    int BeadCount,
    int BodyRun,
    string CategoryId,
    string Region,
    string Description,
    IReadOnlyList<string> Tags)
{
    /// <summary>Compared by value, tags included.</summary>
    /// <remarks>
    /// The generated equality would compare <see cref="Tags"/> by reference, which makes
    /// two identical entries unequal — the same trap the settings document fell into.
    /// </remarks>
    public bool Equals(CharmCatalogEntry? other) =>
        other is not null
        && Id == other.Id
        && DisplayName == other.DisplayName
        && FileName == other.FileName
        && Mass.Equals(other.Mass)
        && RadiusRatio.Equals(other.RadiusRatio)
        && Palette == other.Palette
        && BeadCount == other.BeadCount
        && BodyRun == other.BodyRun
        && CategoryId == other.CategoryId
        && Region == other.Region
        && Description == other.Description
        && Tags.SequenceEqual(other.Tags, StringComparer.Ordinal);

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);
}

/// <summary>One of the Library's categories.</summary>
public sealed record CharmCategory(string Id, string Name);
