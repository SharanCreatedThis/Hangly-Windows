//
//  Diagnostics.cs
//  Hangly
//
//  How a window with no window tells you what went wrong.
//

using System.Text;

namespace Hangly.App.Services;

/// <summary>A log file, and the handlers that make sure something reaches it.</summary>
/// <remarks>
/// <b>Why this exists.</b> Hangly has no main window, no taskbar button and no console:
/// it is a tray icon and a transparent overlay. When it fails during startup there is
/// therefore nothing at all to see — no dialog, no output, no window that closes. The
/// first report from a real machine was exactly that, "no message, no response", which is
/// indistinguishable from the app not having been launched.
///
/// <para>So the app writes a line per startup step to a file it always knows the path of,
/// and installs handlers that catch what would otherwise be a silent exit. The last line
/// in the file is the step that failed. That turns "nothing happened" into an address.</para>
///
/// <para>Logging is best-effort and never throws: a diagnostic that can take the app down
/// is worse than no diagnostic. Every write is wrapped, and a failed write is dropped.</para>
/// </remarks>
public static class Diagnostics
{
    private static readonly Lock Gate = new();
    private static bool installed;

    /// <summary>Where the log goes. Beside the settings, so there is one folder to ask for.</summary>
    /// <remarks>
    /// Roaming for the same reason the settings are: <c>%LOCALAPPDATA%\Hangly</c> is
    /// Velopack's install directory, and an install clears it. A log that an installer
    /// deletes is a log that is missing exactly when someone needs it — the install that
    /// went wrong is the one you want to read about.
    /// </remarks>
    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Hangly",
        "hangly.log");

    /// <summary>Catches anything that would otherwise end the process quietly.</summary>
    /// <remarks>
    /// Handlers only. Starting a fresh log is <see cref="StartLog"/>'s job and happens
    /// later, for a reason worth writing down: this runs before the single-instance check,
    /// and a second copy of Hangly that truncated the log would erase the running copy's
    /// record of its own startup on its way to discovering it should exit. A second copy
    /// must leave the first one exactly as it found it, and that includes its log.
    /// </remarks>
    public static void Install()
    {
        if (installed)
        {
            return;
        }

        installed = true;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Failure("unhandled", args.ExceptionObject as Exception);

        // An exception thrown on a background thread inside a task nobody awaited would
        // otherwise disappear entirely.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Failure("unobserved task", args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>Begins this run's log, replacing the previous run's.</summary>
    /// <remarks>
    /// Truncated per launch rather than appended. The question this file answers is "what
    /// happened the last time I ran it", and a file that grows forever buries that under
    /// every previous run.
    ///
    /// <para>Called only by a process that is going to be the running Hangly — after the
    /// single-instance check on the ordinary path, and by each development switch, which
    /// has the machine to itself for the moment it takes.</para>
    /// </remarks>
    public static void StartLog()
    {
        try
        {
            string? directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                LogPath,
                $"Hangly {typeof(Diagnostics).Assembly.GetName().Version}" +
                $" · {Environment.OSVersion}" +
                $" · {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}" +
                $" · {DateTimeOffset.Now:O}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // No log is survivable. Failing to start because logging failed is not.
        }
    }

    /// <summary>Records that a startup step was reached.</summary>
    public static void Log(string message) => Write($"     {message}");

    /// <summary>Records a failure, with everything needed to place it.</summary>
    public static void Failure(string stage, Exception? exception)
    {
        var text = new StringBuilder();
        text.AppendLine($"FAIL [{stage}] {exception?.GetType().FullName}: {exception?.Message}");

        Exception? inner = exception?.InnerException;
        while (inner is not null)
        {
            text.AppendLine($"  caused by {inner.GetType().FullName}: {inner.Message}");
            inner = inner.InnerException;
        }

        text.Append(exception?.StackTrace);
        Write(text.ToString());
    }

    /// <summary>
    /// Opens and measures every charm, and writes the result to the log.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in the overlay because it runs before there is one: the
    /// <c>--check-artwork</c> switch does this and exits, so the log is the only place it
    /// can report to.
    /// </remarks>
    public static void CheckArtwork()
    {
        try
        {
            Overlay.CharmArtworkCache.ArtworkReport report =
                Overlay.CharmArtworkCache.CheckAll(Overlay.CharmArtworkCache.DefaultDirectory);

            Log($"artwork check: {report.Measured} measured, "
                + $"{report.Missing.Count} missing, {report.Unmeasured.Count} unmeasurable");

            foreach (string charm in report.Missing)
            {
                Log($"  MISSING   {charm}");
            }

            foreach (string charm in report.Unmeasured)
            {
                Log($"  UNMEASURED {charm}");
            }
        }
        catch (Exception exception)
        {
            Failure("artwork check", exception);
        }
    }

    /// <summary>
    /// Imports a file into a throwaway store and reports the outcome.
    /// </summary>
    /// <remarks>
    /// A development switch. The store is a temporary directory, so running this never
    /// adds anything to the charms the person actually has.
    /// </remarks>
    /// <summary>
    /// Sends one real event and writes the payload and the response to the log.
    /// </summary>
    /// <remarks>
    /// Uses the settings the person actually has — their name, their installation
    /// identifier — because a payload built from invented values would not answer the
    /// question being asked, which is what this copy of Hangly sends about this person.
    /// It respects the analytics toggle: with sharing off it reports that and sends
    /// nothing.
    /// </remarks>
    public static void CheckAnalytics()
    {
        try
        {
            var store = new Core.Settings.SettingsStore(Core.Settings.SettingsStore.DefaultPath);
            Log($"analytics check: sharing is {(store.Settings.Privacy.AnalyticsEnabled ? "on" : "off")}");
            Log($"analytics check: name is '{store.Settings.DisplayName}'");

            if (!store.Settings.Privacy.AnalyticsEnabled)
            {
                Log("analytics check: nothing sent, because sharing is off");
                return;
            }

            if (!AppInfo.HasAnalyticsDestination)
            {
                Log("analytics check: no key in this build, so there is nowhere to send");
                return;
            }

            using var provider = new Analytics.PostHogProvider(AppInfo.AnalyticsHost, AppInfo.AnalyticsKey);
            var manager = new Core.Analytics.AnalyticsManager(
                store,
                provider,
                AppInfo.AnalyticsHost,
                AppInfo.HasAnalyticsDestination,
                AppInfo.Version,
                AppInfo.BuildNumber,
                AppInfo.WindowsVersion);

            manager.Start();

            (string payload, int status) = provider
                .CheckAsync(Core.Analytics.Events.AppLaunch)
                .GetAwaiter()
                .GetResult();

            Log($"analytics check: HTTP {status}");
            Log($"analytics check payload: {payload}");
        }
        catch (Exception exception)
        {
            Failure("analytics check", exception);
        }
    }

    /// <summary>Runs a picture through the Create path and says what happened.</summary>
    /// <remarks>
    /// The counterpart of <see cref="CheckImport"/> for the Create tab: a raster goes
    /// through the wrapper and then the ordinary importer, into a scratch store, so the
    /// validation cases can be exercised on real files without a window and without
    /// touching anyone's charms.
    /// </remarks>
    public static void CheckCreate(string path)
    {
        try
        {
            string scratch = Path.Combine(
                Path.GetTempPath(),
                "hangly-create-check-" + Guid.NewGuid().ToString("N"));

            var store = new Core.Import.CustomCharmStore(scratch);
            Import.ImportOutcome outcome = Import.CharmImporter.ImportAny(
                path,
                Import.CharmImporter.NameFor(path),
                store);

            Log($"create check '{Path.GetFileName(path)}': "
                + $"{(outcome.IsAccepted ? "ACCEPTED" : "REFUSED")} — {outcome.Message}");

            if (outcome.IsAccepted && outcome.Entry is not null && store.PathFor(outcome.Entry) is string saved)
            {
                string markup = File.ReadAllText(saved);
                Log($"  stored {new FileInfo(saved).Length / 1024} KB, "
                    + $"raster kept: {markup.Contains("data:image/png;base64", StringComparison.Ordinal)}, "
                    + $"mass {outcome.Entry.Metrics.Mass:0.00}, knot {outcome.Entry.Metrics.KnotInset:0.00}");
            }

            if (System.IO.Directory.Exists(scratch))
            {
                System.IO.Directory.Delete(scratch, recursive: true);
            }
        }
        catch (Exception exception)
        {
            Failure("create check", exception);
        }
    }

    public static void CheckImport(string path)
    {
        try
        {
            string scratch = Path.Combine(
                Path.GetTempPath(),
                "hangly-import-check-" + Guid.NewGuid().ToString("N"));

            var store = new Core.Import.CustomCharmStore(scratch);
            Import.ImportOutcome outcome = Import.CharmImporter.Import(path, store);

            Log($"import check '{Path.GetFileName(path)}': "
                + $"{(outcome.IsAccepted ? "ACCEPTED" : "REFUSED")} — {outcome.Message}");

            if (outcome.IsAccepted && outcome.Entry is not null)
            {
                string stored = store.PathFor(outcome.Entry) is string saved
                    ? File.ReadAllText(saved)
                    : string.Empty;
                foreach (string forbidden in (string[])
                    ["<script", "onload", "onclick", "foreignObject", "@import", "attacker.example", "file:///"])
                {
                    if (stored.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"  LEAKED {forbidden}");
                    }
                }

                Log($"  mass {outcome.Entry.Metrics.Mass:F2}, knot {outcome.Entry.Metrics.KnotInset:F2}, "
                    + $"name '{outcome.Entry.Name}'");
            }

            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
        catch (Exception exception)
        {
            Failure("import check", exception);
        }
    }

    private static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Deliberately swallowed. See the note on the type.
        }
    }
}
