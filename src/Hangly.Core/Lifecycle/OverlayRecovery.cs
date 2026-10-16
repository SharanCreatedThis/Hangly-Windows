//
//  OverlayRecovery.cs
//  Hangly
//
//  What to do when the overlay's frame loop ends on a failure: build it again, on which renderer, or stop.
//

namespace Hangly.Core.Lifecycle;

/// <summary>The next step after the overlay failed.</summary>
public enum RecoveryStep
{
    /// <summary>Build the overlay again on the graphics card.</summary>
    RebuildOnHardware,

    /// <summary>Build it again on the software renderer, which no driver can take away.</summary>
    RebuildOnSoftware,

    /// <summary>Stop trying: the software renderer has failed as often as the hardware did.</summary>
    GiveUp,
}

/// <summary>Counts the overlay's failures and decides each rebuild (W-FRAMELOOP, W-DEVLOSTCAP).</summary>
/// <remarks>
/// <para>Every failure is rebuilt from, not only a lost graphics device. Before 2.3.2 anything else that ended the frame
/// loop — running out of memory, a race in a cache — took the charm away until Hangly was restarted: about 20 installs
/// in three days on 2.3.1.</para>
///
/// <para>At most <see cref="Allowed"/> rebuilds in <see cref="Window"/>, so a failure on every frame cannot become a
/// loop, while a laptop that sleeps for days still recovers every time. Reaching that on the graphics card used to be
/// the end — 40 installs in three days on 2.3.1 were left with no charm. Now it moves the overlay to the software
/// renderer for the rest of the session, where a rope and a few charms cost little, and only the same count of failures
/// there stops it.</para>
/// </remarks>
public sealed class OverlayRecovery
{
    public const int Allowed = 8;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private DateTimeOffset windowStart = DateTimeOffset.MinValue;
    private int failures;

    /// <summary>Whether the overlay has moved to the software renderer; it stays there until Hangly restarts.</summary>
    public bool OnSoftware { get; private set; }

    /// <summary>Failures counted in the current window, on the current renderer.</summary>
    public int Failures => failures;

    /// <summary>Records a failure at <paramref name="now"/> and says what to do about it.</summary>
    public RecoveryStep Next(DateTimeOffset now)
    {
        if (now - windowStart > Window)
        {
            windowStart = now;
            failures = 0;
        }

        if (++failures <= Allowed)
        {
            return OnSoftware ? RecoveryStep.RebuildOnSoftware : RecoveryStep.RebuildOnHardware;
        }

        if (!OnSoftware)
        {
            OnSoftware = true;
            windowStart = now;
            failures = 1;
            return RecoveryStep.RebuildOnSoftware;
        }

        return RecoveryStep.GiveUp;
    }
}
