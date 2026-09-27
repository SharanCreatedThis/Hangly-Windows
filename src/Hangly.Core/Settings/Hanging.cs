//
//  Hanging.cs
//  Hangly
//
//  Putting a charm on the rope: one act, one rule, whichever button did it.
//

using Hangly.Core.Models;

namespace Hangly.Core.Settings;

/// <summary>The one way a charm goes onto the rope. The macOS <c>CharmManager.select</c> follows the same rule.</summary>
/// <remarks>
/// <b>Why it exists.</b> Four places hung a charm — the Library grid, Creator Studio, the
/// Library's SVG import and the tray's favourites — and each did its own subset: the grid
/// counted it and remembered it, Studio and import only remembered it, and the tray did
/// neither, while the import wrote the id list directly and lost the place's size. So
/// "Charms hung" and "Recent" depended on which button somebody happened to use.
///
/// <para><b>The rule.</b> Hanging a charm changes the rope, puts the charm first in
/// Recent, and counts once in "Charms hung" — all in one settings write, so the file is
/// written once for one act. It counts only when something changed: choosing the charm a
/// place already has is not hanging a charm. A place keeps its size; changing which charm
/// is in the middle is not a decision to make the middle large again.</para>
/// </remarks>
public static class Hanging
{
    /// <summary>Puts <paramref name="id"/> in place <paramref name="slot"/> of the rope as it is shown.</summary>
    public static AppSettings Hang(AppSettings settings, int slot, string id)
    {
        ArgumentNullException.ThrowIfNull(settings);
        CharmStackState stack = settings.Overlay.Stack;
        int place = Math.Clamp(slot, 0, stack.Count - 1);
        bool changes = stack.Places[place].Id != id;
        OverlaySettings overlay = settings.Overlay.WithStack(stack.WithCharm(place, id));

        // A charm from a collection brings its collection's cord, but only over the rope
        // Hangly shipped with (CollectionRopes; decision B4, the macOS rule).
        if (changes)
        {
            overlay = overlay with { RopeStyle = CollectionRopes.Adopting(id, overlay.RopeStyle) };
        }

        return Counted(settings with
        {
            Overlay = overlay,
            Library = settings.Library.WithRecent(id),
        }, changes);
    }

    /// <summary>Makes <paramref name="id"/> the only charm on the rope, as the tray's favourites do.</summary>
    /// <remarks>
    /// A favourite picked from a menu is somebody saying "that one", not "that one as well",
    /// and the menu has no way to say which place was meant.
    /// </remarks>
    public static AppSettings HangAlone(AppSettings settings, string id)
    {
        ArgumentNullException.ThrowIfNull(settings);
        bool changes = !settings.Overlay.Stack.Ids.SequenceEqual([id]);
        return Counted(settings with
        {
            Overlay = settings.Overlay.WithStack(CharmStackState.Of([id])),
            Library = settings.Library.WithRecent(id),
        }, changes);
    }

    private static AppSettings Counted(AppSettings settings, bool changes) => !changes ? settings : settings with
    {
        Milestones = settings.Milestones with { CharmsHung = settings.Milestones.CharmsHung + 1 },
    };
}
