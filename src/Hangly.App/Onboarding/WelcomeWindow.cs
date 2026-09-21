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
    private readonly Action openLibrary;
    private readonly TextBox name;
    private readonly Button start;
    private readonly StackPanel askPanel;
    private readonly StackPanel welcomePanel;

    public WelcomeWindow(SettingsStore store, Action openLibrary)
    {
        this.store = store;
        this.openLibrary = openLibrary;

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
            Content = "Continue",
            HorizontalAlignment = HorizontalAlignment.Right,

            // A required name is only required if the button says so before it is pressed
            // rather than after.
            IsEnabled = false,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(start, "WelcomeStart");

        name.TextChanged += (_, _) => start.IsEnabled = name.Text.Trim().Length > 0;
        start.Click += OnStart;

        askPanel = new StackPanel { Spacing = 10 };
        askPanel.Children.Add(new TextBlock
        {
            Text = "Welcome to Hangly",
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });
        askPanel.Children.Add(Line("A charm hangs from the top of your screen, on a rope, and swings."));
        askPanel.Children.Add(Line("Drag it anywhere along the top of your screen."));
        askPanel.Children.Add(Line("It takes about a minute to set up."));
        askPanel.Children.Add(new TextBlock
        {
            Text = "What should Hangly call you?",
            Margin = new Thickness(0, 12, 0, 0),
        });
        askPanel.Children.Add(name);
        askPanel.Children.Add(new TextBlock
        {
            Text = "Used to greet you, and sent with usage data while that is switched on. "
                + "Hangly never reads your Windows or Microsoft account name. "
                + "You can change this later in Appearance.",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });
        askPanel.Children.Add(start);

        // Empty until the name is known, because the first thing it says is the name.
        welcomePanel = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };

        var body = new Grid { Margin = new Thickness(28) };
        body.Children.Add(askPanel);
        body.Children.Add(welcomePanel);

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

        // Dismissing without a name writes nothing, so the card returns next launch rather
        // than leaving the app nameless.
        AppWindow.Closing += (_, _) => Diagnostics.Log(
            store.Settings.DisplayName.Length > 0
                ? "welcome card completed"
                : "welcome card dismissed without a name; it will be shown again");
    }

    /// <summary>Whether onboarding still has to happen.</summary>
    /// <remarks>
    /// Both conditions, not just the flag. A settings file that says the card was seen but
    /// carries no name is one that was hand-edited or written by a build from before names
    /// existed; either way the app needs the name and should ask.
    /// </remarks>
    public static bool IsNeeded(AppSettings settings) =>
        !settings.HasSeenWelcome || settings.DisplayName.Length == 0;

    /// <summary>
    /// The second step: what Hangly is, and two ways out of it.
    /// </summary>
    /// <remarks>
    /// <b>Why there is a second step at all.</b> The first build asked for a name and
    /// closed, and testers read that as the whole of setup — they had typed something into
    /// a box and been returned to their desktop, with no sense that anything had been
    /// installed. Several said they thought it had failed. The name is a question Hangly
    /// asks; it is not a welcome.
    ///
    /// <para>Two buttons rather than one, because the person who wants to go and look at
    /// seventy charms and the person who wants their desktop back are both in the room,
    /// and making the second one dismiss an invitation is how you annoy them.</para>
    /// </remarks>
    private StackPanel BuildWelcome()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Welcome, {store.Settings.DisplayName}",
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });
        panel.Children.Add(Line("A tiny charm that hangs from your screen."));

        foreach (string item in new[]
        {
            "Browse charms from around the world",
            "Create your own charm from a picture",
            "Change the rope, where it hangs and how big it is",
        })
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 2, 0, 2),
            };
            row.Children.Add(new TextBlock { Text = "•", Opacity = 0.6 });
            row.Children.Add(Line(item));
            panel.Children.Add(row);
        }

        // Where Hangly lives, written for Windows rather than for a Mac menu bar.
        //
        // This used to say "right-click the Hangly icon near the clock", which assumes the
        // icon is visible. On Windows 11 it is not: new notification icons go into the
        // overflow behind the chevron by default, so the sentence described something the
        // person could not see and sent them hunting. It now says where to look, that it
        // may be hidden, and how to get it out -- and the Explore Library button beside it
        // means nobody has to find the icon at all to finish setting up.
        panel.Children.Add(new TextBlock
        {
            Text = "Your charm is already hanging at the top right of your screen.",
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Hangly lives next to the clock, and Windows usually tucks new icons "
                + "away behind the ˄ arrow down there. Click the arrow to find it, and drag "
                + "it out onto the taskbar if you would like it to stay put. Right-click it "
                + "any time to change your charm, or to quit.",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
        });

        var explore = new Button { Content = "Explore Library" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(explore, "WelcomeExplore");
        explore.Click += (_, _) =>
        {
            Diagnostics.Log("welcome: explore library");
            openLibrary();
            ProcessLifetime.Dismiss(this);
        };

        var begin = new Button
        {
            Content = "Start Using Hangly",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(begin, "WelcomeBegin");
        begin.Click += (_, _) =>
        {
            Diagnostics.Log("welcome: start using hangly");
            ProcessLifetime.Dismiss(this);
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0),
        };
        buttons.Children.Add(explore);
        buttons.Children.Add(begin);
        panel.Children.Add(buttons);

        return panel;
    }

    /// <summary>Opens straight on the second step, for somebody who already has a name.</summary>
    public void SkipToWelcome() => ShowWelcomeStep();

    private void ShowWelcomeStep()
    {
        welcomePanel.Children.Clear();

        StackPanel built = BuildWelcome();
        while (built.Children.Count > 0)
        {
            UIElement child = built.Children[0];
            built.Children.RemoveAt(0);
            welcomePanel.Children.Add(child);
        }

        askPanel.Visibility = Visibility.Collapsed;
        welcomePanel.Visibility = Visibility.Visible;
    }

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

        // The name is saved before the second step is shown, so dismissing the window from
        // here on is finishing rather than abandoning.
        ShowWelcomeStep();
        Diagnostics.Log("welcome card: name accepted, showing the welcome step");
    }

    private static TextBlock Line(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
    };
}
