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
