//
//  SupportCard.cs
//  Hangly
//
//  The "Enjoying Hangly?" card: what it says, and which launches it appears on.
//

namespace Hangly.Core.Settings;

/// <summary>The support card's words and schedule. The same as macOS's <c>SupportSheet</c> and <c>AppMilestones</c>.</summary>
public static class SupportCard
{
    /// <summary>The call to action, the same words wherever support is offered, on both platforms.</summary>
    public const string CallToAction = "❤️ " + Title;

    /// <summary>The words without the heart: the support sheet's title, and the About page's button.</summary>
    public const string Title = "Support Hangly Development";

    /// <summary>What every support screen says, word for word the same on macOS, with <see cref="Amount"/> in bold.</summary>
    public const string Message = "Even ₹1,000 helps bring new ideas to life.";

    /// <summary><see cref="Message"/> as two balanced lines, for the card's narrow column, so no word is left on a line of its own. The same break on macOS.</summary>
    public static string BalancedMessage => Message.Replace(" new ideas", "\nnew ideas", StringComparison.Ordinal);

    /// <summary>The part of <see cref="Message"/> that is emphasised.</summary>
    public const string Amount = "₹1,000";

    /// <summary>The card appears on every launch that is a multiple of this: 3, 6, 9, 12…</summary>
    /// <remarks>
    /// Counted by <see cref="MilestoneSettings.LaunchCount"/>, which is never reset, so
    /// showing the card does not restart the count. However the card was answered, it
    /// comes back on the next third launch.
    /// </remarks>
    public const int Interval = 3;

    /// <summary>Whether this launch should show the card: launch 3, 6, 9, 12…, once each.</summary>
    public static bool IsDue(AppSettings settings)
    {
        int launch = settings.Milestones.LaunchCount;
        bool shownThisLaunch = settings.HasSeenFollowPrompt && settings.FollowPromptShownAtLaunch == launch;
        return launch >= Interval && launch % Interval == 0 && !shownThisLaunch;
    }
}
