//
//  FrameAudit.cs
//  Hangly
//
//  For the smoothness and leak checks only (HANGLY_AUDIT_FRAMES=1): every frame's own breakdown, and which objects are
//  still alive. Inert, and allocation-free, when the variable is not set.
//

using Hangly.App.Services;

namespace Hangly.App.Overlay;

/// <summary>Per-frame accounting for the overlay's frame loop.</summary>
/// <remarks>
/// The loop already logs the worst of each part every three seconds, which says a slow frame happened but not what that
/// frame was doing: the worst rope time and the worst present time of a window may be two different frames. This keeps
/// each frame's own numbers and reports a frame over budget whole — a spike line naming its physics, sounds, layout, rope,
/// beads, charms, present, uploads to the graphics card, garbage collections and allocations — and keeps the averages a
/// stress run is judged by, and the worst frame of the first seconds after launch.
/// </remarks>
internal sealed class FrameAudit
{
    /// <summary>A frame's work over this is reported whole: a 60 Hz frame's budget.</summary>
    public const double SpikeMs = 16.7;

    /// <summary>How long after the overlay starts counts as start-up.</summary>
    public const double StartupSeconds = 8;

    private readonly long started = System.Diagnostics.Stopwatch.GetTimestamp();
    private long frameStarted;
    private long allocatedAtStart;
    private int gen0, gen1, gen2;
    private long physicsBytes;

    // A three-second window's moving frames, and start-up's.
    private int frames;
    private double workTotal, workWorst;
    private long bytesTotal, physicsBytesTotal;
    private int startupFrames;
    private double startupWorst, startupWorstAt, firstFrameAt = -1;
    private bool startupReported;

    /// <summary>The steps of this frame's tick, as <c>OverlayWindow.Sub</c> names them.</summary>
    public readonly double[] Sub = new double[6];

    public void BeginFrame()
    {
        frameStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
        gen0 = GC.CollectionCount(0);
        gen1 = GC.CollectionCount(1);
        gen2 = GC.CollectionCount(2);
        Array.Clear(Sub);
        RenderTimes.ResetFrame();
        Array.Clear(PresentTimes.Frame);
    }

    /// <summary>What the solver's step allocated, measured around it alone.</summary>
    public void NotePhysicsBytes(long bytes) => physicsBytes = bytes;

    /// <summary>Closes the frame. <paramref name="drew"/>: whether it drew, which is what start-up and averages count.</summary>
    public void EndFrame(bool drew, bool moving, double interval)
    {
        double work = System.Diagnostics.Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocatedAtStart;
        double sinceStart = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds;
        int collections0 = GC.CollectionCount(0) - gen0, collections1 = GC.CollectionCount(1) - gen1, collections2 = GC.CollectionCount(2) - gen2;

        if (drew && sinceStart <= StartupSeconds)
        {
            if (firstFrameAt < 0)
            {
                firstFrameAt = sinceStart;
            }

            startupFrames++;
            if (work > startupWorst)
            {
                startupWorst = work;
                startupWorstAt = sinceStart;
            }
        }
        else if (!startupReported && sinceStart > StartupSeconds)
        {
            startupReported = true;
            Diagnostics.Log($"startup: first frame at {firstFrameAt * 1000:0} ms, {startupFrames} frames drawn in {StartupSeconds:0} s, " +
                $"worst frame {startupWorst:0.0} ms at {startupWorstAt:0.00} s");
        }

        if (drew && moving)
        {
            frames++;
            workTotal += work;
            workWorst = Math.Max(workWorst, work);
            bytesTotal += bytes;
            physicsBytesTotal += physicsBytes;
        }

        if (drew && work > SpikeMs)
        {
            Diagnostics.Log(
                $"SPIKE at {sinceStart:0.00} s: work {work:0.0} ms, interval {interval * 1000:0.0} ms; " +
                $"tick step {Sub[1]:0.0}, sounds {Sub[2]:0.0}, swings {Sub[3]:0.0}, rest {Sub[4]:0.0}, draw {Sub[5]:0.0}; " +
                $"draw: session open {PresentTimes.Frame[0]:0.0}, renderer {PresentTimes.Frame[1]:0.0} " +
                $"(layout {RenderTimes.Frame[0]:0.0}, rope {RenderTimes.Frame[1]:0.0} [to layer {RenderTimes.FrameParts[0]:0.0}, cords {RenderTimes.FrameParts[1]:0.0}, " +
                $"cut-outs {RenderTimes.FrameParts[2]:0.0}, close {RenderTimes.FrameParts[3]:0.0}, composite {RenderTimes.FrameParts[4]:0.0}], " +
                $"beads {RenderTimes.Frame[2]:0.0}, charms {RenderTimes.Frame[3]:0.0}), session close {PresentTimes.Frame[2]:0.0}, present {PresentTimes.Frame[3]:0.0}; " +
                $"uploads {RenderTimes.FrameUploads}; GC +{collections0}/+{collections1}/+{collections2}; allocated {bytes / 1024.0:0.0} KB (physics {physicsBytes} B)");
        }
    }

    /// <summary>The window's averages, then cleared: moving frames, their mean and worst work, and bytes per frame.</summary>
    public string Take()
    {
        string text = frames == 0
            ? "no moving frames"
            : $"{frames} moving frames, work mean {workTotal / frames:0.00} ms, worst {workWorst:0.0} ms; " +
              $"allocated per frame {bytesTotal / (double)frames:0} B (physics {physicsBytesTotal / (double)frames:0} B)";
        frames = 0;
        workTotal = 0;
        workWorst = 0;
        bytesTotal = 0;
        physicsBytesTotal = 0;
        return text;
    }
}

/// <summary>For the leak check only: the overlay's long-lived objects, held weakly, so what is still alive can be counted
/// after a collection.</summary>
internal static class LiveObjects
{
    private static readonly List<(string Kind, WeakReference Reference)> Tracked = [];
    private static readonly Lock Gate = new();

    public static void Track(string kind, object instance)
    {
        if (!PresentTimes.On)
        {
            return;
        }

        lock (Gate)
        {
            Tracked.Add((kind, new WeakReference(instance)));
        }
    }

    /// <summary>After a full collection: how many of each kind were made, and how many are still alive.</summary>
    public static string Report()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        lock (Gate)
        {
            return string.Join(", ", Tracked
                .GroupBy(entry => entry.Kind)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key} {group.Count(entry => entry.Reference.IsAlive)} alive of {group.Count()} made"));
        }
    }
}
