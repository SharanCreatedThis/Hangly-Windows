using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The same cases as macOS's <c>GlowTests</c>.</summary>
public sealed class GlowTableTests
{
    [Fact]
    public void ThreeLevelsInTheSameOrderAndWordsAsMacOS()
    {
        Assert.Equal(
            ["Off", "Soft", "Strong"],
            Enum.GetValues<GlowLevel>().Select(GlowTable.TitleOf));
    }

    [Fact]
    public void SoftIsTheHaloAsItHasAlwaysBeenOffDrawsNoneStrongIsMoreOfBoth()
    {
        Assert.Null(GlowTable.StrengthOf(GlowLevel.Off));
        Assert.Equal(new GlowStrength(1.35, 1), GlowTable.StrengthOf(GlowLevel.Soft));
        Assert.Equal(new GlowStrength(1.65, 2.4), GlowTable.StrengthOf(GlowLevel.Strong));
    }

    [Fact]
    public void NoLevelReachesPastTheRoomTheLayoutKeepsForTheHalo()
    {
        foreach (GlowLevel level in Enum.GetValues<GlowLevel>())
        {
            if (GlowTable.StrengthOf(level) is GlowStrength strength)
            {
                Assert.True(strength.Reach <= RopeConfiguration.Layout.CharmHaloExtent, level.ToString());
            }
        }
    }

    [Fact]
    public void SoftByDefaultAndAnOlderFileOpensWithSoft()
    {
        Assert.Equal(GlowLevel.Soft, new OverlaySettings().Glow);
        AppSettings old = AppSettings.FromJson("""{ "overlay": { "isEnabled": true } }""", out _);
        Assert.Equal(GlowLevel.Soft, old.Overlay.Glow);
    }

    [Fact]
    public void SoftFadesOutWhereTheOldDiscDidAndStrongInsideTheHalosRoom()
    {
        // It fades out three blurs past the edge, against the charm's size: two radii across.
        Assert.Equal(1.36, 1 + (GlowTable.StrengthOf(GlowLevel.Soft)!.Value.Spread * 3 * 2), 2);
        Assert.True(1 + (GlowTable.StrengthOf(GlowLevel.Strong)!.Value.Spread * 3 * 2) <= RopeConfiguration.Layout.CharmHaloExtent);
    }

    [Fact]
    public void EachPartGlowsItsOwnColourLeaningTowardsThePalettesAndNeverDark()
    {
        var red = new CharmColor(0.9, 0.1, 0.1);
        var blue = new CharmColor(0.1, 0.1, 0.9);
        var grey = new CharmColor(0.5, 0.5, 0.5);
        CharmColor overRed = GlowTable.GlowColour(red, grey);
        CharmColor overBlue = GlowTable.GlowColour(blue, grey);
        Assert.True(overRed.Red > overRed.Blue, "the red part glows red");
        Assert.True(overBlue.Blue > overBlue.Red, "the blue part glows blue");

        var black = new CharmColor(0, 0, 0);
        CharmColor overBlack = GlowTable.GlowColour(black, black);
        Assert.True(Math.Max(overBlack.Red, Math.Max(overBlack.Green, overBlack.Blue)) >= 0.5, "light, not a shadow");
        Assert.Equal(new CharmColor(0.85, 0.85, 0.85), GlowTable.TintOf(black));
    }
}
