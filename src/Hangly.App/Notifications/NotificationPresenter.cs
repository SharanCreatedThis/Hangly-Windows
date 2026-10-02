//
//  NotificationPresenter.cs
//  Hangly
//
//  Which card is under the charm, for how long, and what its buttons do.
//

using Hangly.App.Services;
using Hangly.Core.Analytics;
using Hangly.Core.Notifications;
using Microsoft.UI.Dispatching;

namespace Hangly.App.Notifications;

/// <summary>Where the update card has got to after Update Now.</summary>
public enum UpdateNowStage
{
    Idle,
    Downloading,
    Installing,
    Restarting,
    Failed,
}

/// <summary>The card on screen. <see cref="Token"/> changes when the card does, so one leaves and the next arrives.</summary>
public sealed record CardPresentation(
    Guid Token,
    string? Version,
    IReadOnlyList<string> Highlights,
    UpdateNowStage Stage,
    int? Percent,
    Announcement? Broadcast)
{
    public string Id => Broadcast?.Id ?? $"update-{Version}";

    public bool IsUpdate => Broadcast is null;
}

/// <summary>What a button asks the app to do.</summary>
public sealed record NotificationAction(AnnouncementAction Type, string Target)
{
    public static NotificationAction? From(AnnouncementAction type, string target) =>
        type == AnnouncementAction.None || !Announcement.TargetFits(type, target) ? null : new(type, target);

    public string AnalyticsName => Type switch
    {
        AnnouncementAction.OpenLibrary => "open_library",
        AnnouncementAction.OpenCharm => "open_charm",
        AnnouncementAction.OpenCreate => "open_create",
        _ => "open_url",
    };
}

/// <summary>Decides the card under the charm and the bell beside it.</summary>
/// <remarks>
/// Event-driven, as on macOS: it re-decides when the feed answers, the updater changes, the charm appears or goes, a
/// card leaves, or its one timer comes due — the soonest of a card's end, the reminder's return and a scheduled
/// broadcast's start. Nothing polls. Lives on the XAML thread; everything that calls in from elsewhere is marshalled
/// there first. macOS's <c>NotificationPresenter</c>, rule for rule.
/// </remarks>
public sealed class NotificationPresenter
{
    public static readonly TimeSpan LaunchQuiet = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan Breath = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan AfterHover = TimeSpan.FromSeconds(4);

    private readonly DispatcherQueue queue;
    private readonly Updater? updates;
    private readonly string currentVersion;
    private readonly DateTimeOffset launchedAt;
    private readonly Func<bool> isOnboarding;
    private readonly Action<NotificationAction> perform;
    private readonly DispatcherQueueTimer wake;
    private DateTimeOffset? cardEndsAt;
    private DateTimeOffset? restUntil;
    private bool isHovering;
    private CancellationTokenSource? updating;

    public NotificationPresenter(
        DispatcherQueue queue,
        NotificationStore store,
        Updater? updates,
        string currentVersion,
        Func<bool> isOnboarding,
        Action<NotificationAction> perform)
    {
        this.queue = queue;
        Store = store;
        this.updates = updates;
        this.currentVersion = currentVersion;
        this.isOnboarding = isOnboarding;
        this.perform = perform;
        launchedAt = DateTimeOffset.Now;
        wake = queue.CreateTimer();
        wake.IsRepeating = false;
        wake.Tick += (_, _) => Evaluate();
        if (updates is not null)
        {
            updates.StateChanged += () => queue.TryEnqueue(() => Evaluate());
        }

        store.Changed += () => queue.TryEnqueue(() => Changed?.Invoke());
    }

    public NotificationStore Store { get; }

    private static readonly bool AuditEvaluations = Environment.GetEnvironmentVariable("HANGLY_AUDIT_NOTIFICATIONS") == "evaluations";

    /// <summary>Opens the Notification Center. Set after construction: the Center is built from this.</summary>
    public Action OpenCenter { get; set; } = () => { };

    /// <summary>Told when the charm comes back on screen, so the feed can look for anything new.</summary>
    public Action CharmReturned { get; set; } = () => { };

    /// <summary>Opens the Center and says where from: <c>bell</c>, <c>charm_menu</c> or <c>tray</c>.</summary>
    public void ShowCenter(string source)
    {
        HanglyAnalytics.Log(AnalyticsEvent.NotificationCenterOpened(source));
        OpenCenter();
    }

    /// <summary>Raised on the XAML thread whenever the card, the bell or the count may have changed.</summary>
    public event Action? Changed;

    public CardPresentation? Card { get; private set; }

    /// <summary>Whether the charm is on screen for a card to hang under.</summary>
    public bool IsCharmPresent { get; private set; }

    public int UnreadCount => Store.UnreadCount;

    /// <summary>The bell: something unread, the charm on screen, and no card in the way.</summary>
    public bool ShowsBell => Card is null && IsCharmPresent && UnreadCount > 0;

    /// <summary>The overlay's charm appeared or went away.</summary>
    public void SetCharmPresent(bool present)
    {
        if (present == IsCharmPresent)
        {
            return;
        }

        IsCharmPresent = present;
        if (present)
        {
            CharmReturned();
        }

        if (!present && Card is not null)
        {
            // Hidden with the charm, not dismissed: a broadcast that had begun was seen; the reminder keeps its clock.
            Card = null;
            cardEndsAt = null;
        }

        Evaluate();
    }

    // Deciding

    public void Evaluate()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        if (AuditEvaluations)
        {
            Diagnostics.Log("notifications: evaluate");
        }

        Store.Expire(now);
        ReminderDecision decision = UpdateDecision(now);
        if (Card is { } current)
        {
            KeepOrEnd(current, decision, now);
        }

        if (Card is null && CanShow(now))
        {
            string? due = decision.Kind is ReminderKind.Show or ReminderKind.Due ? Store.State.Update.Version : null;
            if (NotificationQueue.Next(Store.State.Waiting, due, now) is { } next)
            {
                Present(next, now);
            }
        }

        Schedule(now, decision);
        Changed?.Invoke();
    }

    private bool CanShow(DateTimeOffset now) =>
        IsCharmPresent && !isOnboarding() && now - launchedAt >= Quiet && (restUntil is null || now >= restUntil);

    /// <summary>The launch's quiet moments; three seconds under the development preview, so a screenshot script need not wait.</summary>
    private static TimeSpan Quiet => Preview.IsOn ? TimeSpan.FromSeconds(3) : LaunchQuiet;

    private ReminderDecision UpdateDecision(DateTimeOffset now)
    {
        string? available = AvailableVersion;
        UpdateReminderState state = Store.State.Update;
        ReminderDecision decision = UpdateReminder.Decide(ref state, available, currentVersion, now);
        Store.UpdateReminderState(_ => state);
        if (available is not null && VersionOrder.IsNewer(available, currentVersion))
        {
            Store.RecordUpdate(available, Highlights, now);
        }
        else
        {
            Store.SettleUpdate(currentVersion, now);
        }

        return decision;
    }

    private IReadOnlyList<string> Highlights => ReleaseNoteHighlights.Extract(Preview.IsOn ? Preview.Notes : updates?.AvailableNotes);

    private string? AvailableVersion => Preview.IsOn ? Preview.Version : updates?.AvailableVersion;

    private void KeepOrEnd(CardPresentation current, ReminderDecision decision, DateTimeOffset now)
    {
        if (current.IsUpdate)
        {
            // Once Update Now has started, the card stays until Hangly restarts or it fails.
            if (current.Stage is not (UpdateNowStage.Idle or UpdateNowStage.Failed) || decision.Kind == ReminderKind.Show)
            {
                return;
            }

            End(DismissReason.Timeout, now);
            return;
        }

        if (now >= current.Broadcast!.ExpireAt || (!isHovering && cardEndsAt is { } ends && now >= ends))
        {
            End(DismissReason.Timeout, now);
        }
    }

    private void Present(NotificationCard next, DateTimeOffset now)
    {
        if (next.Broadcast is { } announcement)
        {
            Store.MarkShown(announcement, now);
            Card = new CardPresentation(Guid.NewGuid(), null, [], UpdateNowStage.Idle, null, announcement);
            cardEndsAt = now.AddSeconds(announcement.DurationSeconds);
            HanglyAnalytics.Log(AnalyticsEvent.NotificationShown(NotificationKind.Broadcast, announcement.Id));
            HanglyAnalytics.Log(AnalyticsEvent.BroadcastShown(announcement.Id));
        }
        else
        {
            string version = next.Version!;
            bool began = false;
            Store.UpdateReminderState(state => UpdateReminder.Begin(state, now, out began));
            Card = new CardPresentation(Guid.NewGuid(), version, Highlights, UpdateNowStage.Idle, null, null);
            cardEndsAt = Store.State.Update.VisibleUntil;

            // One impression per ten minutes up, not one each time the charm comes back inside them.
            if (began)
            {
                HanglyAnalytics.Log(AnalyticsEvent.NotificationShown(NotificationKind.Update, $"update-{version}"));
                HanglyAnalytics.Log(AnalyticsEvent.UpdateBannerShown(version));
            }
        }

        Diagnostics.Log($"notification card: {next.Id}");
    }

    /// <summary>One wake-up, at the soonest moment anything changes.</summary>
    private void Schedule(DateTimeOffset now, ReminderDecision decision)
    {
        var moments = new List<DateTimeOffset>();
        if (cardEndsAt is { } ends && Card is not null && !isHovering)
        {
            moments.Add(ends);
        }

        if (decision.Kind == ReminderKind.Sleep && decision.Until is { } until)
        {
            moments.Add(until);
        }

        if (restUntil is { } rest && rest > now)
        {
            moments.Add(rest);
        }

        if (NotificationQueue.NextStart(Store.State.Waiting, now) is { } start)
        {
            moments.Add(start);
        }

        DateTimeOffset quietEnds = launchedAt + Quiet;
        if (quietEnds > now)
        {
            moments.Add(quietEnds);
        }

        wake.Stop();
        if (moments.Count == 0)
        {
            return;
        }

        TimeSpan delay = moments.Min() - now;
        // DispatcherQueueTimer intervals are milliseconds in an int; a reminder a day away is woken again later.
        wake.Interval = TimeSpan.FromMilliseconds(Math.Clamp(delay.TotalMilliseconds, 250, TimeSpan.FromHours(6).TotalMilliseconds));
        wake.Start();
    }

    private void End(DismissReason reason, DateTimeOffset now)
    {
        if (Card is not { } current)
        {
            return;
        }

        NotificationKind kind = current.IsUpdate ? NotificationKind.Update : NotificationKind.Broadcast;
        HanglyAnalytics.Log(AnalyticsEvent.NotificationDismissed(kind, current.Id, reason));
        if (!current.IsUpdate)
        {
            HanglyAnalytics.Log(AnalyticsEvent.BroadcastDismissed(current.Id, reason));
        }

        Card = null;
        cardEndsAt = null;
        isHovering = false;
        restUntil = now + Breath;
    }

    private void SetStage(UpdateNowStage stage, int? percent = null)
    {
        if (Card is { IsUpdate: true } current)
        {
            Card = current with { Stage = stage, Percent = percent };
            Changed?.Invoke();
        }
    }

    // What people do

    /// <summary>Update Now: Velopack's UpdateManager downloads with progress, installs and restarts.</summary>
    public async void UpdateNow()
    {
        if (Card is not { IsUpdate: true } current || (updates is null && !Preview.IsOn))
        {
            return;
        }

        HanglyAnalytics.Log(AnalyticsEvent.NotificationClicked(NotificationKind.Update, current.Id, "update_now"));
        HanglyAnalytics.Log(AnalyticsEvent.UpdateBannerClicked(current.Version!));
        Store.MarkRead(current.Id, DateTimeOffset.Now);
        SetStage(UpdateNowStage.Downloading, updates?.ReadyVersion is null ? 0 : 100);
        updating?.Cancel();
        updating = new CancellationTokenSource();
        CancellationToken cancel = updating.Token;
        Func<Action<int>, Action, CancellationToken, Task<bool>> run = Preview.IsOn ? Preview.SimulateAsync : updates!.UpdateNowAsync;
        bool started = await run(
            percent => queue.TryEnqueue(() => SetStage(UpdateNowStage.Downloading, percent)),
            () => queue.TryEnqueue(() =>
            {
                SetStage(UpdateNowStage.Installing);
                // Velopack replaces the files once this process has exited; the restart is what is seen next.
                _ = Task.Delay(900).ContinueWith(_ => queue.TryEnqueue(() => SetStage(UpdateNowStage.Restarting)), TaskScheduler.Default);
            }),
            cancel).ConfigureAwait(true);
        if (!started && !cancel.IsCancellationRequested)
        {
            SetStage(UpdateNowStage.Failed);
        }
    }

    /// <summary>The update card's Later or ×: back in an hour.</summary>
    public void RemindLater(DismissReason reason = DismissReason.Later)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        // Closed mid-download: the update installs quietly later instead of restarting Hangly behind a closed card.
        updating?.Cancel();
        Store.UpdateReminderState(state => UpdateReminder.Later(state, now));
        End(reason, now);
        Evaluate();
    }

    /// <summary>Skip This Version.</summary>
    public void SkipVersion()
    {
        Store.UpdateReminderState(UpdateReminder.Skip);
        End(DismissReason.Skipped, DateTimeOffset.Now);
        Evaluate();
    }

    /// <summary>A broadcast's button.</summary>
    public void OpenBroadcast()
    {
        if (Card?.Broadcast is not { } announcement || NotificationAction.From(announcement.ActionType, announcement.ActionTarget) is not { } action)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        HanglyAnalytics.Log(AnalyticsEvent.NotificationClicked(NotificationKind.Broadcast, announcement.Id, action.AnalyticsName));
        HanglyAnalytics.Log(AnalyticsEvent.BroadcastClicked(announcement.Id, action.AnalyticsName));
        Store.MarkRead(announcement.Id, now);
        Card = null;
        cardEndsAt = null;
        restUntil = now + Breath;
        perform(action);
        Evaluate();
    }

    /// <summary>A broadcast's ×.</summary>
    public void CloseBroadcast()
    {
        End(DismissReason.Closed, DateTimeOffset.Now);
        Evaluate();
    }

    /// <summary>The pointer over a broadcast holds it: nobody's card vanishes while it is being read.</summary>
    public void SetHovering(bool hovering)
    {
        if (Card is not { IsUpdate: false } || hovering == isHovering)
        {
            return;
        }

        isHovering = hovering;
        DateTimeOffset now = DateTimeOffset.Now;
        if (!hovering && cardEndsAt is { } ends)
        {
            cardEndsAt = ends > now + AfterHover ? ends : now + AfterHover;
        }

        Evaluate();
    }

    /// <summary>An entry in the Center was chosen.</summary>
    public void Open(NotificationItem item)
    {
        Store.MarkRead(item.Id, DateTimeOffset.Now);
        if (item.Kind == NotificationKind.Update)
        {
            HanglyAnalytics.Log(AnalyticsEvent.NotificationClicked(NotificationKind.Update, item.Id, "update_now"));
            if (item.Version is { } version)
            {
                HanglyAnalytics.Log(AnalyticsEvent.UpdateBannerClicked(version));
            }

            if (updates is not null)
            {
                _ = updates.UpdateNowAsync(_ => { }, () => { }, CancellationToken.None);
            }

            return;
        }

        if (NotificationAction.From(item.ActionType, item.ActionTarget) is { } action)
        {
            HanglyAnalytics.Log(AnalyticsEvent.NotificationClicked(NotificationKind.Broadcast, item.Id, action.AnalyticsName));
            HanglyAnalytics.Log(AnalyticsEvent.BroadcastClicked(item.Id, action.AnalyticsName));
            perform(action);
        }
    }
}

/// <summary>
/// A pretend update, for seeing the update card in a build that cannot update — every developer build, which is not
/// an installed copy. <c>HANGLY_NOTIFICATION_PREVIEW=update</c>; ignored in an installed copy, so no release can be
/// made to show it.
/// </summary>
internal static class Preview
{
    public const string Version = "2.3.0";

    public const string Notes =
        "- **90 new charms.** Pokémon, Naruto and more.\n- **Notification Center.** Hangly tells you what is new.\n- **Faster startup.** Opens sooner.";

    public static bool IsOn { get; } =
        Environment.GetEnvironmentVariable("HANGLY_NOTIFICATION_PREVIEW") is "update" or "update-fail" && !Updater.IsInstalled;

    /// <summary><c>update-fail</c>: the download stops at 40%, to see the failure and Try Again.</summary>
    private static bool Fails { get; } = Environment.GetEnvironmentVariable("HANGLY_NOTIFICATION_PREVIEW") == "update-fail";

    /// <summary>Four seconds of download, then the install, then nothing: there is nothing to restart into.</summary>
    public static async Task<bool> SimulateAsync(Action<int> downloading, Action installing, CancellationToken cancel)
    {
        for (int percent = 0; percent <= 100; percent += 4)
        {
            if (Fails && percent >= 40)
            {
                return false;
            }

            downloading(percent);
            await Task.Delay(160).ConfigureAwait(false);
        }

        if (cancel.IsCancellationRequested)
        {
            return false;
        }

        installing();
        await Task.Delay(1500).ConfigureAwait(false);
        return true;
    }
}
