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

    private AnalyticsManager NewManager(
        SettingsStore store,
        string version = "1.0.0",
        bool hasDestination = true) =>
        new(store, provider, "us.i.posthog.com", hasDestination, version, "7", "10.0.26200");

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

    [Fact(DisplayName = "The provider can send an identify and nothing else")]
    public void ProviderHasOneMethod()
    {
        System.Reflection.MethodInfo method = Assert.Single(typeof(IAnalyticsProvider).GetMethods());
        Assert.Equal(nameof(IAnalyticsProvider.IdentifyAsync), method.Name);
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
