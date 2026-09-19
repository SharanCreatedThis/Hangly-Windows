//
//  ScreenPlacementTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// The placement rules, restated in Win32's y-down convention and checked in it.
/// </summary>
/// <remarks>
/// This is the one part of the port that is a rewrite rather than a transcription — see
/// the note on <see cref="Rect"/> — so it carries the original's whole test list rather
/// than a sample of it, including the case that exists because it caught a real bug: a
/// second display positioned left of the primary, at a negative origin.
/// </remarks>
public class ScreenPlacementTests
{
    private static readonly Rect Primary = new(0, 0, 1920, 1040);
    private static readonly Size Overlay = new(220, 360);

    [Fact(DisplayName = "A centred overlay is centred")]
    public void CentreIsCentred()
    {
        Rect frame = ScreenPlacement.Frame(Overlay, OverlayAnchor.TopCenter, Primary);

        Assert.Equal(Primary.MidX, frame.MidX, 9);
    }

    [Fact(DisplayName = "The overlay hangs from the top of the work area")]
    public void TopAnchoredSitsAtTheTop()
    {
        Rect frame = ScreenPlacement.Frame(Overlay, OverlayAnchor.TopCenter, Primary, topInset: 0);

        // The rope's anchor is a sliver below the top of the window, so the window's own
        // top edge is where the work area starts.
        Assert.Equal(Primary.Top, frame.Top, 9);
    }

    [Fact(DisplayName = "A work area inset by a taskbar pushes the rope down, not behind it")]
    public void TaskbarIsRespected()
    {
        // A top-docked taskbar: the work area starts below it.
        var withTopTaskbar = new Rect(0, 48, 1920, 992);

        Rect frame = ScreenPlacement.Frame(Overlay, OverlayAnchor.TopCenter, withTopTaskbar, topInset: 0);

        Assert.Equal(48, frame.Top, 9);
    }

    [Fact(DisplayName = "Leading and trailing keep their edge inset")]
    public void EdgeInsetIsHonoured()
    {
        Rect leading = ScreenPlacement.Frame(
            Overlay, OverlayAnchor.TopLeading, Primary, edgeInset: 24, topInset: 0);
        Rect trailing = ScreenPlacement.Frame(
            Overlay, OverlayAnchor.TopTrailing, Primary, edgeInset: 24, topInset: 0);

        Assert.Equal(Primary.Left + 24, leading.Left, 9);
        Assert.Equal(Primary.Right - 24, trailing.Right, 9);
    }

    [Fact(DisplayName = "A positive y offset moves the overlay down")]
    public void OffsetDirectionsAreRight()
    {
        Rect nudged = ScreenPlacement.Frame(
            Overlay, OverlayAnchor.TopCenter, Primary, new Vec2(40, 60), topInset: 0);

        Assert.Equal(Primary.MidX + 40, nudged.MidX, 9);
        Assert.Equal(Primary.Top + 60, nudged.Top, 9);
    }

    [Fact(DisplayName = "The charm can reach either edge of the display")]
    public void NoDeadZonesAtTheEdges()
    {
        // What has to stay on screen is the rope, and the rope is at the top centre.
        // Clamping the whole window would stop the charm half a window short of either
        // edge, which is exactly the dead zone this rule exists to remove.
        Rect farLeft = ScreenPlacement.Frame(
            Overlay, OverlayAnchor.TopCenter, Primary, new Vec2(-5000, 0), topInset: 0);
        Rect farRight = ScreenPlacement.Frame(
            Overlay, OverlayAnchor.TopCenter, Primary, new Vec2(5000, 0), topInset: 0);

        Assert.Equal(Primary.Left, farLeft.MidX, 9);
        Assert.Equal(Primary.Right, farRight.MidX, 9);
    }

    [Fact(DisplayName = "A display left of the primary has a negative origin and still works")]
    public void NegativeOriginsAreNotASpecialCase()
    {
        // A second monitor placed to the left of the primary starts at a negative x in
        // virtual-desktop coordinates. The geometry must handle that without a branch.
        var secondary = new Rect(-1920, 0, 1920, 1040);

        Rect frame = ScreenPlacement.Frame(Overlay, OverlayAnchor.TopCenter, secondary, topInset: 0);

        Assert.Equal(-960, frame.MidX, 9);
        Assert.True(frame.Left < 0);
    }

    [Fact(DisplayName = "A display above the primary has a negative y origin and still works")]
    public void NegativeVerticalOriginsWork()
    {
        var above = new Rect(0, -1080, 1920, 1080);

        Rect frame = ScreenPlacement.Frame(Overlay, OverlayAnchor.TopCenter, above, topInset: 0);

        Assert.Equal(-1080, frame.Top, 9);
    }

    [Fact(DisplayName = "An overlay larger than the display is not mangled")]
    public void OversizedOverlaysAreLeftAlone()
    {
        var huge = new Size(4000, 4000);

        Rect frame = ScreenPlacement.Clamp(new Rect(0, 0, huge.Width, huge.Height), Primary);

        // Returned untouched so the caller can decide what to do, rather than silently
        // squashed into a display it does not fit.
        Assert.Equal(huge.Width, frame.Width);
        Assert.Equal(huge.Height, frame.Height);
    }

    [Fact(DisplayName = "An empty display leaves the frame alone")]
    public void EmptyBoundsAreIgnored()
    {
        var proposed = new Rect(10, 20, 220, 360);

        Assert.Equal(proposed, ScreenPlacement.ClampAnchorPoint(proposed, Rect.Zero));
    }
}
