//
//  AppSettings.cs
//  Hangly
//
//  The settings document, and the tolerant reading of it.
//

using System.Text.Json;
using System.Text.Json.Serialization;
using Hangly.Core.Models;

namespace Hangly.Core.Settings;

/// <summary>Everything about the overlay a person can change.</summary>
/// <remarks>
/// A plain immutable record: no change notification, nothing platform-shaped, nothing
/// that can be mutated from two places at once. The store owns the one write path.
/// </remarks>
public sealed record OverlaySettings
{
    public bool IsEnabled { get; init; } = true;

    /// <summary>How visible the charm is, 0.2 to 1.</summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>How large the charm is drawn, as a multiple of the shipped size.</summary>
    public double CharmSize { get; init; } = 1.0;

    /// <summary>How far the charm hangs, as a multiple of the shipped rope.</summary>
    public double RopeLength { get; init; } = 1.0;

    public OverlayAnchor Anchor { get; init; } = OverlayAnchor.TopCenter;

    /// <summary>User nudge from the anchor, in points.</summary>
    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public RopeStyle RopeStyle { get; init; } = RopeStyleTable.Shipped;

    /// <summary>Index of the display to hang on, in the order the system reports them.</summary>
    public int DisplayIndex { get; init; }

    public bool SoundEnabled { get; init; } = true;

    public double SoundVolume { get; init; } = 0.5;

    /// <summary>The charms on the cord, from the anchor down.</summary>
    /// <remarks>
    /// Ids from <c>CharmCatalog</c>, which are the macOS <c>CharmKind</c> raw values, so
    /// a settings file means the same thing on both platforms. One to
    /// <see cref="CharmStack.MaximumCount"/> of them; an unknown id is replaced rather
    /// than dropped, because a file written by a newer build should cost the user a
    /// different charm and not an empty rope.
    /// </remarks>
    public IReadOnlyList<string> CharmIds { get; init; } = [CharmCatalog.DefaultId];

    /// <summary>Compares by value, including the charms.</summary>
    /// <remarks>
    /// Hand-written because the generated one is wrong here. A record compares each
    /// member with <c>EqualityComparer&lt;T&gt;.Default</c>, and for a list that is
    /// reference equality — so two documents naming the same charms compared as
    /// different, and <see cref="SettingsStore"/>'s "has anything actually changed"
    /// guard stopped guarding: every read raised a change and rewrote the file.
    /// </remarks>
    public bool Equals(OverlaySettings? other) =>
        other is not null
        && IsEnabled == other.IsEnabled
        && Opacity.Equals(other.Opacity)
        && CharmSize.Equals(other.CharmSize)
        && RopeLength.Equals(other.RopeLength)
        && Anchor == other.Anchor
        && OffsetX.Equals(other.OffsetX)
        && OffsetY.Equals(other.OffsetY)
        && RopeStyle == other.RopeStyle
        && DisplayIndex == other.DisplayIndex
        && SoundEnabled == other.SoundEnabled
        && SoundVolume.Equals(other.SoundVolume)
        && CharmIds.SequenceEqual(other.CharmIds, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(IsEnabled);
        hash.Add(Opacity);
        hash.Add(CharmSize);
        hash.Add(RopeLength);
        hash.Add(Anchor);
        hash.Add(OffsetX);
        hash.Add(OffsetY);
        hash.Add(RopeStyle);
        hash.Add(DisplayIndex);
        hash.Add(SoundEnabled);
        hash.Add(SoundVolume);
        foreach (string id in CharmIds)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>Clamps every field into the range the app supports.</summary>
    /// <remarks>
    /// Applied on read rather than trusted, because a settings file outlives the build
    /// that wrote it and a hand-edited one outlives good intentions.
    /// </remarks>
    public OverlaySettings Clamped() => this with
    {
        Opacity = Math.Clamp(Opacity, 0.2, 1.0),
        CharmSize = Math.Clamp(CharmSize, 0.5, 2.0),
        RopeLength = Math.Clamp(RopeLength, 0.5, 2.0),
        OffsetX = Math.Clamp(OffsetX, -4000, 4000),
        OffsetY = Math.Clamp(OffsetY, -2000, 2000),
        DisplayIndex = Math.Max(0, DisplayIndex),
        SoundVolume = Math.Clamp(SoundVolume, 0, 1),
        CharmIds = ClampedCharmIds(),
    };

    private IReadOnlyList<string> ClampedCharmIds()
    {
        List<string> ids = [.. CharmIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => CharmCatalog.Contains(id) ? id : CharmCatalog.DefaultId)
            .Take(CharmStack.MaximumCount)];

        // A rope with nothing on it is not a state the app offers, so an empty or
        // entirely unreadable list becomes the charm it opens with.
        return ids.Count > 0 ? ids : [CharmCatalog.DefaultId];
    }
}

/// <summary>What the app is allowed to say about itself, and to whom.</summary>
/// <remarks>
/// Its own section of the document rather than two loose fields, because
/// <c>PRIVACY.md</c> describes this as one decision the user makes and the identifier as
/// something that is discarded with it. Keeping them together is what makes
/// <see cref="Forgotten"/> a single obvious operation instead of two that could drift.
/// </remarks>
public sealed record PrivacySettings
{
    /// <summary>On by default, and switchable off. PRIVACY.md says so in those words.</summary>
    public bool AnalyticsEnabled { get; init; } = true;

    /// <summary>
    /// A random identifier made on this machine the first time anything is sent.
    /// </summary>
    /// <remarks>
    /// Null until then, which is the normal state of an install that has never sent
    /// anything, and null again the moment sharing is switched off. Not derived from
    /// hardware, account or network — a fresh <see cref="Guid"/> and nothing else.
    /// </remarks>
    public Guid? AnonymousId { get; init; }

    /// <summary>
    /// The same settings with sharing off and the identifier thrown away.
    /// </summary>
    /// <remarks>
    /// Discarding rather than keeping is the published promise: switching sharing back
    /// on mints a new identifier, "so the two cannot be joined".
    /// </remarks>
    public PrivacySettings Forgotten() => this with { AnalyticsEnabled = false, AnonymousId = null };
}

/// <summary>Counts the app keeps about itself.</summary>
/// <remarks>
/// Counted whether or not analytics is on, because the follow card is scheduled off the
/// same number and that has nothing to do with analytics.
/// </remarks>
public sealed record MilestoneSettings
{
    public int LaunchCount { get; init; }

    public bool IsFirstLaunch => LaunchCount <= 1;
}

/// <summary>The whole settings document.</summary>
public sealed record AppSettings
{
    /// <summary>
    /// Written on every save so a future migration can be deliberate rather than
    /// archaeological.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    public bool LaunchAtLogin { get; init; }

    public bool HasSeenWelcome { get; init; }

    public OverlaySettings Overlay { get; init; } = new();

    public PrivacySettings Privacy { get; init; } = new();

    public MilestoneSettings Milestones { get; init; } = new();

    public AppSettings Clamped() => this with
    {
        Overlay = Overlay.Clamped(),
        Milestones = Milestones with { LaunchCount = Math.Max(0, Milestones.LaunchCount) },
    };

    /// <summary>The reader and writer both sides of persistence use.</summary>
    /// <remarks>
    /// Enums are written as their names rather than their ordinals. A number would tie
    /// the file to the order the cases happen to be declared in, and inserting a rope
    /// style in the middle of that enum would silently re-point every existing user's
    /// cord at a different one.
    /// </remarks>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },

        // A document written by a newer build may name fields this one has never heard
        // of. Ignoring them is what lets a person move between versions without losing
        // everything they had set.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a document, falling back field by field rather than all at once.</summary>
    /// <remarks>
    /// Decoding is tolerant by design. Throwing on a missing key would mean that a new
    /// field in a future release discards every existing preference; throwing on a
    /// corrupt file would mean a bad write blocks launch. So an unreadable document
    /// yields the defaults, a partial one yields the defaults for what it omits, and an
    /// out-of-range one is clamped.
    /// </remarks>
    public static AppSettings FromJson(string json, out bool wasRecovered)
    {
        wasRecovered = false;

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        try
        {
            AppSettings? decoded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (decoded is null)
            {
                wasRecovered = true;
                return new AppSettings();
            }

            return decoded.Clamped();
        }
        catch (JsonException)
        {
            // Corrupt beyond a missing key. Logged by the caller and replaced with
            // defaults rather than blocking launch.
            wasRecovered = true;
            return new AppSettings();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
