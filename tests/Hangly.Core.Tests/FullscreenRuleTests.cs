using Hangly.Core.Fullscreen;
using Hangly.Core.Geometry;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>When the charm steps out of the way, and every case where it must not.</summary>
public class FullscreenRuleTests
{
    private static readonly Rect Monitor = new(0, 0, 2560, 1440);

    /// <summary>A film playing full screen on the charm's display: the one case that hides.</summary>
    private static readonly ForegroundFacts Film = new(
        WindowBounds: Monitor,
        MonitorBounds: Monitor,
        HasCaption: false,
        IsShellOrDesktop: false,
        IsHangly: false,
        SameDisplayAsCharm: true,
        ForegroundPlaying: true,
        ExclusiveFullScreen: false);

    [Fact]
    public void AFilmFullScreenHides() => Assert.True(FullscreenRule.ShouldHide(Film));

    [Fact]
    public void BrowserFullScreenIsPixelsBeyondTheMonitorAndStillCounts()
    {
        // Browsers and VLC often size a full-screen window a few pixels past the edges.
        Assert.True(FullscreenRule.ShouldHide(Film with { WindowBounds = new Rect(-8, -8, 2576, 1456) }));
    }

    [Fact]
    public void AMaximisedWindowWithTheTaskbarAutoHiddenDoesNot()
    {
        // Covers the whole monitor, holds the display (a video in a normal window), but has
        // a title bar: maximised, not full screen.
        Assert.False(FullscreenRule.ShouldHide(Film with { HasCaption = true }));
    }

    [Fact]
    public void AMaximisedWindowLeavesTheTaskbarStripAndDoesNot()
    {
        Assert.False(FullscreenRule.ShouldHide(Film with { WindowBounds = new Rect(0, 0, 2560, 1392) }));
    }

    [Fact]
    public void ABorderlessUtilityCoveringTheScreenWithoutVideoDoesNot()
    {
        Assert.False(FullscreenRule.ShouldHide(Film with { ForegroundPlaying = false }));
    }

    [Fact]
    public void AVideoInAWindowDoesNot()
    {
        Assert.False(FullscreenRule.ShouldHide(Film with { WindowBounds = new Rect(200, 200, 1280, 720), HasCaption = true }));
    }

    [Fact]
    public void AFilmOnTheOtherMonitorDoesNot()
    {
        Assert.False(FullscreenRule.ShouldHide(Film with { SameDisplayAsCharm = false }));
        Assert.False(FullscreenRule.ShouldHide(Film with { SameDisplayAsCharm = false, ExclusiveFullScreen = true }));
    }

    [Fact]
    public void AnExclusiveFullScreenGameHidesWithoutTheOtherSignals()
    {
        Assert.True(FullscreenRule.ShouldHide(Film with { ForegroundPlaying = false, HasCaption = true, ExclusiveFullScreen = true }));
    }

    [Fact]
    public void TheDesktopAndHanglyItselfNeverDo()
    {
        Assert.False(FullscreenRule.ShouldHide(Film with { IsShellOrDesktop = true }));
        Assert.False(FullscreenRule.ShouldHide(Film with { IsHangly = true }));
    }

    [Fact]
    public void ASecondaryMonitorWithNegativeCoordinatesWorks()
    {
        var left = new Rect(-1920, 0, 1920, 1080);
        Assert.True(FullscreenRule.ShouldHide(Film with { WindowBounds = left, MonitorBounds = left }));
    }
}
