//
//  AnalyticsManager.cs
//  Hangly
//
//  Who is told about this person, when, and whether they are told at all.
//

using System.Globalization;
using Hangly.Core.Settings;

namespace Hangly.Core.Analytics;

/// <summary>What the About page says about where analytics goes.</summary>
public readonly record struct AnalyticsConnection(string Summary, bool IsSending)
{
    public static AnalyticsConnection NotStarted { get; } = new("Not started", false);

    public static AnalyticsConnection NoDestination { get; } = new("No destination configured", false);

    public static AnalyticsConnection Connected(string host) => new($"Connected — {host}", true);
}

/// <summary>Why an identify was sent. There are exactly three.</summary>
public enum IdentifyReason
{
    /// <summary>A person the project has never heard from, once they have given a name.</summary>
    FirstLaunch,

    /// <summary>The name was changed since the project last accepted one.</summary>
    NameChanged,

    /// <summary>A major version the project has not seen this person on.</summary>
    /// <remarks>
    /// Also how somebody identified under the old event-based analytics is carried over:
    /// they have an identifier and no record of a major version, and this build is the
    /// first they have run that keeps one.
    /// </remarks>
    MajorVersion,
}

/// <summary>Decides whether this person is described to the project, and does it.</summary>
/// <remarks>
/// <b>People, not behaviour.</b> The project learns who runs Hangly — a name, a platform,
/// an architecture and the versions of the app and the system — and nothing about what
/// they do with it. One <c>$identify</c> is sent on a first launch, on a rename, and on the
/// first launch of a new major version, and that is all that ever leaves the machine.
///
/// <para>Everything here is the only place that reads the privacy setting, and nothing
/// here can fail visibly. There is no error path to the interface and no exception: an
/// identify that is not accepted is simply tried again on the next launch, because the
/// fields that say what was sent are written only when the project accepts it.</para>
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
    private Task? inFlight;

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

    /// <summary>Raised when anything the About page shows has changed.</summary>
    public event Action? Changed;

    /// <summary>The last identify the project accepted this session, and why it was sent.</summary>
    public IdentifyReason? LastReason { get; private set; }

    public DateTimeOffset? LastSentAt { get; private set; }

    public AnalyticsConnection Connection =>
        !IsEnabled ? AnalyticsConnection.NotStarted
        : hasDestination ? AnalyticsConnection.Connected(host)
        : AnalyticsConnection.NoDestination;

    public bool IsEnabled => store.Settings.Privacy.AnalyticsEnabled;

    /// <summary>Whether a name has been given. Nothing is sent for somebody who has not.</summary>
    public bool HasName => store.Settings.DisplayName.Trim().Length > 0;

    public string Endpoint => hasDestination ? host : "none";

    /// <summary>The installation identifier with most of it hidden.</summary>
    public string? MaskedIdentifier
    {
        get
        {
            if (store.Settings.Privacy.AnonymousId is not Guid id)
            {
                return null;
            }

            string text = id.ToString("D", CultureInfo.InvariantCulture);
            return $"{text[..8]}-••••-••••-••••-••••••••{text[^4..]}";
        }
    }

    /// <summary>Counts the launch, then identifies this person if one of the three triggers applies.</summary>
    /// <remarks>
    /// The launch is counted whether or not analytics is on, because the follow card is
    /// scheduled off the same number and that has nothing to do with analytics.
    /// </remarks>
    public Task Start()
    {
        if (hasStarted)
        {
            return inFlight ?? Task.CompletedTask;
        }

        hasStarted = true;
        store.Update(settings => settings with
        {
            Milestones = settings.Milestones with { LaunchCount = settings.Milestones.LaunchCount + 1 },
        });

        return Sync();
    }

    /// <summary>
    /// Sends an identify if the project's record of this person is out of date, and
    /// otherwise does nothing.
    /// </summary>
    /// <remarks>
    /// Safe to call as often as anything might have changed — after the welcome card takes
    /// a name, after a rename, after sharing is switched back on. It compares what the
    /// project was last told with what is true now, so calling it twice sends once.
    /// </remarks>
    public Task Sync()
    {
        if (inFlight is { IsCompleted: false })
        {
            return inFlight;
        }

        if (!hasStarted || PendingReason() is not IdentifyReason reason)
        {
            return Task.CompletedTask;
        }

        inFlight = Send(reason);
        return inFlight;
    }

    /// <summary>Which of the three triggers applies right now, or null if none does.</summary>
    public IdentifyReason? PendingReason()
    {
        if (!IsEnabled || !HasName || !hasDestination)
        {
            return null;
        }

        PrivacySettings privacy = store.Settings.Privacy;

        if (privacy.IdentifiedMajorVersion is not int identifiedMajor)
        {
            return privacy.AnonymousId is null || privacy.FirstIdentifyPending
                ? IdentifyReason.FirstLaunch
                : IdentifyReason.MajorVersion;
        }

        if (!string.Equals(privacy.IdentifiedName, CurrentName, StringComparison.Ordinal))
        {
            return IdentifyReason.NameChanged;
        }

        return MajorOf(appVersion) > identifiedMajor ? IdentifyReason.MajorVersion : null;
    }

    /// <summary>Turns sharing on or off. Off takes effect on the next line, not later.</summary>
    public Task SetEnabled(bool isEnabled)
    {
        if (isEnabled == IsEnabled)
        {
            return Task.CompletedTask;
        }

        if (!isEnabled)
        {
            // Sharing off discards the identifier and the record of what was sent under
            // it, which PRIVACY.md states plainly.
            store.Update(settings => settings with { Privacy = settings.Privacy.Forgotten() });
            LastReason = null;
            LastSentAt = null;
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        store.Update(settings => settings with
        {
            Privacy = settings.Privacy with { AnalyticsEnabled = true },
        });
        Changed?.Invoke();

        // Back on is a new person, because the old identifier was thrown away on the way
        // out and stitching the two together would defeat the point of throwing it away.
        return Sync();
    }

    /// <summary>What the project stores on the person. Shown on the About page, key by key.</summary>
    /// <remarks>
    /// <b>The name is the only personal thing here.</b> It is typed during onboarding and
    /// can be changed in Appearance; it is never read from the Windows account, the
    /// Microsoft account, the computer name or any other part of the machine.
    ///
    /// <para>It is written three times because PostHog shows a person by the first of
    /// <c>email</c>, <c>name</c> or <c>username</c> it finds, and <c>user_name</c> — which
    /// earlier builds used and existing queries still read — is in none of those lists.</para>
    /// </remarks>
    public Dictionary<string, AnalyticsValue> PersonProperties() => new(StringComparer.Ordinal)
    {
        ["user_name"] = AnalyticsValue.Of(CurrentName),
        ["name"] = AnalyticsValue.Of(CurrentName),
        ["username"] = AnalyticsValue.Of(CurrentName),
        ["platform"] = AnalyticsValue.Of("windows"),
        ["architecture"] = AnalyticsValue.Of(Architecture),
        ["app_version"] = AnalyticsValue.Of(appVersion),
        ["build_number"] = AnalyticsValue.Of(buildNumber),
        ["os_version"] = AnalyticsValue.Of(systemVersion),
        ["$os"] = AnalyticsValue.Of("Windows"),
        ["$os_version"] = AnalyticsValue.Of(systemVersion),
        ["$app_version"] = AnalyticsValue.Of(appVersion),
    };

    /// <summary>What rides on the identify itself, beside the person.</summary>
    /// <remarks>
    /// <b>No geolocation.</b> PostHog derives a country, region and city from the sending
    /// address unless told not to. <c>$geoip_disable</c> is the switch its ingestion
    /// honours; <c>$ip</c> null keeps the address itself off the event.
    /// </remarks>
    public Dictionary<string, AnalyticsValue> EventProperties(IdentifyReason reason) => new(StringComparer.Ordinal)
    {
        ["identify_reason"] = AnalyticsValue.Of(NameOf(reason)),
        ["$lib"] = AnalyticsValue.Of(LibraryName),
        ["$lib_version"] = AnalyticsValue.Of(appVersion),
        ["$geoip_disable"] = AnalyticsValue.Of(true),
        ["$ip"] = AnalyticsValue.Null,
    };

    /// <summary>The words each reason is reported as, identical on both platforms.</summary>
    public static string NameOf(IdentifyReason reason) => reason switch
    {
        IdentifyReason.FirstLaunch => "first_launch",
        IdentifyReason.NameChanged => "name_changed",
        IdentifyReason.MajorVersion => "major_version",
        _ => "unknown",
    };

    /// <summary>The major part of a version string: 1 for "1.0.3", 0 for "0.9.4+abc".</summary>
    public static int MajorOf(string version)
    {
        string head = version.Split('.', '+', '-')[0];
        return int.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out int major) ? major : 0;
    }

    private string CurrentName => store.Settings.DisplayName.Trim();

    private async Task Send(IdentifyReason reason)
    {
        string distinctId = CurrentIdentifier();
        string name = CurrentName;
        bool accepted = await provider
            .IdentifyAsync(distinctId, PersonProperties(), EventProperties(reason))
            .ConfigureAwait(false);

        if (!accepted)
        {
            return;
        }

        // Written only now, and only if sharing is still on and the identifier is still
        // the one that was sent: switching off mid-flight must not leave a record behind.
        store.Update(settings =>
            settings.Privacy.AnalyticsEnabled
            && settings.Privacy.AnonymousId?.ToString("D", CultureInfo.InvariantCulture) == distinctId
                ? settings with
                {
                    Privacy = settings.Privacy with
                    {
                        IdentifiedName = name,
                        IdentifiedMajorVersion = MajorOf(appVersion),
                        FirstIdentifyPending = false,
                    },
                }
                : settings);

        LastReason = reason;
        LastSentAt = DateTimeOffset.Now;
        Changed?.Invoke();
    }

    /// <summary>The installation identifier, minted on first use and stored from then on.</summary>
    /// <remarks>
    /// Minting one marks the first identify as pending, which is what lets a first launch
    /// that could not reach the network still be reported as a first launch next time.
    /// </remarks>
    private string CurrentIdentifier()
    {
        if (store.Settings.Privacy.AnonymousId is not Guid existing)
        {
            Guid minted = Guid.NewGuid();
            store.Update(settings => settings with
            {
                Privacy = settings.Privacy with { AnonymousId = minted, FirstIdentifyPending = true },
            });
            existing = minted;
        }

        return existing.ToString("D", CultureInfo.InvariantCulture);
    }

    /// <summary>What this build calls itself to PostHog: not one of PostHog's SDKs, because it is not one.</summary>
    private const string LibraryName = "hangly-windows";

    /// <summary>Which silicon this copy is running on, as PostHog should see it.</summary>
    private static string Architecture =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            var other => other.ToString().ToLowerInvariant(),
        };
}
