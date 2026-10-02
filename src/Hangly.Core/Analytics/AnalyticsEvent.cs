//
//  AnalyticsEvent.cs
//  Hangly
//
//  The few events Hangly reports, behind one abstraction.
//

namespace Hangly.Core.Analytics;

/// <summary>The events Hangly reports — six in all, with macOS's names and parameters.</summary>
/// <remarks>
/// <c>app_update</c> is sent under the name <c>hangly_app_update</c>: Google Analytics reserves <c>app_update</c> and
/// drops it when it arrives through the Measurement Protocol. On macOS the Firebase SDK records the reserved one itself.
///
/// <para>No nickname, place or free text is ever an event parameter. Custom charms are reported as <c>custom</c>,
/// never by their name.</para>
/// </remarks>
public sealed record AnalyticsEvent(string Name, IReadOnlyDictionary<string, string> Parameters)
{
    /// <summary><c>launch_type</c> is <c>login</c>, <c>manual</c>, or <c>update</c> for the quiet restart after an update.</summary>
    public static AnalyticsEvent AppLaunch(bool atLogin, bool afterUpdate = false) =>
        new("app_launch", new Dictionary<string, string>
        {
            ["launch_type"] = afterUpdate ? "update" : atLogin ? "login" : "manual",
        });

    public static AnalyticsEvent AppUpdate(string previousVersion) =>
        new("hangly_app_update", new Dictionary<string, string> { ["previous_version"] = previousVersion });

    public static AnalyticsEvent CharmSelected(string charmId) =>
        new("charm_selected", new Dictionary<string, string>
        {
            ["charm_id"] = Models.CharmId.IsCustom(charmId) ? "custom" : charmId,
        });

    /// <summary>The style as macOS names it: <c>spiderThread</c>, <c>goldChain</c>.</summary>
    public static AnalyticsEvent RopeSelected(Models.RopeStyle style)
    {
        string name = style.ToString();
        return new("rope_selected", new Dictionary<string, string>
        {
            ["rope_style"] = char.ToLowerInvariant(name[0]) + name[1..],
        });
    }

    public static AnalyticsEvent SupportClicked(SupportSurface surface) =>
        new("support_clicked", new Dictionary<string, string>
        {
            ["surface"] = surface switch
            {
                SupportSurface.Welcome => "welcome",
                SupportSurface.Card => "card",
                SupportSurface.About => "about",
                _ => "release_notes",
            },
        });

    public static AnalyticsEvent WelcomeCompleted(bool explored) =>
        new("welcome_completed", new Dictionary<string, string> { ["path"] = explored ? "explore" : "start" });

    /// <summary>Once a local day while Hangly runs (<see cref="DailyActive"/>).</summary>
    public static AnalyticsEvent DailyActiveDay() => new("daily_active", new Dictionary<string, string>());

    // The update funnel: available → download started → download completed → installed, or failed at a stage.
    // `trigger` is "quiet" (found and fetched by the daily check) or "manual" (Check for Updates).

    public static AnalyticsEvent UpdateAvailable(string toVersion, UpdateTrigger trigger) =>
        new("update_available", new Dictionary<string, string> { ["to_version"] = toVersion, ["trigger"] = TriggerName(trigger) });

    public static AnalyticsEvent UpdateDownloadStarted(string toVersion, UpdateTrigger trigger) =>
        new("update_download_started", new Dictionary<string, string> { ["to_version"] = toVersion, ["trigger"] = TriggerName(trigger) });

    public static AnalyticsEvent UpdateDownloadCompleted(string toVersion, UpdateTrigger trigger) =>
        new("update_download_completed", new Dictionary<string, string> { ["to_version"] = toVersion, ["trigger"] = TriggerName(trigger) });

    /// <summary>Sent by the new version on its first launch, so it is only ever sent by a build that works.</summary>
    public static AnalyticsEvent UpdateInstalled(string fromVersion, string toVersion) =>
        new("update_installed", new Dictionary<string, string> { ["from_version"] = fromVersion, ["to_version"] = toVersion });

    public static AnalyticsEvent UpdateFailed(UpdateStage stage, string error, UpdateTrigger trigger) =>
        new("update_failed", new Dictionary<string, string>
        {
            ["stage"] = stage switch { UpdateStage.Check => "check", UpdateStage.Download => "download", _ => "install" },
            ["error"] = error,
            ["trigger"] = TriggerName(trigger),
        });

    private static string TriggerName(UpdateTrigger trigger) => trigger == UpdateTrigger.Manual ? "manual" : "quiet";

    // The notifications (2.3.0): every card, of either kind, and each kind on its own, so a GA4 report can read the
    // update banner's funnel and a broadcast's reach without filtering. Never the title or the message. macOS's names.

    public static AnalyticsEvent NotificationShown(Notifications.NotificationKind kind, string id) =>
        new("notification_shown", new Dictionary<string, string> { ["kind"] = KindName(kind), ["notification_id"] = Clip(id) });

    public static AnalyticsEvent NotificationClicked(Notifications.NotificationKind kind, string id, string action) =>
        new("notification_clicked", new Dictionary<string, string>
        {
            ["kind"] = KindName(kind),
            ["notification_id"] = Clip(id),
            ["action"] = action,
        });

    public static AnalyticsEvent NotificationDismissed(Notifications.NotificationKind kind, string id, DismissReason reason) =>
        new("notification_dismissed", new Dictionary<string, string>
        {
            ["kind"] = KindName(kind),
            ["notification_id"] = Clip(id),
            ["reason"] = ReasonName(reason),
        });

    public static AnalyticsEvent UpdateBannerShown(string toVersion) =>
        new("update_banner_shown", new Dictionary<string, string> { ["to_version"] = toVersion });

    public static AnalyticsEvent UpdateBannerClicked(string toVersion) =>
        new("update_banner_clicked", new Dictionary<string, string> { ["to_version"] = toVersion });

    public static AnalyticsEvent BroadcastShown(string id) =>
        new("broadcast_shown", new Dictionary<string, string> { ["notification_id"] = Clip(id) });

    public static AnalyticsEvent BroadcastClicked(string id, string action) =>
        new("broadcast_clicked", new Dictionary<string, string> { ["notification_id"] = Clip(id), ["action"] = action });

    public static AnalyticsEvent BroadcastDismissed(string id, DismissReason reason) =>
        new("broadcast_dismissed", new Dictionary<string, string> { ["notification_id"] = Clip(id), ["reason"] = ReasonName(reason) });

    private static string KindName(Notifications.NotificationKind kind) =>
        kind == Notifications.NotificationKind.Update ? "update" : "broadcast";

    private static string ReasonName(DismissReason reason) => reason.ToString().ToLowerInvariant();

    /// <summary>GA4 keeps parameter values up to 100 characters.</summary>
    private static string Clip(string value) => value.Length <= 100 ? value : value[..100];
}

/// <summary>How a card left: on its own, by ×, by Skip This Version, by Later, or cleared from the Center.</summary>
public enum DismissReason
{
    Timeout,
    Closed,
    Skipped,
    Later,
    Cleared,
}

public enum UpdateTrigger
{
    Quiet,
    Manual,
}

public enum UpdateStage
{
    Check,
    Download,
    Install,
}

/// <summary>Where a support button was pressed.</summary>
public enum SupportSurface
{
    Welcome,
    Card,
    About,
    ReleaseNotes,
}

/// <summary>Where events go: GA4 in a build with it configured, nothing in every other, a recorder in tests.</summary>
public interface IUserAnalyticsService
{
    /// <summary>Links events to the installation, never to a person.</summary>
    void SetInstallationId(Guid installationId);

    void Log(AnalyticsEvent analyticsEvent);
}

public sealed class NoOpUserAnalytics : IUserAnalyticsService
{
    public void SetInstallationId(Guid installationId)
    {
    }

    public void Log(AnalyticsEvent analyticsEvent)
    {
    }
}

/// <summary>The app's one service, reachable from the windows that report choices. Set once, at launch.</summary>
public static class HanglyAnalytics
{
    public static IUserAnalyticsService Service { get; private set; } = new NoOpUserAnalytics();

    public static void Use(IUserAnalyticsService service) => Service = service;

    public static void Log(AnalyticsEvent analyticsEvent) => Service.Log(analyticsEvent);
}
