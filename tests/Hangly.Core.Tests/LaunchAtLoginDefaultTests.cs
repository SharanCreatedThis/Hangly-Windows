using Hangly.Core.Lifecycle;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>2.1.2: starting at sign-in is switched on once for every installation, then follows the system.</summary>
public sealed class LaunchAtLoginDefaultTests
{
    [Fact]
    public void ANewInstallAndEveryExistingOneAreDueOnce()
    {
        Assert.True(LaunchAtLoginDefault.IsDue(AppSettings.Defaults));
        var existing = AppSettings.Defaults with { DisplayName = "Sharan", LaunchAtLogin = false, HasSeenWelcome = true };
        Assert.True(LaunchAtLoginDefault.IsDue(existing));
        AppSettings applied = LaunchAtLoginDefault.Applied(existing);
        Assert.False(LaunchAtLoginDefault.IsDue(applied));
        // Only the flag: whether it is on is the system's to say, read back after the entry is written.
        Assert.Equal(existing with { Milestones = applied.Milestones }, applied);
    }

    [Fact]
    public void SettingsWrittenBefore212HaveNotHadIt()
    {
        AppSettings read = AppSettings.FromJson("""{"schemaVersion":1,"displayName":"Sharan","launchAtLogin":false,"milestones":{"launchCount":12}}""", out bool recovered);
        Assert.False(recovered);
        Assert.True(LaunchAtLoginDefault.IsDue(read));
    }

    [Fact]
    public void OnceAppliedItStaysAppliedThroughSavingAndReading()
    {
        AppSettings applied = LaunchAtLoginDefault.Applied(AppSettings.Defaults with { LaunchAtLogin = false });
        AppSettings read = AppSettings.FromJson(applied.ToJson(), out _);
        Assert.False(LaunchAtLoginDefault.IsDue(read));
        Assert.False(read.LaunchAtLogin);
    }
}
