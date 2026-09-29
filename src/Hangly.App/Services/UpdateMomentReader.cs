//
//  UpdateMomentReader.cs
//  Hangly
//
//  What the machine is doing right now, for UpdateTiming.
//

using System.Runtime.InteropServices;
using Hangly.Core.Lifecycle;

namespace Hangly.App.Services;

/// <summary>Reads the signals <see cref="UpdateTiming"/> decides on. Cheap; read once a minute while an update waits.</summary>
internal static class UpdateMomentReader
{
    /// <summary>SHQueryUserNotificationState: a screen saver is up, the session is locked, or another user's is active.</summary>
    private const int QunsNotPresent = 1;

    public static UpdateMoment Read(TimeSpan sinceLaunch, bool windowOpen) => new(
        Idle: IdleTime(),
        SinceLaunch: sinceLaunch,
        Away: SHQueryUserNotificationState(out int state) == 0 && state == QunsNotPresent,
        WindowOpen: windowOpen);

    /// <summary>Time since the last keyboard or mouse input in this session.</summary>
    private static TimeSpan IdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time))
            : TimeSpan.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
