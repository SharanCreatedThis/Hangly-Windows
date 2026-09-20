//
//  CharmId.cs
//  Hangly
//
//  Telling a shipped charm from one somebody made.
//

namespace Hangly.Core.Models;

/// <summary>The shape of a charm's identifier.</summary>
/// <remarks>
/// <b>Why this exists.</b> Until imports, every charm id was a catalogue id and
/// <c>CharmCatalog.Contains</c> was a complete answer to "is this a charm". The settings
/// document leaned on that: an id it did not recognise was replaced with the bead, and
/// an unrecognised favourite was dropped. An imported charm is not in the catalogue and
/// never will be, so both of those would have quietly thrown the user's own charm away —
/// off the rope on the next read, and out of their favourites.
///
/// <para>The macOS build does not have this problem because its identifier is a closed
/// enum of two cases, <c>builtIn</c> or <c>custom</c>. This is the same idea expressed in
/// the id itself: a custom charm's id is the word <c>custom:</c> and a UUID, which is a
/// shape the settings layer can recognise <i>without</i> knowing which imports exist.
/// That matters, because <c>Hangly.Core</c> reads the settings document long before
/// anything has looked in the folder where imports live.</para>
///
/// <para>Well-formed is not the same as present. An id can survive clamping and still
/// name a charm whose file has been deleted; resolving that is the app's job, and
/// <c>CharmLibrary</c> falls back to the bead rather than leaving the rope bare.</para>
/// </remarks>
public static class CharmId
{
    /// <summary>What every imported charm's id begins with.</summary>
    public const string CustomPrefix = "custom:";

    /// <summary>The id for a newly imported charm.</summary>
    public static string ForCustom(Guid id) =>
        CustomPrefix + id.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Whether this names an imported charm, whether or not it still exists.</summary>
    public static bool IsCustom(string? id) =>
        id is not null
        && id.StartsWith(CustomPrefix, StringComparison.Ordinal)
        && Guid.TryParse(id.AsSpan(CustomPrefix.Length), out _);

    /// <summary>
    /// Whether this is an id the app could ever resolve: a catalogue charm, or an import.
    /// </summary>
    public static bool IsWellFormed(string? id) =>
        id is not null && (CharmCatalog.Contains(id) || IsCustom(id));
}
