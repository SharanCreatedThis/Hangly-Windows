using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The support card: launch 3, 6, 9, 12…, however it was answered. The same cases as macOS's <c>FollowPromptTests</c>.</summary>
public sealed class SupportCardTests
{
    private static AppSettings AtLaunch(int launch, int? shownAt = null, bool silenced = false) => new()
    {
        HasSeenWelcome = true,
        Milestones = new MilestoneSettings { LaunchCount = launch },
        HasSeenFollowPrompt = shownAt is not null,
        FollowPromptShownAtLaunch = shownAt ?? 0,
        IsFollowPromptSilenced = silenced,
    };

    [Fact]
    public void EveryThirdLaunchFromTheThirdAndNoOther() =>
        Assert.Equal(
            [3, 6, 9, 12, 15, 18, 21, 24, 27, 30],
            Enumerable.Range(0, 31).Where(launch => SupportCard.IsDue(AtLaunch(launch))));

    [Fact]
    public void OncePerLaunch()
    {
        Assert.False(SupportCard.IsDue(AtLaunch(6, shownAt: 6)));
        Assert.True(SupportCard.IsDue(AtLaunch(9, shownAt: 6)));
    }

    [Fact]
    public void AnAnswerFromAnOlderBuildNoLongerSilencesIt() =>
        Assert.True(SupportCard.IsDue(AtLaunch(30, shownAt: 5, silenced: true)));

    [Fact]
    public void AcrossTwelveLaunchesItAppearsOnThreeSixNineAndTwelveAndTheCountIsNeverReset()
    {
        AppSettings settings = AtLaunch(0);
        List<int> shownAt = [];
        for (int launch = 1; launch <= 12; launch++)
        {
            settings = settings with { Milestones = settings.Milestones with { LaunchCount = settings.Milestones.LaunchCount + 1 } };
            if (SupportCard.IsDue(settings))
            {
                shownAt.Add(launch);
                settings = settings with { HasSeenFollowPrompt = true, FollowPromptShownAtLaunch = launch };
                Assert.False(SupportCard.IsDue(settings));
            }
        }

        Assert.Equal([3, 6, 9, 12], shownAt);
        Assert.Equal(12, settings.Milestones.LaunchCount);
    }

    [Fact]
    public void TheCallToActionAndTheMessageAreTheAgreedWords()
    {
        Assert.Equal("❤️ Support Hangly Development", SupportCard.CallToAction);
        Assert.Equal("Even ₹1,000 helps bring new ideas to life.", SupportCard.Message);
        Assert.Equal("Even ₹1,000 helps bring\nnew ideas to life.", SupportCard.BalancedMessage);

        // The bold run is cut out of the sentence by this, so it has to be in it, once.
        Assert.Equal("₹1,000", SupportCard.Amount);
        Assert.Equal(SupportCard.Message.IndexOf(SupportCard.Amount, StringComparison.Ordinal), SupportCard.Message.LastIndexOf(SupportCard.Amount, StringComparison.Ordinal));
        Assert.True(SupportCard.Message.IndexOf(SupportCard.Amount, StringComparison.Ordinal) > 0);
    }
}
