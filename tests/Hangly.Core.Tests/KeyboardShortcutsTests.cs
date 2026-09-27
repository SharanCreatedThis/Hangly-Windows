using Hangly.Core.Lifecycle;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The shared shortcut table — the same assertions as the macOS KeyboardTests.</summary>
public class KeyboardShortcutsTests
{
    [Fact]
    public void TheTableMatchesTheMacKeyForKey()
    {
        Assert.Equal(10, Enum.GetValues<HanglyShortcut>().Length);
        Assert.All(Enum.GetValues<HanglyShortcut>(), shortcut =>
        {
            Assert.NotEmpty(HanglyShortcuts.WindowsKeysOf(shortcut));
            Assert.NotEmpty(HanglyShortcuts.MacKeysOf(shortcut));
        });
        Assert.Equal(("⌘1", "Ctrl+1"), (HanglyShortcuts.MacKeysOf(HanglyShortcut.Library), HanglyShortcuts.WindowsKeysOf(HanglyShortcut.Library)));
        Assert.Equal(("⇧⌘O", "Ctrl+Shift+O"), (HanglyShortcuts.MacKeysOf(HanglyShortcut.ShowOrHide), HanglyShortcuts.WindowsKeysOf(HanglyShortcut.ShowOrHide)));
        Assert.Equal(("⌥⌘↑", "Alt+Up"), (HanglyShortcuts.MacKeysOf(HanglyShortcut.MoveUp), HanglyShortcuts.WindowsKeysOf(HanglyShortcut.MoveUp)));
    }
}
