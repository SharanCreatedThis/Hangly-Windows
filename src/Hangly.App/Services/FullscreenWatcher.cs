//
//  FullscreenWatcher.cs
//  Hangly
//
//  Reading, for FullscreenRule, what the window in front is doing.
//

using System.Runtime.InteropServices;
using System.Text;
using Hangly.Core.Fullscreen;
using Hangly.Core.Geometry;

namespace Hangly.App.Services;

/// <summary>Gathers <see cref="ForegroundFacts"/> from Windows. Every call here is documented.</summary>
/// <remarks>
/// When it runs, which matters more than what it reads: on a foreground change, from a
/// <c>SetWinEventHook</c> on the overlay's own thread, and on the overlay's existing
/// once-a-second housekeeping tick — only while the setting is on. The second is needed
/// because going full screen inside the same window (F11, a player's full-screen button)
/// changes no foreground, and the event that would see it, location change, fires for the
/// mouse cursor and everything else on screen all the time. macOS polls every two seconds
/// for the same reason.
/// </remarks>
internal static class FullscreenWatcher
{
    public static ForegroundFacts Read(IntPtr overlay)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        ForegroundFacts facts = ReadFacts(overlay);
        if (AuditsFullscreen)
        {
            Diagnostics.Log($"full-screen check took {System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMicroseconds:0} µs");
        }

        return facts;
    }

    private static ForegroundFacts ReadFacts(IntPtr overlay)
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return default(ForegroundFacts) with { IsShellOrDesktop = true };
        }

        GetWindowThreadProcessId(window, out uint process);
        bool isHangly = process == (uint)Environment.ProcessId;

        // The app in front may show its full screen in a window other than the one with
        // focus: VLC keeps its controls in front and plays in a separate, full-screen video
        // window. So a covering, caption-less window of the same process stands in for the
        // front one — the Windows counterpart of the macOS rule looking at every window.
        // The search walks every top-level window, so it runs when the foreground changes
        // and otherwise on one check in three: VLC going full screen is noticed within
        // three seconds, where macOS's poll takes two. Measured at 0.3–0.7 ms a search in
        // the VM, against 0.1 ms for the rest of the check.
        bool searchSiblings = window != lastFront || ++checksSinceSearch >= SiblingSearchEvery;
        if (searchSiblings)
        {
            checksSinceSearch = 0;
            lastFront = window;
            lastSibling = !isHangly && !CoversWithoutCaption(window) ? FullScreenSibling(window, process) : null;
        }

        if (lastSibling is IntPtr sibling && IsWindowVisible(sibling))
        {
            window = sibling;
        }

        GetWindowRect(window, out NativeRect bounds);
        IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(monitor, ref info);

        long style = GetWindowLongPtr(window, GwlStyle).ToInt64();
        bool hasCaption = (style & WsCaption) == WsCaption;

        var facts = new ForegroundFacts(
            WindowBounds: ToRect(bounds),
            MonitorBounds: ToRect(info.Monitor),
            HasCaption: hasCaption,
            IsShellOrDesktop: IsShell(window),
            IsHangly: isHangly,
            SameDisplayAsCharm: overlay != IntPtr.Zero && monitor == MonitorFromWindow(overlay, MonitorDefaultToNearest),
            ForegroundPlaying: false,
            ExclusiveFullScreen: false);

        // The shell's notification state costs 0.7–0.9 ms (measured), and an exclusive
        // full-screen game always covers its monitor, so it is asked only when a window does.
        if (FullscreenRule.CoversMonitor(facts))
        {
            facts = facts with
            {
                ExclusiveFullScreen = SHQueryUserNotificationState(out int state) == 0 && state == RunningD3DFullScreen,
            };
        }

        // Only worth asking the audio system when everything else already says full screen.
        if (!facts.HasCaption && !facts.IsShellOrDesktop && !facts.IsHangly && FullscreenRule.CoversMonitor(facts))
        {
            facts = facts with { ForegroundPlaying = IsPlaying(window, AppProcess(window, process)) };
        }

        if (AuditsFullscreenVerbose)
        {
            HashSet<uint> playing = Interop.AudioSessions.PlayingProcesses();
            Diagnostics.Log(
                $"full-screen check: front {NameOf(AppProcess(window, process))} covers={FullscreenRule.CoversMonitor(facts)} caption={facts.HasCaption} " +
                $"shell={facts.IsShellOrDesktop} same={facts.SameDisplayAsCharm} playing=[{string.Join(",", playing.Select(NameOf))}] -> {facts.ForegroundPlaying}");
        }

        return facts;
    }

    private const int SiblingSearchEvery = 3;
    private static IntPtr lastFront;
    private static IntPtr? lastSibling;
    private static int checksSinceSearch;

    private static bool CoversWithoutCaption(IntPtr window)
    {
        if ((GetWindowLongPtr(window, GwlStyle).ToInt64() & WsCaption) == WsCaption)
        {
            return false;
        }

        GetWindowRect(window, out NativeRect bounds);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromWindow(window, MonitorDefaultToNearest), ref info);
        return bounds.Left <= info.Monitor.Left + 2 && bounds.Top <= info.Monitor.Top + 2
            && bounds.Right >= info.Monitor.Right - 2 && bounds.Bottom >= info.Monitor.Bottom - 2;
    }

    /// <summary>A visible top-level window of the same process that is full screen, if any.</summary>
    private static IntPtr? FullScreenSibling(IntPtr front, uint process)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((candidate, _) =>
        {
            if (candidate != front && IsWindowVisible(candidate))
            {
                GetWindowThreadProcessId(candidate, out uint owner);
                if (owner == process && CoversWithoutCaption(candidate))
                {
                    found = candidate;
                    return false;
                }
            }

            return true;
        }, IntPtr.Zero);
        return found == IntPtr.Zero ? null : found;
    }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumChildProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);

    /// <summary>For audits only: <c>HANGLY_AUDIT_FULLSCREEN</c> logs what each check saw.</summary>
    private static readonly bool AuditsFullscreen = Environment.GetEnvironmentVariable("HANGLY_AUDIT_FULLSCREEN") is { Length: > 0 };

    /// <summary><c>HANGLY_AUDIT_FULLSCREEN=verbose</c> also lists who is playing, which costs a Core Audio read per check.</summary>
    private static readonly bool AuditsFullscreenVerbose = Environment.GetEnvironmentVariable("HANGLY_AUDIT_FULLSCREEN") == "verbose";

    /// <summary>Whether the application behind a process id has an active audio stream.</summary>
    /// <remarks>
    /// Matched by executable name, not process id: a browser plays its sound from a separate
    /// audio process of the same executable as the window.
    /// </remarks>
    private static bool IsPlaying(IntPtr window, uint process)
    {
        string? name = NameOf(process);
        string? app = null;
        bool isFrame = string.Equals(name, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase);
        if (isFrame)
        {
            // A packaged app drawing into a frame it does not own: match by app identity.
            app = Interop.AppIdentity.OfWindow(window);
        }

        foreach (uint playing in Interop.AudioSessions.PlayingProcesses())
        {
            if (playing == process)
            {
                return true;
            }

            if (!isFrame && name is not null && string.Equals(NameOf(playing), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (app is not null && string.Equals(Interop.AppIdentity.OfProcess(playing), app, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The process that owns the app, not its frame.</summary>
    /// <remarks>
    /// A Store app's top-level window can belong to <c>ApplicationFrameHost</c>, which only
    /// draws the frame; the app is whichever process owns a window inside it. Matched by
    /// "a different process" rather than by a class name, because the class differs by app
    /// generation: a <c>Windows.UI.Core.CoreWindow</c> for Netflix and Prime Video, something
    /// else for Windows 11's Media Player — which the VM test found when a class-name match
    /// missed it in full screen.
    /// </remarks>
    private static uint AppProcess(IntPtr window, uint process)
    {
        if (!string.Equals(NameOf(process), "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            return process;
        }

        uint inner = process;
        EnumChildWindows(window, (child, _) =>
        {
            GetWindowThreadProcessId(child, out uint owner);
            if (owner != 0 && owner != process)
            {
                inner = owner;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return inner;
    }

    private static string? NameOf(uint process)
    {
        try
        {
            using var found = System.Diagnostics.Process.GetProcessById((int)process);
            return found.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private delegate bool EnumChildProc(IntPtr window, IntPtr data);

    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr data);

    private static readonly string[] ShellClasses =
    [
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "LockScreenBackstopFrame", "WindowsScreensaverClass",
    ];

    private static bool IsShell(IntPtr window)
    {
        var name = new StringBuilder(128);
        GetClassName(window, name, name.Capacity);
        return ShellClasses.Contains(name.ToString(), StringComparer.Ordinal);
    }

    private static Rect ToRect(NativeRect rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    /// <summary>A foreground-change hook on the calling thread, which must pump messages.</summary>
    public static IntPtr HookForeground(WinEventProc callback) =>
        SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, callback, 0, 0, WineventOutOfContext);

    public static void Unhook(IntPtr hook)
    {
        if (hook != IntPtr.Zero)
        {
            UnhookWinEvent(hook);
        }
    }

    public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);

    private const uint EventSystemForeground = 0x0003;
    private const uint WineventOutOfContext = 0x0000;
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000;
    private const uint MonitorDefaultToNearest = 2;
    private const int RunningD3DFullScreen = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
}
