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
    /// <summary>180% on a new install: see <see cref="CharmCatalog.FirstRunId"/>.</summary>
    public double CharmSize { get; init; } = 1.8;

    /// <summary>How far the charm hangs, as a multiple of the shipped rope.</summary>
    /// <summary>The shipped length on a new install, which is what macOS hangs.</summary>
    public double RopeLength { get; init; } = 1.0;

    /// <summary>
    /// Top right on a new install, out of the way of what is usually in the middle.
    /// </summary>
    public OverlayAnchor Anchor { get; init; } = OverlayAnchor.TopTrailing;

    /// <summary>
    /// Where along the top edge the rope hangs: 0 is hard left, 1 is hard right.
    /// </summary>
    /// <remarks>
    /// <b>This replaces the three corners, and does not delete them.</b> A person choosing
    /// between Top Left, Top Center and Top Right was choosing between three of the
    /// thousands of places a charm can hang, and the two they did not pick were rarely the
    /// one they wanted.
    ///
    /// <para><b>Null means "not chosen yet", which is how an older settings file is
    /// recognised.</b> <see cref="Position"/> falls back to whatever
    /// <see cref="Anchor"/> said, so a document written before this existed opens with the
    /// charm exactly where it was and gains a number on the next write. The anchor is
    /// still written, so a file moved back to an older build still works.</para>
    /// </remarks>
    public double? HorizontalPosition { get; init; }

    /// <summary>Where the rope hangs, as a fraction, whichever way it was recorded.</summary>
    /// <remarks>
    /// <b>The corners are inset, and they have to be.</b> The fraction places the rope,
    /// not the window, so 1 puts the cord against the very edge of the display with half
    /// the charm hanging off it. That is what "hard right" has to mean for a thing drawn
    /// in the middle of its own window — but it is not what anybody meant by choosing
    /// "Top Right" in a build that only offered three corners, and it is what they got on
    /// the first launch after updating. Reported from a laptop that came back from the
    /// update with its charm halfway off the screen.
    ///
    /// <para>Both ends are inset by the same amount. Only the right was reported, because
    /// that is the corner the default used; the left had the identical defect waiting for
    /// anybody who had chosen it.</para>
    ///
    /// <para>This is a migration, so it only decides where a charm goes when nobody has
    /// ever moved the slider. A stored position still wins, and dragging to either end
    /// still reaches 0 and 1.</para>
    /// </remarks>
    public double Position => HorizontalPosition ?? Anchor switch
    {
        OverlayAnchor.TopLeading => LeadingInset,
        OverlayAnchor.TopTrailing => TrailingInset,
        _ => 0.5,
    };

    /// <summary>Where an old Top Left now hangs.</summary>
    /// <remarks>
    /// Stated rather than derived from <see cref="TrailingInset"/>. <c>1 - 0.85</c> is
    /// 0.15000000000000002 in binary floating point, and a position that fails to equal
    /// the number it is documented as is a position somebody will chase later.
    /// </remarks>
    private const double LeadingInset = 0.15;

    /// <summary>Where an old Top Right now hangs.</summary>
    private const double TrailingInset = 0.85;

    /// <summary>User nudge from the anchor, in points.</summary>
    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public RopeStyle RopeStyle { get; init; } = RopeStyleTable.FirstRun;

    /// <summary>What builds before 1.0 stored: a position in the display list, main first.</summary>
    /// <remarks>
    /// Read, and honoured while <see cref="DisplayId"/> is null, so nobody's rope moves on
    /// update; never written again once a display has been chosen by id. See
    /// <see cref="Geometry.DisplayChoice"/>.
    /// </remarks>
    public int DisplayIndex { get; init; }

    /// <summary>The display chosen to hang on, by its stable id; null for the main display.</summary>
    /// <remarks>
    /// Kept while that display is unplugged — the rope falls back to the main display and
    /// returns by itself when it is plugged in again.
    /// </remarks>
    public string? DisplayId { get; init; }

    /// <summary>What the chosen display was called, for a menu to name it while it is away.</summary>
    public string? DisplayName { get; init; }

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
        && HorizontalPosition.Equals(other.HorizontalPosition)
        && OffsetX.Equals(other.OffsetX)
        && OffsetY.Equals(other.OffsetY)
        && RopeStyle == other.RopeStyle
        && DisplayIndex == other.DisplayIndex
        && string.Equals(DisplayId, other.DisplayId, StringComparison.Ordinal)
        && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
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
        hash.Add(HorizontalPosition);
        hash.Add(OffsetX);
        hash.Add(OffsetY);
        hash.Add(RopeStyle);
        hash.Add(DisplayIndex);
        hash.Add(DisplayId, StringComparer.Ordinal);
        hash.Add(DisplayName, StringComparer.Ordinal);
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
        DisplayId = string.IsNullOrWhiteSpace(DisplayId) ? null : DisplayId,
        DisplayName = DisplayId is null ? null : DisplayName,
        HorizontalPosition = HorizontalPosition is double at ? Math.Clamp(at, 0, 1) : null,
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

    /// <summary>The name the project was last told, or null if it has never accepted one.</summary>
    /// <remarks>
    /// Recorded only when an identify is accepted, so a rename made with no network is
    /// sent on a later launch instead of being lost.
    /// </remarks>
    public string? IdentifiedName { get; init; }

    /// <summary>The major version the project was last told about, or null if never.</summary>
    public int? IdentifiedMajorVersion { get; init; }

    /// <summary>Whether the current identifier was minted by a first launch that has not been accepted yet.</summary>
    /// <remarks>
    /// What tells a first launch that failed to send apart from a person who has been
    /// identified under the old event-based analytics and is meeting this build for the
    /// first time. Both have an identifier and no <see cref="IdentifiedMajorVersion"/>;
    /// only the first is a first launch.
    /// </remarks>
    public bool FirstIdentifyPending { get; init; }

    /// <summary>The last local calendar day a <c>daily_active</c> was accepted, as yyyy-MM-dd.</summary>
    /// <remarks>
    /// Written only on acceptance, so a day with no network is retried that day rather than
    /// counted. A day that passes entirely offline is not backfilled: it was not a day the
    /// project saw, and saying it was would be inventing a number.
    /// </remarks>
    public string? LastActiveDay { get; init; }

    /// <summary>
    /// The same settings with sharing off and the identifier thrown away.
    /// </summary>
    /// <remarks>
    /// Discarding rather than keeping is the published promise: switching sharing back
    /// on mints a new identifier, "so the two cannot be joined". What was sent under the
    /// old identifier goes with it, so the new one starts as a first launch.
    /// </remarks>
    public PrivacySettings Forgotten() => this with
    {
        AnalyticsEnabled = false,
        AnonymousId = null,
        IdentifiedName = null,
        IdentifiedMajorVersion = null,
        FirstIdentifyPending = false,
        LastActiveDay = null,
    };
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

    /// <summary>What a machine with no settings file starts with.</summary>
    /// <remarks>
    /// <b>Not the same thing as the property defaults, and deliberately.</b> Every
    /// <c>init</c> value on this record is doing two jobs: it is the value a new install
    /// gets, and it is the value a key missing from an existing file falls back to. For
    /// almost everything those want the same answer.
    ///
    /// <para><see cref="OverlaySettings.HorizontalPosition"/> is the exception. Null there
    /// means "nobody has chosen a position yet", which is what lets
    /// <see cref="OverlaySettings.Position"/> fall back to the old three-corner
    /// <see cref="OverlaySettings.Anchor"/> and open a pre-position settings file with the
    /// charm exactly where it was. Giving the property itself a number would move every
    /// one of those installs. So the new-install position is stated here, where only a
    /// machine with no file at all can see it.</para>
    /// </remarks>
    public static AppSettings Defaults { get; } = new()
    {
        Overlay = new OverlaySettings { HorizontalPosition = 0.85 },
    };

    public int SchemaVersion { get; init; } = 1;

    /// <summary>On by default: a desktop ornament that is not there is not an ornament.</summary>
    public bool LaunchAtLogin { get; init; } = true;

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
