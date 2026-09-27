using Hangly.Core.Lifecycle;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>Opening Hangly again reaches the copy that is running, once per launch.</summary>
/// <remarks>
/// Named events are a Windows object: on other platforms these tests assert the no-op
/// instead, so the suite still runs on the Mac a developer builds on. CI runs it on Windows.
/// </remarks>
public class RelaunchSignalTests
{
    /// <summary>A name per test, so parallel tests and a running Hangly never share one.</summary>
    private static string UniqueName() => $@"Local\Hangly.Test.{Guid.NewGuid():N}";

    [Fact]
    public void ASecondLaunchReachesTheListener()
    {
        string name = UniqueName();
        using RelaunchSignal running = RelaunchSignal.Create(name);
        using var heard = new SemaphoreSlim(0);
        running.Listen(() => heard.Release());

        bool sent = RelaunchSignal.Send(TimeSpan.FromSeconds(1), name);

        if (!OperatingSystem.IsWindows())
        {
            Assert.False(sent);
            return;
        }

        Assert.True(sent);
        Assert.True(heard.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void EveryRelaunchIsHeardAndNoneTwice()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string name = UniqueName();
        using RelaunchSignal running = RelaunchSignal.Create(name);
        int count = 0;
        using var heard = new SemaphoreSlim(0);
        running.Listen(() =>
        {
            Interlocked.Increment(ref count);
            heard.Release();
        });

        for (int launch = 0; launch < 3; launch++)
        {
            Assert.True(RelaunchSignal.Send(TimeSpan.FromSeconds(1), name));
            Assert.True(heard.Wait(TimeSpan.FromSeconds(5)));
        }

        Thread.Sleep(300);
        Assert.Equal(3, Volatile.Read(ref count));
    }

    [Fact]
    public void ARelaunchBeforeTheListenerStartsIsKeptForIt()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // The running copy creates the event at claim time and listens only once its
        // windows exist; a relaunch in between must still open the Library.
        string name = UniqueName();
        using RelaunchSignal running = RelaunchSignal.Create(name);
        Assert.True(RelaunchSignal.Send(TimeSpan.FromSeconds(1), name));

        using var heard = new SemaphoreSlim(0);
        running.Listen(() => heard.Release());
        Assert.True(heard.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WithNothingRunningASendGivesUpAfterItsPatience()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        bool sent = RelaunchSignal.Send(TimeSpan.FromMilliseconds(300), UniqueName());

        Assert.False(sent);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3));
    }
}
