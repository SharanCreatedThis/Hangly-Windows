//
//  CharmStackState.cs
//  Hangly
//
//  The one, two or three charms threaded on the rope.
//

namespace Hangly.Core.Models;

/// <summary>What hangs on the rope, ordered from the anchor down.</summary>
/// <remarks>
/// A transcription of the macOS <c>CharmStack</c>. Named <c>CharmStackState</c> only
/// because <see cref="CharmStack"/> already exists here as the place the maximum count
/// lives.
///
/// <para>A stack is never empty and never longer than <see cref="CharmStack.MaximumCount"/>.
/// Both facts are enforced here rather than checked at every use, so nothing downstream —
/// the solver, the renderer, the menu — carries a "what if there are none" branch.</para>
///
/// <para><b>Every place is kept, whether or not it is in use.</b> The slots are always
/// three long with the ones in use at the end, so shrinking to one and back to three
/// gives the same three charms rather than three copies of the survivor. macOS records
/// that the alternative was tried and is worse than it sounds: someone who glances at a
/// count control and puts it back should not have to rebuild their rope, and any stray
/// write of the count becomes destructive rather than merely wrong. The Windows build did
/// exactly that until this type existed — it grew by duplicating the bottom charm and
/// shrank by discarding it.</para>
/// </remarks>
public readonly record struct CharmStackState
{
    private readonly RopeCharm[] slots;
    private readonly int count;

    private CharmStackState(RopeCharm[] slots, int count)
    {
        this.slots = slots;
        this.count = count;
    }

    /// <summary>How many hang, one to three.</summary>
    public int Count => count == 0 ? 1 : count;

    /// <summary>Every place, in use or not, so the ones put away survive a relaunch.</summary>
    public IReadOnlyList<RopeCharm> StoredSlots => slots ?? Padding(CharmCatalog.DefaultId);

    /// <summary>The places actually on the rope, from the anchor down.</summary>
    public IReadOnlyList<RopeCharm> Places
    {
        get
        {
            IReadOnlyList<RopeCharm> all = StoredSlots;
            return [.. all.Skip(all.Count - Count)];
        }
    }

    /// <summary>The charms actually on the rope, in the same order.</summary>
    public IReadOnlyList<string> Ids => [.. Places.Select(place => place.Id)];

    private static RopeCharm[] Padding(string id) =>
        [.. Enumerable.Repeat(new RopeCharm(id), CharmStack.MaximumCount)];

    /// <summary>A stack of these charms, each at its artwork's own size.</summary>
    public static CharmStackState Of(IReadOnlyList<string> ids) =>
        FromPlaces([.. ids.Select(id => new RopeCharm(id))]);

    /// <summary>A stack of these places, padded to three.</summary>
    /// <remarks>
    /// The unused places above start as copies of the topmost charm, so growing into them
    /// gives something recognisable rather than something arbitrary.
    /// </remarks>
    public static CharmStackState FromPlaces(IReadOnlyList<RopeCharm> places)
    {
        RopeCharm[] trimmed = [.. places.Take(CharmStack.MaximumCount).Select(place => place.Clamped())];
        if (trimmed.Length == 0)
        {
            trimmed = [new RopeCharm(CharmCatalog.DefaultId)];
        }

        RopeCharm[] padded =
        [
            .. Enumerable.Repeat(trimmed[0], CharmStack.MaximumCount - trimmed.Length),
            .. trimmed,
        ];

        return new CharmStackState(padded, trimmed.Length);
    }

    /// <summary>Restores a stack including the places not currently in use.</summary>
    public static CharmStackState Restore(IReadOnlyList<RopeCharm> storedSlots, int count)
    {
        RopeCharm[] trimmed =
        [
            .. storedSlots
                .Skip(Math.Max(0, storedSlots.Count - CharmStack.MaximumCount))
                .Select(place => place.Clamped()),
        ];

        if (trimmed.Length == 0)
        {
            trimmed = [new RopeCharm(CharmCatalog.DefaultId)];
        }

        RopeCharm[] padded =
        [
            .. Enumerable.Repeat(trimmed[0], CharmStack.MaximumCount - trimmed.Length),
            .. trimmed,
        ];

        return new CharmStackState(padded, Math.Clamp(count, 1, CharmStack.MaximumCount));
    }

    /// <summary>Grows or shrinks the stack, keeping every charm already chosen.</summary>
    /// <remarks>
    /// Shrinking hides places from the top, so the charm on the end of the rope — the one
    /// being looked at — is the one that stays. Growing brings back exactly what was
    /// hidden.
    /// </remarks>
    public CharmStackState WithCount(int newCount) =>
        new(StoredSlots.ToArray(), Math.Clamp(newCount, 1, CharmStack.MaximumCount));

    /// <summary>
    /// Puts a charm in a hanging place, leaving that place's size alone.
    /// </summary>
    /// <remarks>
    /// The size describes the composition, and changing your mind about which charm is in
    /// the middle is not a decision to make the middle large again.
    /// </remarks>
    public CharmStackState WithCharm(int slot, string id) => Edit(slot, place => place with { Id = id });

    /// <summary>How large the charm in this place is drawn, relative to its own artwork.</summary>
    public double SizeAt(int slot) => Places[Math.Clamp(slot, 0, Count - 1)].Size;

    public CharmStackState WithSize(int slot, double size) =>
        Edit(slot, place => (place with { Size = size }).Clamped());

    /// <summary>Moves a place up or down the rope, which is all reordering means.</summary>
    public CharmStackState Moved(int source, int destination)
    {
        if (source == destination || source < 0 || source >= Count)
        {
            return this;
        }

        var places = Places.ToList();
        RopeCharm moved = places[source];
        places.RemoveAt(source);
        places.Insert(Math.Clamp(destination, 0, places.Count), moved);
        return FromPlaces(places);
    }

    /// <summary>Replaces every occurrence of a charm, hanging or not.</summary>
    /// <remarks>
    /// Every place, because a deleted import must not come back when the count grows.
    /// </remarks>
    public CharmStackState Replacing(string id, string replacement)
    {
        RopeCharm[] replaced =
        [
            .. StoredSlots.Select(place =>
                string.Equals(place.Id, id, StringComparison.Ordinal)
                    ? place with { Id = replacement }
                    : place),
        ];

        return new CharmStackState(replaced, Count);
    }

    /// <summary>
    /// Edits one hanging place by its position on the rope, ignoring anything out of
    /// range rather than throwing: these indices come from an interface that can be a
    /// frame behind the stack it is drawing.
    /// </summary>
    private CharmStackState Edit(int slot, Func<RopeCharm, RopeCharm> edit)
    {
        if (slot < 0 || slot >= Count)
        {
            return this;
        }

        RopeCharm[] edited = StoredSlots.ToArray();
        int index = edited.Length - Count + slot;
        edited[index] = edit(edited[index]);
        return new CharmStackState(edited, Count);
    }
}
