//
//  ReleaseHighlights.cs
//  Hangly
//
//  What's new, as the window after an update shows it.
//

namespace Hangly.Core.Text;

/// <summary>One thing that is new, in a line or two.</summary>
public sealed record Highlight(string Title, string Text);

/// <summary>The release notes window's words, and when it opens. macOS's <c>ReleaseNotesSheet</c> says the same.</summary>
/// <remarks>
/// A short, written summary rather than the changelog, which is long and is for the release page
/// (<see cref="ReleaseNotesUrl"/> links to it). Written for somebody coming back to their PC to find Hangly updated.
///
/// <para><b>When.</b> Once, on the first launch of a new feature version after an update — 2.0 or 0.9 to 2.1, not
/// 2.1.0 to 2.1.1: a patch stays silent. Never on a first install, which gets the welcome instead.</para>
/// </remarks>
public static class ReleaseHighlights
{
    /// <summary>The feature version these words describe.</summary>
    public const string FeatureVersion = "2.1";

    public const string Heading = "What's new in Hangly 2.1";

    public const string Lead = "Hangly updated itself. Here's what's new.";

    public static IReadOnlyList<Highlight> Items { get; } =
    [
        new("Spider-Man drops in", "With Spider-Man on the rope, Hangly starts with a web across the top of your screen and him dropping in on his strand."),
        new("An elastic rope", "Pull the charm and the cord stretches, then bounces back and settles. Appearance → Motion → Rope."),
        new("Charms that react", "Move quickly towards a charm and it swings away; reach slowly and it lets you take hold."),
        new("On top, or on your desktop", "Hang the charm above every window or on your wallpaper, with a soft or strong glow."),
        new("Sound, and a rope shelf", "Every charm sounds like what it's made of, and Library → Ropes shows every cord."),
        new("Creator Studio", "Drop a picture on the charm and it opens in the Studio, ready to hang."),
        new("Quiet updates", "Hangly now updates itself while you're away, and shows you what's new when you're back."),
        new("Your installation record", "Hangly keeps one record for this PC: the name you gave, your city and your versions. Appearance → Privacy says exactly what."),
    ];

    /// <summary>Whether this launch opens the release notes.</summary>
    /// <param name="previousVersion">The version the last launch ran; null if never recorded (0.9.x did not record it).</param>
    /// <param name="currentVersion">This build's version.</param>
    /// <param name="updatedBefore">Whether this install has run before — an update rather than a first install.</param>
    public static bool ShouldShow(string? previousVersion, string currentVersion, bool updatedBefore)
    {
        if (Feature(currentVersion) != FeatureVersion)
        {
            return false;
        }

        return previousVersion is null ? updatedBefore : Feature(previousVersion) != FeatureVersion;
    }

    /// <summary>"2.1.3" → "2.1".</summary>
    public static string Feature(string version)
    {
        string[] parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }
}
