//
//  NotificationRules.cs
//  Hangly
//
//  When a card shows, which one, and for how long — kept apart so it can be tested.
//

using System.Text.RegularExpressions;

namespace Hangly.Core.Notifications;

/// <summary>The update reminder's own clock, remembered across launches.</summary>
public sealed record UpdateReminderState
{
    /// <summary>The version being reminded about.</summary>
    public string? Version { get; init; }

    /// <summary>While the card is up: when it fades.</summary>
    public DateTimeOffset? VisibleUntil { get; init; }

    /// <summary>While it sleeps: when it comes back.</summary>
    public DateTimeOffset? NextShowAt { get; init; }

    /// <summary>Skip This Version (no longer offered; kept so a skip made earlier still holds). A later version reminds again.</summary>
    public string? SkippedVersion { get; init; }

    /// <summary>
    /// Update in Background: accepted and installing — not reminded about again this run (cleared at launch, so an update
    /// that never happened is offered again).
    /// </summary>
    public string? AcceptedVersion { get; init; }
}

/// <summary>What the update card should be doing.</summary>
public enum ReminderKind
{
    /// <summary>Nothing to remind about.</summary>
    None,

    /// <summary>Should appear as soon as it can; <see cref="UpdateReminder.Begin"/> starts its ten minutes.</summary>
    Due,

    /// <summary>Up, until <see cref="ReminderDecision.Until"/>.</summary>
    Show,

    /// <summary>Asleep until <see cref="ReminderDecision.Until"/>.</summary>
    Sleep,
}

public readonly record struct ReminderDecision(ReminderKind Kind, DateTimeOffset? Until = null);

/// <summary>Shown for ten minutes, asleep for an hour, again — until updated, skipped, or no longer newer.</summary>
/// <remarks>macOS's <c>UpdateReminder</c>, rule for rule.</remarks>
public static class UpdateReminder
{
    public static readonly TimeSpan VisibleFor = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan SleepFor = TimeSpan.FromHours(1);

    /// <summary>Advances <paramref name="state"/> to <paramref name="now"/> and says what the card should be doing.</summary>
    public static ReminderDecision Decide(ref UpdateReminderState state, string? available, string current, DateTimeOffset now)
    {
        if (available is null || !VersionOrder.IsNewer(available, current))
        {
            state = state with { Version = null, VisibleUntil = null, NextShowAt = null };
            return new(ReminderKind.None);
        }

        if (state.SkippedVersion == available || state.AcceptedVersion == available)
        {
            return new(ReminderKind.None);
        }

        if (state.Version != available)
        {
            state = new UpdateReminderState
            {
                Version = available, NextShowAt = now, SkippedVersion = state.SkippedVersion, AcceptedVersion = state.AcceptedVersion,
            };
        }

        if (state.VisibleUntil is DateTimeOffset until)
        {
            if (now < until)
            {
                return new(ReminderKind.Show, until);
            }

            // Faded on its own; asleep for an hour from the moment it faded.
            state = state with { VisibleUntil = null, NextShowAt = until + SleepFor };
        }

        DateTimeOffset due = state.NextShowAt ?? now;
        return due <= now ? new(ReminderKind.Due) : new(ReminderKind.Sleep, due);
    }

    /// <summary>The card has appeared: its ten minutes start now, not when it fell due.</summary>
    public static UpdateReminderState Begin(UpdateReminderState state, DateTimeOffset now) => Begin(state, now, out _);

    /// <summary>
    /// As <see cref="Begin(UpdateReminderState, DateTimeOffset)"/>; <paramref name="began"/> is true when this began a
    /// new ten minutes, false when the card is coming back inside one already begun — not a new impression.
    /// </summary>
    public static UpdateReminderState Begin(UpdateReminderState state, DateTimeOffset now, out bool began)
    {
        began = state.VisibleUntil is null;
        return began ? state with { VisibleUntil = now + VisibleFor, NextShowAt = null } : state;
    }

    /// <summary>× or Later: asleep for an hour from now.</summary>
    public static UpdateReminderState Later(UpdateReminderState state, DateTimeOffset now) =>
        state with { VisibleUntil = null, NextShowAt = now + SleepFor };

    /// <summary>Skip This Version: never again for this one.</summary>
    public static UpdateReminderState Skip(UpdateReminderState state) =>
        state with { SkippedVersion = state.Version, VisibleUntil = null, NextShowAt = null };

    /// <summary>Update in Background: this version is installing by itself; the card is not shown for it again this run.</summary>
    public static UpdateReminderState Accept(UpdateReminderState state) =>
        state with { AcceptedVersion = state.Version, VisibleUntil = null, NextShowAt = null };
}

/// <summary>Dotted versions compared number by number: 2.10.0 is newer than 2.9.9.</summary>
public static class VersionOrder
{
    public static bool IsNewer(string candidate, string current)
    {
        int[] left = Parts(candidate), right = Parts(current);
        for (int index = 0; index < Math.Max(left.Length, right.Length); index++)
        {
            int a = index < left.Length ? left[index] : 0;
            int b = index < right.Length ? right[index] : 0;
            if (a != b)
            {
                return a > b;
            }
        }

        return false;
    }

    private static int[] Parts(string version) =>
        [.. version.Split('.').Select(part => int.TryParse(new string([.. part.TakeWhile(char.IsDigit)]), out int n) ? n : 0)];
}

/// <summary>What the card under the charm shows: an update (<see cref="Version"/>) or a broadcast.</summary>
public sealed record NotificationCard(string? Version, Announcement? Broadcast)
{
    public static NotificationCard Update(string version) => new(version, null);

    public static NotificationCard Of(Announcement announcement) => new(null, announcement);

    public string Id => Broadcast?.Id ?? $"update-{Version}";
}

public static class NotificationQueue
{
    /// <summary>
    /// The next card, if any: <c>High</c> broadcasts first, then the update reminder, then <c>Normal</c> broadcasts,
    /// then <c>Low</c>, oldest start first within each. A broadcast is a pop-up and nothing else — it is not kept in the
    /// Center — so every priority gets its card.
    /// </summary>
    public static NotificationCard? Next(IEnumerable<Announcement> waiting, string? updateDue, DateTimeOffset now)
    {
        List<Announcement> live = [.. waiting
            .Where(a => a.IsLive(now))
            .OrderBy(a => a.StartAt)
            .ThenBy(a => a.Id, StringComparer.Ordinal)];
        if (live.FirstOrDefault(a => a.Priority == AnnouncementPriority.High) is { } urgent)
        {
            return NotificationCard.Of(urgent);
        }

        if (updateDue is not null)
        {
            return NotificationCard.Update(updateDue);
        }

        Announcement? next = live.FirstOrDefault(a => a.Priority == AnnouncementPriority.Normal) ?? live.FirstOrDefault();
        return next is null ? null : NotificationCard.Of(next);
    }

    /// <summary>The soonest moment something waiting becomes live, for a one-shot timer.</summary>
    public static DateTimeOffset? NextStart(IEnumerable<Announcement> waiting, DateTimeOffset now)
    {
        DateTimeOffset[] later = [.. waiting.Select(a => a.StartAt).Where(start => start > now)];
        return later.Length > 0 ? later.Min() : null;
    }
}

/// <summary>
/// The card's bullet points, from release notes: the first three list items, as short as they can be made — the
/// bold lead of <c>- **Faster startup.** …</c> rather than the whole sentence. macOS's <c>ReleaseNoteHighlights</c>.
/// </summary>
public static partial class ReleaseNoteHighlights
{
    public const int Limit = 3;
    public const int Longest = 44;

    public static IReadOnlyList<string> Extract(string? notes)
    {
        if (string.IsNullOrEmpty(notes))
        {
            return [];
        }

        IEnumerable<string> items = notes.Contains("<li", StringComparison.Ordinal) ? HtmlItems(notes) : MarkdownItems(notes);
        return [.. items.Select(Shorten).OfType<string>().Take(Limit)];
    }

    private static IEnumerable<string> HtmlItems(string html)
    {
        foreach (string chunk in html.Split("<li").Skip(1))
        {
            int open = chunk.IndexOf('>', StringComparison.Ordinal);
            if (open < 0)
            {
                continue;
            }

            string body = chunk[(open + 1)..];
            int end = body.IndexOf("</li>", StringComparison.Ordinal);
            yield return end >= 0 ? body[..end] : body;
        }
    }

    private static IEnumerable<string> MarkdownItems(string markdown) =>
        markdown.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            .Select(line => line[2..]);

    private static string? Shorten(string item)
    {
        string text = Lead(item) ?? item;
        text = Tags().Replace(text, string.Empty)
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&#39;", "'", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal);
        text = Spaces().Replace(text, " ").Trim();
        int stop = text.IndexOf(". ", StringComparison.Ordinal);
        if (stop >= 0)
        {
            text = text[..stop];
        }

        text = text.TrimEnd('.');
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= Longest ? text : text[..(Longest - 1)].TrimEnd() + "…";
    }

    private static string? Lead(string item)
    {
        string trimmed = item.Trim();
        foreach ((string open, string close) in new[] { ("<strong>", "</strong>"), ("<b>", "</b>"), ("**", "**") })
        {
            if (!trimmed.StartsWith(open, StringComparison.Ordinal))
            {
                continue;
            }

            string rest = trimmed[open.Length..];
            int end = rest.IndexOf(close, StringComparison.Ordinal);
            if (end >= 0)
            {
                return rest[..end];
            }
        }

        return null;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
