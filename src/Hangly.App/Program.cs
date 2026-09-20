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
        Diagnostics.Install();

        VelopackApp.Build().Run();

        // A development switch, not a feature. It opens and measures all eighty-one
        // charms and writes what failed, which is the one question a screenshot of three
        // of them cannot answer. The macOS build has the same check and calls it from its
        // own development-only launch path.
        if (args.Contains("--check-artwork", StringComparer.Ordinal))
        {
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
            Diagnostics.CheckImport(args[check + 1]);
            return;
        }

        XamlGeneratedProgram.XamlGeneratedMain();
    }
}
