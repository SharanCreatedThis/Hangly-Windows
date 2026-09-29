//
//  RegistrySync.cs
//  Hangly
//
//  Keeps the installation's registry document in step with the record on disk.
//

using Hangly.Core.Settings;

namespace Hangly.Core.Registry;

/// <summary>Uploads the installation record whenever something is due, and keeps trying until the registry has it.</summary>
/// <remarks>
/// <b>Local first.</b> Every change is written to <c>installation.json</c> before anything is sent, and nothing is
/// marked as uploaded until the registry accepts it, so the record survives restarts, updates and lost networks,
/// and arrives when the network does. macOS's <c>RegistrySync</c>, rule for rule.
///
/// <para><b>One request covers everything due</b> — the first registration, a new name, a new version and the
/// daily heartbeat — and there is no request at all when nothing is. The registry refreshes the city and region on
/// each accepted request and returns them.</para>
/// </remarks>
public sealed class RegistrySync
{
    public static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaximumBackoff = TimeSpan.FromHours(6);

    private readonly SynchronizationContext? owner = SynchronizationContext.Current;
    private readonly SettingsStore settings;
    private readonly IRegistryClient? client;
    private readonly Analytics.IUserAnalyticsService analytics;
    private readonly Crashes.CrashStore? crashes;
    private readonly PlatformFacts facts;
    private readonly Func<DateTimeOffset?>? installEvidence;
    private readonly Func<DateTimeOffset> now;
    private readonly TimeSpan retryInterval;
    private TimeSpan backoff;
    private Task? inFlight;
    private CancellationTokenSource? retrying;
    private CancellationTokenSource? dayWatch;

    public RegistrySync(
        InstallationStore store,
        IRegistryClient? client,
        SettingsStore settings,
        PlatformFacts facts,
        TimeSpan? retryInterval = null,
        Func<DateTimeOffset>? now = null,
        Analytics.IUserAnalyticsService? analytics = null,
        Crashes.CrashStore? crashes = null,
        Func<DateTimeOffset?>? installEvidence = null)
    {
        this.installEvidence = installEvidence;
        this.analytics = analytics ?? new Analytics.NoOpUserAnalytics();
        this.crashes = crashes;
        Store = store;
        this.client = client;
        this.settings = settings;
        this.facts = facts;
        this.retryInterval = retryInterval ?? DefaultRetryInterval;
        backoff = this.retryInterval;
        this.now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public InstallationStore Store { get; }

    public bool IsConfigured => client is not null;

    public RegistryFailure? LastFailure { get; private set; }

    public DateTimeOffset? LastAcceptedAt { get; private set; }

    /// <summary>Loads or creates the record, brings it up to date with this launch, and sends whatever is due.</summary>
    public Task Start()
    {
        InstallationRecord record = Store.LoadOrCreate(
            settings.Settings.Privacy.AnonymousId, facts, CurrentNickname, now(), installEvidence?.Invoke());
        analytics.SetInstallationId(record.InstallationId);
        NoteActiveDay();
        RefreshFacts();
        settings.Changed += OnSettingsChanged;
        WatchTheDay();
        return Sync();
    }

    /// <summary>The network has come back. Sends whatever is waiting, if anything is.</summary>
    public void NetworkBecameAvailable() => OnOwner(() => _ = Sync());

    /// <summary>Sends the record if anything is due, unless a send is already on its way.</summary>
    public Task Sync()
    {
        if (inFlight is { IsCompleted: false })
        {
            return inFlight;
        }

        if (client is null || Store.Record is not InstallationRecord record)
        {
            return Task.CompletedTask;
        }

        if (!record.IsDue(now()))
        {
            // Registered and up to date: the moment to send any crash reports still waiting.
            if (record.Uploaded is null || crashes is null)
            {
                return Task.CompletedTask;
            }

            inFlight = UploadCrashes(client, record);
            return inFlight;
        }

        inFlight = Send(client, record);
        return inFlight;
    }

    private async Task Send(IRegistryClient registry, InstallationRecord record)
    {
        UploadedFacts sent = record.Current;
        try
        {
            RegistryResponse response = await registry.SubmitAsync(RegistryRequest.From(record)).ConfigureAwait(false);
            OnOwner(() => Accepted(response, sent));
        }
        catch (RegistryException exception)
        {
            OnOwner(() => Failed(exception.Failure));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            OnOwner(() => Failed(RegistryFailure.Unreachable));
        }
    }

    private void Accepted(RegistryResponse response, UploadedFacts sent)
    {
        inFlight = null;
        Store.Update(record => record with
        {
            Uploaded = sent,
            LastSeen = response.LastSeen,
            City = response.City,
            Region = response.Region,
            Country = response.Country,
        });
        LastAcceptedAt = now();
        LastFailure = null;
        backoff = retryInterval;
        StopRetrying();

        // A rename typed while the request was out is due now.
        _ = Sync();
    }

    private void Failed(RegistryFailure failure)
    {
        inFlight = null;
        LastFailure = failure;
        switch (failure)
        {
            case RegistryFailure.Unreachable:
                RetryAfter(retryInterval);
                break;
            case RegistryFailure.Busy:
                RetryAfter(backoff);
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaximumBackoff.Ticks));
                break;
            default:
                // Nothing will change by asking again; the next change or launch tries again.
                break;
        }
    }

    // Local record

    private string CurrentNickname => settings.Settings.DisplayName.Trim();

    private void OnSettingsChanged(AppSettings changed) => OnOwner(() =>
    {
        if (Store.Record is InstallationRecord record && record.Nickname != CurrentNickname)
        {
            RefreshFacts();
            _ = Sync();
        }
    });

    private void RefreshFacts()
    {
        string nickname = CurrentNickname;
        Store.Update(record => record with
        {
            Nickname = nickname,
            OsVersion = facts.OsVersion,
            AppVersion = facts.AppVersion,
            Architecture = facts.Architecture,
        });
    }

    /// <summary>
    /// Sends crash reports waiting on disk, oldest first, deleting each once the registry has it. Only for a registered
    /// installation: the registry checks the report against the installation's key.
    /// </summary>
    private async Task UploadCrashes(IRegistryClient registry, InstallationRecord record)
    {
        if (crashes is null)
        {
            return;
        }

        foreach ((string path, Crashes.CrashReport report) in crashes.Pending())
        {
            try
            {
                await registry.SubmitCrashAsync(record, report).ConfigureAwait(false);
                crashes.Remove(path);
            }
            catch (RegistryException exception) when (exception.Failure == RegistryFailure.Refused)
            {
                // Malformed or over the daily limit: it will not get better by asking again.
                crashes.Remove(path);
            }
            catch (Exception exception) when (exception is RegistryException or HttpRequestException or TaskCanceledException or IOException)
            {
                return;
            }
        }
    }

    /// <summary>Tells the registry this installation is being uninstalled. For Velopack's uninstall hook.</summary>
    /// <returns>Whether the registry accepted it.</returns>
    public static async Task<bool> SendUninstallAsync(InstallationStore store, IRegistryClient registry, CancellationToken cancellation)
    {
        if (store.LoadExisting() is not InstallationRecord record || record.Uploaded is null)
        {
            // Never registered: there is nothing to mark.
            return false;
        }

        try
        {
            await registry.SubmitAsync(RegistryRequest.From(record, "uninstall"), cancellation).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is RegistryException or HttpRequestException or TaskCanceledException or IOException)
        {
            return false;
        }
    }

    // Retrying

    private void RetryAfter(TimeSpan interval)
    {
        retrying?.Cancel();
        var cancellation = new CancellationTokenSource();
        retrying = cancellation;
        _ = Later(interval, cancellation.Token);
    }

    private async Task Later(TimeSpan interval, CancellationToken cancellation)
    {
        try
        {
            await Task.Delay(interval, cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        OnOwner(() => _ = Sync());
    }

    private void StopRetrying()
    {
        retrying?.Cancel();
        retrying = null;
    }

    /// <summary>Checks once an hour whether the daily heartbeat has come due.</summary>
    private void WatchTheDay()
    {
        if (dayWatch is not null)
        {
            return;
        }

        dayWatch = new CancellationTokenSource();
        _ = DayLoop(dayWatch.Token);
    }

    private async Task DayLoop(CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            OnOwner(() =>
            {
                NoteActiveDay();
                _ = Sync();
            });
        }
    }

    /// <summary>
    /// Sends <c>daily_active</c> once a local day, from the launch and from the hourly check, so an installation that
    /// runs for days without being reopened still counts as active each of them.
    /// </summary>
    private void NoteActiveDay()
    {
        if (Analytics.DailyActive.Due(settings.Settings.Privacy.LastActiveDay, now().ToLocalTime()) is not string today)
        {
            return;
        }

        analytics.Log(Analytics.AnalyticsEvent.DailyActiveDay());
        settings.Update(current => current with { Privacy = current.Privacy with { LastActiveDay = today } });
    }

    private void OnOwner(Action action)
    {
        if (owner is null || SynchronizationContext.Current == owner)
        {
            action();
        }
        else
        {
            owner.Post(_ => action(), null);
        }
    }
}
