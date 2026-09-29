//
//  Ga4UserAnalytics.cs
//  Hangly
//
//  Events to Google Analytics, through the Measurement Protocol.
//

using System.Net.Http.Json;
using Hangly.Core.Analytics;

namespace Hangly.App.Analytics;

/// <summary>The six events, to the same Google Analytics property as macOS's Firebase Analytics.</summary>
/// <remarks>
/// Firebase has no Windows SDK, so Windows sends to the property's Web data stream through the GA4 Measurement
/// Protocol. The Measurement ID and API secret are injected by the release build from repository secrets and never
/// committed; a build without them uses <see cref="NoOpUserAnalytics"/> and sends nothing.
///
/// <para><c>client_id</c> and <c>user_id</c> are the installation ID: events join to the registry document without
/// carrying anything personal. Best effort — an event that cannot be sent is dropped, not queued; the registry, not
/// Analytics, is the record that must not miss anybody.</para>
/// </remarks>
internal sealed class Ga4UserAnalytics : IUserAnalyticsService
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly Uri endpoint;
    private string clientId = Guid.NewGuid().ToString("D");
    private readonly Ga4Session session = new();

    private Ga4UserAnalytics(string measurementId, string apiSecret) =>
        endpoint = new Uri($"https://www.google-analytics.com/mp/collect?measurement_id={Uri.EscapeDataString(measurementId)}&api_secret={Uri.EscapeDataString(apiSecret)}");

    /// <summary>A service for this build's GA4 stream, or null when the build has none.</summary>
    public static Ga4UserAnalytics? For(string measurementId, string apiSecret) =>
        measurementId.StartsWith("G-", StringComparison.Ordinal) && apiSecret.Length > 0
            ? new Ga4UserAnalytics(measurementId, apiSecret)
            : null;

    public void SetInstallationId(Guid installationId) => clientId = installationId.ToString("D").ToLowerInvariant();

    public void Log(AnalyticsEvent analyticsEvent)
    {
        // session_id and engagement_time_msec, or GA4 counts nobody as active (Ga4Session).
        (long sessionId, long engagement) = session.Stamp();
        _ = Send(Ga4Payload.Build(clientId, analyticsEvent, sessionId, engagement));
    }

    private async Task Send(object body)
    {
        try
        {
            using HttpResponseMessage _ = await http.PostAsJsonAsync(endpoint, body).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Dropped: see the remarks.
        }
    }
}
