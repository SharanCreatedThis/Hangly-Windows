using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Hanging a charm is one act with one outcome, whichever button did it.</summary>
public class HangingTests
{
    private static AppSettings Rope(params string[] ids) => new AppSettings() with
    {
        Overlay = new OverlaySettings().WithStack(CharmStackState.Of(ids)),
    };

    [Fact]
    public void HangingChangesThePlaceRemembersAndCountsInOneWrite()
    {
        AppSettings after = Hanging.Hang(Rope("nazar", "daruma"), 1, "hamsa");

        Assert.Equal(["nazar", "hamsa"], after.Overlay.Stack.Ids);
        Assert.Equal("hamsa", after.Library.RecentCharmIds[0]);
        Assert.Equal(1, after.Milestones.CharmsHung);
    }

    [Fact]
    public void ChoosingTheCharmAlreadyThereIsNotAHangButStillMovesItUpRecent()
    {
        AppSettings before = Rope("nazar") with { Library = new LibrarySettings { RecentCharmIds = ["daruma", "nazar"] } };

        AppSettings after = Hanging.Hang(before, 0, "nazar");

        Assert.Equal(0, after.Milestones.CharmsHung);
        Assert.Equal(["nazar", "daruma"], after.Library.RecentCharmIds);
    }

    [Fact]
    public void APlaceKeepsItsSizeWhenItsCharmChanges()
    {
        AppSettings before = Rope("nazar", "daruma");
        before = before with { Overlay = before.Overlay.WithStack(before.Overlay.Stack.WithSize(1, 1.4)) };

        AppSettings after = Hanging.Hang(before, 1, "hamsa");

        Assert.Equal(1.4, after.Overlay.Stack.SizeAt(1));
    }

    [Fact]
    public void AnOutOfRangePlaceIsClampedRatherThanIgnored()
    {
        AppSettings after = Hanging.Hang(Rope("nazar", "daruma"), 9, "hamsa");

        Assert.Equal(["nazar", "hamsa"], after.Overlay.Stack.Ids);
    }

    [Fact]
    public void TheTrayHangsOneCharmAloneAndCountsOnlyAChange()
    {
        AppSettings once = Hanging.HangAlone(Rope("nazar", "daruma"), "hamsa");
        AppSettings again = Hanging.HangAlone(once, "hamsa");

        Assert.Equal(["hamsa"], once.Overlay.Stack.Ids);
        Assert.Equal("hamsa", once.Library.RecentCharmIds[0]);
        Assert.Equal(1, once.Milestones.CharmsHung);
        Assert.Equal(1, again.Milestones.CharmsHung);
    }
}
