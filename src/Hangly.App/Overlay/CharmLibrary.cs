//
//  CharmLibrary.cs
//  Hangly
//
//  Turning catalogue entries into charms the rope can carry.
//

using Hangly.App.Services;
using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.App.Overlay;

/// <summary>Builds the charms named in the settings, measuring each one's artwork.</summary>
/// <remarks>
/// This replaced <c>BuiltInCharms</c>, which hard-coded a single bead because the
/// catalogue had not been ported and the splitter did not exist. Both do now, so there is
/// nothing left to hard-code: the eighty-one entries come from
/// <see cref="CharmCatalog"/>, and everything the table does not state — where the knot
/// sits, where the beads are, how heavy each one is — is measured from the artwork.
///
/// <para>Measuring happens once per charm and only for charms that are actually hung. The
/// full catalogue is eighty-one analysis rasters, which is a second of work nobody asked
/// for at launch; a rope carries at most three.</para>
/// </remarks>
public static class CharmLibrary
{
    /// <summary>The whole fitted square, for a charm whose artwork could not be measured.</summary>
    private static readonly Rect WholeArtwork = new(0, 0, 1, 1);

    /// <summary>Resolves the ids on the cord, in order, into drawable charms.</summary>
    /// <remarks>
    /// The index rather than the catalogue, so an imported charm resolves like any other.
    /// An id naming an import whose drawing has gone falls back to the bead, which is
    /// what stops a deleted charm leaving the rope bare.
    /// </remarks>
    public static IReadOnlyList<CharmDescriptor> Resolve(
        CharmArtworkCache artwork,
        CharmIndex index,
        IReadOnlyList<RopeCharm> places)
    {
        var charms = new List<CharmDescriptor>(places.Count);
        foreach (RopeCharm place in places)
        {
            charms.Add(Describe(artwork, index.Find(place.Id), place.Size));
        }

        // Settings clamping guarantees at least one, but this is the last place before
        // the solver is told what it is carrying, and an empty rope is not a state it
        // should have to reason about.
        if (charms.Count == 0)
        {
            charms.Add(Describe(artwork, index.Find(CharmCatalog.DefaultId), 1));
        }

        return charms;
    }

    private static CharmDescriptor Describe(CharmArtworkCache artwork, CharmCatalogEntry entry, double size)
    {
        CharmArtworkRegions? regions = artwork.Measure(entry);
        if (regions is null)
        {
            // Reported rather than silently accepted: a charm that cannot be measured
            // still hangs, but it hangs with its own beads drawn into it and its knot on
            // the bounding circle, and that is worth knowing about.
            Diagnostics.Log($"charm '{entry.Id}' could not be measured; hanging it whole");
        }

        return new CharmDescriptor(
            entry.Id,
            entry.DisplayName,
            entry.FileName,
            // The place's own trim, applied here so nothing downstream has to carry it:
            // the solver is handed metrics that already describe the charm at the size it
            // will be drawn, which is what keeps its swing and its picture in agreement.
            CharmCatalog.MetricsFor(entry, regions).Scaled(size),
            entry.Palette,
            // A charm whose artwork draws no beads gets the standard three. That is
            // every created charm: a photograph has no cord above the subject, and a
            // charm hanging on a bare string beside seventy that hang on threaded ones
            // looks like a mistake rather than a choice.
            CharmCatalog.BeadsFor(entry, regions) is { Count: > 0 } measured
                ? measured
                : CharmCatalog.DefaultBeads,
            regions?.Body ?? WholeArtwork,
            regions?.Beads ?? []);
    }
}
