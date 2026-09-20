//
//  CharmIndex.cs
//  Hangly
//
//  Every charm the app can hang, shipped or imported.
//

namespace Hangly.Core.Models;

/// <summary>The catalogue plus whatever the user has imported.</summary>
/// <remarks>
/// <b>Imports are projected into <see cref="CharmCatalogEntry"/> rather than given a type
/// of their own.</b> That is the whole design. The solver, the renderer, the Library grid,
/// search, favourites and the recents list all take catalogue entries, and none of them
/// has any reason to care where one came from — so an import that arrives as the same
/// type needs no special case in any of them. The alternative was a second type and a
/// branch at every one of those places, which is the kind of thing that is fine on the
/// day it is written and wrong six features later.
///
/// <para>An instance rather than a static, because the set changes while the app is
/// running: importing a charm builds a new index. <see cref="CharmCatalog"/> stays
/// generated, immutable and free of anything a user did.</para>
/// </remarks>
public sealed class CharmIndex
{
    /// <summary>The category imported charms are filed under.</summary>
    public const string CustomCategoryId = "yours";

    /// <summary>How the Library names that category.</summary>
    public static CharmCategory CustomCategory { get; } = new(CustomCategoryId, "Custom");

    /// <summary>The Custom collection, for the Library's hero cards.</summary>
    /// <remarks>
    /// Only offered when there is something in it. A card promising a collection that
    /// turns out to be empty is worse than no card, and until someone has made a charm
    /// there is nothing to show.
    /// </remarks>
    public static CharmCollection CustomCollection { get; } = new(
        CustomCategoryId,
        "Custom",
        "Charms you made yourself.");

    private Dictionary<string, CharmCatalogEntry>? byId;

    public CharmIndex(IReadOnlyList<CharmCatalogEntry>? custom = null)
    {
        Custom = custom ?? [];

        // With nothing imported this is the catalogue, exactly — the same list, not a
        // copy of it, and no second lookup built over the same eighty-one charms. An
        // index that costs something on every launch to support a feature most launches
        // do not use is a tax, and this one measured at thirteen milliseconds before it
        // was taken off.
        All = Custom.Count == 0 ? CharmCatalog.All : [.. CharmCatalog.All, .. Custom];

        Categories = Custom.Count > 0
            ? [.. CharmCatalog.Categories, CustomCategory]
            : CharmCatalog.Categories;
    }

    private Dictionary<string, CharmCatalogEntry> ById =>
        byId ??= All.ToDictionary(entry => entry.Id, StringComparer.Ordinal);

    /// <summary>Shipped charms first, then imports in the order they were made.</summary>
    public IReadOnlyList<CharmCatalogEntry> All { get; }

    public IReadOnlyList<CharmCatalogEntry> Custom { get; }

    /// <summary>The Library's chips. "Yours" appears only once there is something in it.</summary>
    public IReadOnlyList<CharmCategory> Categories { get; }

    public bool Contains(string id) =>
        Custom.Count == 0 ? CharmCatalog.Contains(id) : ById.ContainsKey(id);

    /// <summary>
    /// The charm with this id, or the plain bead when there is no such charm.
    /// </summary>
    /// <remarks>
    /// The fallback covers an import whose file has been deleted from under the app, as
    /// well as an id from a newer build. Either way the rope is never left bare.
    /// </remarks>
    public CharmCatalogEntry Find(string id)
    {
        if (Custom.Count == 0)
        {
            return CharmCatalog.Find(id);
        }

        return ById.TryGetValue(id, out CharmCatalogEntry? entry)
            ? entry
            : CharmCatalog.Find(CharmCatalog.DefaultId);
    }
}
