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
    /// <summary>145% on a new install: see <see cref="CharmCatalog.FirstRunId"/>.</summary>
    public double CharmSize { get; init; } = 1.45;

    /// <summary>How far the charm hangs, as a multiple of the shipped rope.</summary>
    /// <summary>85% on a new install, so the charm sits nearer the top edge.</summary>
    public double RopeLength { get; init; } = 0.85;

    /// <summary>
    /// Top right on a new install, out of the way of what is usually in the middle.
    /// </summary>
    public OverlayAnchor Anchor { get; init; } = OverlayAnchor.TopTrailing;

    /// <summary>User nudge from the anchor, in points.</summary>
    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public RopeStyle RopeStyle { get; init; } = RopeStyleTable.FirstRun;

    /// <summary>Index of the display to hang on, in the order the system reports them.</summary>
    public int DisplayIndex { get; init; }

    /// <summary>The charms on the cord, from the anchor down.</summary>
    /// <remarks>
    /// Ids from <c>CharmCatalog</c>, which are the macOS <c>CharmKind</c> raw values, so
    /// a settings file means the same thing on both platforms. One to
    /// <see cref="CharmStack.MaximumCount"/> of them; an unknown id is replaced rather
    /// than dropped, because a file written by a newer build should cost the user a
    /// different charm and not an empty rope.
    /// </remarks>
    public IReadOnlyList<string> CharmIds { get; init; } = [CharmCatalog.FirstRunId];

    /// <summary>Every place on the rope, in use or not, from the anchor down.</summary>
    /// <remarks>
    /// <b>Why the places are stored and not just the charms.</b> macOS keeps three places
    /// always, with the ones in use at the end, so that turning the count down and back up
    /// returns the same rope rather than copies of whatever survived. It also carries each
    /// place's own size, which is a trim relative to <see cref="CharmSize"/> and belongs to
    /// the place rather than to the charm in it — see <see cref="RopeCharm"/>.
    ///
    /// <para>Empty in a document written before places existed, which is every document
    /// this build has written until now. <see cref="Stack"/> falls back to
    /// <see cref="CharmIds"/> in that case, so an older settings file opens with the same
    /// rope it had and gains the two hidden places on the next write.</para>
    /// </remarks>
    public IReadOnlyList<RopeCharm> Slots { get; init; } = [];

    /// <summary>How many places hang. Zero means "as many as <see cref="CharmIds"/> names".</summary>
    public int CharmCount { get; init; }

    /// <summary>The rope as the solver and the Library both see it.</summary>
    public CharmStackState Stack =>
        Slots.Count > 0
            ? CharmStackState.Restore(Slots, CharmCount > 0 ? CharmCount : Slots.Count)
            : CharmStackState.Of(CharmIds);

    /// <summary>This overlay carrying a different rope, with both shapes kept in step.</summary>
    /// <remarks>
    /// <see cref="CharmIds"/> is still written, and deliberately: it is what every other
    /// part of this build reads, it is what an older build would read if someone moved a
    /// settings file backwards, and it is the one thing in the document a person editing
    /// it by hand is likely to understand.
    /// </remarks>
    public OverlaySettings WithStack(CharmStackState stack) => this with
    {
        Slots = stack.StoredSlots,
        CharmCount = stack.Count,
        CharmIds = stack.Ids,
    };

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
        && CharmCount == other.CharmCount
        && CharmIds.SequenceEqual(other.CharmIds, StringComparer.Ordinal)
        && Slots.SequenceEqual(other.Slots);

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
        foreach (string id in CharmIds)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        hash.Add(CharmCount);
        foreach (RopeCharm place in Slots)
        {
            hash.Add(place);
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
        CharmIds = ClampedCharmIds(),
        Slots = [.. Slots.Take(CharmStack.MaximumCount).Select(place => place.Clamped())],
        CharmCount = Slots.Count > 0 ? Math.Clamp(CharmCount, 1, CharmStack.MaximumCount) : 0,
    };

    private IReadOnlyList<string> ClampedCharmIds()
    {
        // Well-formed rather than present. An imported charm is not in the catalogue and
        // never will be, so checking the catalogue alone would have thrown the user's own
        // charm off the rope the next time the document was read. Whether the import
        // still exists is the app's question, not this one's — CharmLibrary falls back to
        // the bead when a drawing has gone.
        List<string> ids = [.. CharmIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => CharmId.IsWellFormed(id) ? id : CharmCatalog.DefaultId)
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

    /// <summary>Distinct charms that have been on the cord, ever.</summary>
    /// <remarks>
    /// A count rather than the set, because the About page shows a number and keeping
    /// the identifiers would mean a settings document that grows with curiosity.
    /// </remarks>
    public int CharmsHung { get; init; }

    /// <summary>How many secrets the About page has given up.</summary>
    public int SecretsFound { get; init; }

    /// <summary>Times the rope has swung through vertical.</summary>
    public long SwingsSurvived { get; init; }

    public bool IsFirstLaunch => LaunchCount <= 1;
}

/// <summary>What the Library remembers between visits.</summary>
/// <remarks>
/// Separate from <see cref="OverlaySettings"/> because none of it changes what hangs on
/// the rope: starring a charm and looking at one are things the user does <i>to the
/// Library</i>, and a write here must never be mistaken for a change to the overlay.
/// </remarks>
public sealed record LibrarySettings
{
    /// <summary>Charms the user has starred, in the order they starred them.</summary>
    public IReadOnlyList<string> FavouriteCharmIds { get; init; } = [];

    /// <summary>Charms recently hung, newest first.</summary>
    public IReadOnlyList<string> RecentCharmIds { get; init; } = [];

    /// <summary>How many recents are kept. Enough to be useful, few enough to scan.</summary>
    public const int RecentLimit = 12;

    /// <summary>The same settings with this charm moved to the front of the recents.</summary>
    public LibrarySettings WithRecent(string charmId)
    {
        List<string> recent = [charmId, .. RecentCharmIds.Where(id => id != charmId)];
        if (recent.Count > RecentLimit)
        {
            recent.RemoveRange(RecentLimit, recent.Count - RecentLimit);
        }

        return this with { RecentCharmIds = recent };
    }

    /// <summary>The same settings with this charm starred, or unstarred if it already was.</summary>
    public LibrarySettings WithFavouriteToggled(string charmId) =>
        FavouriteCharmIds.Contains(charmId)
            ? this with { FavouriteCharmIds = [.. FavouriteCharmIds.Where(id => id != charmId)] }
            : this with { FavouriteCharmIds = [.. FavouriteCharmIds, charmId] };

    /// <summary>Compared by value, because two lists of the same ids are the same set.</summary>
    /// <remarks>
    /// The generated equality compares a list by reference, which would make the store
    /// think the Library had changed on every read and rewrite the file each time.
    /// </remarks>
    public bool Equals(LibrarySettings? other) =>
        other is not null
        && FavouriteCharmIds.SequenceEqual(other.FavouriteCharmIds, StringComparer.Ordinal)
        && RecentCharmIds.SequenceEqual(other.RecentCharmIds, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (string id in FavouriteCharmIds)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        foreach (string id in RecentCharmIds)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>Drops ids nothing could ever resolve, so a stale file cannot poison the grid.</summary>
    /// <remarks>
    /// Imported charms pass because their id has a recognisable shape. Dropping them here
    /// would have quietly un-starred every charm somebody made, on the first read after
    /// they made it.
    /// </remarks>
    public LibrarySettings Clamped() => this with
    {
        FavouriteCharmIds = [.. FavouriteCharmIds.Where(Models.CharmId.IsWellFormed).Distinct(StringComparer.Ordinal)],
        RecentCharmIds = [.. RecentCharmIds.Where(Models.CharmId.IsWellFormed).Distinct(StringComparer.Ordinal).Take(RecentLimit)],
    };
}

/// <summary>The whole settings document.</summary>
public sealed record AppSettings
{
    /// <summary>
    /// Written on every save so a future migration can be deliberate rather than
    /// archaeological.
    /// </summary>
    /// <summary>The longest a name is kept, in characters.</summary>
    /// <remarks>
    /// Generous for a name and far short of anything that could carry a payload. A field
    /// whose contents are sent with every analytics event should have a bound that is
    /// stated rather than implied.
    /// </remarks>
    public const int DisplayNameLimit = 40;

    public int SchemaVersion { get; init; } = 1;

    public bool LaunchAtLogin { get; init; }

    public bool HasSeenWelcome { get; init; }

    /// <summary>
    /// The display name the person gave during onboarding.
    /// </summary>
    /// <remarks>
    /// <b>User-provided, and the only name this app ever holds.</b> It is typed into the
    /// welcome window; it is not read from the Windows account, the OS user name, or any
    /// other part of the machine, and there is no code here that could. PRIVACY.md says so
    /// in those terms, because the distinction between a name someone chose to give and a
    /// name taken from their computer is the whole of the difference.
    ///
    /// <para>Empty until onboarding finishes, and onboarding does not finish without it.
    /// Changeable afterwards on the Appearance page, because a name someone is asked for
    /// once and can never correct is a name they will resent.</para>
    /// </remarks>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Whether the follow card has been shown at all.</summary>
    public bool HasSeenFollowPrompt { get; init; }

    /// <summary>Which launch the follow card was last shown at.</summary>
    /// <remarks>
    /// macOS calls this <c>followPromptShownAtLaunch</c>, and it is what turns "maybe
    /// later" into a real answer rather than a synonym for "no": the card comes back a
    /// set number of launches after the one it was last shown at, and not before.
    /// </remarks>
    public int FollowPromptShownAtLaunch { get; init; }

    /// <summary>Whether the person asked not to be shown it again.</summary>
    /// <remarks>
    /// Separate from <see cref="HasSeenFollowPrompt"/> on purpose, which is how macOS
    /// models it too: "seen once" and "do not ask again" are different answers, and a
    /// card that treats them the same either nags or never returns.
    /// </remarks>
    public bool IsFollowPromptSilenced { get; init; }

    public OverlaySettings Overlay { get; init; } = new();

    public PrivacySettings Privacy { get; init; } = new();

    public MilestoneSettings Milestones { get; init; } = new();

    public LibrarySettings Library { get; init; } = new();

    public AppSettings Clamped() => this with
    {
        Overlay = Overlay.Clamped(),
        DisplayName = DisplayName.Trim() is { Length: > 0 } trimmed
            ? trimmed[..Math.Min(trimmed.Length, DisplayNameLimit)]
            : string.Empty,
        Milestones = Milestones with
        {
            LaunchCount = Math.Max(0, Milestones.LaunchCount),
            CharmsHung = Math.Max(0, Milestones.CharmsHung),
            SecretsFound = Math.Max(0, Milestones.SecretsFound),
            SwingsSurvived = Math.Max(0, Milestones.SwingsSurvived),
        },
        Library = Library.Clamped(),
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
