//
//  CreatorCredit.cs
//  Hangly
//
//  Who made this, said the same way everywhere.
//

using Hangly.App.Services;
using Hangly.Core.Analytics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hangly.App.Branding;

/// <summary>The creator credit, built once and used by every window that shows one.</summary>
/// <remarks>
/// <b>One factory rather than seven copies.</b> The credit appears in the navigation
/// pane, on the About page, on the welcome card and on the follow card, and the whole
/// point of it is that it reads as one voice in four places. Four hand-written copies
/// drift: one gets bolder, one loses the handle, one links somewhere slightly different.
///
/// <para><b>Quiet by construction.</b> "Created by" is caption-sized and dimmed; only the
/// handle is emphasised, and only by being a link rather than by being bold. Nothing here
/// asks for anything — the coffee button exists because it already existed on the About
/// page, and it keeps the wording it already had.</para>
///
/// <para>The About page's card is written in the markup instead, because it is a laid-out
/// panel rather than a line of text and XAML is where the rest of that page lives.</para>
/// </remarks>
public static class CreatorCredit
{
    /// <summary>"Created by @sharan.created.this", as one line.</summary>
    /// <remarks>
    /// The handle is the link, not the whole sentence: "Created by" is not somewhere to
    /// go, and underlining it would make the line look like a banner.
    /// </remarks>
    public static FrameworkElement Line()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        row.Children.Add(new TextBlock
        {
            Text = "Created by",
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });

        var handle = new HyperlinkButton
        {
            Content = AppInfo.CreatorHandle,
            NavigateUri = new Uri(AppInfo.InstagramUrl),

            // Pulled back against the label so the two read as one sentence. A
            // HyperlinkButton carries button padding it does not need here.
            Padding = new Thickness(6, 0, 0, 0),
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(handle, "CreatorHandle");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(handle, "Creator on Instagram");
        handle.Resources["ContentControlThemeFontFamily"] = handle.FontFamily;
        handle.FontSize = 12;

        row.Children.Add(handle);
        return row;
    }

    /// <summary>The credit and its two actions, on one line, for the small windows.</summary>
    /// <param name="root">What the coffee sheet opens over.</param>
    /// <param name="source">Which surface asked, which is all the coffee event records.</param>
    /// <remarks>
    /// Website and coffee, and nothing else. Instagram is already here — it is what the
    /// handle links to — and a third would turn a credit into a row of calls to action,
    /// which is the one thing this is not allowed to become. Coffee is the accented
    /// button and the website is a link, which is the same weighting the About page's
    /// creator card uses.
    /// </remarks>
    /// <param name="ownAccent">
    /// Give the coffee button a green of its own instead of the system accent.
    /// </param>
    /// <param name="stacked">
    /// Put the coffee button on its own line under the credit, for a window too narrow
    /// to hold the three of them side by side.
    /// </param>
    public static FrameworkElement Panel(
        FrameworkElement root,
        AnalyticsManager analytics,
        string source,
        bool ownAccent = false,
        bool stacked = false)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 14, 0, 0),
        };

        panel.Children.Add(new Border
        {
            Height = 1,
            Opacity = 0.5,
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                .Resources["DividerStrokeColorDefaultBrush"],
        });

        // One row, not two.
        //
        // The credit was a line with the actions stacked under it, and on the welcome
        // card — 560 by 480 points, and not allowed to grow — that second row fell off
        // the bottom edge. The follow card is smaller still. A credit that costs two rows
        // is a credit that has to be fitted in; one row goes wherever it is put.
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var website = new HyperlinkButton
        {
            Content = "Website",
            NavigateUri = new Uri(AppInfo.CreatorSiteUrl),
            FontSize = 12,
            MinHeight = 0,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(website, "CreatorWebsite");

        // The accent button, the same one the About page's creator card carries. It was a
        // link here, which made the same offer look like two different things depending
        // on which window you were in.
        var coffee = new Button
        {
            Content = "Buy Creator a Coffee",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (ownAccent)
        {
            // Green, because on the welcome card the accent is already spoken for: Explore
            // Library is the accented button there, and two accented buttons on one row
            // read as one decision with two answers rather than as an action and an aside.
            //
            // Set as the button's own resources rather than as its Background, so hover
            // and pressed follow it. Assigning Background alone leaves a green button that
            // turns blue under the pointer.
            coffee.Resources["AccentButtonBackground"] = Green(0x10, 0x7C, 0x10);
            coffee.Resources["AccentButtonBackgroundPointerOver"] = Green(0x0E, 0x6E, 0x0E);
            coffee.Resources["AccentButtonBackgroundPressed"] = Green(0x0B, 0x5C, 0x0B);
            coffee.Resources["AccentButtonBorderBrush"] = Green(0x10, 0x7C, 0x10);
            coffee.Resources["AccentButtonBorderBrushPointerOver"] = Green(0x0E, 0x6E, 0x0E);
            coffee.Resources["AccentButtonBorderBrushPressed"] = Green(0x0B, 0x5C, 0x0B);
        }
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(coffee, "CreatorCoffee");
        coffee.Click += async (_, _) =>
        {
            try
            {
                await Customize.BuyCoffeeSheet.ShowAsync(root, analytics, source);
            }
            catch (Exception exception)
            {
                // A credit that cannot open a dialog must not take the window with it.
                Diagnostics.Failure("coffee sheet", exception);
            }
        };

        actions.Children.Add(Line());
        actions.Children.Add(website);

        if (stacked)
        {
            // The follow card is 460 points wide and the three of them together are wider
            // than that, so the coffee button ran off the right edge with its last word
            // outside the window. Under the credit it has the whole width.
            coffee.Margin = new Thickness(0, 10, 0, 0);
            coffee.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(actions);
            panel.Children.Add(coffee);
            return panel;
        }

        actions.Children.Add(coffee);
        panel.Children.Add(actions);
        return panel;
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush Green(byte red, byte green, byte blue) =>
        new(Windows.UI.Color.FromArgb(255, red, green, blue));
}
