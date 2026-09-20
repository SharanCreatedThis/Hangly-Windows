//
//  WelcomeWindow.cs
//  Hangly
//
//  The first thing anyone sees, and the only place the app asks for a name.
//

using Hangly.App.Services;
using Hangly.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hangly.App.Onboarding;

/// <summary>The welcome card, shown once, in a window of its own.</summary>
/// <remarks>
/// The macOS counterparts are <c>WelcomeCard</c>, <c>WelcomePresenter</c> and
/// <c>WelcomeWindowController</c>. The three lines of copy are quoted from the shipping
/// binary — "Welcome to Hangly", "A charm hangs from the top of your screen, on a rope,
/// and swings.", "Drag it anywhere along the top of your screen." — rather than written
/// here.
///
/// <para><b>A window rather than a dialog</b>, which macOS's name for it already implies
/// and which this build has no choice about anyway: a <c>ContentDialog</c> needs a
/// <c>XamlRoot</c>, and at first launch the only other window is the overlay, which is
/// plain Win32 and has none.</para>
///
/// <para><b>The name is the deliberate deviation.</b> macOS 2.0.0 does not ask for one;
/// 2.1 will, and both platforms will then share this flow. It is typed and never read
/// from the machine — nothing here touches the Windows account, the Microsoft account or
/// the computer name, and no code in this build does. It can be changed afterwards on the
/// Appearance page.</para>
/// </remarks>
public sealed class WelcomeWindow : Window
{
    private readonly SettingsStore store;
    private readonly TextBox name;
    private readonly Button start;

    public WelcomeWindow(SettingsStore store)
    {
        this.store = store;

        Title = "Welcome to Hangly";

        name = new TextBox
        {
            PlaceholderText = "Your name",
            MaxLength = AppSettings.DisplayNameLimit,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(name, "WelcomeName");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(name, "Your name");

        start = new Button
        {
            Content = "Get started",
            HorizontalAlignment = HorizontalAlignment.Right,

            // A required name is only required if the button says so before it is pressed
            // rather than after.
            IsEnabled = false,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(start, "WelcomeStart");

        name.TextChanged += (_, _) => start.IsEnabled = name.Text.Trim().Length > 0;
        start.Click += OnStart;

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(28) };
        body.Children.Add(new TextBlock
        {
            Text = "Welcome to Hangly",
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });
        body.Children.Add(Line("A charm hangs from the top of your screen, on a rope, and swings."));
        body.Children.Add(Line("Drag it anywhere along the top of your screen."));
        body.Children.Add(new TextBlock
        {
            Text = "What should Hangly call you?",
            Margin = new Thickness(0, 12, 0, 0),
        });
        body.Children.Add(name);
        body.Children.Add(new TextBlock
        {
            Text = "Used to greet you, and sent with usage data while that is switched on. "
                + "Hangly never reads your Windows or Microsoft account name. "
                + "You can change this later in Appearance.",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });
        body.Children.Add(start);

        Content = body;

        // AppWindow.Resize takes physical pixels, so the size has to be scaled by the
        // display's DPI or the card comes out half-size on a 200% screen and clips its
        // own copy. The same arithmetic the Customize window does, for the same reason.
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = Interop.NativeMethods.GetDpiForWindow(handle) / 96.0;
        if (scale <= 0)
        {
            scale = 1;
        }

        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)Math.Round(560 * scale),
            (int)Math.Round(480 * scale)));

        // Closing without a name writes nothing, so the card returns next launch rather
        // than leaving the app nameless. The alternative — refusing to close — traps
        // someone who opened it by accident on a machine they cannot log out of.
        Closed += (_, _) => Diagnostics.Log(
            store.Settings.DisplayName.Length > 0
                ? "welcome card completed"
                : "welcome card closed without a name; it will be shown again");
    }

    /// <summary>Whether onboarding still has to happen.</summary>
    /// <remarks>
    /// Both conditions, not just the flag. A settings file that says the card was seen but
    /// carries no name is one that was hand-edited or written by a build from before names
    /// existed; either way the app needs the name and should ask.
    /// </remarks>
    public static bool IsNeeded(AppSettings settings) =>
        !settings.HasSeenWelcome || settings.DisplayName.Length == 0;

    private void OnStart(object sender, RoutedEventArgs args)
    {
        string chosen = name.Text.Trim();
        if (chosen.Length == 0)
        {
            return;
        }

        store.Update(settings => settings with
        {
            HasSeenWelcome = true,
            DisplayName = chosen,
        });

        Close();
    }

    private static TextBlock Line(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
    };
}
