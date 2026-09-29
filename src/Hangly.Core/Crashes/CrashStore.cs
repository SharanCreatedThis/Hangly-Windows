//
//  CrashStore.cs
//  Hangly
//
//  Crash reports written at the moment of the crash, uploaded on the next launch.
//

using System.Text.Json;

namespace Hangly.Core.Crashes;

/// <summary>One crash, as it leaves the machine: <c>crashReports/{id}</c> in Firestore, through the registry.</summary>
public sealed record CrashReport(
    DateTimeOffset OccurredAt,
    bool Fatal,
    string ExceptionType,
    string Message,
    string StackTrace,
    string Source,
    string AppVersion,
    string OsVersion,
    string Architecture);

/// <summary>Keeps crash reports in <c>%LocalAppData%\Hangly\crashes</c> until the registry has them.</summary>
/// <remarks>
/// <b>Written synchronously, at the crash.</b> A process dying of an unhandled exception has no time for the network;
/// it has time to write a file. The next launch sends what it finds and deletes each one only once the registry has
/// accepted it, so a crash while offline is reported later rather than lost.
///
/// <para><b>Scrubbed before it is written:</b> the user profile path and the machine's user name become
/// <c>%USERPROFILE%</c> and <c>%USERNAME%</c>, so a stack trace from a charm in someone's Documents folder does not
/// carry their account name. Messages are cut to 4 KB and stacks to 64 KB, the registry's limits.</para>
/// </remarks>
public sealed class CrashStore(string? directory = null)
{
    public const int MessageLimit = 4 * 1024;
    public const int StackLimit = 64 * 1024;

    /// <summary>At most this many reports are kept waiting; a crash loop keeps the newest.</summary>
    public const int PendingLimit = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Directory { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hangly", "crashes");

    /// <summary>Writes a report now. Never throws: this runs while the process is dying.</summary>
    public void Save(CrashReport report)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            CrashReport clean = Scrub(report);
            string path = Path.Combine(Directory, $"{clean.OccurredAt.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(clean, Json));
            Trim();
        }
        catch
        {
            // Nothing left to do about a crash that cannot even be written down.
        }
    }

    /// <summary>The reports waiting to be sent, oldest first, with the file each came from.</summary>
    public IReadOnlyList<(string Path, CrashReport Report)> Pending()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        var pending = new List<(string, CrashReport)>();
        foreach (string path in System.IO.Directory.GetFiles(Directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (JsonSerializer.Deserialize<CrashReport>(File.ReadAllText(path), Json) is CrashReport report)
                {
                    pending.Add((path, report));
                    continue;
                }
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
            }

            TryDelete(path);
        }

        return pending;
    }

    /// <summary>Removes a report the registry has accepted.</summary>
    public void Remove(string path) => TryDelete(path);

    private void Trim()
    {
        string[] files = System.IO.Directory.GetFiles(Directory, "*.json");
        foreach (string old in files.Order(StringComparer.Ordinal).Take(Math.Max(0, files.Length - PendingLimit)))
        {
            TryDelete(old);
        }
    }

    /// <summary>The report with the profile path and user name taken out, and cut to the registry's limits.</summary>
    public static CrashReport Scrub(CrashReport report, string? profile = null, string? userName = null)
    {
        profile ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        userName ??= Environment.UserName;

        string Clean(string text, int limit)
        {
            if (profile.Length > 0)
            {
                text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            }

            if (userName.Length > 2)
            {
                text = text.Replace(userName, "%USERNAME%", StringComparison.OrdinalIgnoreCase);
            }

            return text.Length > limit ? text[..limit] : text;
        }

        return report with
        {
            Message = Clean(report.Message, MessageLimit),
            StackTrace = Clean(report.StackTrace, StackLimit),
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
