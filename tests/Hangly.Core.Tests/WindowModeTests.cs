using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The same cases as macOS's <c>WindowModeTests</c>, where the platform allows.</summary>
public sealed class WindowModeTests
{
    [Fact]
    public void TwoModesInTheSameOrderAndWordsAsMacOSAlwaysOnTopByDefault()
    {
        Assert.Equal(["Always on Top", "On the Desktop"], Enum.GetValues<WindowMode>().Select(WindowModeTable.TitleOf));
        Assert.Equal(WindowMode.OnTop, new OverlaySettings().WindowMode);
        Assert.Equal(WindowMode.OnTop, AppSettings.FromJson("""{ "overlay": { "isEnabled": true } }""", out _).Overlay.WindowMode);
    }

    [Fact]
    public void TheModeIsKeptAcrossASave()
    {
        var settings = new AppSettings { Overlay = new OverlaySettings { WindowMode = WindowMode.Desktop } };
        Assert.Equal(WindowMode.Desktop, AppSettings.FromJson(settings.ToJson(), out _).Overlay.WindowMode);
    }

    [Theory]
    [InlineData("Progman", true)]
    [InlineData("WorkerW", true)]
    [InlineData("Chrome_WidgetWin_1", false)]
    [InlineData("Shell_TrayWnd", false)]
    [InlineData("", false)]
    public void OnlyTheDesktopItselfLeavesTheCharmUncovered(string className, bool isDesktop) =>
        Assert.Equal(isDesktop, WindowModeTable.IsDesktopClass(className));

    [Fact]
    public void TheTraysAlwaysOnTopTickIsTheWindowSettingBothWays()
    {
        Assert.Equal(WindowMode.OnTop, WindowModeTable.FromAlwaysOnTop(true));
        Assert.Equal(WindowMode.Desktop, WindowModeTable.FromAlwaysOnTop(false));
        Assert.True(WindowModeTable.IsAlwaysOnTop(WindowMode.OnTop));
        Assert.False(WindowModeTable.IsAlwaysOnTop(WindowMode.Desktop));
    }
}
