using Hangly.Core.Geometry;
using Xunit;

namespace Hangly.Core.Tests;

public class DisplayChoiceTests
{
    private static readonly DisplayIdentity Laptop = new(@"\\?\DISPLAY#BOE0A1B#4&1", "Built-in display", true);
    private static readonly DisplayIdentity Left = new(@"\\?\DISPLAY#DEL41A8#5&2", "DELL U2720Q", false);
    private static readonly DisplayIdentity Right = new(@"\\?\DISPLAY#DEL41A8#5&3", "DELL U2720Q", false);
    private static readonly DisplayIdentity Ultrawide = new(@"\\?\DISPLAY#GSM7714#5&4", "LG ULTRAWIDE", false);

    [Fact]
    public void NoChoiceIsTheMainDisplayWhereverItIsListed()
    {
        Assert.Equal(0, DisplayChoice.Resolve([Laptop, Left], chosenId: null));
        Assert.Equal(2, DisplayChoice.Resolve([Left, Right, Laptop], chosenId: null));
    }

    [Fact]
    public void AChosenDisplayIsFoundByIdNotPosition()
    {
        // Triple monitor, enumerated in two different orders across a reboot.
        Assert.Equal(2, DisplayChoice.Resolve([Laptop, Left, Right], Right.Id));
        Assert.Equal(0, DisplayChoice.Resolve([Right, Laptop, Left], Right.Id));
    }

    [Fact]
    public void IdsCompareWithoutCase()
    {
        Assert.Equal(1, DisplayChoice.Resolve([Laptop, Ultrawide], Ultrawide.Id.ToLowerInvariant()));
    }

    [Fact]
    public void UnpluggedFallsBackToMainAndPluggingBackReturns()
    {
        IReadOnlyList<DisplayIdentity> docked = [Laptop, Ultrawide];
        IReadOnlyList<DisplayIdentity> undocked = [Laptop];

        Assert.Equal(1, DisplayChoice.Resolve(docked, Ultrawide.Id));
        Assert.Equal(0, DisplayChoice.Resolve(undocked, Ultrawide.Id));
        Assert.True(DisplayChoice.IsMissing(undocked, Ultrawide.Id));

        // The same remembered id, nothing re-chosen: back on the ultrawide.
        Assert.Equal(1, DisplayChoice.Resolve(docked, Ultrawide.Id));
        Assert.False(DisplayChoice.IsMissing(docked, Ultrawide.Id));
    }

    [Fact]
    public void NothingChosenIsNeverMissing()
    {
        Assert.False(DisplayChoice.IsMissing([Laptop], null));
    }

    [Fact]
    public void ALegacyIndexIsHonouredMainFirstUntilAnIdIsChosen()
    {
        // 0.9.x stored "index 1" meaning the first display after the main one.
        Assert.Equal(1, DisplayChoice.Resolve([Laptop, Left], chosenId: null, legacyIndex: 1));
        Assert.Equal(0, DisplayChoice.Resolve([Left, Laptop], chosenId: null, legacyIndex: 1));

        // Out of range: the main display, as 0.9.x did.
        Assert.Equal(0, DisplayChoice.Resolve([Laptop], chosenId: null, legacyIndex: 3));

        // An id, once chosen, wins.
        Assert.Equal(0, DisplayChoice.Resolve([Laptop, Left], Laptop.Id, legacyIndex: 1));
    }

    [Fact]
    public void NoDisplaysAtAllIsIndexZero()
    {
        Assert.Equal(0, DisplayChoice.Resolve([], "anything"));
    }

    [Fact]
    public void IdenticalMonitorsAreNumbered()
    {
        Assert.Equal(
            ["Built-in display", "DELL U2720Q 1", "DELL U2720Q 2", "LG ULTRAWIDE"],
            DisplayChoice.Labels([Laptop, Left, Right, Ultrawide]));
    }

    [Fact]
    public void AnUnnamedDisplayIsCalledDisplay()
    {
        Assert.Equal(
            ["Display 1", "Display 2"],
            DisplayChoice.Labels([Laptop with { Name = "" }, Left with { Name = "  " }]));
    }
}
