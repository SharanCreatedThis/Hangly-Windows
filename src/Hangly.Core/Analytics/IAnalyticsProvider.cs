//
//  IAnalyticsProvider.cs
//  Hangly
//
//  Where events go, behind an interface so that "nowhere" is a valid answer.
//

namespace Hangly.Core.Analytics;

/// <summary>Somewhere to send events.</summary>
/// <remarks>
/// An interface rather than a direct call into a vendor SDK, for three reasons that all
/// turned out to matter on macOS and matter here: the app has to build and run with no
/// analytics at all, the tests have to be able to read back what would have been sent,
/// and the one place that talks to the network should be small enough to read in a
/// sitting.
/// </remarks>
public interface IAnalyticsProvider
{
    /// <summary>Called once, when analytics is allowed to begin.</summary>
    /// <param name="distinctId">The anonymous installation identifier.</param>
    /// <param name="superProperties">Facts attached to every event from here on.</param>
    void Start(string distinctId, IReadOnlyDictionary<string, AnalyticsValue> superProperties);

    /// <summary>
    /// Person-level properties to attach to the next and every later event.
    /// </summary>
    /// <remarks>
    /// Separate from the super properties because they are about the person rather than
    /// the event, and because they can change while the app is running — a name corrected
    /// on the Appearance page has to reach the project without a relaunch.
    /// </remarks>
    void SetPersonProperties(IReadOnlyDictionary<string, AnalyticsValue> properties);

    void Capture(AnalyticsEvent analyticsEvent);

    /// <summary>
    /// Stops or resumes sending. Off must take effect immediately, and must not be a
    /// filter applied later somewhere else.
    /// </summary>
    void SetEnabled(bool isEnabled);

    /// <summary>Sends whatever is queued. Called when the app is going away.</summary>
    void Flush();
}

/// <summary>Sends nothing, anywhere, ever.</summary>
/// <remarks>
/// What runs when analytics is switched off, when no project key is configured, and in
/// every test that is not specifically about analytics.
/// </remarks>
public sealed class NoOpAnalyticsProvider : IAnalyticsProvider
{
    public void Start(string distinctId, IReadOnlyDictionary<string, AnalyticsValue> superProperties)
    {
    }

    public void SetPersonProperties(IReadOnlyDictionary<string, AnalyticsValue> properties)
    {
    }

    public void Capture(AnalyticsEvent analyticsEvent)
    {
    }

    public void SetEnabled(bool isEnabled)
    {
    }

    public void Flush()
    {
    }
}

/// <summary>Remembers what it was given, and sends nothing.</summary>
/// <remarks>
/// For tests. The point of the provider seam is that a test can read back exactly what
/// would have left the machine, which is the only way to assert a privacy promise rather
/// than describe one.
/// </remarks>
public sealed class RecordingAnalyticsProvider : IAnalyticsProvider
{
    private readonly List<AnalyticsEvent> captured = [];

    public IReadOnlyList<AnalyticsEvent> Captured => captured;

    public string? DistinctId { get; private set; }

    public IReadOnlyDictionary<string, AnalyticsValue>? SuperProperties { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public int StartCount { get; private set; }

    public int FlushCount { get; private set; }

    /// <summary>The person properties last handed over, for tests to assert against.</summary>
    public IReadOnlyDictionary<string, AnalyticsValue>? PersonProperties { get; private set; }

    public void SetPersonProperties(IReadOnlyDictionary<string, AnalyticsValue> properties) =>
        PersonProperties = properties;

    public void Start(string distinctId, IReadOnlyDictionary<string, AnalyticsValue> superProperties)
    {
        DistinctId = distinctId;
        SuperProperties = superProperties;
        StartCount++;
    }

    public void Capture(AnalyticsEvent analyticsEvent) => captured.Add(analyticsEvent);

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void Flush() => FlushCount++;
}
