//
//  DisplayTiming.cs
//  Hangly
//
//  How much display time a frame stands for, from the compositor itself.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Overlay;

/// <summary>The time between the frames the compositor actually shows, counted in refreshes rather than stopwatch time.</summary>
/// <remarks>
/// <b>Why it matters.</b> The rope's physics advances in fixed 1/240-second steps, and each frame used to advance it by
/// however long the stopwatch said the last frame took. That wobbles around 16.7 ms, so the step count per frame wobbled
/// with it — measured on the VM, 3, 4 or 5 steps where a 60 Hz display shows 4 — and the charm moved a quarter too
/// little in one frame and a quarter too much in the next: a fine, constant judder in every swing.
///
/// <para>The compositor counts its refreshes (<c>DWM_TIMING_INFO.cRefresh</c>) and knows its refresh period, so a frame
/// that followed one refresh after the last stands for exactly one period, and one that missed a refresh for exactly two.
/// The physics then moves by display time, not by when the thread happened to wake. Null whenever the compositor will not
/// say, and the stopwatch stands in, as before.</para>
/// </remarks>
internal sealed class DisplayTiming
{
    // DWM_TIMING_INFO is declared with #pragma pack(1): 292 bytes. Only three fields are read.
    private const int StructSize = 292;
    private const int RefreshPeriodOffset = 12;
    private const int RefreshCountOffset = 36;

    private readonly IntPtr buffer = Marshal.AllocHGlobal(StructSize);
    private ulong lastRefresh;

    /// <summary>The display time since the last call, in seconds; null if the compositor cannot say, or nothing changed.</summary>
    public double? Advance()
    {
        Marshal.WriteInt32(buffer, 0, StructSize);
        if (DwmGetCompositionTimingInfo(IntPtr.Zero, buffer) != 0)
        {
            lastRefresh = 0;
            return null;
        }

        ulong refresh = (ulong)Marshal.ReadInt64(buffer, RefreshCountOffset);
        long period = Marshal.ReadInt64(buffer, RefreshPeriodOffset);
        ulong previous = lastRefresh;
        lastRefresh = refresh;
        if (previous == 0 || refresh <= previous || period <= 0)
        {
            return null;
        }

        ulong refreshes = refresh - previous;
        if (refreshes > 30)
        {
            // Half a second or more: a stall or a wake; the clock's own clamp is the right judge of that.
            return null;
        }

        return refreshes * (double)period / System.Diagnostics.Stopwatch.Frequency;
    }

    /// <summary>The next frame starts afresh: after the rope has slept, the refreshes it slept through are not a frame.</summary>
    public void Reset() => lastRefresh = 0;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetCompositionTimingInfo(IntPtr window, IntPtr info);
}
