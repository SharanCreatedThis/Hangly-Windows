//
//  CollectionRopes.cs
//  Hangly
//
//  The cord each collection was drawn to hang on.
//

namespace Hangly.Core.Models;

/// <summary>Each collection's own cord, and the rule for offering it. The macOS <c>CharmCollection.defaultRope</c>.</summary>
/// <remarks>
/// Decision B4: the macOS rule, unchanged. When a charm from a collection is put on the
/// rope from the Library and the rope is still the one Hangly shipped with, the rope
/// becomes that collection's cord. Somebody who has never touched the rope has no opinion
/// about it, and a Spider-Man on white silk is the better first sight of the collection;
/// somebody who has chosen a cord has said what they want, and a charm is not an argument
/// against it — so any other rope, including one this rule put there, is left alone.
/// </remarks>
public static class CollectionRopes
{
    private static readonly Dictionary<string, RopeStyle> Cords = new(StringComparer.Ordinal)
    {
        ["marvel"] = RopeStyle.SpiderThread,
        ["dc"] = RopeStyle.MidnightCord,
        ["tamilSpiritual"] = RopeStyle.TempleThread,
        ["bts"] = RopeStyle.SilverCord,
        ["footballLegends"] = RopeStyle.GoldChain,
        ["musicLegends"] = RopeStyle.SilverChain,
        ["friends"] = RopeStyle.Thread,
        ["breakingBad"] = RopeStyle.Leather,
        ["strangerThings"] = RopeStyle.Neon,

        // 2.2's collections. Nine cords and seventeen collections, so from here on a
        // cord is shared — each still the one the collection reads best on.
        ["onePiece"] = RopeStyle.Leather,
        ["harryPotter"] = RopeStyle.GoldChain,
        ["ben10"] = RopeStyle.Neon,
        ["attackOnTitan"] = RopeStyle.Leather,
        ["naruto"] = RopeStyle.Thread,
        ["gameOfThrones"] = RopeStyle.MidnightCord,
        ["airJordan"] = RopeStyle.SilverChain,
        ["pokemon"] = RopeStyle.SilverCord,
    };

    /// <summary>The cord a collection was drawn to hang on, or null for a category that is not a collection.</summary>
    public static RopeStyle? For(string collectionId) =>
        Cords.TryGetValue(collectionId, out RopeStyle style) ? style : null;

    /// <summary>The rope after <paramref name="charmId"/> is hung on <paramref name="current"/>, by the rule above.</summary>
    public static RopeStyle Adopting(string charmId, RopeStyle current)
    {
        if (current != RopeStyleTable.FirstRun
            || CharmCatalog.All.FirstOrDefault(entry => entry.Id == charmId) is not { } entry
            || For(entry.CategoryId) is not RopeStyle cord)
        {
            return current;
        }

        return cord;
    }
}
