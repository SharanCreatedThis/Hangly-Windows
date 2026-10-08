using Hangly.Core.Crashes;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>What leaves the PC in crash reports and the log: no profile path, no user name (W-LOGPII).</summary>
public sealed class PersonalPathsTests
{
    [Fact]
    public void TheProfilePathBecomesAPlaceholder()
    {
        var personal = new PersonalPaths(@"C:\Users\alex", "alex");
        Assert.Equal(@"settings at %USERPROFILE%\AppData\Roaming\Hangly\settings.json",
            personal.Redact(@"settings at C:\Users\alex\AppData\Roaming\Hangly\settings.json"));
    }

    [Fact]
    public void TheProfilePathIsMatchedWhateverItsCase()
    {
        var personal = new PersonalPaths(@"C:\Users\Alex", "Alex");
        Assert.Equal(@"%USERPROFILE%\Pictures", personal.Redact(@"c:\users\alex\Pictures"));
    }

    [Fact]
    public void TheUserNameIsReplacedAsAWord()
    {
        var personal = new PersonalPaths(@"D:\Profiles\alex", "alex");
        Assert.Equal("signed in as %USERNAME%, share \\\\server\\%USERNAME%\\docs",
            personal.Redact("signed in as alex, share \\\\server\\alex\\docs"));
    }

    [Fact]
    public void AUserNameInsideAnotherWordIsLeftAlone()
    {
        // From the field: a user called "Pro" turned FollowPrompt into Follow%USERNAME%mpt in a crash report.
        var personal = new PersonalPaths(@"C:\Users\Pro", "Pro");
        Assert.Equal("at Hangly.App.Onboarding.FollowPrompt..ctor()", personal.Redact("at Hangly.App.Onboarding.FollowPrompt..ctor()"));
    }

    [Fact]
    public void AVeryShortUserNameIsNotReplacedButTheProfilePathStillIs()
    {
        var personal = new PersonalPaths(@"C:\Users\al", "al");
        Assert.Equal(@"%USERPROFILE%\x.svg, all charms", personal.Redact(@"C:\Users\al\x.svg, all charms"));
    }

    [Fact]
    public void AUserNameWithRegexCharactersIsMatchedLiterally()
    {
        var personal = new PersonalPaths(@"C:\Users\a.b+c", "a.b+c");
        Assert.Equal("%USERNAME% and axb+c", personal.Redact("a.b+c and axb+c"));
    }

    [Fact]
    public void EmptyTextStaysEmpty()
    {
        Assert.Equal(string.Empty, new PersonalPaths(@"C:\Users\alex", "alex").Redact(string.Empty));
    }
}
