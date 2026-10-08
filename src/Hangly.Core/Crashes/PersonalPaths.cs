//
//  PersonalPaths.cs
//  Hangly
//
//  Taking the user's profile path and user name out of text that leaves their hands: crash reports and the log.
//

using System.Text.RegularExpressions;

namespace Hangly.Core.Crashes;

/// <summary>Replaces the profile path with <c>%USERPROFILE%</c> and the user name with <c>%USERNAME%</c>.</summary>
/// <remarks>
/// The user name is replaced only as a whole word. Replaced anywhere, a short name rewrote ordinary text: a crash report
/// from a user called "Pro" arrived naming <c>Follow%USERNAME%mpt</c> for <c>FollowPrompt</c>. A name of two letters or
/// fewer is left alone for the same reason; the profile path still goes, which is where a name usually appears.
/// </remarks>
public sealed class PersonalPaths
{
    private readonly string profile;
    private readonly Regex? userName;

    public PersonalPaths(string? profile = null, string? userName = null)
    {
        this.profile = profile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string name = userName ?? Environment.UserName;
        this.userName = name.Length > 2
            ? new Regex($@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            : null;
    }

    /// <summary>The current user's, made once: the log redacts every line with it.</summary>
    public static PersonalPaths Current { get; } = new();

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (profile.Length > 0)
        {
            text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        return userName is null ? text : userName.Replace(text, "%USERNAME%");
    }
}
