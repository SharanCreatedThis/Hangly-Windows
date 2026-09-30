//
//  LaunchAtLoginDefault.cs
//  Hangly
//
//  Starting Hangly at sign-in is the default, for everyone, once.
//

namespace Hangly.Core.Lifecycle;

/// <summary>Whether this launch should switch starting at sign-in on.</summary>
/// <remarks>
/// Before 2.1.2 the entry was written only on a first run that still needed the welcome. Every later launch copies the
/// system back into the setting, so an install that reached 2.x without the entry — a reinstall over existing
/// settings, an update from a copy that never wrote it, an entry the uninstaller removed — was turned off for good,
/// and people found Hangly off at sign-in by default.
///
/// <para>2.1.2 switches it on once for every installation, then records that it has, and from then on the setting
/// follows the system again, so turning it off in Customize (or in Task Manager's Startup apps) is respected.
/// Nothing before 2.1.2 recorded whether "off" was chosen, so those who had chosen it get it on once too.</para>
/// </remarks>
public static class LaunchAtLoginDefault
{
    public static bool IsDue(Settings.AppSettings settings) => !settings.Milestones.LaunchAtLoginDefaulted;

    public static Settings.AppSettings Applied(Settings.AppSettings settings) => settings with
    {
        Milestones = settings.Milestones with { LaunchAtLoginDefaulted = true },
    };
}
