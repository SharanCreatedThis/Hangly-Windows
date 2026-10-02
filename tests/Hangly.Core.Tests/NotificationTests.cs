using Hangly.Core.Analytics;
using Hangly.Core.Notifications;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The notifications (2.3.0): the macOS suites, case for case.</summary>
public sealed class NotificationTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"hangly-notifications-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static DateTimeOffset Minutes(double value) => T0.AddMinutes(value);

    private static Announcement Make(
        string id = "football",
        AnnouncementPriority priority = AnnouncementPriority.Normal,
        AnnouncementAudience audience = AnnouncementAudience.All,
        string[]? platforms = null,
        DateTimeOffset? start = null,
        DateTimeOffset? expire = null,
        AnnouncementAction action = AnnouncementAction.OpenLibrary,
        string target = "footballLegends",
        string title = "⚽ Football Pack",
        int duration = 30) => new()
        {
            Id = id,
            Title = title,
            Message = "6 New Charms Live",
            ActionType = action,
            ActionTarget = target,
            Priority = priority,
            Platforms = platforms ?? ["mac", "windows"],
            Audience = audience,
            DurationSeconds = duration,
            StartAt = start ?? Minutes(-60),
            ExpireAt = expire ?? Minutes(60 * 48),
        };

    // The update reminder

    private static ReminderDecision Decide(ref UpdateReminderState state, double minute) =>
        UpdateReminder.Decide(ref state, "2.3.0", "2.2.1", Minutes(minute));

    [Fact]
    public void TheReminderIsDueShowsForTenMinutesFromWhenItAppearsSleepsAnHourAndReturns()
    {
        var state = new UpdateReminderState();
        Assert.Equal(ReminderKind.Due, Decide(ref state, 0).Kind);
        Assert.Equal(ReminderKind.Due, Decide(ref state, 5).Kind);
        state = UpdateReminder.Begin(state, Minutes(5));
        Assert.Equal(new ReminderDecision(ReminderKind.Show, Minutes(15)), Decide(ref state, 10));
        Assert.Equal(new ReminderDecision(ReminderKind.Sleep, Minutes(75)), Decide(ref state, 15));
        Assert.Equal(new ReminderDecision(ReminderKind.Sleep, Minutes(75)), Decide(ref state, 74));
        Assert.Equal(ReminderKind.Due, Decide(ref state, 75).Kind);
        state = UpdateReminder.Begin(state, Minutes(75));
        Assert.Equal(new ReminderDecision(ReminderKind.Show, Minutes(85)), Decide(ref state, 80));
    }

    [Fact]
    public void LaterSleepsAnHourAndBeginDoesNotRestartACardAlreadyUp()
    {
        var state = new UpdateReminderState();
        Decide(ref state, 0);
        state = UpdateReminder.Begin(state, Minutes(0));
        state = UpdateReminder.Begin(state, Minutes(3));
        Assert.Equal(Minutes(10), state.VisibleUntil);
        state = UpdateReminder.Later(state, Minutes(4));
        Assert.Equal(new ReminderDecision(ReminderKind.Sleep, Minutes(64)), Decide(ref state, 30));
    }

    [Fact]
    public void SkipStopsThisVersionOnly()
    {
        var state = new UpdateReminderState();
        Decide(ref state, 0);
        state = UpdateReminder.Skip(state);
        Assert.Equal(ReminderKind.None, Decide(ref state, 500).Kind);
        Assert.Equal(ReminderKind.Due, UpdateReminder.Decide(ref state, "2.4.0", "2.2.1", Minutes(501)).Kind);
    }

    [Fact]
    public void UpdatingOrNothingNewerEndsTheReminder()
    {
        var state = new UpdateReminderState();
        Decide(ref state, 0);
        state = UpdateReminder.Begin(state, Minutes(0));
        Assert.Equal(ReminderKind.None, UpdateReminder.Decide(ref state, "2.3.0", "2.3.0", Minutes(1)).Kind);
        Assert.Null(state.Version);
        Assert.Null(state.VisibleUntil);
        Assert.Equal(ReminderKind.None, UpdateReminder.Decide(ref state, null, "2.3.0", Minutes(2)).Kind);
        Assert.Equal(ReminderKind.None, UpdateReminder.Decide(ref state, "2.2.9", "2.3.0", Minutes(3)).Kind);
    }

    [Theory]
    [InlineData("2.10.0", "2.9.9", true)]
    [InlineData("2.3", "2.2.1", true)]
    [InlineData("2.3.0", "2.3", false)]
    [InlineData("2.2.1", "2.3.0", false)]
    public void VersionsCompareNumberByNumber(string candidate, string current, bool newer) =>
        Assert.Equal(newer, VersionOrder.IsNewer(candidate, current));

    // The queue

    [Fact]
    public void HighFirstThenTheUpdateThenNormalOldestFirstAndLowNever()
    {
        Announcement old = Make("old", start: Minutes(-30));
        Announcement fresh = Make("new", start: Minutes(-5));
        Announcement urgent = Make("urgent", AnnouncementPriority.High, start: Minutes(-1));
        Announcement quiet = Make("quiet", AnnouncementPriority.Low, start: Minutes(-90));
        Assert.Equal("urgent", NotificationQueue.Next([fresh, quiet, old, urgent], "2.3.0", Minutes(0))?.Id);
        Assert.Equal("update-2.3.0", NotificationQueue.Next([fresh, old], "2.3.0", Minutes(0))?.Id);
        Assert.Equal("old", NotificationQueue.Next([fresh, old], null, Minutes(0))?.Id);
        Assert.Null(NotificationQueue.Next([quiet], null, Minutes(0)));
    }

    [Fact]
    public void NotBeforeItsStartNorAfterItsExpiry()
    {
        Announcement later = Make("later", start: Minutes(30));
        Announcement gone = Make("gone", start: Minutes(-90), expire: Minutes(-1));
        Assert.Null(NotificationQueue.Next([later, gone], null, Minutes(0)));
        Assert.Equal(Minutes(30), NotificationQueue.NextStart([later, gone], Minutes(0)));
        Assert.Equal("later", NotificationQueue.Next([later], null, Minutes(30))?.Id);
    }

    // Announcements

    [Fact]
    public void TheFeedParsesDroppingOnlyTheEntryThatDoesNot()
    {
        const string json = """
            {"announcements":[
              {"id":"a","title":"⚽ Football Pack","message":"6 New Charms Live","actionType":"openLibrary",
               "actionTarget":"footballLegends","actionLabel":null,"priority":"normal","platforms":["mac","windows"],
               "audience":"all","durationSeconds":30,"startAt":"2026-10-05T08:00:00.000Z","expireAt":"2026-10-07T09:00:00.000Z"},
              {"id":"b","title":"Broken","actionType":"launchMissiles"},
              {"id":"c","title":"Read more","message":"On the site","actionType":"openUrl",
               "actionTarget":"https://www.sharancreatedthis.in","actionLabel":"Read","priority":"high","platforms":["windows"],
               "audience":"test","durationSeconds":60,"startAt":"2026-10-05T08:00:00Z","expireAt":"2026-10-06T08:00:00Z"}
            ],"servedAt":"2026-10-05T09:00:00.000Z"}
            """;
        IReadOnlyList<Announcement> feed = AnnouncementFeedParser.Parse(json);
        Assert.Equal(new[] { "a", "c" }, feed.Select(a => a.Id));
        Assert.Equal("Open Library", feed[0].ButtonTitle);
        Assert.Equal("Read", feed[1].ButtonTitle);
        Assert.Equal(AnnouncementPriority.High, feed[1].Priority);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero), feed[0].StartAt);
        Assert.Empty(AnnouncementFeedParser.Parse("{not json"));
        Assert.Empty(AnnouncementFeedParser.Parse("{\"other\":1}"));
    }

    [Fact]
    public void OnlyWhatThisPlatformCanShowIsUsable()
    {
        Assert.True(Make().IsUsable());
        Assert.False(Make(platforms: ["mac"]).IsUsable());
        Assert.False(Make(title: string.Empty).IsUsable());
        Assert.False(Make(title: new string('x', 61)).IsUsable());
        Assert.False(Make(duration: 4).IsUsable());
        Assert.False(Make(start: Minutes(10), expire: Minutes(5)).IsUsable());
        Assert.False(Make(action: AnnouncementAction.OpenUrl, target: "http://example.com").IsUsable());
        Assert.False(Make(action: AnnouncementAction.OpenUrl, target: "javascript:alert(1)").IsUsable());
        Assert.False(Make(action: AnnouncementAction.OpenUrl, target: "file:///C:/Windows").IsUsable());
        Assert.False(Make(action: AnnouncementAction.None, target: "x").IsUsable());
        Assert.True(Make(action: AnnouncementAction.OpenCharm, target: "spiderMan").IsUsable());
        Assert.True(Make(action: AnnouncementAction.OpenLibrary, target: string.Empty).IsUsable());
        Assert.Null(Make(action: AnnouncementAction.None, target: string.Empty).ButtonTitle);
    }

    // Highlights

    [Fact]
    public void HighlightsAreTheBoldLeadsOfTheFirstThreeItems()
    {
        const string html = """
            <h2>Hangly 2.3</h2><ul>
              <li><strong>90 new charms.</strong> Pokémon, Naruto and more.</li>
              <li><strong>Notification Center.</strong> Hangly now tells you things.</li>
              <li><strong>Faster startup.</strong> Opens in half the time.</li>
              <li><strong>Fourth.</strong> Not shown.</li>
            </ul>
            """;
        Assert.Equal(new[] { "90 new charms", "Notification Center", "Faster startup" }, ReleaseNoteHighlights.Extract(html));

        string markdown = "## 2.3.0\n- **Smoother rope.** Less work per frame.\n- Fixes a crash when a display is unplugged. And more.\n- "
            + string.Concat(Enumerable.Repeat("word ", 20));
        IReadOnlyList<string> lines = ReleaseNoteHighlights.Extract(markdown);
        Assert.Equal("Smoother rope", lines[0]);
        Assert.Equal("Fixes a crash when a display is unplugged", lines[1]);
        Assert.EndsWith("…", lines[2], StringComparison.Ordinal);
        Assert.True(lines[2].Length <= ReleaseNoteHighlights.Longest);
        Assert.Empty(ReleaseNoteHighlights.Extract(null));
        Assert.Empty(ReleaseNoteHighlights.Extract("No list here."));
    }

    // The store

    [Fact]
    public void ABroadcastWaitsIsShownOnceAndNeverReturnsAsACard()
    {
        var store = new NotificationStore(folder);
        Announcement football = Make();
        store.Receive([football], includeTest: false, Minutes(0));
        Assert.Equal(new[] { "football" }, store.State.Waiting.Select(a => a.Id));
        store.MarkShown(football, Minutes(1));
        Assert.Empty(store.State.Waiting);
        Assert.Equal(new[] { "football" }, store.State.History.Select(item => item.Id));
        Assert.Equal(1, store.UnreadCount);
        store.Receive([football], includeTest: false, Minutes(60 * 6));
        Assert.Empty(store.State.Waiting);
        Assert.Single(store.State.History);
    }

    [Fact]
    public void LowGoesToTheCenterTestOnlyToTestersExpiredNever()
    {
        var store = new NotificationStore(persist: false);
        store.Receive(
            [
                Make("quiet", AnnouncementPriority.Low),
                Make("tester", audience: AnnouncementAudience.Test),
                Make("expired", start: Minutes(-100), expire: Minutes(-1)),
                Make("mac", platforms: ["mac"]),
            ],
            includeTest: false,
            Minutes(0));
        Assert.Empty(store.State.Waiting);
        Assert.Equal(new[] { "quiet" }, store.State.History.Select(item => item.Id));
        store.Receive([Make("tester", audience: AnnouncementAudience.Test)], includeTest: true, Minutes(1));
        Assert.Equal(new[] { "tester" }, store.State.Waiting.Select(a => a.Id));
    }

    [Fact]
    public void WithdrawnOrEditedBeforeItShowed()
    {
        var store = new NotificationStore(persist: false);
        store.Receive([Make("a"), Make("b")], includeTest: false, Minutes(0));
        Announcement edited = Make("b", title: "Six new charms");
        store.Receive([edited], includeTest: false, Minutes(1));
        Assert.Equal(new[] { edited }, store.State.Waiting);
        store.Expire(Minutes(60 * 49));
        Assert.Empty(store.State.Waiting);
    }

    [Fact]
    public void ReadAllReadAndClearedAndClearedStaysCleared()
    {
        var store = new NotificationStore(persist: false);
        foreach (string id in new[] { "a", "b", "c" })
        {
            store.MarkShown(Make(id), Minutes(0));
        }

        store.MarkRead("b", Minutes(1));
        Assert.Equal(2, store.UnreadCount);
        store.MarkAllRead(Minutes(2));
        Assert.Equal(0, store.UnreadCount);
        store.ClearAll();
        Assert.Empty(store.State.History);
        store.Receive([Make("a")], includeTest: false, Minutes(4));
        Assert.Empty(store.State.Waiting);
        Assert.Equal(new[] { "a", "b", "c" }, store.State.ReadNotifications.Order());
    }

    [Fact]
    public void OneUpdateEntryReplacedByANewerVersionAndRemovedOnceInstalled()
    {
        var store = new NotificationStore(persist: false);
        store.RecordUpdate("2.3.0", ["90 new charms"], Minutes(0));
        store.RecordUpdate("2.3.0", ["90 new charms"], Minutes(1));
        Assert.Equal(new[] { "update-2.3.0" }, store.State.History.Select(item => item.Id));
        Assert.Equal("Hangly 2.3 Available", store.State.History[0].Title);
        store.RecordUpdate("2.3.1", [], Minutes(2));
        Assert.Equal(new[] { "update-2.3.1" }, store.State.History.Select(item => item.Id));
        Assert.Equal("Hangly 2.3.1 Available", store.State.History[0].Title);
        store.RemoveUpdate();
        Assert.Empty(store.State.History);
    }

    [Fact]
    public void EverythingSurvivesARelaunchIncludingWhatIsStillWaiting()
    {
        var store = new NotificationStore(folder);
        store.Receive([Make("waiting"), Make("shown")], includeTest: false, Minutes(0));
        store.MarkShown(Make("shown"), Minutes(1));
        store.UpdateReminderState(state => state with { SkippedVersion = "2.3.0" });
        var reopened = new NotificationStore(folder);
        Assert.Equal(new[] { "waiting" }, reopened.State.Waiting.Select(a => a.Id));
        Assert.Equal(new[] { "shown" }, reopened.State.History.Select(item => item.Id));
        Assert.Equal(new[] { "shown" }, reopened.State.ShownNotifications);
        Assert.Equal("2.3.0", reopened.State.Update.SkippedVersion);
        // Records holding lists compare those by reference: the fields, then the list's contents.
        Assert.Equal(store.State.Waiting[0] with { Platforms = [] }, reopened.State.Waiting[0] with { Platforms = [] });
        Assert.Equal(store.State.Waiting[0].Platforms, reopened.State.Waiting[0].Platforms);
    }

    [Fact]
    public void AnUnreadableDocumentIsAFreshStart()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notifications.json"), "{not json");
        Assert.Empty(new NotificationStore(folder).State.History);
    }

    [Fact]
    public void HistoryKeepsTheNewestHundred()
    {
        var store = new NotificationStore(persist: false);
        for (int index = 0; index < NotificationStore.HistoryLimit + 5; index++)
        {
            store.MarkShown(Make($"a{index}"), Minutes(index));
        }

        Assert.Equal(NotificationStore.HistoryLimit, store.State.History.Count);
        Assert.Equal($"a{NotificationStore.HistoryLimit + 4}", store.State.History[0].Id);
        Assert.Equal(NotificationStore.HistoryLimit + 5, store.State.ShownNotifications.Count);
    }

    [Fact]
    public void TheChangedEventFiresOnlyForRealChanges()
    {
        var store = new NotificationStore(persist: false);
        int changes = 0;
        store.Changed += () => changes++;
        store.MarkRead("nothing", Minutes(0));
        Assert.Equal(0, changes);
        store.MarkShown(Make(), Minutes(0));
        Assert.Equal(1, changes);
    }

    // Analytics

    [Fact]
    public void TheEventsHaveMacOSNamesAndParameters()
    {
        AnalyticsEvent shown = AnalyticsEvent.NotificationShown(NotificationKind.Broadcast, "football");
        Assert.Equal("notification_shown", shown.Name);
        Assert.Equal(new Dictionary<string, string> { ["kind"] = "broadcast", ["notification_id"] = "football" }, shown.Parameters);
        Assert.Equal("timeout", AnalyticsEvent.BroadcastDismissed("x", DismissReason.Timeout).Parameters["reason"]);
        Assert.Equal("update_banner_clicked", AnalyticsEvent.UpdateBannerClicked("2.3.0").Name);
        Assert.Equal("2.3.0", AnalyticsEvent.UpdateBannerShown("2.3.0").Parameters["to_version"]);
        Assert.Equal("open_library", AnalyticsEvent.BroadcastClicked("x", "open_library").Parameters["action"]);
        Assert.Equal(100, AnalyticsEvent.BroadcastShown(new string('x', 140)).Parameters["notification_id"].Length);
    }
}
