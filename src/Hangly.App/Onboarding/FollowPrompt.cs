//
//  FollowPrompt.cs
//  Hangly
//
//  The card that asks, once in a while, whether you want to follow along.
//

using Hangly.App.Services;
using Hangly.Core.Analytics;
using Hangly.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hangly.App.Onboarding;

/// <summary>Whether the card was answered, and how.</summary>
/// <remarks>The macOS type is <c>FollowPromptAnswer</c>, with these three cases.</remarks>
public enum FollowAnswer
{
    /// <summary>Closed without choosing. Asked again later.</summary>
    Dismissed,

    /// <summary>"Maybe later". Asked again later.</summary>
    MaybeLater,

    /// <summary>"Follow". Never asked again.</summary>
    Followed,
}

/// <summary>The follow card, and the rules about when it appears.</summary>
/// <remarks>
/// The macOS counterparts are <c>FollowPrompt</c>, <c>FollowPromptPresenter</c> and
/// <c>FollowPromptWindowController</c>, with three settings behind them —
/// <c>hasSeenFollowPrompt</c>, <c>isFollowPromptSilenced</c> and
/// <c>followPromptShownAtLaunch</c> — and four analytics events, all of whose names this
/// build already carried.
///
/// <para><b>What is quoted and what is not.</b> The structure, the settings and the event
/// names are read from the shipping binary. The card's own words are <em>not</em> in it as
/// literals, so the copy below is written here in the app's voice and is the one part of
/// this that is not parity. It is marked as such in Docs/LIBRARY-AUDIT.md.</para>
///
/// <para><b>Why a launch number and not a charm count.</b> macOS records the launch the
/// card was last shown at, which is what makes "maybe later" mean later rather than never:
/// the card returns a set number of launches afterwards. The two thresholds below are the
/// inferred part — macOS's own numbers are not recoverable from strings — and they are
/// deliberately unhurried, because a card that asks to be followed is asking for a favour
/// and should do it rarely.</para>
/// </remarks>
public sealed class FollowPrompt : Window
{
    /// <summary>The earliest launch the card may appear at.</summary>
    /// <remarks>
    /// Not the first, and not the second. Someone who has opened Hangly three times has
    /// decided to keep it, and that is the earliest moment asking is anything other than
    /// presumptuous.
    /// </remarks>
    public const int FirstLaunch = 3;

    /// <summary>How many launches pass before "maybe later" is asked again.</summary>
    public const int LaunchesBetween = 10;

    private readonly SettingsStore store;
    private readonly AnalyticsManager analytics;
    private FollowAnswer answer = FollowAnswer.Dismissed;
    private bool recorded;

    public FollowPrompt(SettingsStore store, AnalyticsManager analytics)
    {
        this.store = store;
        this.analytics = analytics;

        Title = "Hangly";

        var follow = new Button
        {
            Content = "Follow",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(follow, "FollowButton");

        var later = new Button { Content = "Maybe later" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(later, "FollowLaterButton");

        follow.Click += (_, _) => Answer(FollowAnswer.Followed);
        later.Click += (_, _) => Answer(FollowAnswer.MaybeLater);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(later);
        buttons.Children.Add(follow);

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(28) };
        body.Children.Add(new TextBlock
        {
            Text = "Enjoying Hangly?",
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });
        body.Children.Add(new TextBlock
        {
            Text = "It is made by one person. New charms, and whatever gets built next, "
                + "turn up on Instagram first.",
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "@sharan.created.this",
            Opacity = 0.7,
            Margin = new Thickness(0, 4, 0, 8),
        });
        body.Children.Add(buttons);

        Content = body;

        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = Interop.NativeMethods.GetDpiForWindow(handle) / 96.0;
        if (scale <= 0)
        {
            scale = 1;
        }

        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)Math.Round(460 * scale),
            (int)Math.Round(300 * scale)));

        // Closing by the title bar is an answer too, and the one macOS calls dismissed.
        Closed += (_, _) => Record();
    }

    /// <summary>Whether the card is due.</summary>
    /// <remarks>
    /// Pure and static so the rule can be tested without a window, which is the only way
    /// "asked again ten launches later" is checkable at all.
    /// </remarks>
    public static bool IsDue(AppSettings settings)
    {
        if (settings.IsFollowPromptSilenced)
        {
            return false;
        }

        // Never while onboarding is still owed: the first thing someone sees should not
        // be two windows asking for things.
        if (WelcomeWindow.IsNeeded(settings))
        {
            return false;
        }

        int launches = settings.Milestones.LaunchCount;
        if (launches < FirstLaunch)
        {
            return false;
        }

        return !settings.HasSeenFollowPrompt
            || launches - settings.FollowPromptShownAtLaunch >= LaunchesBetween;
    }

    /// <summary>Records that the card was shown, and when.</summary>
    public void Shown()
    {
        analytics.Track(Events.FollowPopupShown);
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

        switch (answer)
        {
            case FollowAnswer.Followed:
                analytics.Track(Events.FollowPopupFollowClicked);
                analytics.Track(Events.FollowInstagramClicked);
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(AppInfo.InstagramUrl));

                // Someone who followed is never asked again. That is the whole of what
                // silencing is for.
                store.Update(settings => settings with { IsFollowPromptSilenced = true });
                break;

            case FollowAnswer.MaybeLater:
                analytics.Track(Events.FollowPopupMaybeLater);
                break;

            default:
                analytics.Track(Events.FollowPopupDismissed);
                break;
        }

        Diagnostics.Log($"follow card answered: {answer}");
    }
}
