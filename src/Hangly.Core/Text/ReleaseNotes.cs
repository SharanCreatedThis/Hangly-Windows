//
//  ReleaseNotes.cs
//  Hangly
//
//  Turning a release's markdown into something a plain text box can show.
//

using System.Text.RegularExpressions;

namespace Hangly.Core.Text;

/// <summary>Reads release notes as text rather than as markup.</summary>
/// <remarks>
/// Velopack hands over whatever the release was packaged with, which is Markdown, and the
/// About page has no Markdown renderer. Rather than ship a renderer for a box that shows
/// a few lines a year, the markers that would otherwise read as litter are removed and the
/// words are left alone.
///
/// <para>Here rather than beside the updater because it is a function of a string, and
/// something that is only a function of a string can be tested without a window.</para>
/// </remarks>
public static class ReleaseNotes
{
    private static readonly Regex Bullet = new(@"^[-*+]\s+", RegexOptions.Compiled);
    private static readonly Regex Link = new(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Emphasis = new(@"(\*\*|__|\*|`)", RegexOptions.Compiled);
    private static readonly Regex BlankRun = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>Strips the markup and keeps the words.</summary>
    /// <remarks>
    /// Link text survives and the URL beside it does not, because a bare URL in a box
    /// nobody can click is noise. Bullets become a real bullet character rather than
    /// disappearing: a list that loses its marks reads as one long sentence.
    /// </remarks>
    public static string Plain(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = new List<string>();

        foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();

            // A rule is a separator drawn in characters. Without the rule it is litter.
            if (line is "---" or "***" or "___")
            {
                continue;
            }

            line = line.TrimStart('#', '>', ' ');
            line = Bullet.Replace(line, "• ");
            line = Link.Replace(line, "$1");
            line = Emphasis.Replace(line, string.Empty);

            lines.Add(line.TrimEnd());
        }

        // Runs of blank lines collapse, because Markdown uses them for spacing that a
        // wrapped text box does not need.
        return BlankRun.Replace(string.Join("\n", lines), "\n\n").Trim();
    }
}
