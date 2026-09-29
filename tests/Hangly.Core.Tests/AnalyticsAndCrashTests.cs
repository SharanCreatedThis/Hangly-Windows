using Hangly.Core.Analytics;
using Hangly.Core.Crashes;
using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The six events (macOS's <c>AnalyticsEventTests</c>) and the crash store.</summary>
public sealed class AnalyticsAndCrashTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "hangly-crashes-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheEventsHaveTheAgreedNamesAndParameters()
    {
        Assert.Equal("app_launch", AnalyticsEvent.AppLaunch(atLogin: true).Name);
        Assert.Equal("login", AnalyticsEvent.AppLaunch(atLogin: true).Parameters["launch_type"]);
        Assert.Equal("manual", AnalyticsEvent.AppLaunch(atLogin: false).Parameters["launch_type"]);
        Assert.Equal("update", AnalyticsEvent.AppLaunch(atLogin: false, afterUpdate: true).Parameters["launch_type"]);
        Assert.Equal("hangly_app_update", AnalyticsEvent.AppUpdate("0.9.4").Name);
        Assert.Equal("spiderMan", AnalyticsEvent.CharmSelected("spiderMan").Parameters["charm_id"]);
        Assert.Equal("spiderThread", AnalyticsEvent.RopeSelected(RopeStyle.SpiderThread).Parameters["rope_style"]);
        Assert.Equal("release_notes", AnalyticsEvent.SupportClicked(SupportSurface.ReleaseNotes).Parameters["surface"]);
        Assert.Equal("explore", AnalyticsEvent.WelcomeCompleted(explored: true).Parameters["path"]);
    }

    [Fact]
    public void ACharmSomebodyMadeIsReportedAsCustomNeverByName() =>
        Assert.Equal("custom", AnalyticsEvent.CharmSelected(CharmId.ForCustom(Guid.NewGuid())).Parameters["charm_id"]);

    private static CrashReport Report(string message = "boom", string stack = "   at Hangly.X()") =>
        new(DateTimeOffset.UtcNow, true, "System.Exception", message, stack, "AppDomain", "2.1.0", "10.0.26100.0", "x64");

    [Fact]
    public void AReportIsKeptUntilRemovedAndSurvivesARestart()
    {
        new CrashStore(folder).Save(Report());
        (string path, CrashReport report) = Assert.Single(new CrashStore(folder).Pending());
        Assert.Equal("boom", report.Message);
        new CrashStore(folder).Remove(path);
        Assert.Empty(new CrashStore(folder).Pending());
    }

    [Fact]
    public void TheProfilePathAndUserNameAreScrubbedAndTheStackIsCut()
    {
        CrashReport scrubbed = CrashStore.Scrub(
            Report(message: @"C:\Users\sharan\Documents\charm.svg", stack: @"at X() in C:\Users\sharan\a.cs" + new string('x', CrashStore.StackLimit)),
            profile: @"C:\Users\sharan",
            userName: "sharan");
        Assert.Equal(@"%USERPROFILE%\Documents\charm.svg", scrubbed.Message);
        Assert.StartsWith(@"at X() in %USERPROFILE%\a.cs", scrubbed.StackTrace, StringComparison.Ordinal);
        Assert.Equal(CrashStore.StackLimit, scrubbed.StackTrace.Length);
    }

    [Fact]
    public void ACrashLoopKeepsTheNewestTwenty()
    {
        var store = new CrashStore(folder);
        for (int i = 0; i < CrashStore.PendingLimit + 5; i++)
        {
            store.Save(Report(message: $"crash {i}") with { OccurredAt = DateTimeOffset.UtcNow.AddSeconds(i) });
        }

        Assert.Equal(CrashStore.PendingLimit, store.Pending().Count);
    }
}
