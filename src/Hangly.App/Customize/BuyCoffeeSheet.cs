//
//  BuyCoffeeSheet.cs
//  Hangly
//
//  The creator's UPI code, shown rather than linked to.
//

using Hangly.App.Services;
using Hangly.Core.Analytics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Hangly.App.Customize;

/// <summary>The coffee sheet: a QR to scan, an address to copy, a link to open.</summary>
/// <remarks>
/// macOS ships this as <c>BuyCoffeeSheet</c> around <c>CreatorUPIQR.png</c>, and Windows
/// ships the same file. That is the whole reason this is a sheet and not a hyperlink: a
/// UPI code is scanned off the screen by a phone, so the thing that has to happen is that
/// the picture appears, not that a browser opens.
///
/// <para><b>Why the button opens a web page rather than a <c>upi:</c> link.</b> The
/// deep link is what macOS would hand to a phone, and Windows has no handler registered
/// for that scheme — following it raises the "how do you want to open this?" chooser and
/// then nothing. The address is right there to copy, and the button goes to the page that
/// works on a desktop.</para>
///
/// <para>The three analytics events this fires — <c>coffee_sheet_opened</c>,
/// <c>coffee_qr_viewed</c> and <c>coffee_copy_upi</c> — were defined when the analytics
/// table was ported and have been listed in STATUS.md as "defined but never fired" since.
/// They fire now, with the same names macOS uses.</para>
/// </remarks>
internal static class BuyCoffeeSheet
{
    /// <summary>Where the QR lands beside the executable.</summary>
    private static string QrPath => Path.Combine(AppContext.BaseDirectory, "Assets", "CreatorUPIQR.png");

    /// <summary>How large the code can be over this host without crowding it out.</summary>
    /// <remarks>
    /// The rest of the sheet — the dialog's own chrome, a title, a line of thanks, the
    /// address, two buttons and their spacing — measured at about 390 points over the
    /// follow card, and a dialog cannot exceed its host. What is left over is the code's.
    /// The number is measured rather than guessed, twice: at 240 the payment link was off
    /// the bottom edge entirely, and at 330 it was underneath the dialog's own button
    /// strip with only its top edge showing.
    /// </remarks>
    private static double QrSide(FrameworkElement root)
    {
        double available = root.XamlRoot?.Size.Height ?? 0;
        return available <= 0 ? 150 : Math.Clamp(available - 390, 100, 190);
    }

    /// <summary>Shows the sheet over <paramref name="root"/>.</summary>
    /// <param name="source">Which surface asked, which is all the events record.</param>
    public static async Task ShowAsync(
        FrameworkElement root,
        AnalyticsManager analytics,
        string source)
    {
        analytics.Track(Events.CoffeeSheetOpened(source));

        var address = new TextBlock
        {
            Text = AppInfo.UpiId,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        };

        var copied = new TextBlock
        {
            Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "Copied",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(copied, "CoffeeCopied");

        // The address is the button.
        //
        // It was a line of text with a "Copy UPI ID" button under it, which is two
        // controls for one idea and one row of height this sheet does not have to spare
        // over a small window. Clicking the thing you want is what everybody tries first.
        var copy = new Button
        {
            Content = address,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(14, 8, 14, 8),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(copy, "CoffeeUpiId");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(copy, $"Copy UPI ID {AppInfo.UpiId}");
        ToolTipService.SetToolTip(copy, "Click to copy");
        copy.Click += (_, _) =>
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(AppInfo.UpiId);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

            // Said rather than animated away: the confirmation is the point, and a
            // toast that fades needs a timer this sheet would have to own and cancel.
            copied.Opacity = 1;
            analytics.Track(Events.CoffeeCopyUpi(source));
        };

        var body = new StackPanel { Spacing = 10, MinWidth = 260 };
        body.Children.Add(new TextBlock
        {
            Text = "Thank you for using Hangly.",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (File.Exists(QrPath))
        {
            body.Children.Add(new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image
                {
                    // Sized to the window it opens over.
                    //
                    // A ContentDialog is laid out inside its host's XamlRoot and can be
                    // no taller than it. This sheet opens over three windows of very
                    // different sizes, and a fixed code that suited the Library pushed
                    // the address and the payment link off the bottom of the follow card
                    // — not scrolled, cut off. A scroller was tried and only turned a
                    // clipped sheet into a scrolling one, which is not what anybody wants
                    // from a QR code.
                    //
                    // A third of the host's height, floored so it stays scannable and
                    // capped so it does not dominate the Library's.
                    Width = QrSide(root),
                    Height = QrSide(root),
                    Source = new BitmapImage(new Uri(QrPath)),
                },
            });

            analytics.Track(Events.CoffeeQrViewed(source));
        }
        else
        {
            // Reported rather than hidden: a coffee sheet with no code in it is a
            // packaging failure, and a blank space would not say so.
            Diagnostics.Log($"coffee QR missing at {QrPath}");
            body.Children.Add(new TextBlock
            {
                Text = "The QR code is missing from this build. The address below still works.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        body.Children.Add(copy);
        body.Children.Add(copied);

        // The payment link is the dialog's own button rather than one more control in the
        // body.
        //
        // A ContentDialog clips its content rather than shrinking it, and this sheet opens
        // over windows as small as the follow card. Shrinking the code was tried twice and
        // only moved which control ended up underneath the dialog's button strip. The
        // button strip is the one part that is always laid out and never clipped, so the
        // one action that must never go missing lives there.
        var sheet = new ContentDialog
        {
            XamlRoot = root.XamlRoot,
            Title = "Support the Creator",
            Content = body,
            PrimaryButtonText = "Open payment page",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
        };
        sheet.PrimaryButtonClick += (_, _) =>
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(AppInfo.CoffeeUrl));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(sheet, "CoffeeSheet");

        await sheet.ShowAsync();
    }
}
