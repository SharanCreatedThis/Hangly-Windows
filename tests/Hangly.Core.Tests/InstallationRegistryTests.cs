using Hangly.Core.Registry;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The installation registry. The same cases as macOS's <c>InstallationRegistryTests</c>.</summary>
public sealed class InstallationRegistryTests : IDisposable
{
    private static readonly PlatformFacts Windows = new("10.0.26100.0", "2.1.0", "x64");
    private readonly string folder = Path.Combine(Path.GetTempPath(), "hangly-registry-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // The permanent ID

    [Fact]
    public void CreatedOnceEveryLaterLoadReturnsTheSameIdAndWriteKey()
    {
        InstallationRecord first = new InstallationStore(folder).LoadOrCreate(null, Windows, string.Empty, DateTimeOffset.UtcNow);
        InstallationRecord again = new InstallationStore(folder).LoadOrCreate(null, Windows, "Sharan", DateTimeOffset.UtcNow);
        Assert.Equal(first.InstallationId, again.InstallationId);
        Assert.Equal(first.WriteKey, again.WriteKey);
        Assert.Equal(43, first.WriteKey.Length);
    }

    [Fact]
    public void AnUpdateOrASettingsResetCannotMintASecondInstallation()
    {
        InstallationRecord original = new InstallationStore(folder).LoadOrCreate(null, Windows, "Sharan", DateTimeOffset.UtcNow);
        InstallationRecord updated = new InstallationStore(folder)
            .LoadOrCreate(Guid.NewGuid(), Windows with { AppVersion = "2.2.0" }, string.Empty, DateTimeOffset.UtcNow);
        Assert.Equal(original.InstallationId, updated.InstallationId);
    }

    [Fact]
    public void AnEarlierBuildsAnalyticsIdentifierBecomesTheInstallationId()
    {
        Guid legacy = Guid.NewGuid();
        Assert.Equal(legacy, new InstallationStore(folder).LoadOrCreate(legacy, Windows, string.Empty, DateTimeOffset.UtcNow).InstallationId);
    }

    [Fact]
    public void AnUnreadableFileIsKeptAsideNeverOverwritten()
    {
        var store = new InstallationStore(folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(store.FilePath, "not json");
        store.LoadOrCreate(null, Windows, string.Empty, DateTimeOffset.UtcNow);
        Assert.Contains(Directory.GetFiles(folder), path => Path.GetFileName(path).StartsWith("installation.corrupt-", StringComparison.Ordinal));
        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public void TheIdAndWriteKeyCannotBeChangedThroughUpdate()
    {
        var store = new InstallationStore(folder);
        InstallationRecord created = store.LoadOrCreate(null, Windows, "Sharan", DateTimeOffset.UtcNow);
        store.Update(record => record with { InstallationId = Guid.NewGuid(), WriteKey = "x", Nickname = "Sharan K" });
        Assert.Equal(created.InstallationId, store.Record!.InstallationId);
        Assert.Equal(created.WriteKey, store.Record.WriteKey);
        Assert.Equal("Sharan K", new InstallationStore(folder).LoadOrCreate(null, Windows, string.Empty, DateTimeOffset.UtcNow).Nickname);
    }

    // What is due

    [Fact]
    public void NothingIsDueUntilThereIsAName()
    {
        InstallationRecord record = new InstallationStore(folder).LoadOrCreate(null, Windows, string.Empty, DateTimeOffset.UtcNow);
        Assert.False(record.IsDue(DateTimeOffset.UtcNow));
        Assert.True((record with { Nickname = "Sharan" }).IsDue(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AfterARegistrationOnlyAChangeOrADaysHeartbeatIsDue()
    {
        var start = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        InstallationRecord record = new InstallationStore(folder).LoadOrCreate(null, Windows, "Sharan", start);
        record = record with { Uploaded = record.Current, LastSeen = start };
        Assert.False(record.IsDue(start.AddHours(23)));
        Assert.True(record.IsDue(start.AddHours(24)));
        Assert.True((record with { Nickname = "Sharan K" }).IsDue(start.AddMinutes(1)));
        Assert.True((record with { AppVersion = "2.2.0" }).IsDue(start.AddMinutes(1)));
    }

    // Sending

    private (RegistrySync Sync, SettingsStore Settings) MakeSync(
        FakeRegistryClient? client,
        string name = "Sharan",
        Func<DateTimeOffset>? now = null,
        Hangly.Core.Crashes.CrashStore? crashes = null,
        DateTimeOffset? installedAt = null,
        Func<AppSettings, AppSettings>? configure = null)
    {
        var settings = new SettingsStore(Path.Combine(folder, "settings.json"));
        settings.Update(s => (configure ?? (x => x))(s with { DisplayName = name }));
        var sync = new RegistrySync(
            new InstallationStore(folder), client, settings, Windows, TimeSpan.FromHours(1), now, crashes: crashes,
            installEvidence: () => installedAt);
        return (sync, settings);
    }

    [Fact]
    public void TheInstallDateIsTheEarliestFolderTheFirstLaunchMade()
    {
        string roaming = Path.Combine(folder, "Roaming", "Hangly");
        string local = Path.Combine(folder, "Local", "Hangly");
        Directory.CreateDirectory(roaming);
        Directory.CreateDirectory(local);
        Directory.SetCreationTimeUtc(roaming, new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc));
        Directory.SetCreationTimeUtc(local, new DateTime(2026, 9, 22, 8, 0, 0, DateTimeKind.Utc));
        Assert.Equal(
            new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero),
            InstallEvidence.Earliest([roaming, local, Path.Combine(folder, "missing")]));
        Assert.Null(InstallEvidence.Earliest([Path.Combine(folder, "missing")]));
    }

    [Fact]
    public void TheInstallDateIsRecordedOnceFilledInForAnOlderRecordAndSent()
    {
        var installed = new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);
        InstallationRecord first = new InstallationStore(folder).LoadOrCreate(null, Windows, "Sharan", DateTimeOffset.UtcNow);
        Assert.Null(first.InstalledAt);
        Assert.Null(RegistryRequest.From(first).InstalledAt);
        InstallationRecord filled = new InstallationStore(folder).LoadOrCreate(null, Windows, "Sharan", DateTimeOffset.UtcNow, installed);
        Assert.Equal(installed, filled.InstalledAt);
        InstallationRecord kept = new InstallationStore(folder).LoadOrCreate(null, Windows, "Sharan", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Assert.Equal(installed, kept.InstalledAt);
        Assert.Equal("2026-09-21T08:00:00Z", RegistryRequest.From(kept).InstalledAt);
    }

    [Fact]
    public async Task AnInstallationThatSwitchedAnalyticsOffRegistersTooWithItsInstallDate()
    {
        // What switching analytics off left behind in 0.9.x: the switch off and no identifier.
        var client = new FakeRegistryClient();
        var installed = new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);
        (RegistrySync sync, _) = MakeSync(
            client, installedAt: installed, configure: s => s with { Privacy = s.Privacy.Forgotten() });
        await sync.Start();
        RegistryRequest sent = Assert.Single(client.Requests);
        Assert.Equal("2026-09-21T08:00:00Z", sent.InstalledAt);
    }

    [Fact]
    public async Task CrashReportsWaitForRegistrationThenGoAndAreRemoved()
    {
        var crashes = new Hangly.Core.Crashes.CrashStore(Path.Combine(folder, "crashes"));
        crashes.Save(new Hangly.Core.Crashes.CrashReport(DateTimeOffset.UtcNow, true, "System.Exception", "boom", "   at X()", "AppDomain", "2.1.0", "10.0.26100.0", "x64"));
        var client = new FakeRegistryClient();
        (RegistrySync sync, _) = MakeSync(client, crashes: crashes);
        await sync.Start();
        await sync.Sync();
        Assert.Single(client.Crashes);
        Assert.Empty(crashes.Pending());
    }

    [Fact]
    public async Task AnUninstallIsSentOnlyForARegisteredInstallation()
    {
        var client = new FakeRegistryClient();
        Assert.False(await RegistrySync.SendUninstallAsync(new InstallationStore(folder), client, CancellationToken.None));
        (RegistrySync sync, _) = MakeSync(client);
        await sync.Start();
        Assert.True(await RegistrySync.SendUninstallAsync(new InstallationStore(folder), client, CancellationToken.None));
        Assert.Equal("uninstall", client.Requests[^1].Event);
    }

    [Fact]
    public async Task TheFirstLaunchWithANameRegistersOnceAndRecordsWhatWasAccepted()
    {
        var client = new FakeRegistryClient();
        (RegistrySync sync, _) = MakeSync(client);
        await sync.Start();
        RegistryRequest sent = Assert.Single(client.Requests);
        Assert.Equal("Sharan", sent.Nickname);
        Assert.Equal("Windows", sent.OsName);
        Assert.Equal("sync", sent.Event);
        Assert.Equal("windows", sent.Platform);
        Assert.Equal(sync.Store.Record!.InstallationId.ToString("D"), sent.InstallationId);
        Assert.Equal(sync.Store.Record.Current, sync.Store.Record.Uploaded);
        Assert.Equal("Bengaluru", sync.Store.Record.City);
        Assert.Equal("Karnataka", sync.Store.Record.Region);
        await sync.Sync();
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task NoNameNoRequestOnboardingComesFirst()
    {
        var client = new FakeRegistryClient();
        (RegistrySync sync, _) = MakeSync(client, name: string.Empty);
        await sync.Start();
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task ARenameIsSentWithoutAnythingElseAskingForIt()
    {
        var client = new FakeRegistryClient();
        (RegistrySync sync, SettingsStore settings) = MakeSync(client);
        await sync.Start();
        settings.Update(s => s with { DisplayName = "Sharan K" });
        await sync.Sync();
        Assert.Equal(["Sharan", "Sharan K"], client.Requests.Select(r => r.Nickname));
    }

    [Fact]
    public async Task OfflineTheRecordStaysPendingOnDiskAndGoesWhenItCan()
    {
        var client = new FakeRegistryClient { Failure = RegistryFailure.Unreachable };
        (RegistrySync sync, _) = MakeSync(client);
        await sync.Start();
        Assert.Null(sync.Store.Record!.Uploaded);
        Assert.Equal(RegistryFailure.Unreachable, sync.LastFailure);

        client.Failure = null;
        await sync.Sync();
        Assert.NotNull(sync.Store.Record!.Uploaded);
        Assert.Single(client.Requests.Select(r => r.InstallationId).Distinct());
    }

    [Fact]
    public async Task ARefusedRequestIsNotRetriedInALoop()
    {
        var client = new FakeRegistryClient { Failure = RegistryFailure.Refused };
        (RegistrySync sync, _) = MakeSync(client);
        await sync.Start();
        Assert.Equal(RegistryFailure.Refused, sync.LastFailure);
        Assert.Null(sync.Store.Record!.Uploaded);
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task ADayLaterTheHeartbeatIsSentWithinTheDayItIsNot()
    {
        var clock = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        var client = new FakeRegistryClient { LastSeen = () => clock };
        (RegistrySync sync, _) = MakeSync(client, now: () => clock);
        await sync.Start();
        clock = clock.AddHours(12);
        await sync.Sync();
        Assert.Single(client.Requests);
        clock = clock.AddHours(12);
        await sync.Sync();
        Assert.Equal(2, client.Requests.Count);
    }
}

/// <summary>Stands in for the registry function.</summary>
internal sealed class FakeRegistryClient : IRegistryClient
{
    public List<RegistryRequest> Requests { get; } = [];

    public RegistryFailure? Failure { get; set; }

    public Func<DateTimeOffset> LastSeen { get; set; } = () => DateTimeOffset.UtcNow;

    public List<Hangly.Core.Crashes.CrashReport> Crashes { get; } = [];

    public Task SubmitCrashAsync(InstallationRecord record, Hangly.Core.Crashes.CrashReport report, CancellationToken cancellation = default)
    {
        Crashes.Add(report);
        return Task.CompletedTask;
    }

    public Task<RegistryResponse> SubmitAsync(RegistryRequest request, CancellationToken cancellation = default)
    {
        Requests.Add(request);
        return Failure is RegistryFailure failure
            ? Task.FromException<RegistryResponse>(new RegistryException(failure))
            : Task.FromResult(new RegistryResponse("Bengaluru", "Karnataka", "IN", LastSeen()));
    }
}
