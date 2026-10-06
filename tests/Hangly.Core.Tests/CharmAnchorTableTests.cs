//
//  CharmAnchorTableTests.cs
//  Hangly
//
//  The shared anchor table ships with the app and covers every built-in charm. Measuring it against the artwork is
//  macOS's CharmAnchorTests (and this app's --check-artwork parity report); this holds the table itself.
//

using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

public class CharmAnchorTableTests
{
    private static string TablePath()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "src", "Hangly.App", "Assets", "Anchors", "CharmAnchors.json")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        return Path.Combine(directory, "src", "Hangly.App", "Assets", "Anchors", "CharmAnchors.json");
    }

    [Fact(DisplayName = "The anchor table covers every built-in charm, and nothing else")]
    public void CoversTheCatalogue()
    {
        CharmAnchors? table = CharmAnchors.Load(TablePath());
        Assert.NotNull(table);
        var ids = CharmCatalog.All.Select(entry => entry.Id).ToHashSet();
        var missing = ids.Where(id => !table.Charms.ContainsKey(id)).ToList();
        var stale = table.Charms.Keys.Where(id => !ids.Contains(id)).ToList();
        Assert.True(missing.Count == 0, $"No anchors for {string.Join(", ", missing)}. Run the Mac repo's Scripts/generate-charm-anchors.sh.");
        Assert.True(stale.Count == 0, $"Anchors for charms that do not exist: {string.Join(", ", stale)}.");
    }

    [Fact(DisplayName = "A ring charm's anchors replace the measured hook and exit")]
    public void AppliesToRegions()
    {
        CharmAnchors table = CharmAnchors.Load(TablePath())!;
        CharmAnchors.Entry bell = table.Charms["templeBell"];
        var measured = new CharmArtworkRegions(new Hangly.Core.Geometry.Rect(0.2, 0.1, 0.6, 0.8), []);
        CharmArtworkRegions applied = CharmAnchors.Apply(measured, bell);
        Assert.NotNull(applied.Hook);
        Assert.Equal(bell.Hook!.EyeTop, applied.Hook!.Value.EyeTop);
        Assert.Equal(bell.BottomExit!.Y, applied.AxisLastBottom);
        Assert.NotNull(applied.RopeLeavesOffset);
    }
}
