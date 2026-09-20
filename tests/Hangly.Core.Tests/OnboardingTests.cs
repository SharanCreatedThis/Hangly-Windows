//
//  OnboardingTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The rules behind the welcome card and the follow card.</summary>
/// <remarks>
/// The rules live in Hangly.App because they need its windows, so what is asserted here
/// is the settings shape they read and write. The decision functions themselves are
/// exercised by the UI suite; these are the invariants that must hold whatever the UI
/// does, and the ones a hand-edited settings file can break.
/// </remarks>
public class OnboardingTests
{
    [Fact(DisplayName = "A name is kept, trimmed, and bounded")]
    public void DisplayNameIsTrimmedAndBounded()
    {
        Assert.Equal("Sharan", new AppSettings { DisplayName = "  Sharan  " }.Clamped().DisplayName);
        Assert.Equal(string.Empty, new AppSettings { DisplayName = "   " }.Clamped().DisplayName);

        string long_ = new('a', 200);
        Assert.Equal(AppSettings.DisplayNameLimit, new AppSettings { DisplayName = long_ }.Clamped().DisplayName.Length);
    }

    [Fact(DisplayName = "A name is never invented from the machine")]
    public void DisplayNameDefaultsToEmpty()
    {
        // The default is empty, not the OS user, not the machine name, not anything
        // read from the environment. Onboarding is the only thing that fills it.
        Assert.Equal(string.Empty, new AppSettings().DisplayName);
        Assert.NotEqual(Environment.UserName, new AppSettings().DisplayName);
    }

    [Fact(DisplayName = "The follow card's three answers are three different states")]
    public void FollowAnswersAreDistinct()
    {
        var fresh = new AppSettings();
        Assert.False(fresh.HasSeenFollowPrompt);
        Assert.False(fresh.IsFollowPromptSilenced);
        Assert.Equal(0, fresh.FollowPromptShownAtLaunch);

        // "Maybe later" records that it was shown, at a launch, and does not silence it.
        var later = fresh with { HasSeenFollowPrompt = true, FollowPromptShownAtLaunch = 3 };
        Assert.False(later.IsFollowPromptSilenced);

        // "Follow" silences it for good.
        var followed = later with { IsFollowPromptSilenced = true };
        Assert.True(followed.IsFollowPromptSilenced);
    }

    [Fact(DisplayName = "Onboarding settings survive a round trip through the document")]
    public void OnboardingSurvivesSerialisation()
    {
        var before = new AppSettings
        {
            DisplayName = "Sharan",
            HasSeenWelcome = true,
            HasSeenFollowPrompt = true,
            FollowPromptShownAtLaunch = 7,
            IsFollowPromptSilenced = true,
        };

        string json = System.Text.Json.JsonSerializer.Serialize(before, AppSettings.JsonOptions);
        AppSettings after = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions)!;

        Assert.Equal("Sharan", after.DisplayName);
        Assert.True(after.HasSeenWelcome);
        Assert.True(after.HasSeenFollowPrompt);
        Assert.Equal(7, after.FollowPromptShownAtLaunch);
        Assert.True(after.IsFollowPromptSilenced);
    }

    [Fact(DisplayName = "A document written before names existed still loads")]
    public void OlderDocumentsLoad()
    {
        const string json = """{"schemaVersion":1,"hasSeenWelcome":true,"overlay":{"charmIds":["nazar"]}}""";
        AppSettings settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            json, AppSettings.JsonOptions)!;

        Assert.True(settings.HasSeenWelcome);

        // No name, which is exactly the state that sends someone back through onboarding
        // rather than leaving the app nameless.
        Assert.Equal(string.Empty, settings.DisplayName);
    }
}
