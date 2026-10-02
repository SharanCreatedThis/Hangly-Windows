//
//  NotificationCenterWindow.cs
//  Hangly
//
//  Everything Hangly has said, newest first.
//

using Hangly.App.Services;
using Hangly.Core.Analytics;
using Hangly.Core.Notifications;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Hangly.App.Notifications;

/// <summary>The Notification Center: unread first, then earlier, searchable.</summary>
/// <remarks>
/// Kept and brought back rather than rebuilt, and hidden rather than closed (<see cref="Onboarding.ProcessLifetime"/>),
/// so closing it can never end the app. Opened from the bell, the charm's right-click menu and the tray. Mica, with
/// the title bar folded into the content, as the follow card; macOS's <c>NotificationCenterView</c>.
/// </remarks>
public sealed class NotificationCenterWindow : Window
{
    private readonly NotificationPresenter presenter;
    private readonly StackPanel list = new() { Spacing = 8, Padding = new Thickness(14, 4, 14, 18) };
    private readonly TextBlock unread = new() { FontSize = 12, Opacity = 0.6 };
    private readonly TextBox search = new() { PlaceholderText = "Search notifications" };
    private readonly MenuFlyoutItem markAll = new() { Text = "Mark All as Read" };
    private readonly MenuFlyoutItem clearAll = new() { Text = "Clear All" };

    /// <summary>What was last drawn, so a change that is not to the history or the search draws nothing.</summary>
    private (NotificationState? State, string Query, int Shown) rendered;

    /// <summary>Rows built at once: the newest fifty, then a hundred more each time Show Earlier is pressed.</summary>
    private int visibleCount = PageSize;

    private const int PageSize = 50;

    public NotificationCenterWindow(NotificationPresenter presenter)
    {
        this.presenter = presenter;
        Title = "Notifications";

        var title = new TextBlock { Text = "Notifications", FontSize = 22, FontWeight = FontWeights.SemiBold };
        var heading = new StackPanel { Spacing = 2 };
        heading.Children.Add(title);
        heading.Children.Add(unread);

        markAll.Click += (_, _) => presenter.Store.MarkAllRead(DateTimeOffset.Now);
        clearAll.Click += (_, _) => ClearAll();
        var more = new MenuFlyout();
        more.Items.Add(markAll);
        more.Items.Add(clearAll);
        var menu = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 14 },
            Flyout = more,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(menu, "More");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(menu, "More actions");

        var header = new Grid { Padding = new Thickness(18, 40, 14, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(heading);
        Grid.SetColumn(menu, 1);
        header.Children.Add(menu);

        search.Margin = new Thickness(18, 0, 18, 12);
        search.TextChanged += (_, _) =>
        {
            visibleCount = PageSize;
            Rebuild();
        };

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(header);
        Grid.SetRow(search, 1);
        body.Children.Add(search);
        var scroller = new ScrollViewer { Content = list };
        Grid.SetRow(scroller, 2);
        body.Children.Add(scroller);
        Content = body;

        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        Interop.WindowPlacement.SizeAndCentre(this, 420, 600);
        Interop.WindowIcon.Apply(this);
        Interop.WindowPlacement.FixSize(this);

        // A small panel to read, not a document: no minimise, no maximise — close is the only way out, and it hides.
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenterStyle)
        {
            presenterStyle.IsMaximizable = false;
            presenterStyle.IsMinimizable = false;
        }

        Onboarding.ProcessLifetime.KeepAlive(this);

        presenter.Changed += () =>
        {
            if (AppWindow.IsVisible)
            {
                Rebuild();
            }
        };
        Rebuild();
    }

    /// <summary>Opens the window, or brings it forward, in the middle of the display the pointer is on.</summary>
    public void Present()
    {
        bool wasOpen = AppWindow.IsVisible;
        if (!wasOpen)
        {
            Interop.WindowPlacement.SizeAndCentre(this, 420, 600);
            visibleCount = PageSize;
        }

        Rebuild();
        AppWindow.Show();
        Interop.WindowPlacement.BringToFront(this);
        Diagnostics.Log("notification center opened");
    }

    private void Rebuild()
    {
        NotificationState state = presenter.Store.State;
        string trimmed = search.Text.Trim();
        if (ReferenceEquals(rendered.State, state) && rendered.Query == trimmed && rendered.Shown == visibleCount)
        {
            return;
        }

        rendered = (state, trimmed, visibleCount);
        int unreadCount = state.History.Count(item => !item.IsRead);
        unread.Text = unreadCount == 0 ? "All caught up" : $"{unreadCount} unread";
        markAll.IsEnabled = unreadCount > 0;
        clearAll.IsEnabled = state.History.Count > 0;

        string query = search.Text.Trim();
        List<NotificationItem> items = [.. state.History.Where(item => query.Length == 0
            || item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Highlights.Any(line => line.Contains(query, StringComparison.CurrentCultureIgnoreCase)))];

        list.Children.Clear();
        if (state.History.Count == 0)
        {
            list.Children.Add(Empty("No notifications", "Updates and news from Hangly show up here."));
            return;
        }

        if (items.Count == 0)
        {
            list.Children.Add(Empty("No results", $"Nothing matches “{query}”."));
            return;
        }

        List<NotificationItem> page = [.. items.Take(visibleCount)];
        Section("NEW", [.. page.Where(item => !item.IsRead)]);
        Section("EARLIER", [.. page.Where(item => item.IsRead)]);
        if (items.Count > page.Count)
        {
            var more = new HyperlinkButton
            {
                Content = $"Show earlier ({items.Count - page.Count} more)",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
            };
            more.Click += (_, _) =>
            {
                visibleCount += 100;
                Rebuild();
            };
            list.Children.Add(more);
        }
    }

    private void Section(string title, List<NotificationItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        list.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            CharacterSpacing = 80,
            Opacity = 0.45,
            Margin = new Thickness(6, 8, 0, 0),
        });
        foreach (NotificationItem item in items)
        {
            list.Children.Add(Row(item));
        }
    }

    private UIElement Row(NotificationItem item)
    {
        bool update = item.Kind == NotificationKind.Update;
        var badge = new Grid { Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Top };
        badge.Children.Add(new Ellipse { Fill = new SolidColorBrush(Accent(update)) });
        badge.Children.Add(new FontIcon
        {
            Glyph = update ? "" : "",
            FontSize = 13,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        });

        var text = new StackPanel { Spacing = 3 };
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 13,
            FontWeight = item.IsRead ? FontWeights.Medium : FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var when = new TextBlock { Text = Relative(item.ReceivedAt), FontSize = 11, Opacity = 0.45, Margin = new Thickness(8, 1, 0, 0) };
        Grid.SetColumn(when, 1);
        titleRow.Children.Add(when);
        text.Children.Add(titleRow);
        text.Children.Add(new TextBlock { Text = item.Message, FontSize = 12, Opacity = 0.68, TextWrapping = TextWrapping.Wrap });
        if (item.Highlights.Count > 0)
        {
            text.Children.Add(new TextBlock
            {
                Text = string.Join("\n", item.Highlights.Select(line => $"• {line}")),
                FontSize = 11.5,
                Opacity = 0.68,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (item.ButtonTitle is { } label)
        {
            Button action = Branding.HanglyButtons.Primary(label, height: 30);
            action.HorizontalAlignment = HorizontalAlignment.Left;
            action.Margin = new Thickness(0, 6, 0, 0);
            action.FontSize = 12.5;
            action.Click += (_, _) => presenter.Open(item);
            text.Children.Add(action);
        }

        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(badge);
        Grid.SetColumn(text, 1);
        content.Children.Add(text);

        var card = new Grid();
        card.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = content,
        });
        if (!item.IsRead)
        {
            card.Children.Add(new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(Accent(true)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 5, 0, 0),
            });
        }

        card.Tapped += (_, _) => presenter.Store.MarkRead(item.Id, DateTimeOffset.Now);
        if (!item.IsRead)
        {
            var markRead = new MenuFlyoutItem { Text = "Mark as Read" };
            markRead.Click += (_, _) => presenter.Store.MarkRead(item.Id, DateTimeOffset.Now);
            var flyout = new MenuFlyout();
            flyout.Items.Add(markRead);
            card.ContextFlyout = flyout;
        }

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card, $"{(item.IsRead ? string.Empty : "Unread. ")}{item.Title}. {item.Message}");
        return card;
    }

    private static UIElement Empty(string title, string detail)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 120, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new FontIcon { Glyph = "", FontSize = 28, Opacity = 0.4 });
        panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = detail, FontSize = 12, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center });
        return panel;
    }

    private void ClearAll()
    {
        foreach (NotificationItem item in presenter.Store.State.History)
        {
            HanglyAnalytics.Log(AnalyticsEvent.NotificationDismissed(item.Kind, item.Id, DismissReason.Cleared));
            if (item.Kind == NotificationKind.Broadcast)
            {
                HanglyAnalytics.Log(AnalyticsEvent.BroadcastDismissed(item.Id, DismissReason.Cleared));
            }
        }

        presenter.Store.ClearAll();
        Diagnostics.Log("notifications cleared");
    }

    private Windows.UI.Color Accent(bool brand)
    {
        bool dark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
        return brand
            ? dark ? Windows.UI.Color.FromArgb(255, 0x8F, 0x7E, 0xFF) : Windows.UI.Color.FromArgb(255, 0x6D, 0x5A, 0xE5)
            : Windows.UI.Color.FromArgb(255, 0xA8, 0x55, 0xF7);
    }

    private static string Relative(DateTimeOffset when)
    {
        TimeSpan ago = DateTimeOffset.Now - when;
        return ago.TotalMinutes < 1 ? "now"
            : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes}m"
            : ago.TotalDays < 1 ? $"{(int)ago.TotalHours}h"
            : ago.TotalDays < 7 ? $"{(int)ago.TotalDays}d"
            : when.ToLocalTime().ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture);
    }
}
