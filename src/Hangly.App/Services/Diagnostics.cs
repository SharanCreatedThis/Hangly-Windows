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

    /// <summary>Starts a fresh log and catches anything that would end the process quietly.</summary>
    public static void Install()
    {
        if (installed)
        {
            return;
        }

        installed = true;

        try
        {
            string? directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Truncated per launch rather than appended. The question this file answers is
            // "what happened the last time I ran it", and a file that grows forever buries
            // that under every previous run.
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
