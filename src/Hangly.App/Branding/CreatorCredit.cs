//
//  CreatorCredit.cs
//  Hangly
//
//  Who made this, said the same way everywhere.
//

using Hangly.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hangly.App.Branding;

/// <summary>The creator credit, built once and used by every window that shows one.</summary>
/// <remarks>
/// <b>One factory rather than seven copies.</b> The credit appears in the navigation
/// pane, on the About page and on the Enjoying Hangly card, and the whole point of it is
/// that it reads as one voice in each. Four hand-written copies
/// drift: one gets bolder, one loses the handle, one links somewhere slightly different.
///
/// <para><b>Quiet by construction.</b> "Created by" is caption-sized and dimmed; only the
/// handle is emphasised, and only by being a link rather than by being bold. Nothing here
/// asks for anything: support is offered by the cards' own buttons.</para>
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
}
