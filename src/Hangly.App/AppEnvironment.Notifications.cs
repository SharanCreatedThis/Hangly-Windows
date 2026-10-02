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
    private NotificationCenterWindow? notificationCenter;

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
                isOnboarding: () => activeWelcome is { } welcome && welcome.AppWindow.IsVisible,
                perform: Perform);
            notifications.OpenCenter = OpenNotifications;
            notifications.CharmReturned = () => announcements?.Request(FetchTrigger.CharmVisible);
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
        notificationCard.Show(notifications.Card, notifications.ShowsBell, notifications.UnreadCount);
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
                presenter.UpdateNow();
                break;
            case CardCommand.Later:
                presenter.RemindLater();
                break;
            case CardCommand.Skip:
                presenter.SkipVersion();
                break;
            case CardCommand.Close when presenter.Card is { IsUpdate: true }:
                presenter.RemindLater(DismissReason.Closed);
                break;
            case CardCommand.Close:
                presenter.CloseBroadcast();
                break;
            case CardCommand.Open:
                presenter.OpenBroadcast();
                break;
            case CardCommand.Bell:
                presenter.ShowCenter("bell");
                break;
        }
    }

    /// <summary>The Notification Center, made once and brought back.</summary>
    private void OpenNotifications()
    {
        if (notifications is null)
        {
            return;
        }

        try
        {
            notificationCenter ??= new NotificationCenterWindow(notifications);
            notificationCenter.Present();
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("notification center", exception);
        }
    }

    /// <summary>Right-click on the charm: Notifications first, because it is the one thing only here.</summary>
    private void ShowCharmMenu()
    {
        int unread = notifications?.UnreadCount ?? 0;
        tray?.ShowMenu(
        [
            new MenuEntry(unread > 0 ? $"Notifications ({unread})" : "Notifications", () => notifications?.ShowCenter("charm_menu")),
            MenuEntry.Separator,
            new MenuEntry("Library", OpenLibrary),
            new MenuEntry("Hide Charm", () => store.UpdateOverlay(overlay => overlay with { IsEnabled = false })),
        ]);
    }

    /// <summary>The tray's line for the Center, with the unread count when there is one.</summary>
    private List<MenuEntry> NotificationsMenu()
    {
        if (notifications is null)
        {
            return [];
        }

        int unread = notifications.UnreadCount;
        return [new MenuEntry(unread > 0 ? $"Notifications ({unread})" : "Notifications…", () => notifications.ShowCenter("tray"))];
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
            case AnnouncementAction.OpenNotifications:
                notifications?.ShowCenter("card");
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
