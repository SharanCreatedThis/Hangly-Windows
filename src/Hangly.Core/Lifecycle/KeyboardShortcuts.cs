//
//  KeyboardShortcuts.cs
//  Hangly
//
//  The shortcuts both apps share, in one table.
//

namespace Hangly.Core.Lifecycle;

/// <summary>Every shortcut the two apps share: Ctrl here where a Mac has ⌘. The macOS <c>HanglyShortcut</c>.</summary>
/// <remarks>
/// Decision B1: none of these is global. They work in Hangly's own windows, where they
/// cannot collide with any other app. A test on each side pins the table.
/// </remarks>
public enum HanglyShortcut
{
    ShowOrHide,
    Library,
    Create,
    Appearance,
    About,
    Search,
    Favourite,
    MoveUp,
    MoveDown,
    Close,
}

public static class HanglyShortcuts
{
    /// <summary>What the shortcut does, in words.</summary>
    public static string TitleOf(HanglyShortcut shortcut) => shortcut switch
    {
        HanglyShortcut.ShowOrHide => "Show or hide the charm",
        HanglyShortcut.Library => "Library",
        HanglyShortcut.Create => "Create",
        HanglyShortcut.Appearance => "Appearance",
        HanglyShortcut.About => "About",
        HanglyShortcut.Search => "Search the Library",
        HanglyShortcut.Favourite => "Favourite the selected charm or rope",
        HanglyShortcut.MoveUp => "Move the selected place up the rope",
        HanglyShortcut.MoveDown => "Move the selected place down the rope",
        _ => "Close the window",
    };

    /// <summary>The keys on Windows.</summary>
    public static string WindowsKeysOf(HanglyShortcut shortcut) => shortcut switch
    {
        HanglyShortcut.ShowOrHide => "Ctrl+Shift+O",
        HanglyShortcut.Library => "Ctrl+1",
        HanglyShortcut.Create => "Ctrl+2",
        HanglyShortcut.Appearance => "Ctrl+3",
        HanglyShortcut.About => "Ctrl+4",
        HanglyShortcut.Search => "Ctrl+F",
        HanglyShortcut.Favourite => "Ctrl+D",
        HanglyShortcut.MoveUp => "Alt+Up",
        HanglyShortcut.MoveDown => "Alt+Down",
        _ => "Ctrl+W",
    };

    /// <summary>The keys on a Mac, for the documentation both apps share.</summary>
    public static string MacKeysOf(HanglyShortcut shortcut) => shortcut switch
    {
        HanglyShortcut.ShowOrHide => "⇧⌘O",
        HanglyShortcut.Library => "⌘1",
        HanglyShortcut.Create => "⌘2",
        HanglyShortcut.Appearance => "⌘3",
        HanglyShortcut.About => "⌘4",
        HanglyShortcut.Search => "⌘F",
        HanglyShortcut.Favourite => "⌘D",
        HanglyShortcut.MoveUp => "⌥⌘↑",
        HanglyShortcut.MoveDown => "⌥⌘↓",
        _ => "⌘W",
    };
}
