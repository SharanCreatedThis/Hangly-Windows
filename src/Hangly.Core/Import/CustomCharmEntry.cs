//
//  CustomCharmEntry.cs
//  Hangly
//
//  An imported drawing as a charm.
//

using Hangly.Core.Models;

namespace Hangly.Core.Import;

/// <summary>What is persisted about an imported charm, without the drawing.</summary>
/// <remarks>
/// The fields, their names and their order are the macOS <c>CustomCharmEntry</c>'s, so
/// the two manifests describe the same thing in the same words. Kept separate from the
/// charm itself so the Library can list every import without opening a single file, and
/// so metrics and palette are computed once at import rather than on every launch.
///
/// <para>One deviation, and it is the whole of this milestone's divergence:
/// <see cref="ImageFileName"/> names an <c>.svg</c> here and a <c>.png</c> on macOS. See
/// PORTING.md for why.</para>
/// </remarks>
public sealed record CustomCharmEntry
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>File name inside the charm store's directory.</summary>
    public required string ImageFileName { get; init; }

    public required CharmMetrics Metrics { get; init; }

    public required CharmPalette Palette { get; init; }

    /// <summary>The id this charm is known by everywhere else in the app.</summary>
    public string CharmId => Models.CharmId.ForCustom(Id);

    /// <summary>
    /// The import as the rest of the app sees every charm.
    /// </summary>
    /// <remarks>
    /// Projected into a catalogue entry so that search, the grid, the solver and the
    /// renderer need no idea that imports exist. See <see cref="CharmIndex"/>.
    ///
    /// <para><b>Bead count is zero, deliberately.</b> A shipped charm's artwork is one
    /// tall picture — a cord, some beads, then the charm — and the splitter is told how
    /// many of those parts are beads. An imported drawing is a subject on its own with no
    /// cord above it, so there are no beads to find, and saying so is what stops the
    /// splitter reading the top of somebody's drawing as a bead and hanging the rest of
    /// it underneath.</para>
    /// </remarks>
    public CharmCatalogEntry AsCatalogEntry(string absolutePath) => new(
        Id: CharmId,
        DisplayName: Name,
        FileName: absolutePath,
        Mass: Metrics.Mass,
        RadiusRatio: Metrics.RadiusRatio,
        Palette: Palette,
        BeadCount: 0,
        BodyRun: 0,
        CategoryId: CharmIndex.CustomCategoryId,
        Region: "Yours",
        Description: "A charm you imported.",
        Tags: ["custom", "yours", "imported"]);
}
