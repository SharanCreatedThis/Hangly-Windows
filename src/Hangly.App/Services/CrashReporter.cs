//
//  CrashReporter.cs
//  Hangly
//
//  Turns a failure into a crash report on disk, for the registry to send next launch.
//

using Hangly.Core.Crashes;

namespace Hangly.App.Services;

/// <summary>Records crashes and non-fatal errors, from the one place every failure is already reported.</summary>
/// <remarks>
/// <see cref="Diagnostics.Failure"/> sees every unhandled exception — the AppDomain's, WinUI's and unobserved
/// tasks' — and every failure Hangly catches and carries on from. This writes each as a <see cref="CrashReport"/>
/// (<see cref="CrashStore"/>), which the registry sends once this installation is registered.
///
/// <para><b>Fatal</b> is the AppDomain's unhandled exception: the process is ending. Everything else is non-fatal,
/// and at most <see cref="NonFatalLimit"/> of those are kept per launch, so a failure repeated every frame is one
/// report, not thousands.</para>
/// </remarks>
internal static class CrashReporter
{
    public const int NonFatalLimit = 5;

    private static readonly CrashStore Store = new();
    private static int nonFatalThisLaunch;

    public static void Record(string stage, Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        bool fatal = stage == "unhandled";
        if (!fatal && Interlocked.Increment(ref nonFatalThisLaunch) > NonFatalLimit)
        {
            return;
        }

        string source = stage switch
        {
            "unhandled" => "AppDomain",
            "xaml" => "Application",
            "unobserved task" => "TaskScheduler",
            _ => "Diagnostics",
        };
        Store.Save(new CrashReport(
            DateTimeOffset.UtcNow,
            fatal,
            exception.GetType().FullName ?? exception.GetType().Name,
            $"[{stage}] {exception.Message}",
            exception.ToString(),
            source,
            AppInfo.Version,
            AppInfo.WindowsVersion,
            AppInfo.Architecture));
    }

    /// <summary>The store the registry uploads from.</summary>
    public static CrashStore Pending => Store;
}
