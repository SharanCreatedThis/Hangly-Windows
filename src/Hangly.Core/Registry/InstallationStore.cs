//
//  InstallationStore.cs
//  Hangly
//
//  Where the installation's permanent identity lives.
//

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hangly.Core.Registry;

/// <summary>Keeps <c>installation.json</c> in <c>%AppData%\Hangly</c>, next to the settings.</summary>
/// <remarks>
/// <b>Created once, never regenerated.</b> The ID is made on the first launch that has none and is then only ever
/// read. It is kept out of <c>settings.json</c> on purpose: a settings reset, a corrupt settings file or an update
/// all leave this file alone, and Velopack's installs and uninstalls touch <c>%LocalAppData%</c>, never
/// <c>%AppData%</c>. So updating Hangly can never register a second installation. macOS's
/// <c>InstallationStore</c>.
///
/// <para>Written atomically (a temporary file, then replaced over), so a crash mid-write leaves the previous
/// file, never half of one.</para>
/// </remarks>
public sealed class InstallationStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public InstallationStore(string? directory = null)
    {
        FilePath = Path.Combine(directory ?? DefaultDirectory, "installation.json");
    }

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Hangly");

    public string FilePath { get; }

    public InstallationRecord? Record { get; private set; }

    /// <summary>The record if there is one, without creating it: for the uninstall hook, which must not register.</summary>
    public InstallationRecord? LoadExisting() => Record = Read();

    /// <summary>Reads the record, or creates it if there is none.</summary>
    /// <param name="legacyId">The analytics identifier an earlier build kept. Adopted as the installation ID on
    /// creation, so a person who used an earlier version is the same installation, not a new one.</param>
    /// <param name="installedAt">The local evidence of the original install (<see cref="InstallEvidence"/>), recorded
    /// once; a record from before it was kept gets it on the next load.</param>
    public InstallationRecord LoadOrCreate(
        Guid? legacyId, PlatformFacts facts, string nickname, DateTimeOffset now, DateTimeOffset? installedAt = null)
    {
        if (Read() is InstallationRecord existing)
        {
            Record = existing;
            if (existing.InstalledAt is null && installedAt is not null)
            {
                Update(record => record with { InstalledAt = installedAt });
            }

            return Record ?? existing;
        }

        var created = new InstallationRecord
        {
            InstallationId = legacyId ?? Guid.NewGuid(),
            WriteKey = MakeWriteKey(),
            CreatedAt = now,
            InstalledAt = installedAt is { } date && date > now ? now : installedAt,
            Nickname = nickname,
            OsVersion = facts.OsVersion,
            AppVersion = facts.AppVersion,
            Architecture = facts.Architecture,
        };
        Record = created;
        Save();
        return created;
    }

    /// <summary>Changes the record and writes it at once. The ID and write key are init-only and cannot change.</summary>
    public void Update(Func<InstallationRecord, InstallationRecord> change)
    {
        if (Record is not InstallationRecord current)
        {
            return;
        }

        InstallationRecord next = change(current) with
        {
            InstallationId = current.InstallationId,
            WriteKey = current.WriteKey,
            CreatedAt = current.CreatedAt,
        };
        if (next == current)
        {
            return;
        }

        Record = next;
        Save();
    }

    private InstallationRecord? Read()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            InstallationRecord? record = JsonSerializer.Deserialize<InstallationRecord>(File.ReadAllText(FilePath), Json);
            if (record is not null && record.InstallationId != Guid.Empty && record.WriteKey.Length > 0)
            {
                return record;
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        // Unreadable. Kept aside rather than overwritten, so nothing is destroyed, and a new record is made. The old
        // ID cannot be used without its write key, which is what proves ownership of its document.
        try
        {
            File.Move(FilePath, Path.Combine(Path.GetDirectoryName(FilePath)!, $"installation.corrupt-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json"));
        }
        catch (IOException)
        {
        }

        return null;
    }

    private void Save()
    {
        if (Record is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Record, Json));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Kept in memory; written on the next change. Nothing is sent that the file does not also hold.
        }
    }

    /// <summary>32 random bytes, base64url without padding: 43 characters.</summary>
    public static string MakeWriteKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
