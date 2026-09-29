//
//  Ga4Session.cs
//  Hangly
//
//  Sessions and engagement for events sent through the GA4 Measurement Protocol.
//

namespace Hangly.Core.Analytics;

/// <summary>
/// The two parameters that make a Measurement Protocol event count: <c>session_id</c> and <c>engagement_time_msec</c>.
/// </summary>
/// <remarks>
/// GA4 counts a user as active — in Realtime, Active users, engagement and session reports — only from events that
/// carry both. Without them (2.1.0) every Windows event arrived and every Windows user counted as 0 active users.
/// Firebase's SDK does this itself on macOS; the Measurement Protocol leaves it to the sender.
///
/// <para>A session is what GA4 means by one: it begins with the first event and ends after 30 minutes without one,
/// GA4's default timeout. Its id is the Unix time, in seconds, it began at. Engagement is the time since the previous
/// event in the same session — never more than the timeout — and 1 ms for the first, since GA4 ignores an event whose
/// engagement is 0.</para>
/// </remarks>
public sealed class Ga4Session
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    private readonly Func<DateTimeOffset> now;
    private readonly object gate = new();
    private DateTimeOffset? last;
    private long sessionId;

    public Ga4Session(Func<DateTimeOffset>? now = null) => this.now = now ?? (() => DateTimeOffset.UtcNow);

    /// <summary>The session this event belongs to and the engagement it reports.</summary>
    public (long SessionId, long EngagementMilliseconds) Stamp()
    {
        lock (gate)
        {
            DateTimeOffset time = now();
            if (last is not DateTimeOffset previous || time - previous > Timeout || time < previous)
            {
                sessionId = time.ToUnixTimeSeconds();
                last = time;
                return (sessionId, 1);
            }

            last = time;
            return (sessionId, Math.Max(1, (long)(time - previous).TotalMilliseconds));
        }
    }
}

/// <summary>A Measurement Protocol request body, built apart from sending it so it can be tested.</summary>
public static class Ga4Payload
{
    /// <summary>GA4 drops a parameter value longer than this.</summary>
    public const int ValueLimit = 100;

    public static Dictionary<string, object> Build(
        string clientId, AnalyticsEvent analyticsEvent, long sessionId, long engagementMilliseconds)
    {
        var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((string key, string value) in analyticsEvent.Parameters)
        {
            parameters[key] = value.Length > ValueLimit ? value[..ValueLimit] : value;
        }

        parameters["session_id"] = sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        parameters["engagement_time_msec"] = engagementMilliseconds;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["client_id"] = clientId,
            ["user_id"] = clientId,
            ["events"] = new object[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["name"] = analyticsEvent.Name,
                    ["params"] = parameters,
                },
            },
        };
    }
}

/// <summary>Once a local day, a <c>daily_active</c> event: a tray app can run for days without another event.</summary>
public static class DailyActive
{
    /// <summary>Today's key if the day has not been reported yet, otherwise null.</summary>
    public static string? Due(string? lastReportedDay, DateTimeOffset localNow)
    {
        string today = localNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return string.Equals(lastReportedDay, today, StringComparison.Ordinal) ? null : today;
    }
}
