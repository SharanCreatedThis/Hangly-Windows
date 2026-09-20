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
            IsTextSelectionEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(address, "CoffeeUpiId");

        var copied = new TextBlock
        {
            Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "Copied",
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(copied, "CoffeeCopied");

        var copy = new Button { Content = "Copy UPI ID", HorizontalAlignment = HorizontalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(copy, "CoffeeCopyButton");
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

        var open = new Button { Content = "Open payment page", HorizontalAlignment = HorizontalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(open, "CoffeeOpenButton");
        open.Click += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(AppInfo.CoffeeUrl));

        var body = new StackPanel { Spacing = 12, MinWidth = 300 };
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
                    Width = 220,
                    Height = 220,
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

        body.Children.Add(address);
        body.Children.Add(copy);
        body.Children.Add(copied);
        body.Children.Add(open);

        var sheet = new ContentDialog
        {
            XamlRoot = root.XamlRoot,
            Title = "Support the Creator",
            Content = body,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(sheet, "CoffeeSheet");

        await sheet.ShowAsync();
    }
}
