//
//  FullscreenRule.cs
//  Hangly
//
//  Whether a film is on, on the display the charm hangs on.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Fullscreen;

/// <summary>What the system says about the window in front, read by the platform side.</summary>
/// <param name="WindowBounds">The foreground window, in desktop pixels.</param>
/// <param name="MonitorBounds">The whole monitor that window is on, not its work area.</param>
/// <param name="HasCaption">Whether the window has a title bar (<c>WS_CAPTION</c>).</param>
/// <param name="IsShellOrDesktop">The desktop, taskbar, Start, lock screen or screen saver.</param>
/// <param name="IsHangly">One of Hangly's own windows.</param>
/// <param name="SameDisplayAsCharm">Whether that monitor is the one the charm hangs on.</param>
/// <param name="ForegroundPlaying">The application in front has an active audio stream.</param>
/// <param name="ExclusiveFullScreen">Windows reports a Direct3D exclusive full-screen app.</param>
public readonly record struct ForegroundFacts(
    Rect WindowBounds,
    Rect MonitorBounds,
    bool HasCaption,
    bool IsShellOrDesktop,
    bool IsHangly,
    bool SameDisplayAsCharm,
    bool ForegroundPlaying,
    bool ExclusiveFullScreen);

/// <summary>The rule: hide the charm when full-screen content is playing on its display.</summary>
/// <remarks>
/// The macOS <c>FullscreenVideoWatcher</c>'s rule, restated for Windows: two signals, both
/// needed, because each alone is wrong.
///
/// <list type="bullet">
/// <item><b>Full screen</b> — the window in front covers its whole monitor, taskbar strip
/// included, and has no title bar. The title bar is what separates a maximised window when
/// the taskbar auto-hides (which also covers the whole monitor) from real full screen.</item>
/// <item><b>Playing</b> — the application in front has an active audio stream. A film, a
/// game and a video in a browser all do; a borderless utility, a slideshow of still pages
/// and a full-screen document do not.</item>
/// </list>
///
/// <para>A Direct3D exclusive full-screen game counts on its own: nothing else can be on
/// screen with it.</para>
///
/// <para><b>Why not "something is keeping the display awake", which is what macOS reads.</b>
/// It was the first version, and the VM test matrix failed four of nine cases on it:
/// Windows' aggregate display-required state was set with nothing playing, because
/// Parallels Tools holds a permanent display request — as keep-awake utilities, video calls
/// and some GPU tools do on ordinary PCs. Windows offers no documented way for an ordinary
/// process to learn <em>who</em> holds a power request; macOS does, which is why the rule
/// differs there. Audio sessions are attributable to a process, so the signal can be tied
/// to the window in front.</para>
///
/// <para>And one rule macOS gains at the same time: only on the display the charm hangs on.
/// A film on the other monitor is not in the charm's way.</para>
/// </remarks>
public static class FullscreenRule
{
    /// <summary>Pixels of slack when comparing a window with its monitor.</summary>
    public const double Tolerance = 2;

    public static bool ShouldHide(ForegroundFacts facts)
    {
        if (facts.IsShellOrDesktop || facts.IsHangly || !facts.SameDisplayAsCharm)
        {
            return false;
        }

        if (facts.ExclusiveFullScreen)
        {
            return true;
        }

        return CoversMonitor(facts) && !facts.HasCaption && facts.ForegroundPlaying;
    }

    /// <summary>Whether the window covers its whole monitor.</summary>
    public static bool CoversMonitor(ForegroundFacts facts) =>
        facts.WindowBounds.Left <= facts.MonitorBounds.Left + Tolerance
        && facts.WindowBounds.Top <= facts.MonitorBounds.Top + Tolerance
        && facts.WindowBounds.Right >= facts.MonitorBounds.Right - Tolerance
        && facts.WindowBounds.Bottom >= facts.MonitorBounds.Bottom - Tolerance;
}
