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
/// <para><b>When.</b> Once, on the first launch of a new feature version after an update — 2.1, 2.0 or 0.9 to 2.2, not
/// 2.2.0 to 2.2.1: a patch stays silent. Never on a first install, which gets the welcome instead.</para>
/// </remarks>
public static class ReleaseHighlights
{
    /// <summary>The feature version these words describe.</summary>
    public const string FeatureVersion = "2.3";

    public const string Heading = "What's new in Hangly 2.3";

    public const string Lead = "Hangly updated itself. Here's what's new.";

    public static IReadOnlyList<Highlight> Items { get; } =
    [
        new("Notification Center", "News about new charms and collections now arrives under your charm, once, and waits in the Notification Center."),
        new("Update from the charm", "When a new version is ready, a card under the charm says what's new. Update Now installs it, or choose Later."),
        new("Right-click the charm", "Notifications, the Library and Hide Charm, right where the charm hangs."),
        new("Works with Smart App Control", "Hangly no longer uses the unsigned files Windows 11 blocked, so it starts on every PC."),
        new("Faster, smaller updates", "The Library opens without freezing, and an update downloads only what changed."),
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
