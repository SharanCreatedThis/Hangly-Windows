using Hangly.Core.Lifecycle;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>When an update installs itself. The same cases as macOS's <c>QuietUpdateTests</c>.</summary>
public sealed class UpdateTimingTests
{
    private static UpdateMoment Moment(double idleMinutes = 0, double sinceLaunchMinutes = 60, bool away = false, bool windowOpen = false) =>
        new(TimeSpan.FromMinutes(idleMinutes), TimeSpan.FromMinutes(sinceLaunchMinutes), away, windowOpen);

    [Fact]
    public void NeverInFrontOfSomebodyHoweverLongTheyAreIdle()
    {
        Assert.False(UpdateTiming.ShouldInstall(Moment(idleMinutes: 0)));
        Assert.False(UpdateTiming.ShouldInstall(Moment(idleMinutes: 240)));
    }

    [Fact]
    public void OnceLockedOrAScreenSaverIsUpAndInputHasStopped()
    {
        Assert.False(UpdateTiming.ShouldInstall(Moment(idleMinutes: 1, away: true)));
        Assert.True(UpdateTiming.ShouldInstall(Moment(idleMinutes: 2, away: true)));
    }

    [Fact]
    public void NeverRightAfterAStartOrWithAHanglyWindowOpen()
    {
        Assert.False(UpdateTiming.ShouldInstall(Moment(idleMinutes: 60, sinceLaunchMinutes: 9, away: true)));
        Assert.False(UpdateTiming.ShouldInstall(Moment(idleMinutes: 60, away: true, windowOpen: true)));
        Assert.True(UpdateTiming.ShouldInstall(Moment(idleMinutes: 60, sinceLaunchMinutes: 10, away: true)));
    }
}
