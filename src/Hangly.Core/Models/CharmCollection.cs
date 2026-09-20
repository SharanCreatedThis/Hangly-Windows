//
//  CharmCollection.cs
//  Hangly
//
//  A themed set of charms, as the Library offers it.
//

namespace Hangly.Core.Models;

/// <summary>One collection, as the Library's cards present it.</summary>
/// <remarks>
/// A collection is a category that has something to say about itself. Categories like
/// "Protection" or "Luck &amp; Fortune" are ways of filtering the whole catalogue; a
/// collection is a set somebody assembled — Marvel, Breaking Bad, Tamil Spiritual — and
/// macOS gives those a card with artwork, a count and a line, above the grid.
///
/// <para><see cref="Description"/> is quoted from the shipping macOS build rather than
/// written here; see the generator for where each line came from.</para>
/// </remarks>
/// <param name="Id">The category id every charm in it carries.</param>
/// <param name="Name">What the card and the chip both call it.</param>
/// <param name="Description">The one line under the name.</param>
public readonly record struct CharmCollection(string Id, string Name, string Description);
