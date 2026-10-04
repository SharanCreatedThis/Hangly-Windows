//
//  AppEnvironment.Notifications.cs
//  Hangly
//
//  The notifications, put together and started.
//

using Hangly.App.Notifications;
using Hangly.App.Services;
using Hangly.App.Tray;
using Hangly.Core.Analytics;
using Hangly.Core.Notifications;

namespace Hangly.App;

public sealed partial class AppEnvironment
{
    /// <summary>The rollback switch (NOTIFICATIONS-ARCHITECTURE.md §11): off, nothing is fetched or shown.</summary>
    public static readonly bool NotificationsEnabled = true;

    private NotificationStore? notificationStore;
    private NotificationPresenter? notifications;
    private AnnouncementFeed? announcements;
    private NotificationCardWindow? notificationCard;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? auditSwing;

    /// <summary>Starts fetching and showing. In bootstrap, before the overlay, on the XAML thread.</summary>
    private void StartNotifications()
    {
        if (!NotificationsEnabled)
        {
            return;
        }

        try
        {
            xamlQueue ??= Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            Microsoft.UI.Dispatching.DispatcherQueue queue = xamlQueue;
            notificationStore = new NotificationStore();
            notifications = new NotificationPresenter(
                queue,
                notificationStore,
                Updates,
                AppInfo.Version,
                isOnboarding: () => activeWelcome is { } welcome && Interop.WindowPlacement.IsShown(welcome),
                perform: Perform);
            notifications.CharmReturned = () => announcements?.Request(FetchTrigger.CharmVisible);
            notifications.ExitForUpdate = Exit;
            notificationCard = new NotificationCardWindow(queue) { OnDesktop = store.Settings.Overlay.WindowMode == Core.Models.WindowMode.Desktop };
            notificationCard.Command += OnCardCommand;
            notificationCard.HoverChanged += hovering => notifications.SetHovering(hovering);
            notifications.Changed += ShowCard;
            announcements = new AnnouncementFeed(notificationStore, action => queue.TryEnqueue(() => action()));
            announcements.Received += () => notifications.Evaluate();
            announcements.Start();
            if (overlay is { } existing)
            {
                HookNotifications(existing);
            }

            Diagnostics.Log($"notifications started; announcements {(announcements.Endpoints.Count == 0 ? "off in this build" : string.Join(", ", announcements.Endpoints.Select(uri => uri.Host)))}");
        }
        catch (Exception exception)
        {
            // Notifications are never on the path to a charm.
            Diagnostics.Failure("notifications", exception);
        }
    }

    /// <summary>The overlay is up: the card follows its charm, and its right-click opens the charm's menu.</summary>
    private void HookNotifications(Overlay.OverlayWindow window)
    {
        if (notifications is null || xamlQueue is not { } queue)
        {
            return;
        }

        void Follow()
        {
            if (overlay != window)
            {
                return;
            }

            notificationCard!.Column = window.RestColumn;
            notificationCard.Place();
            notifications.SetCharmPresent(window.IsCharmShown && window.RestColumn is not null);
        }

        window.CharmMoved += () => queue.TryEnqueue(Follow);
        queue.TryEnqueue(Follow);

        // The card swings with the overlay's own frames, on its thread; a rebuilt overlay (a lost graphics device)
        // hooks in again here.
        NotificationCardWindow card = notificationCard!;
        window.Framed += (direction, elapsed) => card.OnOverlayFrame(direction, elapsed);

        // For the screenshot scripts only, and never in an installed copy: a push every eight seconds, to watch the
        // card swing with the charm without anybody's mouse.
        if (Environment.GetEnvironmentVariable("HANGLY_AUDIT_SWING") == "1" && !Updater.IsInstalled && auditSwing is null)
        {
            auditSwing = queue.CreateTimer();
            auditSwing.Interval = TimeSpan.FromSeconds(8);
            auditSwing.Tick += (_, _) => overlay?.Nudge();
            auditSwing.Start();
        }
        window.CharmRightClicked += () => queue.TryEnqueue(ShowCharmMenu);
    }

    private void CharmGone() => notifications?.SetCharmPresent(false);

    private void ShowCard()
    {
        if (notifications is null || notificationCard is null)
        {
            return;
        }

        notificationCard.Column = overlay?.RestColumn;
        notificationCard.Show(notifications.Card);
    }

    private void OnCardCommand(CardCommand command)
    {
        if (notifications is not { } presenter)
        {
            return;
        }

        switch (command)
        {
            case CardCommand.UpdateNow:
                presenter.UpdateInBackground();
                break;
            case CardCommand.Later:
                presenter.RemindLater();
                break;
            case CardCommand.Skip:
                presenter.SkipVersion();
                break;
            case CardCommand.Close:
                presenter.CloseBroadcast();
                break;
            case CardCommand.Open:
                presenter.OpenBroadcast();
                break;
        }
    }

    /// <summary>
    /// Right-click on the charm. There is no Notifications item: notifications show under the charm and leave after their
    /// time, and are not kept anywhere to open (Sharan, 4 Oct).
    /// </summary>
    private void ShowCharmMenu()
    {
        tray?.ShowMenu(
        [
            new MenuEntry("Library", OpenLibrary),
            new MenuEntry("Hide Charm", () => store.UpdateOverlay(overlay => overlay with { IsEnabled = false })),
        ]);
    }

    /// <summary>What a notification's button asks for.</summary>
    private void Perform(NotificationAction action)
    {
        switch (action.Type)
        {
            case AnnouncementAction.OpenLibrary:
                OpenCustomize();
                customize?.ShowFromNotification(action.Target.Length > 0 ? action.Target : null, null);
                break;
            case AnnouncementAction.OpenCharm:
                OpenCustomize();
                customize?.ShowFromNotification(null, action.Target);
                break;
            case AnnouncementAction.OpenCreate:
                OpenCreate();
                break;
            case AnnouncementAction.OpenUrl when Uri.TryCreate(action.Target, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps:
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
                break;
            default:
                break;
        }

        Diagnostics.Log($"notification action: {action.AnalyticsName}");
    }

    /// <summary>The clock jumped — a wake from sleep, most often: catch up on whatever came due, and look for anything new.</summary>
    private void NotificationsAfterWake()
    {
        notifications?.Evaluate();
        announcements?.Request(FetchTrigger.Wake);
    }

    private void StopNotifications()
    {
        announcements?.Dispose();
        announcements = null;
        notificationCard?.Dispose();
        notificationCard = null;
    }
}
