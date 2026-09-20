//
//  CharmStackStateTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The places on the rope, against the macOS CharmStack they are transcribed from.</summary>
public class CharmStackStateTests
{
    [Fact(DisplayName = "Turning the count down and back up returns the same rope")]
    public void CountIsNotDestructive()
    {
        CharmStackState three = CharmStackState.Of(["nazar", "hamsa", "daruma"]);

        CharmStackState one = three.WithCount(1);
        Assert.Equal(["daruma"], one.Ids);

        // The charm on the end of the rope is the one being looked at, so it is the one
        // that stays, and the two above come back exactly as they were.
        Assert.Equal(["nazar", "hamsa", "daruma"], one.WithCount(3).Ids);
    }

    [Fact(DisplayName = "A place's size survives the charm in it changing")]
    public void SizeBelongsToThePlace()
    {
        CharmStackState stack = CharmStackState.Of(["nazar", "hamsa", "daruma"])
            .WithSize(1, 0.7)
            .WithCharm(1, "manekiNeko");

        Assert.Equal("manekiNeko", stack.Ids[1]);
        Assert.Equal(0.7, stack.SizeAt(1), 6);
    }

    [Fact(DisplayName = "A place's size is clamped to the range macOS allows")]
    public void SizeIsClamped()
    {
        CharmStackState stack = CharmStackState.Of(["nazar"]);
        Assert.Equal(RopeCharm.MaximumSize, stack.WithSize(0, 9).SizeAt(0), 6);
        Assert.Equal(RopeCharm.MinimumSize, stack.WithSize(0, 0).SizeAt(0), 6);
    }

    [Fact(DisplayName = "Reordering moves a place and carries its size with it")]
    public void ReorderCarriesTheSize()
    {
        CharmStackState stack = CharmStackState.Of(["nazar", "hamsa", "daruma"]).WithSize(0, 1.5);

        CharmStackState moved = stack.Moved(0, 2);
        Assert.Equal(["hamsa", "daruma", "nazar"], moved.Ids);
        Assert.Equal(1.5, moved.SizeAt(2), 6);
    }

    [Fact(DisplayName = "A deleted charm is replaced in the places that are put away too")]
    public void ReplaceReachesHiddenPlaces()
    {
        CharmStackState hidden = CharmStackState.Of(["custom:abc", "hamsa", "daruma"]).WithCount(1);
        Assert.Equal(["daruma"], hidden.Ids);

        CharmStackState replaced = hidden.Replacing("custom:abc", CharmCatalog.DefaultId);

        // The proof is that it does not come back when the rope grows again.
        Assert.DoesNotContain("custom:abc", replaced.WithCount(3).Ids);
    }

    [Fact(DisplayName = "A settings document with no places still opens its rope")]
    public void OlderDocumentsStillLoad()
    {
        var overlay = new OverlaySettings { CharmIds = ["nazar", "hamsa"] };

        Assert.Equal(["nazar", "hamsa"], overlay.Stack.Ids);
        Assert.Equal(2, overlay.Stack.Count);
        Assert.All(overlay.Stack.Places, place => Assert.Equal(1, place.Size, 6));
    }

    [Fact(DisplayName = "Writing a stack keeps the charm list in step")]
    public void WritingKeepsBothShapes()
    {
        OverlaySettings written = new OverlaySettings()
            .WithStack(CharmStackState.Of(["nazar", "hamsa", "daruma"]).WithCount(2));

        Assert.Equal(["hamsa", "daruma"], written.CharmIds);
        Assert.Equal(2, written.CharmCount);
        Assert.Equal(CharmStack.MaximumCount, written.Slots.Count);
    }

    [Fact(DisplayName = "Scaling a charm moves its mass and radius together, linearly")]
    public void ScalingIsLinear()
    {
        var metrics = new CharmMetrics(3.0, 0.2, 0.9);
        CharmMetrics doubled = metrics.Scaled(2);

        Assert.Equal(6.0, doubled.Mass, 6);
        Assert.Equal(0.4, doubled.RadiusRatio, 6);

        // The knot is a proportion of the radius, so it is already right at any size.
        Assert.Equal(0.9, doubled.KnotInset, 6);
        Assert.Equal(metrics, metrics.Scaled(1));
    }
}
