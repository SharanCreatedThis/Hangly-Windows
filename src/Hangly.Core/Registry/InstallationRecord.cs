//
//  InstallationRecord.cs
//  Hangly
//
//  This installation, as the registry knows it: one record, kept on disk.
//

namespace Hangly.Core.Registry;

/// <summary>One installation of Hangly: its permanent ID, what it is, and what the registry has accepted.</summary>
/// <remarks>
/// Stored as <c>installation.json</c> in <c>%AppData%\Hangly</c> (<see cref="InstallationStore"/>), apart from the
/// settings, so a settings reset or an update can never mint a second identity. macOS's
/// <c>InstallationRecord</c>, field for field.
///
/// <para>The file is also the upload queue: whatever differs from <see cref="Uploaded"/>, or a heartbeat once a
/// day, is due (<see cref="IsDue"/>), and stays due across restarts and lost networks until the registry
/// accepts it.</para>
/// </remarks>
public sealed record InstallationRecord
{
    /// <summary>The primary key of <c>users/{installationId}</c>. Created once; never changed.</summary>
    public required Guid InstallationId { get; init; }

    /// <summary>Proves this installation owns its document. Sent only to the registry, which keeps a hash.</summary>
    public required string WriteKey { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The earliest sign on this PC of when Hangly was first installed (<see cref="InstallEvidence"/>).</summary>
    /// <remarks>Sent so an installation from before 2.1.0 that had analytics switched off, and so has no PostHog
    /// record, keeps its real install date. The registry uses it only when it is more than a day old and no server
    /// saw the install.</remarks>
    public DateTimeOffset? InstalledAt { get; init; }

    /// <summary>What Hangly calls this person. Required before anything is sent; can be changed.</summary>
    public string Nickname { get; init; } = string.Empty;

    public string Platform { get; init; } = "windows";

    /// <summary>"Windows" — the registry's <c>osName</c>, beside the lowercase <see cref="Platform"/>.</summary>
    public string OsName => "Windows";

    public string OsVersion { get; init; } = string.Empty;

    public string AppVersion { get; init; } = string.Empty;

    public string Architecture { get; init; } = string.Empty;

    /// <summary>Where the registry last placed this installation. Never sent up: the registry returns it.</summary>
    public string? City { get; init; }

    public string? Region { get; init; }

    public string? Country { get; init; }

    /// <summary>What the registry last accepted; null until the first registration.</summary>
    public UploadedFacts? Uploaded { get; init; }

    /// <summary>The registry's <c>lastSeen</c> after the last accepted upload.</summary>
    public DateTimeOffset? LastSeen { get; init; }

    /// <summary>How often <c>lastSeen</c> is refreshed when nothing else has changed.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromHours(24);

    /// <summary>The facts as they are now, in the shape <see cref="Uploaded"/> records them.</summary>
    public UploadedFacts Current => new(Nickname, OsVersion, AppVersion, Architecture);

    /// <summary>Whether an upload is owed: the first registration, a changed nickname or version, or the daily heartbeat.</summary>
    /// <remarks>Never before there is a nickname: onboarding asks for one first.</remarks>
    public bool IsDue(DateTimeOffset now)
    {
        if (Nickname.Length == 0)
        {
            return false;
        }

        if (Uploaded is null || Uploaded != Current || LastSeen is null)
        {
            return true;
        }

        return now - LastSeen.Value >= HeartbeatInterval;
    }
}

/// <summary>The facts the registry last accepted.</summary>
public sealed record UploadedFacts(string Nickname, string OsVersion, string AppVersion, string Architecture);

/// <summary>When Hangly was first installed on this PC, as far as the PC can tell.</summary>
/// <remarks>
/// No build before 2.1.0 wrote the date down, but the first launch of any version made <c>%AppData%\Hangly</c> for
/// its settings, and Velopack made <c>%LocalAppData%\Hangly</c> to install into. Windows keeps a folder's creation
/// time across updates, so the earlier of the two is the evidence. macOS's <c>InstallEvidence</c>.
/// </remarks>
public static class InstallEvidence
{
    public static IReadOnlyList<string> DefaultFolders =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hangly"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hangly"),
    ];

    /// <summary>The earliest creation time among the folders that exist, or null when none does.</summary>
    public static DateTimeOffset? Earliest(IEnumerable<string> folders)
    {
        DateTimeOffset? earliest = null;
        foreach (string folder in folders)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                var created = new DateTimeOffset(Directory.GetCreationTimeUtc(folder), TimeSpan.Zero);
                if (earliest is null || created < earliest)
                {
                    earliest = created;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return earliest;
    }
}

/// <summary>What this machine is, for the record.</summary>
public sealed record PlatformFacts(string OsVersion, string AppVersion, string Architecture)
{
    public string Platform => "windows";
}
