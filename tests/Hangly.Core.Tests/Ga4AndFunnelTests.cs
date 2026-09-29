using System.Text.Json;
using Hangly.Core.Analytics;
using Hangly.Core.Models;
using Hangly.Core.Registry;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>GA4 sessions and engagement, <c>daily_active</c>, and the update funnel (2.1.1).</summary>
public sealed class Ga4AndFunnelTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"hangly-ga4-{Guid.NewGuid():N}");

    public Ga4AndFunnelTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    }

    // Sessions

    [Fact]
    public void TheFirstEventBeginsASessionIdentifiedByItsStartAndEngagesOneMillisecond()
    {
        var clock = new Clock();
        var session = new Ga4Session(() => clock.Now);
        (long id, long engaged) = session.Stamp();
        Assert.Equal(clock.Now.ToUnixTimeSeconds(), id);
        Assert.Equal(1, engaged);
    }

    [Fact]
    public void LaterEventsInTheSessionReportTheTimeSinceThePreviousOne()
    {
        var clock = new Clock();
        var session = new Ga4Session(() => clock.Now);
        (long id, _) = session.Stamp();
        clock.Now = clock.Now.AddMinutes(12);
        (long sameId, long engaged) = session.Stamp();
        Assert.Equal(id, sameId);
        Assert.Equal(12 * 60 * 1000, engaged);
    }

    [Fact]
    public void ThirtyMinutesWithoutAnEventEndsTheSession()
    {
        var clock = new Clock();
        var session = new Ga4Session(() => clock.Now);
        (long first, _) = session.Stamp();
        clock.Now = clock.Now.AddMinutes(30);
        Assert.Equal(first, session.Stamp().SessionId);
        clock.Now = clock.Now.AddMinutes(31);
        (long next, long engaged) = session.Stamp();
        Assert.NotEqual(first, next);
        Assert.Equal(clock.Now.ToUnixTimeSeconds(), next);
        Assert.Equal(1, engaged);
    }

    [Fact]
    public void AClockThatGoesBackBeginsANewSessionRatherThanReportingNegativeEngagement()
    {
        var clock = new Clock();
        var session = new Ga4Session(() => clock.Now);
        session.Stamp();
        clock.Now = clock.Now.AddMinutes(-5);
        Assert.Equal(1, session.Stamp().EngagementMilliseconds);
    }

    // The request body

    [Fact]
    public void EveryEventCarriesSessionIdAndEngagementTimeBesideItsOwnParameters()
    {
        Dictionary<string, object> body = Ga4Payload.Build(
            "ce319cc7-eae6-4a95-ae97-f38fcab87ad6", AnalyticsEvent.AppLaunch(atLogin: true), 1790668087, 1);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(body));
        JsonElement root = json.RootElement;
        Assert.Equal("ce319cc7-eae6-4a95-ae97-f38fcab87ad6", root.GetProperty("client_id").GetString());
        Assert.Equal("ce319cc7-eae6-4a95-ae97-f38fcab87ad6", root.GetProperty("user_id").GetString());
        JsonElement analyticsEvent = root.GetProperty("events")[0];
        Assert.Equal("app_launch", analyticsEvent.GetProperty("name").GetString());
        JsonElement parameters = analyticsEvent.GetProperty("params");
        Assert.Equal("login", parameters.GetProperty("launch_type").GetString());
        Assert.Equal("1790668087", parameters.GetProperty("session_id").GetString());
        Assert.Equal(1, parameters.GetProperty("engagement_time_msec").GetInt64());
    }

    [Fact]
    public void AParameterValueIsCutToWhatGa4Keeps()
    {
        AnalyticsEvent failed = AnalyticsEvent.UpdateFailed(UpdateStage.Download, new string('x', 150), UpdateTrigger.Quiet);
        var parameters = (Dictionary<string, object>)((Dictionary<string, object>)((object[])Ga4Payload.Build("id", failed, 1, 1)["events"])[0])["params"];
        Assert.Equal(Ga4Payload.ValueLimit, ((string)parameters["error"]).Length);
    }

    // Once a day

    [Fact]
    public void DailyActiveIsDueOnceEachLocalDay()
    {
        var morning = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(5.5));
        Assert.Equal("2026-10-01", DailyActive.Due(null, morning));
        Assert.Null(DailyActive.Due("2026-10-01", morning.AddHours(15)));
        Assert.Equal("2026-10-02", DailyActive.Due("2026-10-01", morning.AddHours(16)));
    }

    private sealed class Recorder : IUserAnalyticsService
    {
        public List<AnalyticsEvent> Events { get; } = [];

        public void SetInstallationId(Guid installationId)
        {
        }

        public void Log(AnalyticsEvent analyticsEvent) => Events.Add(analyticsEvent);
    }

    [Fact]
    public void TheRegistrySendsDailyActiveOnStartAndNotAgainTheSameDay()
    {
        var clock = new Clock();
        var recorder = new Recorder();
        var settings = new SettingsStore(Path.Combine(folder, "settings.json"));
        settings.Update(s => s with { DisplayName = "Sharan" });
        var facts = new PlatformFacts("10.0.26200.0", "2.1.1", "arm64");
        _ = new RegistrySync(new InstallationStore(folder), null, settings, facts, now: () => clock.Now, analytics: recorder).Start();
        _ = new RegistrySync(new InstallationStore(folder), null, settings, facts, now: () => clock.Now.AddHours(2), analytics: recorder).Start();
        Assert.Single(recorder.Events, e => e.Name == "daily_active");
        Assert.NotNull(settings.Settings.Privacy.LastActiveDay);
    }

    // The update funnel

    [Fact]
    public void TheFunnelEventsCarryTheirVersionsStageAndTrigger()
    {
        Assert.Equal("update_available", AnalyticsEvent.UpdateAvailable("2.1.1", UpdateTrigger.Quiet).Name);
        Assert.Equal("2.1.1", AnalyticsEvent.UpdateAvailable("2.1.1", UpdateTrigger.Quiet).Parameters["to_version"]);
        Assert.Equal("quiet", AnalyticsEvent.UpdateDownloadStarted("2.1.1", UpdateTrigger.Quiet).Parameters["trigger"]);
        Assert.Equal("manual", AnalyticsEvent.UpdateDownloadCompleted("2.1.1", UpdateTrigger.Manual).Parameters["trigger"]);
        Assert.Equal("update_download_started", AnalyticsEvent.UpdateDownloadStarted("2.1.1", UpdateTrigger.Quiet).Name);
        Assert.Equal("update_download_completed", AnalyticsEvent.UpdateDownloadCompleted("2.1.1", UpdateTrigger.Quiet).Name);
        AnalyticsEvent installed = AnalyticsEvent.UpdateInstalled("0.9.x", "2.1.1");
        Assert.Equal("update_installed", installed.Name);
        Assert.Equal("0.9.x", installed.Parameters["from_version"]);
        Assert.Equal("2.1.1", installed.Parameters["to_version"]);
        AnalyticsEvent failed = AnalyticsEvent.UpdateFailed(UpdateStage.Install, "IOException", UpdateTrigger.Manual);
        Assert.Equal(("update_failed", "install", "IOException", "manual"), (failed.Name, failed.Parameters["stage"], failed.Parameters["error"], failed.Parameters["trigger"]));
        Assert.Equal("daily_active", AnalyticsEvent.DailyActiveDay().Name);
    }

    [Fact]
    public void EveryEventNameAndParameterIsWithinGa4Limits()
    {
        AnalyticsEvent[] all =
        [
            AnalyticsEvent.AppLaunch(atLogin: false, afterUpdate: true), AnalyticsEvent.AppUpdate("0.9.x"),
            AnalyticsEvent.UpdateAvailable("2.1.1", UpdateTrigger.Quiet), AnalyticsEvent.UpdateDownloadStarted("2.1.1", UpdateTrigger.Quiet),
            AnalyticsEvent.UpdateDownloadCompleted("2.1.1", UpdateTrigger.Quiet), AnalyticsEvent.UpdateInstalled("2.1.0", "2.1.1"),
            AnalyticsEvent.UpdateFailed(UpdateStage.Check, "HttpRequestException", UpdateTrigger.Quiet), AnalyticsEvent.DailyActiveDay(),
        ];
        string[] reserved = ["session_start", "user_engagement", "first_visit", "first_open", "app_update", "app_remove", "screen_view"];
        foreach (AnalyticsEvent analyticsEvent in all)
        {
            Assert.InRange(analyticsEvent.Name.Length, 1, 40);
            Assert.DoesNotContain(analyticsEvent.Name, reserved);
            Assert.All(analyticsEvent.Parameters.Keys, key => Assert.InRange(key.Length, 1, 40));
        }
    }
}
