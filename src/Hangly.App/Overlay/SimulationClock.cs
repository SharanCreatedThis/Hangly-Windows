//
//  SimulationClock.cs
//  Hangly
//
//  Display-synchronised tick source for the simulation.
//

namespace Hangly.App.Overlay;

/// <summary>Drives the simulation from the compositor's own frame signal.</summary>
/// <remarks>
/// The port of the original's <c>CADisplayLink</c>. It was
/// <c>CompositionTarget.Rendering</c> until the overlay stopped being a XAML window —
/// that event only fires while there is a XAML tree being composed — so the frame loop
/// now calls <see cref="Advance"/> once per <c>DwmFlush</c> instead. Both are the same
/// signal: one frame of the desktop compositor, on the thread that owns the window, so
/// every simulated frame corresponds to a frame the user sees. A <c>DispatcherTimer</c>
/// would drift against the compositor and show as judder.
///
/// <para>The clock does not own the wait. Whoever is pacing the loop calls
/// <see cref="Advance"/>; this class only decides what that frame is worth.</para>
///
/// <para><b>Throttling.</b> Windows gives no equivalent of <c>preferredFrameRateRange</c>,
/// so the idle rate is implemented by skipping ticks rather than by asking for fewer:
/// once the rope has settled the handler still runs at the display rate but only passes
/// one tick in four downstream. The clock keeps running rather than stopping, because
/// the same tick is what polls the cursor for a grab — thirty per second keeps that
/// responsive at a fraction of the cost.</para>
///
/// <para>The delta is measured with <see cref="System.Diagnostics.Stopwatch"/> because
/// the solver needs a number it can trust on every frame, and nothing the compositor
/// hands back is one.</para>
/// </remarks>
public sealed class SimulationClock
{
    private readonly System.Diagnostics.Stopwatch stopwatch = new();

    /// <summary>Full display rate, used while the rope is moving.</summary>
    private const int ActiveDivisor = 1;

    /// <summary>
    /// Idle divisor: one tick in four, which is 30 per second on a 120 Hz display and 15
    /// on a 60 Hz one. Both are comfortably above what noticing a cursor needs.
    /// </summary>
    private const int IdleDivisor = 4;

    private bool isRunning;
    private double lastTimestamp;
    private int divisor = ActiveDivisor;
    private int skipped;
    private double carried;

    /// <summary>Called once per tick with the time since the previous delivered tick.</summary>
    public event Action<double>? Tick;

    /// <summary>Seconds covered by the most recent delivered tick.</summary>
    public double LastDelta { get; private set; }

    /// <summary>Smoothed frames per second, for the debug read-out.</summary>
    public double FramesPerSecond { get; private set; }

    public bool IsRunning => isRunning;

    public void Start()
    {
        if (isRunning)
        {
            return;
        }

        stopwatch.Restart();
        lastTimestamp = 0;
        skipped = 0;
        carried = 0;
        divisor = ActiveDivisor;
        isRunning = true;
    }

    public void Stop()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        stopwatch.Stop();
        FramesPerSecond = 0;
        LastDelta = 0;
    }

    /// <summary>Switches between the full display rate and the idle rate.</summary>
    public void SetThrottled(bool throttled) => divisor = throttled ? IdleDivisor : ActiveDivisor;

    /// <summary>Accounts for one compositor frame, and delivers a tick if it is due.</summary>
    public void Advance()
    {
        if (!isRunning)
        {
            return;
        }

        double now = stopwatch.Elapsed.TotalSeconds;
        double delta = now - lastTimestamp;
        lastTimestamp = now;

        // The first frame has no predecessor to measure against.
        if (delta <= 0)
        {
            return;
        }

        UpdateFrameRate(delta);

        // Skipped frames are accumulated rather than dropped, so throttling changes how
        // often the solver is asked to advance and never how far it advances. A dropped
        // interval would make a settled rope wake up slower than a moving one.
        carried += delta;
        skipped += 1;
        if (skipped < divisor)
        {
            return;
        }

        skipped = 0;
        LastDelta = carried;
        carried = 0;
        Tick?.Invoke(LastDelta);
    }

    private void UpdateFrameRate(double delta)
    {
        double instantaneous = 1 / delta;
        FramesPerSecond = FramesPerSecond == 0
            ? instantaneous
            : (FramesPerSecond * 0.9) + (instantaneous * 0.1);
    }
}
