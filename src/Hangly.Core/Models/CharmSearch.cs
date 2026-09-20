//
//  CharmSearch.cs
//  Hangly
//
//  Finding a charm by typing part of it.
//

using System.Globalization;
using System.Text;

namespace Hangly.Core.Models;

/// <summary>What the Library is showing: everything, a saved set, or one category.</summary>
public abstract record CharmFilter
{
    private CharmFilter()
    {
    }

    public static CharmFilter All { get; } = new Everything();

    public static CharmFilter Favourites { get; } = new Favourite();

    public static CharmFilter Recent { get; } = new Recently();

    public static CharmFilter Category(string id) => new OfCategory(id);

    public sealed record Everything : CharmFilter;

    public sealed record Favourite : CharmFilter;

    public sealed record Recently : CharmFilter;

    public sealed record OfCategory(string Id) : CharmFilter;
}

/// <summary>Searching and filtering the catalogue.</summary>
/// <remarks>
/// In <c>Hangly.Core</c> and free of any window, because "typing pancha finds
/// Pánchángjié" is a claim about text that a test should be able to make without opening
/// a grid.
/// </remarks>
public static class CharmSearch
{
    /// <summary>
    /// Lower-cased and stripped of accents, so what is typed matches what is read.
    /// </summary>
    /// <remarks>
    /// Half the catalogue is named in languages that use marks an English keyboard does
    /// not have — Pánchángjié, Nazar boncuğu, Dhrishti bomma. Somebody typing "pancha" or
    /// "boncugu" means the charm they are looking at, and a search that fails them is a
    /// search that only works for charms with plain names.
    ///
    /// <para>Decomposing to <see cref="NormalizationForm.FormD"/> splits a letter from
    /// its marks; dropping everything in <see cref="UnicodeCategory.NonSpacingMark"/>
    /// leaves the letter. The Turkish dotless ı and the ğ survive as themselves, which is
    /// correct — they are letters, not decorated ones — so "boncugu" matching "boncuğu"
    /// is handled by the fold below rather than by the decomposition.</para>
    /// </remarks>
    public static string Fold(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);

        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Letters that are their own letter rather than a decorated one, and so
            // survive decomposition. Somebody typing an English keyboard still means them.
            folded.Append(character switch
            {
                'ğ' or 'Ğ' => 'g',
                'ı' => 'i',
                'İ' => 'i',
                'ş' or 'Ş' => 's',
                'ø' or 'Ø' => 'o',
                'æ' or 'Æ' => 'a',
                'ð' or 'Ð' => 'd',
                'þ' or 'Þ' => 't',
                'ł' or 'Ł' => 'l',
                _ => char.ToLowerInvariant(character),
            });
        }

        return folded.ToString();
    }

    /// <summary>Everything about a charm that typing can reach.</summary>
    public static string HaystackFor(CharmCatalogEntry charm) =>
        Fold(string.Join(' ', [charm.DisplayName, charm.Region, charm.Description, .. charm.Tags]));

    /// <summary>
    /// The charms matching a filter and a query, in catalogue order.
    /// </summary>
    /// <param name="index">The catalogue plus whatever has been imported.</param>
    /// <param name="favourites">Ids the user has starred.</param>
    /// <param name="recent">Ids most recently hung, newest first.</param>
    public static IReadOnlyList<CharmCatalogEntry> Apply(
        CharmIndex index,
        CharmFilter filter,
        string query,
        IReadOnlyCollection<string> favourites,
        IReadOnlyList<string> recent)
    {
        IEnumerable<CharmCatalogEntry> charms = filter switch
        {
            CharmFilter.Favourite => index.All.Where(charm => favourites.Contains(charm.Id)),

            // Recents keep their own order — most recent first — because that order is
            // the whole information in the list.
            CharmFilter.Recently => recent
                .Where(index.Contains)
                .Select(index.Find),

            CharmFilter.OfCategory category => index.All
                .Where(charm => charm.CategoryId == category.Id),

            _ => index.All,
        };

        string folded = Fold(query.Trim());
        if (folded.Length > 0)
        {
            charms = charms.Where(charm => HaystackFor(charm).Contains(folded, StringComparison.Ordinal));
        }

        return [.. charms];
    }
}
