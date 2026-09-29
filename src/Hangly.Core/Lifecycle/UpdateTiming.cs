//
//  UpdateTiming.cs
//  Hangly
//
//  When a downloaded update installs itself: while nobody is there.
//

namespace Hangly.Core.Lifecycle;

/// <summary>What the machine is doing, as far as installing an update is concerned.</summary>
/// <param name="Idle">Time since the last keyboard or mouse input.</param>
/// <param name="SinceLaunch">How long this Hangly has been running.</param>
/// <param name="Away">The session is locked, a screen saver is up, or another user's session is active.</param>
/// <param name="WindowOpen">A Hangly window (Library, welcome, a card) is on screen.</param>
public readonly record struct UpdateMoment(TimeSpan Idle, TimeSpan SinceLaunch, bool Away, bool WindowOpen);

/// <summary>Decides when a downloaded update is installed. macOS's <c>UpdateTiming</c>, case for case.</summary>
/// <remarks>
/// <b>Only while the person is away.</b> Installing restarts Hangly, and the new version opens its release notes in
/// the middle of the screen, so it must never happen in front of anybody — over their work, a film or a call.
/// Waiting for a Quit alone could take weeks (Hangly starts at sign-in and is rarely quit), so the update goes in
/// once the session is locked or a screen saver is up and there has been no input for a couple of minutes; the
/// release notes are waiting when they come back. Otherwise the next Quit or start installs it, as before.
/// <para>Being idle in front of an unlocked screen is not enough: whether a film is playing cannot be read
/// reliably without admin rights, and release notes over a film would be the opposite of silent.</para>
/// </remarks>
public static class UpdateTiming
{
    /// <summary>Never this soon after a start: a launch is not interrupted, and a failed install cannot loop.</summary>
    public static readonly TimeSpan SettleAfterLaunch = TimeSpan.FromMinutes(10);

    /// <summary>Away this long without input — so unlocking the machine does not catch it mid-restart.</summary>
    public static readonly TimeSpan IdleWhileAway = TimeSpan.FromMinutes(2);

    /// <summary>How often a waiting update looks for its moment.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(1);

    public static bool ShouldInstall(UpdateMoment moment) =>
        moment.SinceLaunch >= SettleAfterLaunch
        && !moment.WindowOpen
        && moment.Away
        && moment.Idle >= IdleWhileAway;
}
