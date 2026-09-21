//
//  ProcessLifetime.cs
//  Hangly
//
//  Keeping the app alive when it has no windows of its own.
//

using Hangly.App.Services;
using Microsoft.UI.Xaml;

namespace Hangly.App.Onboarding;

/// <summary>Holds the process open while Hangly has no XAML window on screen.</summary>
/// <remarks>
/// <b>WinUI ends the process when its last window closes.</b> Hangly usually has none —
/// the charm is a plain Win32 layered window and the tray is a message-only one — so for
/// most of its life there is no window to close and nothing to end. The moment one does
/// exist and then goes away, the count falls from one to zero and WinUI shuts everything
/// down.
///
/// <para><b>Which is exactly what first-run did.</b> The welcome card was the only XAML
/// window in the process; pressing <i>Get started</i> closed it, WinUI ended the app, and
/// the charm and the tray icon went with it. Testers had to find Hangly again and start it
/// a second time before it did anything, which reads as the installer having failed.
/// Measured on a clean profile: <c>welcome card completed</c> in the log, then no
/// process.</para>
///
/// <para><b>The fix is to let the onboarding windows hide rather than close</b>, which is
/// what <see cref="Customize.CustomizeWindow"/> already does. A window that is hidden has
/// not closed, so the count never reaches zero, and the window is released for real when
/// the app quits.</para>
///
/// <para><b>Why not one permanent window that is never shown.</b> That was the first fix
/// and it worked, and it was measured: an empty <c>Window</c> costs about twelve megabytes
/// of working set and two hundred handles, on <em>every</em> launch, to solve a problem
/// that only exists on the few launches where an onboarding card appears. Hiding the card
/// that is already there costs nothing on any other launch.</para>
/// </remarks>
public static class ProcessLifetime
{
    private static readonly List<Window> Held = [];

    /// <summary>
    /// Makes a window hide instead of closing, and keeps it alive until the app quits.
    /// </summary>
    /// <remarks>
    /// Called by the onboarding windows, which are the only ones that can be the last
    /// window in the process. Dismissing one looks exactly the same to the person doing
    /// it — the window goes — and the difference is that the app is still there
    /// afterwards.
    /// </remarks>
    public static void KeepAlive(Window window)
    {
        Held.Add(window);

        window.AppWindow.Closing += (sender, args) =>
        {
            if (!Held.Contains(window))
            {
                return;
            }

            args.Cancel = true;
            sender.Hide();
            Diagnostics.Log("onboarding window hidden; the app stays running");
        };
    }

    /// <summary>
    /// Dismisses a kept window the way its own buttons should: hidden, not closed.
    /// </summary>
    /// <remarks>
    /// <b><c>Window.Close()</c> does not raise <c>AppWindow.Closing</c>.</b> That event is
    /// for the title bar's close button, so the guard in <see cref="KeepAlive"/> catches a
    /// person dismissing the card and does nothing at all for the card dismissing itself.
    /// Measured: with the guard in place and the buttons still calling <c>Close()</c>, the
    /// app went down on <i>Start Using Hangly</i> exactly as it had before.
    /// </remarks>
    public static void Dismiss(Window window) => window.AppWindow.Hide();

    /// <summary>Lets the process end. Called on the way out, and nowhere else.</summary>
    public static void Release()
    {
        Window[] windows = [.. Held];
        Held.Clear();

        foreach (Window window in windows)
        {
            window.Close();
        }
    }
}
