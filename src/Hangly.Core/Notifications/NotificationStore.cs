//
//  NotificationStore.cs
//  Hangly
//
//  What this installation has received, shown and read.
//

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hangly.Core.Notifications;

public enum NotificationKind
{
    Update,
    Broadcast,
}

/// <summary>One entry in the Notification Center.</summary>
public sealed record NotificationItem
{
    /// <summary>The announcement's ID, or <c>update-&lt;version&gt;</c>.</summary>
    public required string Id { get; init; }

    public NotificationKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>An update's bullet points.</summary>
    public IReadOnlyList<string> Highlights { get; init; } = [];

    public AnnouncementAction ActionType { get; init; }

    public string ActionTarget { get; init; } = string.Empty;

    public string? ButtonTitle { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }

    public DateTimeOffset? ExpireAt { get; init; }

    public DateTimeOffset? ReadAt { get; init; }

    /// <summary>For an update: the version it offers.</summary>
    public string? Version { get; init; }

    [JsonIgnore]
    public bool IsRead => ReadAt is not null;
}

/// <summary>Everything the notifications remember, in one document.</summary>
public sealed record NotificationState
{
    /// <summary>Newest first, at most <see cref="NotificationStore.HistoryLimit"/>.</summary>
    public IReadOnlyList<NotificationItem> History { get; init; } = [];

    /// <summary>Every broadcast that has had its card, or went straight to the Center. Never a card again.</summary>
    public IReadOnlyList<string> ShownNotifications { get; init; } = [];

    /// <summary>Read, by ID — kept past the history for the same reason.</summary>
    public IReadOnlyList<string> ReadNotifications { get; init; } = [];

    /// <summary>Received but not yet shown: waiting for their start, for the charm, or for the card before them.</summary>
    public IReadOnlyList<Announcement> Waiting { get; init; } = [];

    public UpdateReminderState Update { get; init; } = new();

    public DateTimeOffset? LastFetchAt { get; init; }
}

/// <summary><c>notifications.json</c> in <c>%AppData%\Hangly</c>, next to <c>installation.json</c>.</summary>
/// <remarks>
/// Local on purpose: read state per installation in Firestore would be a write for every view of every broadcast;
/// on disk it costs nothing and "never twice" holds just the same. <c>%AppData%</c> rather than <c>%LocalAppData%</c>
/// because Velopack's installs and uninstalls touch only the latter. Written atomically. Used from the XAML thread;
/// the lock is for the feed's thread handing in what it fetched. macOS's <c>NotificationStore</c>, rule for rule.
/// </remarks>
public sealed class NotificationStore
{
    public const int HistoryLimit = 100;
    public const int RememberedLimit = 500;

    private static readonly JsonSerializerOptions Json = new(AnnouncementFeedParser.Options)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object gate = new();
    private readonly string? filePath;
    private NotificationState state;

    public NotificationStore(string? directory = null, bool persist = true)
    {
        filePath = persist ? Path.Combine(directory ?? Registry.InstallationStore.DefaultDirectory, "notifications.json") : null;
        state = Read(filePath) ?? new NotificationState();
    }

    /// <summary>Raised after any change, on the thread that made it.</summary>
    public event Action? Changed;

    public NotificationState State
    {
        get
        {
            lock (gate)
            {
                return state;
            }
        }
    }

    public int UnreadCount => State.History.Count(item => !item.IsRead);

    // Receiving

    /// <summary>
    /// Takes in what the feed sent: new broadcasts wait for their card; <c>Low</c> ones go straight to the Center.
    /// Anything already shown, cleared or expired is ignored, and a waiting one the dashboard withdrew is dropped.
    /// </summary>
    public void Receive(IEnumerable<Announcement> announcements, bool includeTest, DateTimeOffset now)
    {
        List<Announcement> usable = [.. announcements.Where(a =>
            a.IsUsable() && a.ExpireAt > now && (a.Audience == AnnouncementAudience.All || includeTest))];
        Mutate(current =>
        {
            HashSet<string> served = [.. usable.Select(a => a.Id)];
            List<Announcement> waiting = [.. current.Waiting.Where(a => served.Contains(a.Id) && a.ExpireAt > now)];
            List<string> shown = [.. current.ShownNotifications];
            NotificationState next = current;
            foreach (Announcement announcement in usable)
            {
                if (shown.Contains(announcement.Id))
                {
                    continue;
                }

                int index = waiting.FindIndex(a => a.Id == announcement.Id);
                if (index >= 0)
                {
                    // Edited in the dashboard since: the newest words win.
                    waiting[index] = announcement;
                    continue;
                }

                if (announcement.Priority == AnnouncementPriority.Low)
                {
                    Remember(shown, announcement.Id);
                    next = Record(next, ItemFor(announcement, now));
                }
                else
                {
                    waiting.Add(announcement);
                }
            }

            return next with { Waiting = waiting, ShownNotifications = shown, LastFetchAt = now };
        });
    }

    /// <summary>Drops waiting broadcasts that have expired.</summary>
    public void Expire(DateTimeOffset now)
    {
        if (State.Waiting.Any(a => a.ExpireAt <= now))
        {
            Mutate(current => current with { Waiting = [.. current.Waiting.Where(a => a.ExpireAt > now)] });
        }
    }

    // Showing

    /// <summary>A broadcast's card has appeared: never again as a card, and in the Center as unread.</summary>
    public void MarkShown(Announcement announcement, DateTimeOffset now) => Mutate(current =>
    {
        List<string> shown = [.. current.ShownNotifications];
        Remember(shown, announcement.Id);
        return Record(
            current with { Waiting = [.. current.Waiting.Where(a => a.Id != announcement.Id)], ShownNotifications = shown },
            ItemFor(announcement, now));
    });

    /// <summary>The update reminder, as one Center entry per version.</summary>
    public void RecordUpdate(string version, IReadOnlyList<string> highlights, DateTimeOffset now)
    {
        string id = $"update-{version}";
        if (State.History.Any(item => item.Id == id))
        {
            return;
        }

        Mutate(current => Record(
            current with { History = [.. current.History.Where(item => item.Kind != NotificationKind.Update)] },
            new NotificationItem
            {
                Id = id,
                Kind = NotificationKind.Update,
                Title = $"Hangly {ShortVersion(version)} Available",
                Message = "A new version of Hangly is ready.",
                Highlights = highlights,
                ButtonTitle = "Update Now",
                ReceivedAt = now,
                Version = version,
            }));
    }

    /// <summary>The update entry goes once that version is installed or no longer offered.</summary>
    public void RemoveUpdate()
    {
        if (State.History.Any(item => item.Kind == NotificationKind.Update))
        {
            Mutate(current => current with { History = [.. current.History.Where(item => item.Kind != NotificationKind.Update)] });
        }
    }

    public void UpdateReminderState(Func<UpdateReminderState, UpdateReminderState> change) =>
        Mutate(current => current with { Update = change(current.Update) });

    // Reading

    public void MarkRead(string id, DateTimeOffset now) => Mutate(current =>
    {
        if (current.History.FirstOrDefault(item => item.Id == id) is not { IsRead: false })
        {
            return current;
        }

        List<string> read = [.. current.ReadNotifications];
        Remember(read, id);
        return current with
        {
            History = [.. current.History.Select(item => item.Id == id ? item with { ReadAt = now } : item)],
            ReadNotifications = read,
        };
    });

    public void MarkAllRead(DateTimeOffset now) => Mutate(current =>
    {
        List<string> read = [.. current.ReadNotifications];
        foreach (NotificationItem item in current.History.Where(item => !item.IsRead))
        {
            Remember(read, item.Id);
        }

        return current with
        {
            History = [.. current.History.Select(item => item.IsRead ? item : item with { ReadAt = now })],
            ReadNotifications = read,
        };
    });

    /// <summary>Dismiss all: the Center is emptied. Nothing cleared comes back as a card.</summary>
    public void ClearAll() => Mutate(current =>
    {
        List<string> read = [.. current.ReadNotifications];
        foreach (NotificationItem item in current.History)
        {
            Remember(read, item.Id);
        }

        return current with { History = [], ReadNotifications = read };
    });

    // Storage

    private void Mutate(Func<NotificationState, NotificationState> change)
    {
        bool changed;
        lock (gate)
        {
            NotificationState next = change(state);
            changed = !Same(next, state);
            if (changed)
            {
                state = next;
                Save(next);
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Records hold lists, which compare by reference; the document is what matters.</summary>
    private static bool Same(NotificationState a, NotificationState b) =>
        ReferenceEquals(a, b) || JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);

    private void Save(NotificationState document)
    {
        if (filePath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            string temporary = filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, Json));
            File.Move(temporary, filePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not saved this time; the next change tries again. At worst a broadcast shows twice.
        }
    }

    private static NotificationState? Read(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NotificationState>(File.ReadAllText(path), Json);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Unreadable is a fresh start, not a crash.
            return null;
        }
    }

    private static void Remember(List<string> list, string id)
    {
        if (list.Contains(id))
        {
            return;
        }

        list.Add(id);
        if (list.Count > RememberedLimit)
        {
            list.RemoveRange(0, list.Count - RememberedLimit);
        }
    }

    private static NotificationState Record(NotificationState current, NotificationItem item)
    {
        if (current.ReadNotifications.Contains(item.Id))
        {
            item = item with { ReadAt = item.ReadAt ?? item.ReceivedAt };
        }

        List<NotificationItem> history = [item, .. current.History.Where(existing => existing.Id != item.Id)];
        return current with { History = [.. history.Take(HistoryLimit)] };
    }

    public static NotificationItem ItemFor(Announcement announcement, DateTimeOffset now) => new()
    {
        Id = announcement.Id,
        Kind = NotificationKind.Broadcast,
        Title = announcement.Title,
        Message = announcement.Message,
        ActionType = announcement.ActionType,
        ActionTarget = announcement.ActionTarget,
        ButtonTitle = announcement.ButtonTitle,
        ReceivedAt = now,
        ExpireAt = announcement.ExpireAt,
    };

    /// <summary>"2.3" for 2.3.0, as a headline says it; "2.3.1" stays whole.</summary>
    public static string ShortVersion(string version) =>
        version.EndsWith(".0", StringComparison.Ordinal) && version.Split('.').Length == 3 ? version[..^2] : version;
}
