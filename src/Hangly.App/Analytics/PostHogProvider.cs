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

/// <summary>Tells PostHog who is running Hangly, and nothing else anywhere.</summary>
/// <remarks>
/// <b>Hand-written against PostHog's capture endpoint rather than using their SDK.</b> The
/// capture API is one POST of one JSON object, and the only object this app sends is an
/// <c>$identify</c>. What that buys is worth more than the code it costs — this file is
/// short enough to read in a sitting, so <c>PRIVACY.md</c>'s claim about what leaves the
/// machine is something a person can check rather than take on trust, and there is no
/// transitive dependency that could start collecting something on its own.
///
/// <para><b>Nothing here is allowed to fail visibly.</b> Every exception is swallowed into
/// the log and reported as "not accepted", which the manager answers by trying again on
/// the next launch. Analytics must never be able to delay, block or change what the app
/// does.</para>
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

    public PostHogProvider(string host, string key)
    {
        this.key = key;
        // A bare host is PostHog over HTTPS, which is all a release is ever given. A host
        // that states its own scheme is a test destination — the capture server the offline
        // verification runs against — and is taken as written.
        endpoint = new Uri(host.Contains("://", StringComparison.Ordinal)
            ? $"{host.TrimEnd('/')}/i/v0/e/"
            : $"https://{host}/i/v0/e/");
        client = new HttpClient
        {
            // Short on purpose. There is nothing useful to do about a slow request, and a
            // missed identify is sent again next launch anyway.
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<bool> IdentifyAsync(
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> personProperties,
        IReadOnlyDictionary<string, AnalyticsValue> eventProperties)
    {
        Payload payload = PayloadFor(distinctId, personProperties, eventProperties);

        try
        {
            using HttpResponseMessage response =
                await client.PostAsJsonAsync(endpoint, payload, Json).ConfigureAwait(false);

            // Logged either way. A privacy claim is easier to believe when the log says
            // what left and what came back.
            Diagnostics.Log($"analytics: {(int)response.StatusCode} for $identify ({Reason(eventProperties)})");
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            // Deliberately not Failure(): a machine with no network is not a fault.
            Diagnostics.Log($"analytics: $identify ({Reason(eventProperties)}) not sent ({exception.GetType().Name})");
            return false;
        }
    }

    /// <summary>The exact JSON an identify would put on the wire, for <c>--check-analytics</c>.</summary>
    public string Preview(
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> personProperties,
        IReadOnlyDictionary<string, AnalyticsValue> eventProperties) =>
        JsonSerializer.Serialize(PayloadFor(distinctId, personProperties, eventProperties) with { ApiKey = "phc_…" }, Json);

    public void Dispose() => client.Dispose();

    private Payload PayloadFor(
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> personProperties,
        IReadOnlyDictionary<string, AnalyticsValue> eventProperties)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string name, AnalyticsValue value) in eventProperties)
        {
            properties[name] = Unwrap(value);
        }

        // PostHog stores $set on the person the identify names. It is the only place the
        // name and the platform facts are written.
        properties["$set"] = personProperties.ToDictionary(pair => pair.Key, pair => Unwrap(pair.Value), StringComparer.Ordinal);

        return new Payload(key, "$identify", distinctId, properties);
    }

    private static string Reason(IReadOnlyDictionary<string, AnalyticsValue> eventProperties) =>
        eventProperties.TryGetValue("identify_reason", out AnalyticsValue? value) && value is AnalyticsValue.Text text
            ? text.Value
            : "unknown";

    /// <summary>The closed set of value types, as the things JSON has.</summary>
    /// <remarks>
    /// <see cref="AnalyticsValue.Null"/> becomes a JSON null. The ignore-null option above
    /// applies to the payload's own members, not to dictionary entries, so <c>$ip</c>
    /// reaches the wire as <c>null</c> rather than disappearing.
    /// </remarks>
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
