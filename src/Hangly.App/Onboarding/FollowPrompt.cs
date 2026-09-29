//
//  FollowPrompt.cs
//  Hangly
//
//  "Enjoying Hangly?": the support card, every third launch.
//

using Hangly.App.Services;
using Hangly.Core.Analytics;
using Hangly.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hangly.App.Onboarding;

/// <summary>How the card was closed. None of them stops it coming back on the next third launch.</summary>
/// <remarks>The macOS type is <c>FollowPromptAnswer</c>.</remarks>
public enum FollowAnswer
{
    /// <summary>Continue, or closed by the title bar.</summary>
    Dismissed,

    /// <summary>Opened Instagram.</summary>
    Followed,
}

/// <summary>The "Enjoying Hangly?" support card, and when it appears.</summary>
/// <remarks>
/// The macOS counterparts are <c>FollowPrompt</c>, <c>FollowPromptPresenter</c> and
/// <c>FollowPromptWindowController</c>: the same title, words and buttons in the same
/// order — the call to action first, then Instagram, then Continue.
///
/// <para><b>When.</b> Every third launch, from the third: 3, 6, 9, 12… The rule and the
/// words are in Core (<see cref="SupportCard"/>) so they are tested, and so the two
/// platforms can be checked against each other.</para>
///
/// <para>The creator credit here is the line alone, not the panel the welcome card uses:
/// that panel carries the same call to action, and one card offering it twice reads as
/// pressure.</para>
/// </remarks>
public sealed class FollowPrompt : Window
{
    private readonly SettingsStore store;

    private FollowAnswer answer = FollowAnswer.Dismissed;

    private bool recorded;

    public FollowPrompt(SettingsStore store)
    {
        this.store = store;
        Title = "Hangly";

        // Four groups with room between them, largest first: the question; who made it; the
        // sentence; the buttons. macOS's rhythm, number for number — 14 between the title
        // and the credit, 28 before the sentence, 32 before the buttons, a 36-point margin —
        // centred in a card as tall as the support sheet needs (SupportSheet.QrSide).
        var body = new StackPanel
        {
            Margin = new Thickness(36),
            VerticalAlignment = VerticalAlignment.Center,
        };

        Button support = Branding.HanglyButtons.Primary(SupportCard.CallToAction, height: 48);
        support.Margin = new Thickness(0, 32, 0, 0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(support, "FollowSupportButton");
        support.Click += async (_, _) =>
        {
            HanglyAnalytics.Log(AnalyticsEvent.SupportClicked(SupportSurface.Card));
            try
            {
                await Customize.SupportSheet.ShowAsync(body);
            }
            catch (Exception exception)
            {
                Diagnostics.Failure("support sheet", exception);
            }
        };

        Button follow = Branding.HanglyButtons.Secondary("Instagram");
        follow.Margin = new Thickness(0, 12, 0, 0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(follow, "FollowButton");
        follow.Click += (_, _) => Answer(FollowAnswer.Followed);

        var carryOn = new HyperlinkButton
        {
            Content = "Continue",
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin = new Thickness(0, 8, 0, 0),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(carryOn, "FollowContinueButton");
        carryOn.Click += (_, _) => Answer(FollowAnswer.Dismissed);

        // Hangly's mark in Hangly's colour, as on macOS.
        body.Children.Add(new TextBlock
        {
            Text = "\u2726",
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x6D, 0x5A, 0xE5)),
        });
        body.Children.Add(new TextBlock
        {
            Text = "Enjoying Hangly?",
            FontSize = 26,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
        });

        // "Created by" quiet and small; the handle is the line that matters.
        body.Children.Add(new TextBlock
        {
            Text = "CREATED BY",
            FontSize = 11,
            CharacterSpacing = 120,
            Opacity = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0),
        });
        body.Children.Add(new TextBlock
        {
            Text = AppInfo.CreatorHandle,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        });

        TextBlock message = Customize.SupportSheet.MessageBlock(balanced: true);
        message.Margin = new Thickness(0, 28, 0, 0);
        body.Children.Add(message);
        body.Children.Add(support);
        body.Children.Add(follow);
        body.Children.Add(carryOn);
        Content = body;

        // Mica, Windows 11's own window material, rather than a flat fill, running up under
        // the caption buttons so the card has no separate title bar — as the macOS card.
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;

        // Tall enough for the support sheet, which opens inside this window and can be no
        // taller than it (SupportSheet.QrSide).
        Interop.WindowPlacement.SizeAndCentre(this, 440, 600);
        Interop.WindowIcon.Apply(this);
        Interop.WindowPlacement.FixSize(this);

        // Closing by the title bar is an answer too, and the one macOS calls dismissed.
        Closed += (_, _) => Record();
    }

    /// <summary>Whether the card is due: every third launch (<see cref="SupportCard.IsDue"/>).</summary>
    public static bool IsDue(AppSettings settings) =>
        // Never while onboarding is still owed: the first thing someone sees should not
        // be two windows asking for things.
        !WelcomeWindow.IsNeeded(settings) && SupportCard.IsDue(settings);

    /// <summary>Records that the card was shown, and when.</summary>
    public void Shown()
    {
        int launch = store.Settings.Milestones.LaunchCount;
        store.Update(settings => settings with
        {
            HasSeenFollowPrompt = true,
            FollowPromptShownAtLaunch = launch,
        });
    }

    private void Answer(FollowAnswer chosen)
    {
        answer = chosen;
        Record();
        ProcessLifetime.Dismiss(this);
    }

    /// <summary>
    /// Writes the answer exactly once.
    /// </summary>
    /// <remarks>
    /// Guarded because both paths reach here: pressing a button records and then closes,
    /// and closing records. Without the guard, every button press would report twice —
    /// which is the specific thing the verification for this asks about.
    /// </remarks>
    private void Record()
    {
        if (recorded)
        {
            return;
        }

        recorded = true;

        if (answer == FollowAnswer.Followed)
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(AppInfo.InstagramUrl));
        }

        Diagnostics.Log($"follow card answered: {answer}");
    }
}
