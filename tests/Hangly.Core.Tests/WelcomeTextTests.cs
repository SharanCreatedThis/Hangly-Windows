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
        Assert.Equal("A little delight, every time you look up.", WelcomeText.Tagline);
        Assert.Equal("Choose your charm. Make every detail yours.", WelcomeText.Invitation);
        Assert.Equal("Explore Library", WelcomeText.Explore);
        Assert.Equal("Start Using Hangly", WelcomeText.Start);
    }
}
