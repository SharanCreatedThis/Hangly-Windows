//
//  Announcement.cs
//  Hangly
//
//  A broadcast from the dashboard, as the announcements endpoint sends it.
//

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Hangly.Core.Notifications;

/// <summary>What an announcement's button does.</summary>
public enum AnnouncementAction
{
    None,
    OpenLibrary,
    OpenCharm,
    OpenCreate,
    OpenUrl,
}

/// <summary><c>Low</c> never gets a card — straight to the Notification Center. <c>High</c> goes before anything else waiting.</summary>
public enum AnnouncementPriority
{
    Low,
    Normal,
    High,
}

/// <summary><c>Test</c> is shown only on installs marked as testers, to check a broadcast before everyone sees it.</summary>
public enum AnnouncementAudience
{
    All,
    Test,
}

/// <summary>One broadcast written in the admin dashboard.</summary>
/// <remarks>
/// The same document passes three checks before it is shown: Firestore's rules when it is written, the endpoint when
/// it is served (<c>functions/src/announcements.ts</c>), and <see cref="IsUsable"/> here — the limits are stated in
/// all three on purpose, so one written past one of them, from the Firebase console say, still never reaches a card it
/// would not fit. macOS's <c>Announcement</c>, rule for rule.
/// </remarks>
public sealed partial record Announcement
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public AnnouncementAction ActionType { get; init; }

    public string ActionTarget { get; init; } = string.Empty;

    public string? ActionLabel { get; init; }

    public AnnouncementPriority Priority { get; init; } = AnnouncementPriority.Normal;

    public IReadOnlyList<string> Platforms { get; init; } = [];

    public AnnouncementAudience Audience { get; init; } = AnnouncementAudience.All;

    public int DurationSeconds { get; init; } = 30;

    public DateTimeOffset StartAt { get; init; }

    public DateTimeOffset ExpireAt { get; init; }

    /// <summary>This build's name in <see cref="Platforms"/>.</summary>
    public const string Platform = "windows";

    /// <summary>The button's words, or null for one that is only to be read.</summary>
    [JsonIgnore]
    public string? ButtonTitle => ActionType switch
    {
        AnnouncementAction.None => null,
        _ when !string.IsNullOrEmpty(ActionLabel) => ActionLabel,
        AnnouncementAction.OpenLibrary => "Open Library",
        AnnouncementAction.OpenCharm => "Show Charm",
        AnnouncementAction.OpenCreate => "Open Create",
        _ => "Open",
    };

    public bool IsLive(DateTimeOffset now) => StartAt <= now && now < ExpireAt;

    /// <summary>Whether this can be shown here: the endpoint's checks, restated.</summary>
    /// <remarks>Lengths in UTF-16 units, as Firestore's rules and the endpoint count them (measured): 🔔 is two.</remarks>
    public bool IsUsable(string platform = Platform) =>
        Title.Length is > 0 and <= AnnouncementLimits.Title
        && Message.Length is > 0 and <= AnnouncementLimits.Message
        && ActionTarget.Length <= AnnouncementLimits.ActionTarget
        && (ActionLabel?.Length ?? 0) <= AnnouncementLimits.ActionLabel
        && DurationSeconds is >= AnnouncementLimits.MinimumDuration and <= AnnouncementLimits.MaximumDuration
        && ExpireAt > StartAt
        && Platforms.Contains(platform)
        && IdPattern().IsMatch(Id)
        && TargetFits(ActionType, ActionTarget);

    /// <summary>An https link and nothing else may be opened; a collection or charm must look like one.</summary>
    public static bool TargetFits(AnnouncementAction type, string target) => type switch
    {
        AnnouncementAction.None or AnnouncementAction.OpenCreate => target.Length == 0,
        AnnouncementAction.OpenLibrary => target.Length == 0 || CollectionPattern().IsMatch(target),
        AnnouncementAction.OpenCharm => CharmPattern().IsMatch(target),
        AnnouncementAction.OpenUrl => Uri.TryCreate(target, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0,
        _ => false,
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CollectionPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CharmPattern();
}

/// <summary>The same limits as <c>functions/src/announcements.ts</c> and <c>firestore.rules</c>.</summary>
public static class AnnouncementLimits
{
    public const int Title = 60;
    public const int Message = 200;
    public const int ActionTarget = 300;
    public const int ActionLabel = 24;
    public const int MinimumDuration = 5;
    public const int MaximumDuration = 300;
}

/// <summary>Reads what <c>GET /announcements</c> returns. One unreadable entry is dropped; the rest stand.</summary>
public static class AnnouncementFeedParser
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>The announcements in <paramref name="json"/>; empty for anything unreadable.</summary>
    public static IReadOnlyList<Announcement> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("announcements", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var parsed = new List<Announcement>();
            foreach (JsonElement element in list.EnumerateArray())
            {
                try
                {
                    if (element.Deserialize<Announcement>(Options) is { } announcement)
                    {
                        parsed.Add(announcement);
                    }
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
                {
                    // One bad entry is dropped; the rest of the feed stands.
                }
            }

            return parsed;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
