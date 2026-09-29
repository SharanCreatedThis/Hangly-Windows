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
    private readonly Grid body;
    private readonly Action? abandoned;
    private readonly WelcomeHero hero;

    /// <summary>The charm, the question, the field and Continue, and a margin under them.</summary>
    private const double NameStepHeight = 470;

    /// <summary>Tall enough for the support sheet opened from the second step.</summary>
    private const double WelcomeStepHeight = 600;

    public WelcomeWindow(
        SettingsStore store,
        Action openLibrary,
        Action? abandoned = null,
        Hangly.Core.Models.CharmIndex? charms = null)
    {
        this.store = store;
        this.openLibrary = openLibrary;
        this.abandoned = abandoned;

        Title = "Welcome to Hangly";

        // A roomier field than the default: 44 points tall, 14 in from each side, a
        // 12-point corner. The same field as the macOS card.
        name = new TextBox
        {
            PlaceholderText = "Your name",
            MaxLength = AppSettings.DisplayNameLimit,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 44,
            FontSize = 15,
            Padding = new Thickness(14, 11, 14, 11),
            CornerRadius = Branding.HanglyButtons.Corner,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 28, 0, 0),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(name, "WelcomeName");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(name, "Your name");

        start = Branding.HanglyButtons.Primary("Continue");
        start.Margin = new Thickness(0, 20, 0, 0);

        // A required name is only required if the button says so before it is pressed
        // rather than after.
        start.IsEnabled = false;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(start, "WelcomeStart");

        name.TextChanged += (_, _) => start.IsEnabled = name.Text.Trim().Length > 0;
        start.Click += OnStart;

        // The name step: the charm, one question, the field, Continue — and nothing that
        // explains the question, because somebody asked what to be called already knows
        // what the box is for. The macOS card's numbers: a 304-point column, 10 under the
        // charm, 28 to the field, 20 to the button.
        hero = new WelcomeHero(store.Settings.Overlay, charms ?? new Hangly.Core.Models.CharmIndex());
        askPanel = new StackPanel();
        askPanel.Children.Add(new TextBlock
        {
            Text = "What should we call you?",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
        });
        askPanel.Children.Add(name);
        askPanel.Children.Add(start);

        // Empty until the name is known, because the first thing it says is the name.
        welcomePanel = new StackPanel { Visibility = Visibility.Collapsed };

        // One centred 304-point column for both steps under the charm, as on macOS: the
        // card is the app, introducing itself.
        var steps = new Grid();
        steps.Children.Add(askPanel);
        steps.Children.Add(welcomePanel);
        // The charm runs the full width of the card from its very top edge, under the
        // caption buttons, where the web spreads; the step sits centred below it.
        var column = new StackPanel
        {
            Width = 304,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 0, 28),
        };
        column.Children.Add(steps);

        body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(hero.View);
        Grid.SetRow(column, 1);
        body.Children.Add(column);

        Content = body;

        // Mica, Windows 11's own window material, as on the Enjoying Hangly card.
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        ExtendsContentIntoTitleBar = true;

        // Sized to the step, as the macOS card is: the name step is short, and the second
        // step grows to 600 (ShowWelcomeStep), because the support sheet opens inside this
        // window from there and a dialog can be no taller than its host (SupportSheet.QrSide).
        Interop.WindowPlacement.SizeAndCentre(this, 560, NameStepHeight);
        Interop.WindowIcon.Apply(this);
        Interop.WindowPlacement.FixSize(this);

        // Closing this without a name closes Hangly.
        //
        // The name is not optional and the card said so by disabling its own button, but
        // the title bar's X went around that: the app carried on running, nameless, and
        // reported itself that way for the rest of the session. There is no useful state
        // between "has been asked" and "has answered", so dismissing the question is
        // declining to run rather than a way past it. The card comes back next launch,
        // which is where somebody who changed their mind will find it.
        Closed += (_, _) => hero.Dispose();

        AppWindow.Closing += (_, _) =>
        {
            if (store.Settings.DisplayName.Length > 0)
            {
                Diagnostics.Log("welcome card completed");
                return;
            }

            Diagnostics.Log("welcome card dismissed without a name; quitting");
            abandoned?.Invoke();
        };
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
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = Hangly.Core.Lifecycle.WelcomeText.Title(store.Settings.DisplayName),
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        });

        // The shared words (WelcomeText), the same as the macOS card: two lines, no list.
        panel.Children.Add(Centred(Hangly.Core.Lifecycle.WelcomeText.Tagline, new Thickness(0, 7, 0, 0)));
        panel.Children.Add(Centred(Hangly.Core.Lifecycle.WelcomeText.Invitation, new Thickness(0, 4, 0, 0)));

        // Explore first and filled, support second at the same width, the way out as text.
        Button explore = Branding.HanglyButtons.Primary(Hangly.Core.Lifecycle.WelcomeText.Explore);
        explore.Margin = new Thickness(0, 26, 0, 0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(explore, "WelcomeExplore");
        explore.Click += (_, _) =>
        {
            Diagnostics.Log("welcome: explore library");
            Hangly.Core.Analytics.HanglyAnalytics.Log(Hangly.Core.Analytics.AnalyticsEvent.WelcomeCompleted(explored: true));
            openLibrary();
            ProcessLifetime.Dismiss(this);
        };

        Button support = Branding.HanglyButtons.Secondary(SupportCard.CallToAction);
        support.Margin = new Thickness(0, 10, 0, 0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(support, "WelcomeSupport");
        support.Click += async (_, _) =>
        {
            Hangly.Core.Analytics.HanglyAnalytics.Log(Hangly.Core.Analytics.AnalyticsEvent.SupportClicked(Hangly.Core.Analytics.SupportSurface.Welcome));
            try
            {
                await Customize.SupportSheet.ShowAsync(body);
            }
            catch (Exception exception)
            {
                Diagnostics.Failure("support sheet", exception);
            }
        };

        var begin = new HyperlinkButton
        {
            Content = Hangly.Core.Lifecycle.WelcomeText.Start,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.75,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(begin, "WelcomeBegin");
        begin.Click += (_, _) =>
        {
            Diagnostics.Log("welcome: start using hangly");
            Hangly.Core.Analytics.HanglyAnalytics.Log(Hangly.Core.Analytics.AnalyticsEvent.WelcomeCompleted(explored: false));
            ProcessLifetime.Dismiss(this);
        };

        panel.Children.Add(explore);
        panel.Children.Add(support);
        panel.Children.Add(begin);

        // Where Hangly lives, and the way back that needs no tray icon: opening Hangly
        // again opens the Library (LaunchIntent). Windows usually tucks a new tray icon
        // behind the ˄ arrow, so the line says so rather than send somebody hunting.
        panel.Children.Add(new TextBlock
        {
            Text = "Hangly lives by the clock, behind the ˄ arrow if Windows has tucked it away. "
                + Hangly.Core.Lifecycle.HelpText.OpenAgain,
            Opacity = 0.55,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        });

        return panel;
    }

    private static TextBlock Centred(string text, Thickness margin) => new()
    {
        Text = text,
        Opacity = 0.7,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = margin,
    };

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
        Interop.WindowPlacement.SizeAndCentre(this, 560, WelcomeStepHeight);
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

        // The installation registry sees the nickname arrive (settings changed) and registers
        // this installation now, so the person is counted on the day they arrived.

        // The name is saved before the second step is shown, so dismissing the window from
        // here on is finishing rather than abandoning.
        ShowWelcomeStep();
        Diagnostics.Log("welcome card: name accepted, showing the welcome step");
    }

}
