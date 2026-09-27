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
}
