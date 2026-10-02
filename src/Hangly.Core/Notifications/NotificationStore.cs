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

    /// <summary>The feed's ETag, so an unchanged feed costs a 304.</summary>
    public string? FeedETag { get; init; }
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
    public const int HistoryLimit = 1000;
    public const int RememberedLimit = 2000;

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
    public void Receive(IEnumerable<Announcement> announcements, bool includeTest, DateTimeOffset now, string? etag = null)
    {
        List<Announcement> usable = [.. announcements.Where(a =>
            a.IsUsable() && a.ExpireAt > now && (a.Audience == AnnouncementAudience.All || includeTest))];
        Mutate(current =>
        {
            HashSet<string> served = [.. usable.Select(a => a.Id)];
            // One the feed no longer serves was withdrawn in the dashboard — expired early or deleted — not missed.
            List<Announcement> waiting = [.. current.Waiting.Where(a => served.Contains(a.Id))];
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

            return next with { Waiting = waiting, ShownNotifications = shown, LastFetchAt = now, FeedETag = etag };
        });
    }

    /// <summary>The feed answered 304: nothing changed since the last answer.</summary>
    public void NoteUnchanged(DateTimeOffset now) => Mutate(current => current with { LastFetchAt = now });

    /// <summary>
    /// A waiting broadcast that reached its expiry without its card — the PC was asleep, the charm hidden — is not
    /// lost: it goes into the Center, unread, and is never shown as a card.
    /// </summary>
    public void Expire(DateTimeOffset now)
    {
        if (!State.Waiting.Any(a => a.ExpireAt <= now))
        {
            return;
        }

        Mutate(current =>
        {
            List<string> shown = [.. current.ShownNotifications];
            NotificationState next = current with { Waiting = [.. current.Waiting.Where(a => a.ExpireAt > now)] };
            foreach (Announcement missed in current.Waiting.Where(a => a.ExpireAt <= now))
            {
                Remember(shown, missed.Id);
                next = Record(next, ItemFor(missed, now));
            }

            return next with { ShownNotifications = shown };
        });
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
        if (State.History.Any(item => item.Id == id && item.ButtonTitle is not null))
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

    /// <summary>
    /// No update on offer any more. One this copy now runs becomes a record that it was installed — read, with no button —
    /// so the Center keeps the history; one withdrawn before it was installed goes.
    /// </summary>
    public void SettleUpdate(string current, DateTimeOffset now)
    {
        if (State.History.FirstOrDefault(item => item.Kind == NotificationKind.Update && item.ButtonTitle is not null) is not { } offered)
        {
            return;
        }

        Mutate(state =>
        {
            if (offered.Version is not { } version || VersionOrder.IsNewer(version, current))
            {
                return state with { History = [.. state.History.Where(item => item.Id != offered.Id)] };
            }

            NotificationItem installed = offered with
            {
                Title = $"Updated to Hangly {ShortVersion(version)}",
                Message = "You're on the newest version.",
                ButtonTitle = null,
                ReadAt = offered.ReadAt ?? now,
            };
            return state with { History = [.. state.History.Select(item => item.Id == offered.Id ? installed : item)] };
        });
    }

    public void UpdateReminderState(Func<UpdateReminderState, UpdateReminderState> change) => Mutate(current =>
    {
        UpdateReminderState next = change(current.Update);

        // The reminder holds no lists, so record equality is the whole comparison: unchanged is not written.
        return next == current.Update ? current : current with { Update = next };
    });

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
        if (current.History.All(item => item.IsRead))
        {
            return current;
        }

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
        if (current.History.Count == 0)
        {
            return current;
        }

        List<string> read = [.. current.ReadNotifications];
        foreach (NotificationItem item in current.History)
        {
            Remember(read, item.Id);
        }

        return current with { History = [], ReadNotifications = read };
    });

    // Storage

    /// <summary>Applies a change; each operation returns the same document when it changes nothing, and that is not written.</summary>
    /// <remarks>
    /// By reference, not by content: comparing the serialised document was the first way, and with a thousand entries
    /// it cost two serialisations of the whole history on every evaluation, most of which change nothing.
    /// </remarks>
    private void Mutate(Func<NotificationState, NotificationState> change)
    {
        bool changed;
        lock (gate)
        {
            NotificationState next = change(state);
            changed = !ReferenceEquals(next, state);
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
            string text = File.ReadAllText(path);
            try
            {
                return JsonSerializer.Deserialize<NotificationState>(text, Json);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                // Written by another version — a kind or an action this one does not know: read what can be read.
                return ReadLenient(text) ?? throw new JsonException("unreadable", exception);
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Unreadable is a fresh start, not a crash; the file is set aside rather than overwritten, to be looked at.
            try
            {
                File.Move(path, Path.Combine(Path.GetDirectoryName(path)!, $"notifications.corrupt-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json"));
            }
            catch (Exception moveFailure) when (moveFailure is IOException or UnauthorizedAccessException)
            {
                // Left where it is: the next save replaces it.
            }

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Field by field, entry by entry: one entry this version cannot read is skipped, not the document.</summary>
    private static NotificationState? ReadLenient(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            List<T> Each<T>(string name)
            {
                var list = new List<T>();
                if (root.TryGetProperty(name, out JsonElement array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in array.EnumerateArray())
                    {
                        try
                        {
                            if (element.Deserialize<T>(Json) is { } value)
                            {
                                list.Add(value);
                            }
                        }
                        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
                        {
                            // Skipped.
                        }
                    }
                }

                return list;
            }

            T? One<T>(string name)
                where T : class
            {
                try
                {
                    return root.TryGetProperty(name, out JsonElement value) ? value.Deserialize<T>(Json) : null;
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException)
                {
                    return null;
                }
            }

            DateTimeOffset? lastFetch = root.TryGetProperty("lastFetchAt", out JsonElement fetched) && fetched.TryGetDateTimeOffset(out DateTimeOffset at) ? at : null;
            return new NotificationState
            {
                History = Each<NotificationItem>("history"),
                ShownNotifications = Each<string>("shownNotifications"),
                ReadNotifications = Each<string>("readNotifications"),
                Waiting = Each<Announcement>("waiting"),
                Update = One<UpdateReminderState>("update") ?? new UpdateReminderState(),
                LastFetchAt = lastFetch,
                FeedETag = root.TryGetProperty("feedETag", out JsonElement etag) && etag.ValueKind == JsonValueKind.String ? etag.GetString() : null,
            };
        }
        catch (JsonException)
        {
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
