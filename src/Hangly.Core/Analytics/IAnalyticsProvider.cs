//
//  IAnalyticsProvider.cs
//  Hangly
//
//  Where the one message goes, behind an interface so that "nowhere" is a valid answer.
//

namespace Hangly.Core.Analytics;

/// <summary>Somewhere to say who is running Hangly.</summary>
/// <remarks>
/// An interface rather than a direct call into a vendor SDK, for three reasons that all
/// turned out to matter on macOS and matter here: the app has to build and run with no
/// analytics at all, the tests have to be able to read back what would have been sent,
/// and the one place that talks to the network should be small enough to read in a
/// sitting.
///
/// <para><b>It can say exactly three things.</b> Hangly used to report behaviour — launches,
/// quits, every charm hung — and at twelve thousand people that was millions of events a
/// month to learn things nobody was reading. What is wanted is a register of people and
/// whether they are still here: an <c>$identify</c>, a daily "still running", and, on
/// Windows, "uninstalled". The operational events are a closed enum, so this interface has
/// no way to send anything else.</para>
/// </remarks>
public interface IAnalyticsProvider
{
    /// <summary>Tells the project who this person is.</summary>
    /// <param name="distinctId">The installation identifier.</param>
    /// <param name="personProperties">What PostHog stores on the person, as <c>$set</c>.</param>
    /// <param name="eventProperties">What rides on the identify itself.</param>
    /// <returns>
    /// Whether the project accepted it. The manager only records a person as identified on
    /// <see langword="true"/>, so a first launch with no network is retried on the next
    /// launch rather than never counted.
    /// </returns>
    Task<bool> IdentifyAsync(
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> personProperties,
        IReadOnlyDictionary<string, AnalyticsValue> eventProperties);

    /// <summary>Sends one of the two operational events, and says whether it was accepted.</summary>
    /// <param name="operationalEvent">Which one. There is no way to name any other.</param>
    /// <param name="distinctId">The installation identifier.</param>
    /// <param name="properties">What rides on the event.</param>
    /// <param name="personProperties">What PostHog should update on the person, as <c>$set</c>, or null.</param>
    Task<bool> SendAsync(
        OperationalEvent operationalEvent,
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> properties,
        IReadOnlyDictionary<string, AnalyticsValue>? personProperties);
}

/// <summary>The only events besides <c>$identify</c> that can ever leave the machine.</summary>
/// <remarks>Operational, not behavioural: neither says anything about what a person did in the app.</remarks>
public enum OperationalEvent
{
    /// <summary><c>daily_active</c> — this install ran today. At most once per calendar day.</summary>
    DailyActive,

    /// <summary><c>app_uninstalled</c> — Windows only, from the uninstaller, once.</summary>
    Uninstalled,
}

/// <summary>The wire names of <see cref="OperationalEvent"/>, identical on both platforms.</summary>
public static class OperationalEvents
{
    public static string NameOf(OperationalEvent operationalEvent) => operationalEvent switch
    {
        OperationalEvent.DailyActive => "daily_active",
        OperationalEvent.Uninstalled => "app_uninstalled",
        _ => throw new ArgumentOutOfRangeException(nameof(operationalEvent)),
    };
}

/// <summary>Sends nothing, anywhere, ever.</summary>
/// <remarks>
/// What runs when no project key is configured — every development and CI build — and in
/// every test that is not specifically about analytics. It reports failure, so nothing is
/// ever recorded as sent by a build that sent nothing.
/// </remarks>
public sealed class NoOpAnalyticsProvider : IAnalyticsProvider
{
    public Task<bool> IdentifyAsync(
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> personProperties,
        IReadOnlyDictionary<string, AnalyticsValue> eventProperties) => Task.FromResult(false);

    public Task<bool> SendAsync(
        OperationalEvent operationalEvent,
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> properties,
        IReadOnlyDictionary<string, AnalyticsValue>? personProperties) => Task.FromResult(false);
}

/// <summary>One identify, as it would have left the machine.</summary>
public sealed record IdentifyCall(
    string DistinctId,
    IReadOnlyDictionary<string, AnalyticsValue> PersonProperties,
    IReadOnlyDictionary<string, AnalyticsValue> EventProperties);

/// <summary>One operational event, as it would have left the machine.</summary>
public sealed record EventCall(
    OperationalEvent Event,
    string DistinctId,
    IReadOnlyDictionary<string, AnalyticsValue> Properties,
    IReadOnlyDictionary<string, AnalyticsValue>? PersonProperties);

/// <summary>Remembers what it was given, and sends nothing.</summary>
/// <remarks>
/// For tests. The point of the provider seam is that a test can read back exactly what
/// would have left the machine, which is the only way to assert a privacy promise rather
/// than describe one.
/// </remarks>
public sealed class RecordingAnalyticsProvider : IAnalyticsProvider
{
    private readonly List<IdentifyCall> calls = [];
    private readonly List<EventCall> events = [];
    private readonly List<string> order = [];

    public IReadOnlyList<IdentifyCall> Calls => calls;

    public IReadOnlyList<EventCall> Events => events;

    /// <summary>Everything, in the order it was sent: <c>$identify</c> or an event's wire name.</summary>
    public IReadOnlyList<string> Order => order;

    /// <summary>What the pretend project answers. Set false to stand in for no network.</summary>
    public bool Accepts { get; set; } = true;

    public Task<bool> IdentifyAsync(
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> personProperties,
        IReadOnlyDictionary<string, AnalyticsValue> eventProperties)
    {
        calls.Add(new IdentifyCall(distinctId, personProperties, eventProperties));
        order.Add("$identify");
        return Task.FromResult(Accepts);
    }

    public Task<bool> SendAsync(
        OperationalEvent operationalEvent,
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> properties,
        IReadOnlyDictionary<string, AnalyticsValue>? personProperties)
    {
        events.Add(new EventCall(operationalEvent, distinctId, properties, personProperties));
        order.Add(OperationalEvents.NameOf(operationalEvent));
        return Task.FromResult(Accepts);
    }
}
