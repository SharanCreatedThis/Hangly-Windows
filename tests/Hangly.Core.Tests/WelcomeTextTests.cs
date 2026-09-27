using Hangly.Core.Lifecycle;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The welcome words are the shared ones — the same assertions as the macOS UpdateAndWelcomeTests.</summary>
public class WelcomeTextTests
{
    [Fact]
    public void TheWelcomeWordsAreTheSharedOnes()
    {
        Assert.Equal("Welcome, Sharan", WelcomeText.Title(" Sharan "));
        Assert.Equal("Welcome to Hangly", WelcomeText.Title(""));
        Assert.Equal("A tiny charm that hangs from your screen.", WelcomeText.Tagline);
        Assert.Equal(
            ["Browse charms from around the world", "Drop a picture on the charm to make your own", "Change the rope, where it hangs and how big it is"],
            WelcomeText.Things);
        Assert.Equal("Explore Library", WelcomeText.Explore);
        Assert.Equal("Start Using Hangly", WelcomeText.Start);
    }
}
