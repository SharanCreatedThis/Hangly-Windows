//
//  LaunchIntent.cs
//  Hangly
//
//  Where a launch should land: the welcome card, the Library, or nowhere.
//

namespace Hangly.Core.Lifecycle;

/// <summary>What a launch opens.</summary>
public enum LaunchDestination
{
    /// <summary>Onboarding is not finished: the welcome card, as on every first launch.</summary>
    Welcome,

    /// <summary>A person opened Hangly: the Library, the control centre for the charms.</summary>
    Library,

    /// <summary>Windows started it — at sign-in, or after an update — and nobody asked for a window.</summary>
    Quiet,
}

/// <summary>Decides where a cold launch lands. The macOS app follows the same rule.</summary>
/// <remarks>
/// <b>Why a launch opens the Library at all.</b> Hangly has no main window, so a person
/// who starts it from the Start menu, a shortcut or the .exe — usually to change their
/// charm — used to get nothing visible, which read as broken. Opening Hangly is now how you
/// get to your charms, whether it was running already (see <see cref="RelaunchSignal"/>) or
/// not.
///
/// <para><b>Why not every launch.</b> Windows starts Hangly at sign-in, and Velopack
/// restarts it after an update. A Library opening in front of somebody every morning, or
/// in the middle of their work after a silent update, would be the app answering a
/// question nobody asked. Those launches say so with an argument (<see cref="LoginArgument"/>,
/// <see cref="UpdatedArgument"/>).</para>
///
/// <para><b>The one guess.</b> A sign-in entry written by an older build has no argument,
/// and the first sign-in after updating starts from it before it can be rewritten. So an
/// argument-less launch within <see cref="BootGrace"/> of the PC starting counts as a
/// sign-in when the entry is the old kind. It is a guess used only while an old entry
/// exists, and it errs towards quiet.</para>
/// </remarks>
public static class LaunchIntent
{
    /// <summary>Added to the sign-in entry this build writes.</summary>
    public const string LoginArgument = "--login";

    /// <summary>Passed by the updater when it restarts the app after applying an update.</summary>
    public const string UpdatedArgument = "--updated";

    /// <summary>How soon after the PC starts an argument-less launch is taken for an old sign-in entry.</summary>
    public static readonly TimeSpan BootGrace = TimeSpan.FromMinutes(3);

    /// <param name="arguments">The command line, without the program.</param>
    /// <param name="needsWelcome">Whether onboarding (the name) is still owed.</param>
    /// <param name="sinceBoot">How long the PC has been running.</param>
    /// <param name="hasLegacyLoginEntry">Whether the sign-in entry predates <see cref="LoginArgument"/>.</param>
    public static LaunchDestination Decide(
        IReadOnlyCollection<string> arguments,
        bool needsWelcome,
        TimeSpan sinceBoot,
        bool hasLegacyLoginEntry)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (needsWelcome)
        {
            return LaunchDestination.Welcome;
        }

        if (arguments.Contains(LoginArgument, StringComparer.OrdinalIgnoreCase)
            || arguments.Contains(UpdatedArgument, StringComparer.OrdinalIgnoreCase))
        {
            return LaunchDestination.Quiet;
        }

        return hasLegacyLoginEntry && sinceBoot < BootGrace
            ? LaunchDestination.Quiet
            : LaunchDestination.Library;
    }
}

/// <summary>The sign-in entry's command line: written one way, read back either way.</summary>
public static class LoginEntry
{
    /// <summary>What this build writes: the quoted path, then <see cref="LaunchIntent.LoginArgument"/>.</summary>
    public static string Format(string executable) => $"\"{executable}\" {LaunchIntent.LoginArgument}";

    /// <summary>The program an entry starts, and whether it says it is a sign-in launch.</summary>
    public static (string Executable, bool MarksLogin) Parse(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string trimmed = entry.Trim();
        string executable;
        string rest;
        if (trimmed.StartsWith('"'))
        {
            int close = trimmed.IndexOf('"', 1);
            executable = close > 0 ? trimmed[1..close] : trimmed.Trim('"');
            rest = close > 0 ? trimmed[(close + 1)..] : string.Empty;
        }
        else
        {
            int space = trimmed.IndexOf(' ');
            executable = space > 0 ? trimmed[..space] : trimmed;
            rest = space > 0 ? trimmed[space..] : string.Empty;
        }

        bool marks = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(LaunchIntent.LoginArgument, StringComparer.OrdinalIgnoreCase);
        return (executable, marks);
    }
}
