//
//  AnalyticsTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Analytics;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// PRIVACY.md is a published promise, so these are not tests of an implementation
/// detail — each one is a sentence from that document, asserted against what a recording
/// provider actually received.
/// </summary>
/// <remarks>
/// The promise is now small enough to state in one line: one <c>$identify</c>, on a first
/// launch, a rename or a new major version, and nothing else, ever.
/// </remarks>
public class AnalyticsTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "hangly-analytics-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingAnalyticsProvider provider = new();

    /// <summary>A store for somebody who has been through onboarding.</summary>
    private SettingsStore NewStore(string name = "Test Person")
    {
        SettingsStore store = NewNamelessStore();
        store.Update(settings => settings with { DisplayName = name });
        return store;
    }

    /// <summary>A store for somebody who has not answered yet.</summary>
    private SettingsStore NewNamelessStore()
    {
        Directory.CreateDirectory(directory);
        return new SettingsStore(Path.Combine(directory, "settings.json"));
    }

    /// <summary>The same document read again, which is what a relaunch is.</summary>
    private SettingsStore Relaunched() => new(Path.Combine(directory, "settings.json"));

    /// <summary>The time the managers in a test believe it is. Tests move it to make a day pass.</summary>
    private DateTimeOffset now = new(2026, 9, 26, 10, 0, 0, TimeSpan.FromHours(5.5));

    private AnalyticsManager NewManager(
        SettingsStore store,
        string version = "1.0.0",
        bool hasDestination = true,
        TimeSpan? retryInterval = null,
        TimeSpan? dayWatchInterval = null) =>
        new(
            store, provider, "us.i.posthog.com", hasDestination, version, "7", "10.0.26200",
            retryInterval, dayWatchInterval, () => now);

    /// <summary>Waits, up to a limit, for something asynchronous to become true.</summary>
    private static async Task<bool> Eventually(Func<bool> condition, int milliseconds = 3000)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static string Reason(IdentifyCall call) =>
        ((AnalyticsValue.Text)call.EventProperties["identify_reason"]).Value;

    private static string Text(IReadOnlyDictionary<string, AnalyticsValue> properties, string key) =>
        ((AnalyticsValue.Text)properties[key]).Value;

    // MARK: - The three triggers

    [Fact(DisplayName = "A first launch identifies the person once, as a first launch")]
    public async Task FirstLaunchIdentifiesOnce()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);

        await manager.Start();
        await manager.Start();
        await manager.Sync();

        IdentifyCall call = Assert.Single(provider.Calls);
        Assert.Equal("first_launch", Reason(call));
        Assert.Equal(store.Settings.Privacy.AnonymousId?.ToString("D"), call.DistinctId);
    }

    [Fact(DisplayName = "An ordinary relaunch sends nothing")]
    public async Task RelaunchSendsNothing()
    {
        await NewManager(NewStore()).Start();
        await NewManager(Relaunched()).Start();
        await NewManager(Relaunched()).Start();

        Assert.Single(provider.Calls);
    }

    [Fact(DisplayName = "A rename identifies the person again, under the new name, once")]
    public async Task RenameIdentifiesOnce()
    {
        SettingsStore store = NewStore("Before");
        AnalyticsManager manager = NewManager(store);
        await manager.Start();

        store.Update(settings => settings with { DisplayName = "After" });
        await manager.Sync();
        await manager.Sync();

        Assert.Equal(2, provider.Calls.Count);
        IdentifyCall rename = provider.Calls[1];
        Assert.Equal("name_changed", Reason(rename));
        Assert.Equal("After", Text(rename.PersonProperties, "name"));
        Assert.Equal(provider.Calls[0].DistinctId, rename.DistinctId);
    }

    [Fact(DisplayName = "A rename made on a later launch is still sent")]
    public async Task RenameBetweenLaunchesIsSent()
    {
        await NewManager(NewStore("Before")).Start();

        SettingsStore store = Relaunched();
        store.Update(settings => settings with { DisplayName = "After" });
        await NewManager(store).Start();

        Assert.Equal("name_changed", Reason(provider.Calls[^1]));
    }

    [Fact(DisplayName = "A new major version identifies the person again; a minor one does not")]
    public async Task MajorVersionsOnly()
    {
        await NewManager(NewStore(), "1.0.0").Start();
        await NewManager(Relaunched(), "1.4.2").Start();
        Assert.Single(provider.Calls);

        await NewManager(Relaunched(), "2.0.0").Start();
        Assert.Equal(2, provider.Calls.Count);
        Assert.Equal("major_version", Reason(provider.Calls[1]));
        Assert.Equal("2.0.0", Text(provider.Calls[1].PersonProperties, "app_version"));

        await NewManager(Relaunched(), "2.1.0").Start();
        Assert.Equal(2, provider.Calls.Count);
    }

    [Fact(DisplayName = "Going back to an older version sends nothing")]
    public async Task DowngradeSendsNothing()
    {
        await NewManager(NewStore(), "2.0.0").Start();
        await NewManager(Relaunched(), "1.9.0").Start();

        Assert.Single(provider.Calls);
    }

    [Fact(DisplayName = "Somebody identified by the old analytics carries over as a major version, under the same identity")]
    public async Task LegacyIdentityCarriesOver()
    {
        Guid legacy = Guid.NewGuid();
        SettingsStore store = NewStore();
        store.Update(settings => settings with { Privacy = settings.Privacy with { AnonymousId = legacy } });

        await NewManager(store).Start();

        IdentifyCall call = Assert.Single(provider.Calls);
        Assert.Equal("major_version", Reason(call));
        Assert.Equal(legacy.ToString("D"), call.DistinctId);
    }

    // MARK: - Nothing else, ever

    [Fact(DisplayName = "The provider can send an identify and the two operational events, and nothing else")]
    public void ProviderIsClosed()
    {
        Assert.Equal(
            [nameof(IAnalyticsProvider.IdentifyAsync), nameof(IAnalyticsProvider.SendAsync)],
            typeof(IAnalyticsProvider).GetMethods().Select(method => method.Name).Order());
        Assert.Equal(
            ["app_uninstalled", "daily_active"],
            Enum.GetValues<OperationalEvent>().Select(OperationalEvents.NameOf).Order());
    }

    [Fact(DisplayName = "Nothing at all is sent until the person has a name")]
    public async Task NothingIsSentWhileNameless()
    {
        SettingsStore store = NewNamelessStore();
        AnalyticsManager manager = NewManager(store);

        await manager.Start();
        Assert.Empty(provider.Calls);
        Assert.Null(store.Settings.Privacy.AnonymousId);

        store.Update(settings => settings with { DisplayName = "Named" });
        await manager.Sync();

        Assert.Equal("first_launch", Reason(Assert.Single(provider.Calls)));
    }

    [Fact(DisplayName = "Whitespace is not a name")]
    public async Task WhitespaceIsNotAName()
    {
        await NewManager(NewStore("   ")).Start();

        Assert.Empty(provider.Calls);
    }

    [Fact(DisplayName = "A build with no destination sends nothing, and says so")]
    public async Task NoDestinationSendsNothing()
    {
        AnalyticsManager manager = NewManager(NewStore(), hasDestination: false);

        await manager.Start();

        Assert.Empty(provider.Calls);
        Assert.Equal(AnalyticsConnection.NoDestination, manager.Connection);
    }

    // MARK: - Failure is retried, not lost

    [Fact(DisplayName = "A first launch with no network is sent on the next launch, still as a first launch")]
    public async Task FailedFirstLaunchIsRetried()
    {
        provider.Accepts = false;
        await NewManager(NewStore()).Start();
        Assert.Null(Relaunched().Settings.Privacy.IdentifiedMajorVersion);

        provider.Accepts = true;
        await NewManager(Relaunched()).Start();

        Assert.Equal(2, provider.Calls.Count);
        Assert.Equal("first_launch", Reason(provider.Calls[1]));
        Assert.Equal(provider.Calls[0].DistinctId, provider.Calls[1].DistinctId);
    }

    [Fact(DisplayName = "An accepted identify is remembered, and only an accepted one")]
    public async Task OnlyAcceptedIsRecorded()
    {
        SettingsStore store = NewStore("Recorded");
        await NewManager(store, "3.2.1").Start();

        PrivacySettings privacy = store.Settings.Privacy;
        Assert.Equal("Recorded", privacy.IdentifiedName);
        Assert.Equal(3, privacy.IdentifiedMajorVersion);
        Assert.False(privacy.FirstIdentifyPending);
    }

    // MARK: - One person, however often they change

    [Fact(DisplayName = "Five renames are five updates to one person, never a second one")]
    public async Task FiveRenamesStayOnePerson()
    {
        SettingsStore store = NewStore("Name 0");
        AnalyticsManager manager = NewManager(store);
        await manager.Start();

        for (int rename = 1; rename <= 6; rename++)
        {
            store.Update(settings => settings with { DisplayName = $"Name {rename}" });
            await manager.Sync();
        }

        Assert.Equal(7, provider.Calls.Count);
        Assert.Single(provider.Calls.Select(call => call.DistinctId).Distinct());
        Assert.All(provider.Calls.Skip(1), call => Assert.Equal("name_changed", Reason(call)));
        Assert.Equal("Name 6", Text(provider.Calls[^1].PersonProperties, "name"));
        Assert.Equal("Name 6", store.Settings.Privacy.IdentifiedName);
    }

    [Fact(DisplayName = "Updates and reboots keep the same person")]
    public async Task UpdatesAndRebootsKeepThePerson()
    {
        await NewManager(NewStore(), "1.0.0").Start();
        foreach (string version in new[] { "1.0.1", "1.1.0", "1.2.0", "2.0.0", "2.0.1" })
        {
            // Each is a relaunch against the same document: an update, or a reboot.
            await NewManager(Relaunched(), version).Start();
        }

        Assert.Equal(2, provider.Calls.Count);
        Assert.Single(provider.Calls.Select(call => call.DistinctId).Distinct());
        Assert.Equal("major_version", Reason(provider.Calls[1]));
    }

    // MARK: - Offline onboarding

    [Fact(DisplayName = "Offline, then the network returns: sent at once, without a relaunch, exactly once")]
    public async Task NetworkReturningSendsThePendingIdentify()
    {
        provider.Accepts = false;
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store, retryInterval: TimeSpan.FromHours(1));
        await manager.Start();
        Assert.True(manager.IsRetrying);

        provider.Accepts = true;
        manager.NetworkBecameAvailable();
        Assert.True(await Eventually(() => store.Settings.Privacy.IdentifiedMajorVersion is not null));

        manager.NetworkBecameAvailable();
        await manager.Sync();

        Assert.Equal(2, provider.Calls.Count);
        Assert.Equal("first_launch", Reason(provider.Calls[1]));
        Assert.Single(provider.Calls.Select(call => call.DistinctId).Distinct());
        Assert.False(manager.IsRetrying);
    }

    [Fact(DisplayName = "A network that returns silently is caught by the retry, which then stops")]
    public async Task RetryCatchesASilentReturn()
    {
        provider.Accepts = false;
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store, retryInterval: TimeSpan.FromMilliseconds(30));
        await manager.Start();

        provider.Accepts = true;
        Assert.True(await Eventually(() => store.Settings.Privacy.IdentifiedMajorVersion is not null));
        Assert.True(await Eventually(() => !manager.IsRetrying));

        int sent = provider.Calls.Count;
        await Task.Delay(150);
        Assert.Equal(sent, provider.Calls.Count);
    }

    [Fact(DisplayName = "Offline install, reboot while still offline, then online: one person")]
    public async Task OfflineAcrossARebootIsStillOnePerson()
    {
        provider.Accepts = false;
        await NewManager(NewStore(), retryInterval: TimeSpan.FromHours(1)).Start();
        await NewManager(Relaunched(), retryInterval: TimeSpan.FromHours(1)).Start();

        provider.Accepts = true;
        AnalyticsManager online = NewManager(Relaunched(), retryInterval: TimeSpan.FromHours(1));
        await online.Start();
        online.NetworkBecameAvailable();
        await online.Sync();

        // Two attempts that never arrived, one that did, and nothing after it: every
        // attempt carried the same identifier, so PostHog holds one person.
        Assert.Single(provider.Calls.Select(call => call.DistinctId).Distinct());
        Assert.Equal(["first_launch", "first_launch", "first_launch"], provider.Calls.Select(Reason));
        Assert.NotNull(Relaunched().Settings.Privacy.IdentifiedMajorVersion);
    }

    [Fact(DisplayName = "Nothing retries when nothing is pending")]
    public async Task NoRetryWhenSent()
    {
        AnalyticsManager manager = NewManager(NewStore(), retryInterval: TimeSpan.FromMilliseconds(20));
        await manager.Start();

        Assert.False(manager.IsRetrying);
        await Task.Delay(100);
        Assert.Single(provider.Calls);
    }

    [Fact(DisplayName = "Switching off while retrying stops the retry")]
    public async Task TurningOffStopsRetrying()
    {
        provider.Accepts = false;
        AnalyticsManager manager = NewManager(NewStore(), retryInterval: TimeSpan.FromMilliseconds(20));
        await manager.Start();
        Assert.True(manager.IsRetrying);

        await manager.SetEnabled(false);

        Assert.False(manager.IsRetrying);
        int sent = provider.Calls.Count;
        await Task.Delay(100);
        Assert.Equal(sent, provider.Calls.Count);
    }

    // MARK: - Threads

    [Fact(DisplayName = "A network notification on a pool thread writes the settings on the owner's thread")]
    public async Task WorkHappensOnTheOwnersThread()
    {
        using var ui = new SingleThreadContext();
        SettingsStore store = NewStore();
        provider.Accepts = false;

        AnalyticsManager manager = await ui.Run(() => NewManager(store, retryInterval: TimeSpan.FromHours(1)));
        await ui.Run(() => manager.Start());

        // Every write, recorded as it is announced. Waiting on the settings value itself
        // raced: the store updates its value before raising Changed, so a fast machine
        // could read the value and check the thread before the handler had run.
        var writtenOn = new System.Collections.Concurrent.ConcurrentQueue<int>();
        store.Changed += settings =>
        {
            if (settings.Privacy.IdentifiedMajorVersion is not null)
            {
                writtenOn.Enqueue(Environment.CurrentManagedThreadId);
            }
        };
        provider.Accepts = true;

        await Task.Run(manager.NetworkBecameAvailable);
        Assert.True(await Eventually(() => !writtenOn.IsEmpty));

        Assert.All(writtenOn, thread => Assert.Equal(ui.ThreadId, thread));
    }

    /// <summary>A stand-in for the UI thread: one thread, running whatever is posted to it.</summary>
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback, object?)> queue = [];
        private readonly Thread thread;

        public SingleThreadContext()
        {
            thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach ((SendOrPostCallback callback, object? state) in queue.GetConsumingEnumerable())
                {
                    callback(state);
                }
            })
            { IsBackground = true };
            thread.Start();
        }

        public int ThreadId => thread.ManagedThreadId;

        public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));

        public Task<T> Run<T>(Func<T> work)
        {
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(_ => done.SetResult(work()), null);
            return done.Task;
        }

        public Task Run(Func<Task> work)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(_ => work().ContinueWith(_ => done.SetResult(), TaskScheduler.Default), null);
            return done.Task;
        }

        public void Dispose() => queue.CompleteAdding();
    }

    // MARK: - The daily heartbeat

    [Fact(DisplayName = "A first launch identifies, then says the install is active today, in that order")]
    public async Task FirstLaunchIdentifiesThenHeartbeats()
    {
        SettingsStore store = NewStore();
        await NewManager(store).Start();

        Assert.Equal(["$identify", "daily_active"], provider.Order);
        Assert.Equal(provider.Calls[0].DistinctId, provider.Events[0].DistinctId);
        Assert.Equal("2026-09-26", store.Settings.Privacy.LastActiveDay);
    }

    [Fact(DisplayName = "At most one heartbeat a day, however many launches")]
    public async Task OneHeartbeatADay()
    {
        await NewManager(NewStore()).Start();
        await NewManager(Relaunched()).Start();
        now = now.AddHours(8);
        await NewManager(Relaunched()).Start();

        Assert.Single(provider.Events);
    }

    [Fact(DisplayName = "The next day's launch sends a heartbeat and no identify")]
    public async Task NextDayIsOneHeartbeat()
    {
        await NewManager(NewStore()).Start();
        now = now.AddDays(1);
        await NewManager(Relaunched()).Start();

        Assert.Equal(["$identify", "daily_active", "daily_active"], provider.Order);
    }

    [Fact(DisplayName = "An app left running past midnight counts the new day by itself")]
    public async Task RunningPastMidnightCountsTheDay()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store, dayWatchInterval: TimeSpan.FromMilliseconds(20));
        await manager.Start();

        now = now.AddDays(1);
        Assert.True(await Eventually(() => provider.Events.Count == 2));
        Assert.Equal("2026-09-27", store.Settings.Privacy.LastActiveDay);

        await Task.Delay(100);
        Assert.Equal(2, provider.Events.Count);
    }

    [Fact(DisplayName = "The heartbeat carries the platform and version, and nothing about the person or their use")]
    public async Task HeartbeatCarriesOnlyOperationalFacts()
    {
        await NewManager(NewStore("Zq Distinct Tester"), "1.4.0").Start();
        EventCall beat = Assert.Single(provider.Events);

        Assert.Equal(OperationalEvent.DailyActive, beat.Event);
        Assert.Equal("1.4.0", Text(beat.Properties, "app_version"));
        Assert.Equal("1.4.0", Text(beat.PersonProperties!, "app_version"));
        Assert.Equal(AnalyticsValue.Of(true), beat.Properties["$geoip_disable"]);
        Assert.Equal(AnalyticsValue.Null, beat.Properties["$ip"]);

        IEnumerable<string> keys = beat.Properties.Keys.Concat(beat.PersonProperties!.Keys);
        string[] forbidden = ["name", "charm", "rope", "display", "screen", "position", "count"];
        Assert.DoesNotContain(keys, key => forbidden.Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            beat.Properties.Values.Concat(beat.PersonProperties.Values),
            value => value == AnalyticsValue.Of("Zq Distinct Tester"));
    }

    [Fact(DisplayName = "An update moves the person's version on its first day, without an identify")]
    public async Task UpdateMovesTheVersionByHeartbeat()
    {
        await NewManager(NewStore(), "1.0.0").Start();
        now = now.AddDays(1);
        await NewManager(Relaunched(), "1.1.0").Start();

        Assert.Single(provider.Calls);
        Assert.Equal("1.1.0", Text(provider.Events[^1].PersonProperties!, "app_version"));
    }

    [Fact(DisplayName = "No heartbeat while the identify is still waiting: never an anonymous person")]
    public async Task NoHeartbeatBeforeIdentify()
    {
        provider.Accepts = false;
        SettingsStore store = NewStore();
        await NewManager(store, retryInterval: TimeSpan.FromHours(1)).Start();

        Assert.Empty(provider.Events);
        Assert.Null(store.Settings.Privacy.LastActiveDay);
    }

    [Fact(DisplayName = "A heartbeat that could not be sent is retried, and the day recorded only when it arrives")]
    public async Task FailedHeartbeatIsRetried()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store, retryInterval: TimeSpan.FromMilliseconds(30));
        await manager.Start();

        now = now.AddDays(1);
        provider.Accepts = false;
        await manager.Sync();
        Assert.Equal("2026-09-26", store.Settings.Privacy.LastActiveDay);
        Assert.True(manager.IsRetrying);

        provider.Accepts = true;
        Assert.True(await Eventually(() => store.Settings.Privacy.LastActiveDay == "2026-09-27"));
        Assert.True(await Eventually(() => !manager.IsRetrying));
    }

    [Fact(DisplayName = "No heartbeat when switched off, nameless, or without a destination")]
    public async Task NoHeartbeatWithoutConsent()
    {
        SettingsStore off = NewStore();
        off.Update(settings => settings with { Privacy = settings.Privacy.Forgotten() });
        await NewManager(off).Start();
        await NewManager(Relaunched(), hasDestination: false).Start();

        Assert.Empty(provider.Events);
    }

    // MARK: - Uninstall

    [Fact(DisplayName = "Uninstalling tells the project once, under the same identity, with no name")]
    public async Task UninstallIsReported()
    {
        SettingsStore store = NewStore("Zq Distinct Tester");
        AnalyticsManager manager = NewManager(store);
        await manager.Start();

        Assert.True(await manager.UninstalledAsync());

        EventCall gone = provider.Events[^1];
        Assert.Equal(OperationalEvent.Uninstalled, gone.Event);
        Assert.Equal(provider.Calls[0].DistinctId, gone.DistinctId);
        Assert.Null(gone.PersonProperties);
        Assert.DoesNotContain(gone.Properties.Values, value => value == AnalyticsValue.Of("Zq Distinct Tester"));
    }

    [Fact(DisplayName = "Uninstalling says nothing when switched off, or for a person never identified")]
    public async Task UninstallRespectsConsent()
    {
        SettingsStore off = NewStore();
        off.Update(settings => settings with { Privacy = settings.Privacy.Forgotten() });
        Assert.False(await NewManager(off).UninstalledAsync());

        SettingsStore neverSent = NewNamelessStore();
        Assert.False(await NewManager(neverSent).UninstalledAsync());

        Assert.Empty(provider.Events);
    }

    // MARK: - Consent

    [Fact(DisplayName = "Switched off, nothing is sent, and the launch is still counted")]
    public async Task DisabledSendsNothing()
    {
        SettingsStore store = NewStore();
        store.Update(settings => settings with { Privacy = settings.Privacy.Forgotten() });

        await NewManager(store).Start();

        Assert.Empty(provider.Calls);
        Assert.Equal(1, store.Settings.Milestones.LaunchCount);
    }

    [Fact(DisplayName = "Switching off forgets the identifier and what was sent under it")]
    public async Task TurningOffForgets()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        await manager.Start();

        await manager.SetEnabled(false);

        PrivacySettings privacy = store.Settings.Privacy;
        Assert.False(privacy.AnalyticsEnabled);
        Assert.Null(privacy.AnonymousId);
        Assert.Null(privacy.IdentifiedName);
        Assert.Null(privacy.IdentifiedMajorVersion);
    }

    [Fact(DisplayName = "Switching back on is a new person with a new identifier")]
    public async Task TurningBackOnIsANewPerson()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        await manager.Start();
        await manager.SetEnabled(false);

        await manager.SetEnabled(true);

        Assert.Equal(2, provider.Calls.Count);
        Assert.Equal("first_launch", Reason(provider.Calls[1]));
        Assert.NotEqual(provider.Calls[0].DistinctId, provider.Calls[1].DistinctId);
    }

    [Fact(DisplayName = "A rename while switched off is not sent")]
    public async Task RenameWhileOffIsNotSent()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        await manager.Start();
        await manager.SetEnabled(false);

        store.Update(settings => settings with { DisplayName = "Hidden" });
        await manager.Sync();

        Assert.Single(provider.Calls);
    }

    // MARK: - What the payload carries

    [Fact(DisplayName = "The name is set under the keys PostHog displays people by")]
    public async Task NameIsSetWhereItIsRead()
    {
        await NewManager(NewStore("Shown Name")).Start();

        IReadOnlyDictionary<string, AnalyticsValue> person = provider.Calls[0].PersonProperties;
        Assert.Equal("Shown Name", Text(person, "name"));
        Assert.Equal("Shown Name", Text(person, "username"));
        Assert.Equal("Shown Name", Text(person, "user_name"));
    }

    [Fact(DisplayName = "The person carries the platform and the versions")]
    public async Task PersonCarriesPlatformFacts()
    {
        await NewManager(NewStore(), "1.2.3").Start();

        IReadOnlyDictionary<string, AnalyticsValue> person = provider.Calls[0].PersonProperties;
        Assert.Equal("windows", Text(person, "platform"));
        Assert.Equal("Windows", Text(person, "$os"));
        Assert.Equal("1.2.3", Text(person, "app_version"));
        Assert.Equal("7", Text(person, "build_number"));
        Assert.Equal("10.0.26200", Text(person, "os_version"));
        Assert.Contains(Text(person, "architecture"), new[] { "arm64", "x64", "x86" });
    }

    [Fact(DisplayName = "Geolocation is declined, not merely unmentioned")]
    public async Task GeolocationIsDeclined()
    {
        await NewManager(NewStore()).Start();

        IReadOnlyDictionary<string, AnalyticsValue> properties = provider.Calls[0].EventProperties;
        Assert.Equal(AnalyticsValue.Of(true), properties["$geoip_disable"]);
        Assert.Equal(AnalyticsValue.Null, properties["$ip"]);
    }

    [Fact(DisplayName = "Nothing on the identify is read off the machine")]
    public async Task NothingIsTakenFromTheMachine()
    {
        // A name deliberately unlike any account or machine name, so a leak cannot pass by
        // coincidence the way it once did.
        await NewManager(NewStore("Zq Distinct Tester")).Start();

        IdentifyCall call = provider.Calls[0];
        IEnumerable<string> values = call.PersonProperties.Values
            .Concat(call.EventProperties.Values)
            .OfType<AnalyticsValue.Text>()
            .Select(text => text.Value);

        Assert.DoesNotContain(Environment.UserName, values);
        Assert.DoesNotContain(Environment.MachineName, values);
    }

    [Fact(DisplayName = "Nothing describing the desktop or the rope is sent")]
    public async Task NothingAboutUseIsSent()
    {
        await NewManager(NewStore()).Start();

        IdentifyCall call = provider.Calls[0];
        IEnumerable<string> keys = call.PersonProperties.Keys.Concat(call.EventProperties.Keys);
        string[] forbidden = ["charm", "charm_count", "active_charm_ids", "rope_style", "display", "screen", "position"];

        Assert.DoesNotContain(keys, key => forbidden.Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    [Theory(DisplayName = "Major versions are read the same way on both platforms")]
    [InlineData("0.9.4", 0)]
    [InlineData("1.0.0", 1)]
    [InlineData("2.1.0+416b85d", 2)]
    [InlineData("10.0.0-beta", 10)]
    [InlineData("garbage", 0)]
    public void MajorVersionParsing(string version, int expected) =>
        Assert.Equal(expected, AnalyticsManager.MajorOf(version));

    [Fact(DisplayName = "The identifier is masked, and is not the identifier")]
    public async Task IdentifierIsMasked()
    {
        SettingsStore store = NewStore();
        AnalyticsManager manager = NewManager(store);
        await manager.Start();

        string full = store.Settings.Privacy.AnonymousId!.Value.ToString("D");
        Assert.NotNull(manager.MaskedIdentifier);
        Assert.NotEqual(full, manager.MaskedIdentifier);
        Assert.StartsWith(full[..8], manager.MaskedIdentifier, StringComparison.Ordinal);
    }
}
