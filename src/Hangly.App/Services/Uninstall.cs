//
//  Uninstall.cs
//  Hangly
//
//  The last thing Hangly does on a machine.
//


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
/// <para><b>The registry is told, if this installation is registered.</b> One request with
/// <c>event: "uninstall"</c>, with a ten-second limit so the whole hook stays well inside
/// Velopack's thirty. There is no retry: after this there is no Hangly to retry from. An
/// uninstall with no network, or one done by deleting the folder by hand, is still counted —
/// later — when the installation falls silent (the dashboard's thirty-day expiry).</para>
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
            if (Installation.HttpRegistryClient.For(AppInfo.RegistryUrl) is not { } registry)
            {
                Diagnostics.Log("uninstall: no registry in this build; nothing sent");
                return;
            }

            // The registry marks the installation uninstalled (uninstalledAt), which the dashboard counts at once
            // rather than waiting for thirty days of silence. installation.json stays in %AppData%, so a reinstall
            // is the same installation and is counted as one.
            using var timeout = new CancellationTokenSource(SendLimit);
            Task<bool> sending = Core.Registry.RegistrySync.SendUninstallAsync(
                new Core.Registry.InstallationStore(), registry, timeout.Token);
            bool finished = sending.Wait(SendLimit);
            Diagnostics.Log(finished && sending.Result
                ? "uninstall: reported to the registry"
                : "uninstall: not reported (never registered, offline or too slow)");
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("uninstall: report", exception);
        }
    }
}
