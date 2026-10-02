using Hangly.Core.Text;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>When the release notes open after an update. The same cases as macOS's <c>ReleaseNotesTests</c>.</summary>
public sealed class ReleaseHighlightsTests
{
    [Fact]
    public void OnceAfterAnUpdateToANewFeatureVersion()
    {
        Assert.True(ReleaseHighlights.ShouldShow("2.1.2", "2.2.0", updatedBefore: true));
        Assert.True(ReleaseHighlights.ShouldShow("0.9.4", "2.2.0", updatedBefore: true));
        // Recorded now, so the next launch does not show them again.
        Assert.False(ReleaseHighlights.ShouldShow("2.2.0", "2.2.0", updatedBefore: true));
    }

    [Fact]
    public void ZeroNineDidNotRecordItsVersionSoAnInstallThatRanBeforeCounts()
    {
        Assert.True(ReleaseHighlights.ShouldShow(null, "2.2.0", updatedBefore: true));
        Assert.False(ReleaseHighlights.ShouldShow(null, "2.2.0", updatedBefore: false));
    }

    [Fact]
    public void APatchStaysSilentAndAVersionWithoutWordsShowsNothing()
    {
        Assert.False(ReleaseHighlights.ShouldShow("2.2.0", "2.2.1", updatedBefore: true));
        Assert.False(ReleaseHighlights.ShouldShow("2.2.1", "2.3.0", updatedBefore: true));
    }

    [Fact]
    public void TheWordsAreShortAndNameNoOtherProduct()
    {
        Assert.InRange(ReleaseHighlights.Items.Count, 4, 8);
        foreach (Highlight item in ReleaseHighlights.Items)
        {
            Assert.InRange(item.Title.Length, 1, 32);
            Assert.InRange(item.Text.Length, 1, 150);
            Assert.DoesNotContain("PostHog", item.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("coffee", item.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
