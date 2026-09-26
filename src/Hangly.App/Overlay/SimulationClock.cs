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
/// <para><b>Idling.</b> Once the rope has settled, the frame loop stops waiting on the
/// compositor and waits on its message queue with a timeout instead, so ticks arrive about
/// thirty times a second — enough to notice a grab. The clock used to do this itself, by
/// passing one compositor frame in four downstream, which still woke the thread at the
/// display's full rate to do nothing on three of them.</para>
///
/// <para>The delta is measured with <see cref="System.Diagnostics.Stopwatch"/> because
/// the solver needs a number it can trust on every frame, and nothing the compositor
/// hands back is one.</para>
/// </remarks>
public sealed class SimulationClock
{
    private readonly System.Diagnostics.Stopwatch stopwatch = new();

    private bool isRunning;
    private double lastTimestamp;

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
        LastDelta = delta;
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
