//
//  PostHogProvider.cs
//  Hangly
//
//  The one place that talks to the network.
//

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangly.App.Services;
using Hangly.Core.Analytics;

namespace Hangly.App.Analytics;

/// <summary>Sends events to PostHog, and nothing else anywhere.</summary>
/// <remarks>
/// <b>Hand-written against PostHog's capture endpoint rather than using their SDK.</b>
/// The macOS build wraps the vendor SDK behind its own provider protocol for the same
/// reasons this exists at all, and on Windows the wrapper turned out to be the whole
/// job: the capture API is one POST of one JSON object. What that buys is worth more
/// than the code it costs — this file is short enough to read in a sitting, so
/// <c>PRIVACY.md</c>'s claim about what leaves the machine is something a person can
/// check rather than take on trust, and there is no transitive dependency that could
/// start collecting something on its own.
///
/// <para><b>Nothing here is allowed to fail visibly.</b> Every send is fire-and-forget
/// and every exception is swallowed into the log. An event that cannot be sent is a fact
/// nobody needs, and analytics must never be able to delay, block or change what the app
/// does — which PRIVACY.md also states.</para>
/// </remarks>
public sealed class PostHogProvider : IAnalyticsProvider, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient client;
    private readonly string key;
    private readonly Uri endpoint;

    private Dictionary<string, object?> superProperties = [];
    private string distinctId = string.Empty;
    private bool isEnabled = true;

    public PostHogProvider(string host, string key)
    {
        this.key = key;
        endpoint = new Uri($"https://{host}/i/v0/e/");
        client = new HttpClient
        {
            // Short on purpose. A stalled analytics request must not outlive the thing
            // it is describing, and there is nothing useful to do about a slow one.
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public void Start(string distinctId, IReadOnlyDictionary<string, AnalyticsValue> superProperties)
    {
        this.distinctId = distinctId;
        this.superProperties = superProperties.ToDictionary(
            pair => pair.Key,
            pair => Unwrap(pair.Value),
            StringComparer.Ordinal);
    }

    public void Capture(AnalyticsEvent analyticsEvent)
    {
        if (!isEnabled || key.Length == 0 || distinctId.Length == 0)
        {
            return;
        }

        var properties = new Dictionary<string, object?>(superProperties, StringComparer.Ordinal)
        {
            ["distinct_id"] = distinctId,
        };

        foreach ((string name, AnalyticsValue value) in analyticsEvent.Properties)
        {
            properties[name] = Unwrap(value);
        }

        var payload = new Payload(key, analyticsEvent.Name, distinctId, properties);

        // Fire and forget. Awaiting this would put the network on the path of a menu
        // click, and the result is not used for anything.
        _ = SendAsync(payload);
    }

    public void SetEnabled(bool isEnabled) => this.isEnabled = isEnabled;

    /// <summary>
    /// Nothing is queued, so there is nothing to flush.
    /// </summary>
    /// <remarks>
    /// Each event is its own request, posted as it happens. Batching would mean holding
    /// events in memory to be lost on a crash, and at this volume — a handful per session
    /// — there is nothing to gain by it.
    /// </remarks>
    public void Flush()
    {
    }

    public void Dispose() => client.Dispose();

    private async Task SendAsync(Payload payload)
    {
        try
        {
            using HttpResponseMessage response =
                await client.PostAsJsonAsync(endpoint, payload, Json).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Diagnostics.Log($"analytics: {(int)response.StatusCode} for '{payload.Event}'");
            }
        }
        catch (Exception exception)
        {
            // Deliberately not Failure(): a machine with no network is not a fault, and
            // a stack trace per event would bury the log it shares with startup.
            Diagnostics.Log($"analytics: '{payload.Event}' not sent ({exception.GetType().Name})");
        }
    }

    /// <summary>The closed set of value types, as the things JSON has.</summary>
    private static object? Unwrap(AnalyticsValue value) => value switch
    {
        AnalyticsValue.Text text => text.Value,
        AnalyticsValue.Integer integer => integer.Value,
        AnalyticsValue.Number number => number.Value,
        AnalyticsValue.Flag flag => flag.Value,
        AnalyticsValue.List list => list.Value,
        _ => null,
    };

    private sealed record Payload(
        [property: JsonPropertyName("api_key")] string ApiKey,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("distinct_id")] string DistinctId,
        [property: JsonPropertyName("properties")] Dictionary<string, object?> Properties);
}
