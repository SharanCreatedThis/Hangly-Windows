//
//  WindowMode.cs
//  Hangly
//
//  Whether the charm hangs over everything or on the desktop behind it.
//

namespace Hangly.Core.Models;

/// <summary>Appearance → Behaviour → Window. The same two choices, in the same order and words, as macOS.</summary>
/// <remarks>Always on Top is what Hangly has always done, so it is the default and nothing changes on update.</remarks>
public enum WindowMode
{
    /// <summary>Above every app's windows.</summary>
    OnTop,

    /// <summary>Above the wallpaper and the desktop icons, behind every app's windows.</summary>
    Desktop,
}

public static class WindowModeTable
{
    /// <summary>For the tray's "Always on Top" tick: ticked is Always on Top, unticked On the Desktop. The macOS menu reads it the same way.</summary>
    public static WindowMode FromAlwaysOnTop(bool alwaysOnTop) => alwaysOnTop ? WindowMode.OnTop : WindowMode.Desktop;

    public static bool IsAlwaysOnTop(WindowMode mode) => mode == WindowMode.OnTop;

    public static string TitleOf(WindowMode mode) => mode == WindowMode.Desktop ? "On the Desktop" : "Always on Top";

    /// <summary>Whether a top-level window of this class is the desktop itself.</summary>
    /// <remarks>
    /// <c>Progman</c> is the desktop; <c>WorkerW</c> is the window Explorer moves the icons
    /// and the wallpaper into on some builds. A cursor over either is over the desktop, so a
    /// charm hung there is what it is over. The full-screen watcher recognises the shell
    /// by the same two names.
    /// </remarks>
    public static bool IsDesktopClass(string className) => className is "Progman" or "WorkerW";
}
