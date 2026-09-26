//
//  DisplayChoice.cs
//  Hangly
//
//  Which display the rope hangs on, when displays come and go.
//

namespace Hangly.Core.Geometry;

/// <summary>One attached display, as far as choosing between them goes.</summary>
/// <param name="Id">
/// Stable across reboots, re-plugging and rearranging: the monitor's device interface path
/// on Windows, the display's UUID on macOS. Never an index, because an index is a position
/// in a list the system reorders whenever a display arrives.
/// </param>
/// <param name="Name">What the monitor calls itself, for a menu.</param>
/// <param name="IsPrimary">Whether this is the display the system considers main.</param>
public readonly record struct DisplayIdentity(string Id, string Name, bool IsPrimary);

/// <summary>Resolves the remembered choice of display against the displays attached now.</summary>
/// <remarks>
/// The rule, which the macOS build follows word for word:
///
/// <list type="bullet">
/// <item>No choice means the main display, whichever that is today.</item>
/// <item>A chosen display that is attached is used.</item>
/// <item>A chosen display that is not attached falls back to the main display, and the
/// choice is kept. When the display comes back the rope goes back to it, without anybody
/// choosing again. That is what makes a laptop that docks and undocks every day behave.</item>
/// </list>
/// </remarks>
public static class DisplayChoice
{
    /// <summary>The index in <paramref name="displays"/> to hang on.</summary>
    /// <param name="displays">The attached displays, in any order.</param>
    /// <param name="chosenId">The remembered choice, or null for the main display.</param>
    /// <param name="legacyIndex">
    /// What builds before 1.0 stored instead: a position in the list, main display first.
    /// Honoured while no display has been chosen by id, so nobody's rope moves on update.
    /// </param>
    public static int Resolve(IReadOnlyList<DisplayIdentity> displays, string? chosenId, int legacyIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(displays);
        if (displays.Count == 0)
        {
            return 0;
        }

        if (chosenId is not null)
        {
            for (int index = 0; index < displays.Count; index++)
            {
                if (string.Equals(displays[index].Id, chosenId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }
        else if (legacyIndex > 0)
        {
            IReadOnlyList<int> order = MainFirst(displays);
            if (legacyIndex < order.Count)
            {
                return order[legacyIndex];
            }
        }

        return Main(displays);
    }

    /// <summary>Whether the remembered display is chosen but not attached.</summary>
    public static bool IsMissing(IReadOnlyList<DisplayIdentity> displays, string? chosenId) =>
        chosenId is not null
        && !displays.Any(display => string.Equals(display.Id, chosenId, StringComparison.OrdinalIgnoreCase));

    /// <summary>The main display's index; the first one when none says it is main.</summary>
    public static int Main(IReadOnlyList<DisplayIdentity> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        for (int index = 0; index < displays.Count; index++)
        {
            if (displays[index].IsPrimary)
            {
                return index;
            }
        }

        return 0;
    }

    /// <summary>A name for each display that tells it apart from the others.</summary>
    /// <remarks>
    /// Two identical monitors are the ordinary dual-monitor desk, and a menu offering
    /// "DELL U2720Q" twice offers nothing. The second and later of a name are numbered in
    /// the order given, and an empty name reads as "Display".
    /// </remarks>
    public static IReadOnlyList<string> Labels(IReadOnlyList<DisplayIdentity> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var labels = new string[displays.Count];
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totals = displays
            .GroupBy(display => Named(display.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < displays.Count; index++)
        {
            string name = Named(displays[index].Name);
            int count = seen[name] = seen.GetValueOrDefault(name) + 1;
            labels[index] = totals[name] > 1 ? $"{name} {count}" : name;
        }

        return labels;
    }

    private static string Named(string name) => string.IsNullOrWhiteSpace(name) ? "Display" : name.Trim();

    private static List<int> MainFirst(IReadOnlyList<DisplayIdentity> displays)
    {
        int main = Main(displays);
        var order = new List<int> { main };
        for (int index = 0; index < displays.Count; index++)
        {
            if (index != main)
            {
                order.Add(index);
            }
        }

        return order;
    }
}
