using Hangly.Core.Lifecycle;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Where a cold launch lands: Welcome → Name → Library on day one, the Library after that, quiet at sign-in.</summary>
public class LaunchIntentTests
{
    private static readonly TimeSpan LongAfterBoot = TimeSpan.FromHours(5);

    [Fact]
    public void AFirstLaunchIsTheWelcomeWhateverStartedIt()
    {
        Assert.Equal(LaunchDestination.Welcome, LaunchIntent.Decide([], needsWelcome: true, LongAfterBoot, false));
        Assert.Equal(LaunchDestination.Welcome, LaunchIntent.Decide(["--login"], needsWelcome: true, LongAfterBoot, false));
    }

    [Fact]
    public void AReturningUserWhoOpensHanglyGetsTheLibrary() =>
        Assert.Equal(LaunchDestination.Library, LaunchIntent.Decide([], needsWelcome: false, LongAfterBoot, false));

    [Theory]
    [InlineData("--login")]
    [InlineData("--LOGIN")]
    [InlineData("--updated")]
    public void SignInAndUpdateRestartsStayQuiet(string argument) =>
        Assert.Equal(LaunchDestination.Quiet, LaunchIntent.Decide([argument], needsWelcome: false, LongAfterBoot, false));

    [Fact]
    public void AnOldSignInEntryRightAfterBootIsTakenForASignIn()
    {
        Assert.Equal(LaunchDestination.Quiet, LaunchIntent.Decide([], false, TimeSpan.FromMinutes(1), hasLegacyLoginEntry: true));

        // The guess is only made while an old entry exists, and only near boot.
        Assert.Equal(LaunchDestination.Library, LaunchIntent.Decide([], false, TimeSpan.FromMinutes(1), hasLegacyLoginEntry: false));
        Assert.Equal(LaunchDestination.Library, LaunchIntent.Decide([], false, TimeSpan.FromMinutes(10), hasLegacyLoginEntry: true));
    }

    [Fact]
    public void TheEntryIsWrittenQuotedWithTheLoginArgumentAndReadBack()
    {
        string written = LoginEntry.Format(@"C:\Users\A B\AppData\Local\Hangly\current\Hangly.exe");

        Assert.Equal(@"""C:\Users\A B\AppData\Local\Hangly\current\Hangly.exe"" --login", written);
        Assert.Equal((@"C:\Users\A B\AppData\Local\Hangly\current\Hangly.exe", true), LoginEntry.Parse(written));
    }

    [Theory]
    [InlineData(@"""C:\Hangly\Hangly.exe""", @"C:\Hangly\Hangly.exe", false)]
    [InlineData(@"C:\Hangly\Hangly.exe", @"C:\Hangly\Hangly.exe", false)]
    [InlineData(@"C:\Hangly\Hangly.exe --login", @"C:\Hangly\Hangly.exe", true)]
    [InlineData(@"  ""C:\Hangly\Hangly.exe""   --login  ", @"C:\Hangly\Hangly.exe", true)]
    public void OldAndNewEntriesAreBothUnderstood(string entry, string executable, bool marksLogin) =>
        Assert.Equal((executable, marksLogin), LoginEntry.Parse(entry));

    [Fact]
    public void TheHintIsTheApprovedWordsAndMatchesAbout()
    {
        Assert.Equal("Open Hangly again anytime to return to your Library.", HelpText.OpenAgain);

        // About states it in XAML, which cannot reference a constant; this keeps the two equal.
        string xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Hangly.App", "Customize", "CustomizeWindow.xaml"));
        Assert.Contains($"Text=\"{HelpText.OpenAgain}\"", xaml, StringComparison.Ordinal);
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));
}
