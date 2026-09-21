//
//  SingleInstance.cs
//  Hangly
//
//  One Hangly per session, decided before a window exists.
//

namespace Hangly.App.Services;

/// <summary>Makes sure only one copy of Hangly is running, and steps aside if one is.</summary>
/// <remarks>
/// <b>What went wrong without it.</b> Starting the app twice produced two of everything:
/// two overlays drawing two ropes, two tray icons, and — the part that cost something —
/// two processes that both own <c>settings.json</c>. <see cref="Core.Settings.SettingsStore"/>
/// reads the file once at construction and writes the whole document on every change, so
/// the second copy's first write erased whatever the first copy had done since it
/// started. Measured, not theorised: two processes at 146 MB each, and a name typed in
/// one window gone after a rope style was chosen in the other.
///
/// <para><b>A named mutex, not a window search.</b> Looking for an existing window is a
/// race — two copies launched together both look, both find nothing, and both carry on.
/// The kernel decides a mutex exactly once, and hands exactly one caller
/// <c>createdNew</c>, whichever order they arrive in.</para>
///
/// <para><b><c>Local\</c>, not <c>Global\</c>.</b> The name is session-scoped, so two
/// people signed in to the same machine each get their own Hangly, which is what a
/// per-user install means. A global name would let one person's session stop another's
/// app from starting.</para>
///
/// <para><b>Nothing is released by hand.</b> Windows drops the mutex when the process
/// ends, however it ends — a clean quit, a crash, or being killed from Task Manager — so
/// there is no stale lock to recover from and no file to clean up. The handle is held in
/// a static field for exactly one reason: to keep the garbage collector from finalising
/// it while the app is still running.</para>
/// </remarks>
public static class SingleInstance
{
    /// <summary>
    /// Session-scoped, and named for the app rather than for the path it runs from.
    /// </summary>
    /// <remarks>
    /// Deliberately not derived from the install directory. An installed copy and a copy
    /// run out of a build folder are still two Hanglys writing to one settings file, and
    /// the file is what this protects.
    /// </remarks>
    private const string Name = @"Local\Hangly.SingleInstance";

    /// <summary>Held for the life of the process. See the note on the type.</summary>
    private static Mutex? held;

    /// <summary>
    /// Claims the right to be the running copy. False means another copy has it.
    /// </summary>
    /// <remarks>
    /// Called on the ordinary launch path only — after Velopack's hooks, which run in
    /// their own short-lived processes and must not be turned away, and after the
    /// development switches, which do their work without a window and exit.
    ///
    /// <para>An <see cref="AbandonedMutexException"/> means the previous holder died
    /// without releasing: that copy is gone, this one is now the only Hangly, and the
    /// right answer is to take over rather than refuse to start.</para>
    /// </remarks>
    public static bool Claim()
    {
        try
        {
            held = new Mutex(initiallyOwned: true, Name, out bool createdNew);
            if (createdNew)
            {
                return true;
            }

            // Not the owner. Drop the handle rather than leaving it open, so nothing about
            // this process affects the copy that is running.
            held.Dispose();
            held = null;
            return false;
        }
        catch (AbandonedMutexException)
        {
            // Inherited from a process that died. It is ours now.
            return true;
        }
        catch (Exception exception)
        {
            // A profile that refuses to create a kernel object is not a reason to refuse
            // to start. One Hangly is better than two; a running Hangly is better than
            // neither.
            Diagnostics.Log($"single-instance check unavailable: {exception.GetType().Name}");
            return true;
        }
    }
}
