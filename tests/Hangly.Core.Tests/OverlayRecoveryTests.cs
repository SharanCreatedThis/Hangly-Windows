using Hangly.Core.Lifecycle;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Rebuilding the overlay after failures (W-FRAMELOOP, W-DEVLOSTCAP).</summary>
public sealed class OverlayRecoveryTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheFirstFailuresRebuildOnTheGraphicsCard()
    {
        var recovery = new OverlayRecovery();
        for (int i = 0; i < OverlayRecovery.Allowed; i++)
        {
            Assert.Equal(RecoveryStep.RebuildOnHardware, recovery.Next(Start.AddSeconds(i)));
        }

        Assert.False(recovery.OnSoftware);
    }

    [Fact]
    public void OneFailureTooManyMovesToTheSoftwareRendererInsteadOfGivingUp()
    {
        var recovery = new OverlayRecovery();
        for (int i = 0; i < OverlayRecovery.Allowed; i++)
        {
            recovery.Next(Start.AddSeconds(i));
        }

        Assert.Equal(RecoveryStep.RebuildOnSoftware, recovery.Next(Start.AddSeconds(20)));
        Assert.True(recovery.OnSoftware);
        Assert.Equal(1, recovery.Failures);
    }

    [Fact]
    public void OnlyTheSameCountOfFailuresOnSoftwareGivesUp()
    {
        var recovery = new OverlayRecovery();
        DateTimeOffset now = Start;
        for (int i = 0; i <= OverlayRecovery.Allowed; i++)
        {
            recovery.Next(now = now.AddSeconds(1));
        }

        for (int i = 1; i < OverlayRecovery.Allowed; i++)
        {
            Assert.Equal(RecoveryStep.RebuildOnSoftware, recovery.Next(now = now.AddSeconds(1)));
        }

        Assert.Equal(RecoveryStep.GiveUp, recovery.Next(now.AddSeconds(1)));
    }

    [Fact]
    public void FailuresFurtherApartThanTheWindowNeverAddUp()
    {
        var recovery = new OverlayRecovery();
        for (int i = 0; i < OverlayRecovery.Allowed * 3; i++)
        {
            Assert.Equal(RecoveryStep.RebuildOnHardware, recovery.Next(Start + (OverlayRecovery.Window * i) + TimeSpan.FromSeconds(i + 1)));
        }

        Assert.False(recovery.OnSoftware);
    }

    [Fact]
    public void TheSoftwareRendererIsKeptForTheSession()
    {
        var recovery = new OverlayRecovery();
        for (int i = 0; i <= OverlayRecovery.Allowed; i++)
        {
            recovery.Next(Start.AddSeconds(i));
        }

        Assert.Equal(RecoveryStep.RebuildOnSoftware, recovery.Next(Start.AddHours(5)));
    }
}
