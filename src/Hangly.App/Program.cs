//
//  Program.cs
//  Hangly
//
//  The entry point, which exists only so something can run before XAML does.
//

using Hangly.App.Services;
using Velopack;

namespace Hangly.App;

/// <summary>Hangly's entry point.</summary>
/// <remarks>
/// WinUI generates one of these, and it is perfectly good: <c>DISABLE_XAML_GENERATED_MAIN</c>
/// does not remove it, it renames it to <see cref="XamlGeneratedProgram.XamlGeneratedMain"/>
/// and lets something else decide when to call it. Nothing here reimplements the WinUI
/// startup sequence — COM wrappers, the dispatcher queue's synchronisation context and
/// <c>Application.Start</c> are all still the generated code's business.
///
/// <para><b>Why there is anything before it.</b> Velopack drives installation, update and
/// uninstallation by relaunching the app with its own arguments, doing the work, and
/// exiting. That has to happen before a window, a tray icon or a settings file exists,
/// because on an update run none of them should be created at all — the process is there
/// to move files and leave. <c>VelopackApp.Build().Run()</c> is therefore the first thing
/// that happens, and on an ordinary launch it returns immediately and costs nothing.</para>
/// </remarks>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Installed before Velopack rather than after, because a hook that fails is
        // invisible: an update run has no window to report from and exits on its own.
        // Handlers only — the log is not started until this process knows it is the
        // Hangly that will run. See Diagnostics.Install.
        Diagnostics.Install();

        // The uninstall hook removes the run-at-login entry and, if the project knows this
        // person, tells it once. See Services/Uninstall.cs for what it may and may not do.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Uninstall.Run())
            .Run();

        // A development switch, not a feature. It opens and measures all seventy
        // charms and writes what failed, which is the one question a screenshot of three
        // of them cannot answer. The macOS build has the same check and calls it from its
        // own development-only launch path.
        if (args.Contains("--check-artwork", StringComparer.Ordinal))
        {
            Diagnostics.StartLog();
            Diagnostics.CheckArtwork();
            return;
        }

        // Runs a file through the real importer and says what happened, without a window
        // and without touching the user's charms. The rejection paths are the ones worth
        // exercising on real files — a hostile SVG is not something to hand-write a
        // fixture for when the actual file is right there.
        int check = Array.IndexOf(args, "--check-import");
        if (check >= 0 && check + 1 < args.Length)
        {
            Diagnostics.StartLog();
            Diagnostics.CheckImport(args[check + 1]);
            return;
        }

        // Sends one real event with the real key and reports the payload and the status
        // code. The dashboard is not the only place a privacy claim should be checkable,
        // and this is what makes "exactly this leaves the machine" a fact rather than a
        // promise.
        int create = Array.IndexOf(args, "--check-create");
        if (create >= 0 && create + 1 < args.Length)
        {
            Diagnostics.StartLog();
            Diagnostics.CheckCreate(args[create + 1]);
            return;
        }

        // Every charm on the rope, drawn offscreen by the real renderer; see RopeSheet.
        int sheet = Array.IndexOf(args, "--rope-sheet");
        if (sheet >= 0 && sheet + 1 < args.Length)
        {
            RopeSheet.Write(args[sheet + 1]);
            return;
        }

        if (args.Contains("--check-registry", StringComparer.Ordinal))
        {
            Diagnostics.StartLog();
            Diagnostics.CheckRegistry();
            return;
        }

        // Last, and only on the path that builds a window. Velopack's hooks above run in
        // their own processes and must not be turned away, and the development switches
        // have already done their work and returned.
        if (!SingleInstance.Claim())
        {
            // Opening Hangly again is how people look for it — from the Start menu, a
            // shortcut, the .exe — usually to change their charm. The running copy is
            // told, and opens its Library in front; this copy leaves. It used to leave
            // silently, which read as a click that did nothing. The line is appended to
            // the running copy's log, as before.
            bool told = SingleInstance.TellRunningCopy();
            Diagnostics.Log(told
                ? "Hangly is already running; asked it to open the Library"
                : "Hangly is already running but did not answer; this copy is exiting");
            return;
        }

        Diagnostics.StartLog();
        XamlGeneratedProgram.XamlGeneratedMain();
    }
}
