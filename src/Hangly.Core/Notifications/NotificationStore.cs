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

/// <summary>Everything the notifications remember, in one document.</summary>
public sealed record NotificationState
{
    /// <summary>
    /// Every broadcast that has had its card, by ID alone: never shown again. Nothing else of a notification is kept — no
    /// history, no read state (Sharan, 4 Oct: a notification shows under the charm, leaves after its time, and is gone;
    /// there is no Notification Center to keep it in).
    /// </summary>
    public IReadOnlyList<string> ShownNotifications { get; init; } = [];

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

    // Receiving

    /// <summary>
    /// Takes in what the feed sent: new broadcasts wait for their card, whatever their priority.
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

                waiting.Add(announcement);
            }

            return next with { Waiting = waiting, ShownNotifications = shown, LastFetchAt = now, FeedETag = etag };
        });
    }

    /// <summary>The feed answered 304: nothing changed since the last answer.</summary>
    public void NoteUnchanged(DateTimeOffset now) => Mutate(current => current with { LastFetchAt = now });

    /// <summary>
    /// A waiting broadcast that reached its expiry without its card — the PC was asleep, the charm hidden — has missed
    /// its moment. A broadcast is a pop-up and nothing more, so it is let go, and remembered so it is never shown late.
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
            }

            return next with { ShownNotifications = shown };
        });
    }

    // Showing

    /// <summary>
    /// A broadcast's card has appeared: never again. It is a pop-up, not a message to keep.
    /// </summary>
    public void MarkShown(Announcement announcement, DateTimeOffset now) => Mutate(current =>
    {
        List<string> shown = [.. current.ShownNotifications];
        Remember(shown, announcement.Id);
        return current with { Waiting = [.. current.Waiting.Where(a => a.Id != announcement.Id)], ShownNotifications = shown };
    });

    public void UpdateReminderState(Func<UpdateReminderState, UpdateReminderState> change) => Mutate(current =>
    {
        UpdateReminderState next = change(current.Update);

        // The reminder holds no lists, so record equality is the whole comparison: unchanged is not written.
        return next == current.Update ? current : current with { Update = next };
    });

    // Storage

    /// <summary>Applies a change; each operation returns the same document when it changes nothing, and that is not written.</summary>
    /// <remarks>
    /// By reference, not by content: comparing the serialised document would cost two serialisations on every
    /// evaluation, most of which change nothing.
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
                ShownNotifications = Each<string>("shownNotifications"),
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

    /// <summary>"2.3" for 2.3.0, as a headline says it; "2.3.1" stays whole.</summary>
    public static string ShortVersion(string version) =>
        version.EndsWith(".0", StringComparison.Ordinal) && version.Split('.').Length == 3 ? version[..^2] : version;
}
