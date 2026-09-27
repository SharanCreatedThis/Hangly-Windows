//
//  RelaunchSignal.cs
//  Hangly
//
//  "Somebody opened Hangly again": carried from the copy that is turned away to the copy that runs.
//

namespace Hangly.Core.Lifecycle;

/// <summary>A named, session-scoped event the running copy listens on and a second copy sets.</summary>
/// <remarks>
/// <b>Why it exists.</b> People open Hangly again — from the Start menu, a desktop
/// shortcut, the .exe — when they want to change their charm and have not found the tray
/// icon. The second copy is turned away by the single-instance mutex, as it must be, and
/// until this existed it left silently: the click looked broken. Now it sets this event
/// and the running copy opens the Library, which is what macOS does with its reopen event.
///
/// <para><b>An event, not a pipe or a window message.</b> There is one thing to say and no
/// reply to wait for. A kernel event says it with nothing to parse, and the listener is a
/// thread-pool wait (<see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle, WaitOrTimerCallback, object?, int, bool)"/>)
/// — no thread of its own and no CPU at all until the event is set.</para>
///
/// <para><b>Created early, listened to later.</b> The running copy creates the event when
/// it claims the mutex, before any window exists, and starts listening once it has a
/// Library to open. It is auto-reset, so a relaunch that lands in between is not lost:
/// the event stays set until the listener takes it.</para>
///
/// <para><b>Windows only.</b> Named events are a Windows kernel object; elsewhere every
/// member is a harmless no-op, so Core stays buildable and testable on any platform.</para>
/// </remarks>
public sealed class RelaunchSignal : IDisposable
{
    /// <summary>Session-scoped, like the single-instance mutex it sits next to.</summary>
    public const string DefaultName = @"Local\Hangly.OpenLibrary";

    private readonly EventWaitHandle? handle;
    private RegisteredWaitHandle? registration;

    private RelaunchSignal(EventWaitHandle? handle) => this.handle = handle;

    /// <summary>Creates (or opens) the event, as the copy that will listen. Call once, early.</summary>
    public static RelaunchSignal Create(string name = DefaultName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new RelaunchSignal(null);
        }

        return new RelaunchSignal(new EventWaitHandle(false, EventResetMode.AutoReset, name));
    }

    /// <summary>Calls <paramref name="onRelaunch"/>, on a thread-pool thread, each time the event is set.</summary>
    /// <remarks>Only the first call registers; the callback should marshal to its own thread.</remarks>
    public void Listen(Action onRelaunch)
    {
        ArgumentNullException.ThrowIfNull(onRelaunch);
        if (handle is null || registration is not null)
        {
            return;
        }

        registration = ThreadPool.RegisterWaitForSingleObject(
            handle,
            (_, _) => onRelaunch(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Tells the running copy to open its Library. True when it was told.</summary>
    /// <param name="patience">
    /// How long to keep trying when the running copy has claimed the mutex but not yet
    /// created the event — two launches a moment apart. Tried every 100 ms.
    /// </param>
    public static bool Send(TimeSpan patience, string name = DefaultName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        DateTime giveUp = DateTime.UtcNow + patience;
        while (true)
        {
            if (EventWaitHandle.TryOpenExisting(name, out EventWaitHandle? existing))
            {
                using (existing)
                {
                    return existing.Set();
                }
            }

            if (DateTime.UtcNow >= giveUp)
            {
                return false;
            }

            Thread.Sleep(100);
        }
    }

    public void Dispose()
    {
        registration?.Unregister(null);
        registration = null;
        handle?.Dispose();
    }
}
