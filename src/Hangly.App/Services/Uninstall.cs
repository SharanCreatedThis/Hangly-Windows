//
//  Uninstall.cs
//  Hangly
//
//  The last thing Hangly does on a machine.
//

using Hangly.App.Analytics;
using Hangly.Core.Analytics;
using Hangly.Core.Settings;

namespace Hangly.App.Services;

/// <summary>What runs when somebody uninstalls Hangly through Settings → Apps.</summary>
/// <remarks>
/// Velopack runs the installed copy with <c>--veloapp-uninstall</c> just before it removes
/// it, through <c>OnBeforeUninstallFastCallback</c>: no window may be shown, the work
/// cannot cancel the uninstall, and the process is killed after thirty seconds. Two things
/// are done, both quietly, each unable to stop the other.
///
/// <para><b>The run-at-login entry is removed.</b> It named an executable that is about to
/// be deleted, and before this every uninstall left Windows trying to start it at every
/// sign-in. That was hardening finding R2.</para>
///
/// <para><b>The project is told, if it knows the person.</b> One <c>app_uninstalled</c>,
/// only with sharing on and only for somebody already identified, with a ten-second limit
/// so the whole hook stays well inside Velopack's thirty. There is no retry: after this
/// there is no Hangly to retry from. An uninstall with no network, or one done by deleting
/// the folder by hand, goes unreported — PRIVACY.md says so.</para>
///
/// <para>The settings and the user's charms in <c>%APPDATA%\Hangly</c> are left where they
/// are, deliberately, so a reinstall remembers them. This does not change that.</para>
/// </remarks>
internal static class Uninstall
{
    private static readonly TimeSpan SendLimit = TimeSpan.FromSeconds(10);

    public static void Run()
    {
        Diagnostics.Log("uninstall: started");

        try
        {
            new RegistryLaunchAtLogin().SetEnabled(false);
            Diagnostics.Log("uninstall: run-at-login entry removed");
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("uninstall: run-at-login", exception);
        }

        try
        {
            if (!AppInfo.HasAnalyticsDestination)
            {
                Diagnostics.Log("uninstall: no analytics destination in this build; nothing sent");
                return;
            }

            var store = new SettingsStore(SettingsStore.DefaultPath);
            using var provider = new PostHogProvider(AppInfo.AnalyticsHost, AppInfo.AnalyticsKey);
            var manager = new AnalyticsManager(
                store,
                provider,
                AppInfo.AnalyticsHost,
                AppInfo.HasAnalyticsDestination,
                AppInfo.Version,
                AppInfo.BuildNumber,
                AppInfo.WindowsVersion);

            Task<bool> sending = manager.UninstalledAsync();
            bool finished = sending.Wait(SendLimit);
            Diagnostics.Log(finished && sending.Result
                ? "uninstall: reported"
                : "uninstall: not reported (off, never identified, offline or too slow)");
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("uninstall: report", exception);
        }
    }
}
