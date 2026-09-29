//
//  ReleaseNotesWindow.cs
//  Hangly
//
//  "What's new in Hangly": waiting in the middle of the screen after an update.
//

using Hangly.App.Services;
using Hangly.Core.Analytics;
using Hangly.Core.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hangly.App.Onboarding;

/// <summary>The release notes, once, after an update to a new feature version (<see cref="ReleaseHighlights"/>).</summary>
/// <remarks>
/// Updates install themselves while the person is away (<see cref="Core.Lifecycle.UpdateTiming"/>), so this is
/// usually the first sign there was one: centred on the display under the pointer, like every Hangly window, and
/// the only thing the restart after an update opens. macOS's <c>ReleaseNotesSheet</c> in its window, with the same
/// words; Mica and the Hangly buttons, like the Enjoying Hangly card.
/// </remarks>
public sealed class ReleaseNotesWindow : Window
{
    private static readonly SolidColorBrush Accent = new(Windows.UI.Color.FromArgb(255, 0x6D, 0x5A, 0xE5));

    public ReleaseNotesWindow()
    {
        Title = "What's new in Hangly";

        var list = new StackPanel { Spacing = 18, Margin = new Thickness(0, 24, 0, 0) };
        foreach (Highlight item in ReleaseHighlights.Items)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var mark = new TextBlock { Text = "✦", FontSize = 14, Foreground = Accent, Margin = new Thickness(0, 2, 0, 0) };
            var words = new StackPanel { Spacing = 3 };
            words.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            words.Children.Add(new TextBlock
            {
                Text = item.Text,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            Grid.SetColumn(words, 1);
            row.Children.Add(mark);
            row.Children.Add(words);
            list.Children.Add(row);
        }

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "✦",
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Accent,
        });
        header.Children.Add(new TextBlock
        {
            Text = ReleaseHighlights.Heading,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
        });
        header.Children.Add(new TextBlock
        {
            Text = ReleaseHighlights.Lead,
            FontSize = 14,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        });

        var fullNotes = new HyperlinkButton
        {
            Content = "Full release notes",
            FontSize = 13,
            NavigateUri = new Uri(AppInfo.ReleaseNotesUrl),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
        };

        var scrolled = new StackPanel { Margin = new Thickness(36, 36, 36, 12) };
        scrolled.Children.Add(header);
        scrolled.Children.Add(list);
        scrolled.Children.Add(fullNotes);

        var footer = new StackPanel { Margin = new Thickness(36, 8, 36, 28), Spacing = 10 };
        Button carryOn = Branding.HanglyButtons.Primary("Continue", height: 44);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(carryOn, "ReleaseNotesContinueButton");
        carryOn.Click += (_, _) => ProcessLifetime.Dismiss(this);
        Button support = Branding.HanglyButtons.Secondary(Core.Settings.SupportCard.CallToAction);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(support, "ReleaseNotesSupportButton");
        var root = new Grid();
        support.Click += async (_, _) =>
        {
            HanglyAnalytics.Log(AnalyticsEvent.SupportClicked(SupportSurface.ReleaseNotes));
            try
            {
                await Customize.SupportSheet.ShowAsync(root);
            }
            catch (Exception exception)
            {
                Diagnostics.Failure("support sheet", exception);
            }
        };
        footer.Children.Add(carryOn);
        footer.Children.Add(support);

        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroller = new ScrollViewer { Content = scrolled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(footer, 1);
        root.Children.Add(scroller);
        root.Children.Add(footer);
        Content = root;

        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        Interop.WindowPlacement.SizeAndCentre(this, 480, 680);
        Interop.WindowIcon.Apply(this);
        Interop.WindowPlacement.FixSize(this);
    }
}
