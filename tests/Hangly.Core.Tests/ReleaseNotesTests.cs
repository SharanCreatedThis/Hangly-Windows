//
//  ReleaseNotesTests.cs
//  Hangly.Core.Tests
//
//  What the About page shows when a release describes itself.
//

using Hangly.Core.Text;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>
/// Release notes: markdown in, something a text box can show out.
/// </summary>
public class ReleaseNotesTests
{
    [Fact]
    public void NothingToSayIsEmpty()
    {
        Assert.Equal(string.Empty, ReleaseNotes.Plain(null));
        Assert.Equal(string.Empty, ReleaseNotes.Plain("   \n\n  "));
    }

    [Fact]
    public void HeadingsLoseTheirHashes()
    {
        Assert.Equal("What's new", ReleaseNotes.Plain("## What's new"));
    }

    [Fact]
    public void BulletsKeepTheirShape()
    {
        Assert.Equal("• One\n• Two", ReleaseNotes.Plain("- One\n* Two"));
    }

    [Fact]
    public void EmphasisIsRemovedAndTheWordsStay()
    {
        Assert.Equal("Not signed yet.", ReleaseNotes.Plain("**Not signed** yet."));
        Assert.Equal("Uses Hangly.exe now.", ReleaseNotes.Plain("Uses `Hangly.exe` now."));
    }

    [Fact]
    public void LinkTextSurvivesAndTheUrlDoesNot()
    {
        Assert.Equal(
            "See the notes for details.",
            ReleaseNotes.Plain("See [the notes](https://example.com/x) for details."));
    }

    [Fact]
    public void RulesAreDroppedRatherThanDrawn()
    {
        Assert.Equal("Before\n\nAfter", ReleaseNotes.Plain("Before\n\n---\n\nAfter"));
    }

    [Fact]
    public void BlankRunsCollapseToOne()
    {
        Assert.Equal("One\n\nTwo", ReleaseNotes.Plain("One\n\n\n\n\nTwo"));
    }

    /// <summary>
    /// The shape 0.9.0 actually ships: a line, a blank, then a list. A wrapped bullet
    /// keeps its continuation on its own line, which is what the box was sized for.
    /// </summary>
    [Fact]
    public void TheRealSectionReadsAsProse()
    {
        string plain = ReleaseNotes.Plain(
            "The first Hangly for Windows.\n"
            + "\n"
            + "- A charm hangs from a rope on your desktop, swings when you push it,\n"
            + "  and settles the way a real one would.\n"
            + "- Seventy charms, in collections.\n");

        Assert.Equal(
            "The first Hangly for Windows.\n"
            + "\n"
            + "• A charm hangs from a rope on your desktop, swings when you push it,\n"
            + "and settles the way a real one would.\n"
            + "• Seventy charms, in collections.",
            plain);
    }
}
