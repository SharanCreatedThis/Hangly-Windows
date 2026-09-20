//
//  AnalyticsManager.cs
//  Hangly
//
//  What is sent, when, and whether it is sent at all.
//

using Hangly.Core.Settings;

namespace Hangly.Core.Analytics;

/// <summary>Where events are going, as the inspector reports it.</summary>
public readonly record struct AnalyticsConnection(string Summary, bool IsSending)
{
    /// <summary>Nothing started, because sharing is off or the app has not finished launching.</summary>
    public static AnalyticsConnection NotStarted { get; } = new("Not started", false);

    /// <summary>No project key in this build, so there is nowhere to send.</summary>
    public static AnalyticsConnection NoDestination { get; } = new("No destination configured", false);

    public static AnalyticsConnection Connected(string host) => new($"Connected — {host}", true);
}

/// <summary>Decides what leaves the machine.</summary>
/// <remarks>
/// Every event in the app goes through here, and here is the only place that reads the
/// privacy setting. Switching analytics off stops capture at this gate <b>and</b> tells
/// the provider to stop — belt and braces, because "we filter it later" is how data gets
/// sent by accident.
///
/// <para>Nothing here is allowed to fail visibly. There is no error path to the interface
/// and nothing throws: an event that cannot be sent is a fact nobody needs.</para>
///
/// <para>In <c>Hangly.Core</c> rather than the app, so that every claim
/// <c>PRIVACY.md</c> makes is something a test can assert against a recording provider
/// rather than something a person has to take on trust.</para>
/// </remarks>
public sealed class AnalyticsManager
{
    private readonly SettingsStore store;
    private readonly IAnalyticsProvider provider;
    private readonly string host;
    private readonly bool hasDestination;
    private readonly string appVersion;
    private readonly string buildNumber;
    private readonly string systemVersion;

    private bool hasStarted;

    public AnalyticsManager(
        SettingsStore store,
        IAnalyticsProvider provider,
        string host,
        bool hasDestination,
        string appVersion,
        string buildNumber,
        string systemVersion)
    {
        this.store = store;
        this.provider = provider;
        this.host = host;
        this.hasDestination = hasDestination;
        this.appVersion = appVersion;
        this.buildNumber = buildNumber;
        this.systemVersion = systemVersion;
    }

    /// <summary>Raised when anything the inspector shows has changed.</summary>
    public event Action? Changed;

    public string? LastEventName { get; private set; }

    public DateTimeOffset? LastEventAt { get; private set; }

    public int SentCount { get; private set; }

    public AnalyticsConnection Connection { get; private set; } = AnalyticsConnection.NotStarted;

    public bool IsEnabled => store.Settings.Privacy.AnalyticsEnabled;

    /// <summary>Where events would be sent, whether or not any are.</summary>
    public string Endpoint => hasDestination ? host : "none";

    /// <summary>The installation identifier with most of it hidden.</summary>
    /// <remarks>
    /// Enough to tell two machines apart when somebody reads it out, and not enough to be
    /// worth writing down. Null when no identifier exists, which is the normal state of
    /// an install that has never sent anything.
    /// </remarks>
    public string? MaskedIdentifier
    {
        get
        {
            if (store.Settings.Privacy.AnonymousId is not Guid id)
            {
                return null;
            }

            string text = id.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
            return $"{text[..8]}-••••-••••-••••-••••••••{text[^4..]}";
        }
    }

    /// <summary>Counts the launch, starts the provider if allowed, and says hello.</summary>
    /// <remarks>
    /// The launch is counted whether or not analytics is on, because the follow card is
    /// scheduled off the same number and that has nothing to do with analytics.
    ///
    /// <para>Guarded against running twice. Two <c>app_launch</c> events from one launch
    /// is not a rounding error; it is every per-launch number doubled.</para>
    /// </remarks>
    public void Start()
    {
        if (hasStarted)
        {
            return;
        }

        hasStarted = true;

        bool isFirstLaunch = false;
        store.Update(settings =>
        {
            MilestoneSettings milestones = settings.Milestones with
            {
                LaunchCount = settings.Milestones.LaunchCount + 1,
            };

            isFirstLaunch = milestones.IsFirstLaunch;
            return settings with { Milestones = milestones };
        });

        if (!IsEnabled)
        {
            return;
        }

        StartProvider();

        if (isFirstLaunch)
        {
            Track(Events.AppFirstLaunch);
        }

        Track(Events.AppLaunch);
    }

    /// <summary>Called as the app goes away, so the queue is not lost with it.</summary>
    public void Stop()
    {
        if (!hasStarted || !IsEnabled)
        {
            return;
        }

        Track(Events.AppQuit);
        provider.Flush();
    }

    public void Track(AnalyticsEvent analyticsEvent)
    {
        if (!IsEnabled)
        {
            return;
        }

        var properties = new Dictionary<string, AnalyticsValue>(analyticsEvent.Properties, StringComparer.Ordinal);
        foreach ((string key, AnalyticsValue value) in RopeProperties())
        {
            // The event's own properties win. A charm event naming a charm should not
            // have it overwritten by the rope's summary of the same thing.
            properties.TryAdd(key, value);
        }

        // Read fresh every time, so a name corrected on the Appearance page reaches the
        // project with the next event rather than the next launch.
        properties["user_name"] = AnalyticsValue.Of(store.Settings.DisplayName);
        provider.SetPersonProperties(PersonProperties());

        provider.Capture(new AnalyticsEvent(analyticsEvent.Name, properties));

        LastEventName = analyticsEvent.Name;
        LastEventAt = DateTimeOffset.Now;
        SentCount++;
        Changed?.Invoke();
    }

    /// <summary>Turns sharing on or off. Off stops capture on the next line, not later.</summary>
    public void SetEnabled(bool isEnabled)
    {
        if (isEnabled == IsEnabled)
        {
            return;
        }

        if (!isEnabled)
        {
            provider.SetEnabled(false);
            provider.Flush();
            Connection = AnalyticsConnection.NotStarted;
            LastEventName = null;
            LastEventAt = null;
            SentCount = 0;

            // Sharing off discards the identifier, which PRIVACY.md states plainly.
            store.Update(settings => settings with { Privacy = settings.Privacy.Forgotten() });
            Changed?.Invoke();
            return;
        }

        store.Update(settings => settings with
        {
            Privacy = settings.Privacy with { AnalyticsEnabled = true },
        });

        // Back on means a new identity, because the old one was discarded on the way out
        // and stitching the two would defeat the point of discarding it.
        provider.SetEnabled(true);
        StartProvider();
        Track(Events.AppLaunch);
    }

    private void StartProvider()
    {
        provider.Start(CurrentIdentifier(), SuperProperties());
        provider.SetPersonProperties(PersonProperties());
        Connection = hasDestination ? AnalyticsConnection.Connected(host) : AnalyticsConnection.NoDestination;
        Changed?.Invoke();
    }

    /// <summary>The installation identifier, minted on first use and stored from then on.</summary>
    private string CurrentIdentifier()
    {
        Guid identifier = store.Settings.Privacy.AnonymousId ?? Guid.NewGuid();
        store.Update(settings => settings with
        {
            Privacy = settings.Privacy with { AnonymousId = settings.Privacy.AnonymousId ?? identifier },
        });

        return store.Settings.Privacy.AnonymousId?.ToString("D", System.Globalization.CultureInfo.InvariantCulture)
            ?? identifier.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Facts about the build, the machine and the person, attached to every event.
    /// </summary>
    /// <remarks>
    /// <b>The name is here deliberately, and it is the only personal thing in the whole
    /// payload.</b> It is typed during onboarding and can be corrected on the Appearance
    /// page; it is never read from the Windows account, the Microsoft account, the
    /// computer name or any other part of the machine. PRIVACY.md says so in those terms.
    ///
    /// <para><c>os_version</c> and <c>windows_version</c> both carry the same string, on
    /// purpose. macOS sends <c>macos_version</c>, so a query that wants "which OS version"
    /// across both platforms needs a key that means the same thing on each, and a query
    /// that wants Windows specifically still has the old one. Naming only one of them
    /// would break one of those two questions.</para>
    /// </remarks>
    private Dictionary<string, AnalyticsValue> SuperProperties() => new(StringComparer.Ordinal)
    {
        ["user_name"] = AnalyticsValue.Of(store.Settings.DisplayName),
        ["platform"] = AnalyticsValue.Of("windows"),
        ["architecture"] = AnalyticsValue.Of(Architecture),
        ["app_version"] = AnalyticsValue.Of(appVersion),
        ["build_number"] = AnalyticsValue.Of(buildNumber),
        ["os_version"] = AnalyticsValue.Of(systemVersion),
        ["windows_version"] = AnalyticsValue.Of(systemVersion),
        ["analytics_enabled"] = AnalyticsValue.Of(IsEnabled),
    };

    /// <summary>Which silicon this copy is running on, as PostHog should see it.</summary>
    private static string Architecture =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            var other => other.ToString().ToLowerInvariant(),
        };

    /// <summary>
    /// The person-level properties, sent with every event so they stay current.
    /// </summary>
    /// <remarks>
    /// PostHog reads <c>$set</c> off an event and applies it to the person the event
    /// belongs to. Sending it every time rather than once is what makes a renamed person
    /// actually rename: super properties are handed over when the provider starts, and a
    /// name changed on the Appearance page afterwards would otherwise not reach the
    /// project until the next launch.
    /// </remarks>
    public Dictionary<string, AnalyticsValue> PersonProperties() => new(StringComparer.Ordinal)
    {
        ["user_name"] = AnalyticsValue.Of(store.Settings.DisplayName),
        ["platform"] = AnalyticsValue.Of("windows"),
        ["architecture"] = AnalyticsValue.Of(Architecture),
        ["app_version"] = AnalyticsValue.Of(appVersion),
        ["os_version"] = AnalyticsValue.Of(systemVersion),
    };

    /// <summary>What is on the rope, which is the shape of how the app is used.</summary>
    private Dictionary<string, AnalyticsValue> RopeProperties()
    {
        OverlaySettings overlay = store.Settings.Overlay;
        return new Dictionary<string, AnalyticsValue>(StringComparer.Ordinal)
        {
            ["charm_count"] = AnalyticsValue.Of(overlay.CharmIds.Count),
            ["active_charm_ids"] = AnalyticsValue.Of([.. overlay.CharmIds.Select(Events.NameOf)]),
            ["rope_style"] = AnalyticsValue.Of(overlay.RopeStyle.ToString()),
        };
    }
}
