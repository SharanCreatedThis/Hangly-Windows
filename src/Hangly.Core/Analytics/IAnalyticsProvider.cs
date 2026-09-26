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
/// <para><b>It can say exactly one thing.</b> Hangly used to report behaviour — launches,
/// quits, every charm hung — and at twelve thousand people that was millions of events a
/// month to learn things nobody was reading. What is wanted is a register of people: who
/// they are, on what, at which version. That is one <c>$identify</c>, and this interface
/// has no way to send anything else.</para>
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
}

/// <summary>One identify, as it would have left the machine.</summary>
public sealed record IdentifyCall(
    string DistinctId,
    IReadOnlyDictionary<string, AnalyticsValue> PersonProperties,
    IReadOnlyDictionary<string, AnalyticsValue> EventProperties);

/// <summary>Remembers what it was given, and sends nothing.</summary>
/// <remarks>
/// For tests. The point of the provider seam is that a test can read back exactly what
/// would have left the machine, which is the only way to assert a privacy promise rather
/// than describe one.
/// </remarks>
public sealed class RecordingAnalyticsProvider : IAnalyticsProvider
{
    private readonly List<IdentifyCall> calls = [];

    public IReadOnlyList<IdentifyCall> Calls => calls;

    /// <summary>What the pretend project answers. Set false to stand in for no network.</summary>
    public bool Accepts { get; set; } = true;

    public Task<bool> IdentifyAsync(
        string distinctId,
        IReadOnlyDictionary<string, AnalyticsValue> personProperties,
        IReadOnlyDictionary<string, AnalyticsValue> eventProperties)
    {
        calls.Add(new IdentifyCall(distinctId, personProperties, eventProperties));
        return Task.FromResult(Accepts);
    }
}
