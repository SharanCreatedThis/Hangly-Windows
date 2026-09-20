//
//  LibraryParityTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Models;
using Hangly.Core.Settings;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The data the Library's new surfaces are built from.</summary>
public class LibraryParityTests
{
    [Fact(DisplayName = "Every collection has charms behind it")]
    public void CollectionsAreNotEmpty()
    {
        Assert.NotEmpty(CharmCatalog.Collections);

        foreach (CharmCollection collection in CharmCatalog.Collections)
        {
            Assert.Contains(CharmCatalog.All, charm => charm.CategoryId == collection.Id);
            Assert.False(string.IsNullOrWhiteSpace(collection.Description));
        }
    }

    [Fact(DisplayName = "Every collection is also an offered category")]
    public void CollectionsAreCategories()
    {
        var categories = CharmCatalog.Categories.Select(category => category.Id).ToHashSet();
        foreach (CharmCollection collection in CharmCatalog.Collections)
        {
            Assert.Contains(collection.Id, categories);
        }
    }

    /// <summary>The detail panel shows all four, so all four have to be there.</summary>
    [Fact(DisplayName = "Every charm has a region, a description and at least one tag")]
    public void EveryCharmIsDescribed()
    {
        string[] undescribed =
        [
            .. CharmCatalog.All
                .Where(charm =>
                    string.IsNullOrWhiteSpace(charm.Region)
                    || string.IsNullOrWhiteSpace(charm.Description)
                    || charm.Tags.Count == 0)
                .Select(charm => charm.Id),
        ];

        Assert.Empty(undescribed);
    }

    [Fact(DisplayName = "A secret is never the one just shown")]
    public void SecretsDoNotRepeat()
    {
        var random = new Random(7);
        string? previous = null;

        for (int draw = 0; draw < 400; draw++)
        {
            string secret = SecretVault.Reveal(random, previous);
            Assert.NotEqual(previous, secret);
            Assert.True(
                secret == SecretVault.Rare || SecretVault.Secrets.Contains(secret),
                $"'{secret}' is not in the vault");

            previous = secret;
        }
    }

    [Fact(DisplayName = "The rare secret is rare, but does turn up")]
    public void TheRareSecretIsRare()
    {
        var random = new Random(11);
        string? previous = null;
        int rare = 0;

        for (int draw = 0; draw < 4000; draw++)
        {
            string secret = SecretVault.Reveal(random, previous);
            if (secret == SecretVault.Rare)
            {
                rare++;
            }

            previous = secret;
        }

        Assert.InRange(rare, 1, 400);
    }

    [Fact(DisplayName = "Milestone counters never go backwards past zero")]
    public void MilestonesAreClamped()
    {
        var settings = new AppSettings
        {
            Milestones = new MilestoneSettings
            {
                LaunchCount = -3,
                CharmsHung = -1,
                SecretsFound = -9,
                SwingsSurvived = -100,
            },
        }.Clamped();

        Assert.Equal(0, settings.Milestones.LaunchCount);
        Assert.Equal(0, settings.Milestones.CharmsHung);
        Assert.Equal(0, settings.Milestones.SecretsFound);
        Assert.Equal(0, settings.Milestones.SwingsSurvived);
    }

    /// <summary>A push has to be a push: the charm must actually move sideways.</summary>
    [Fact(DisplayName = "Pushing the rope sends the charm the way it was pushed")]
    public void PushMovesTheCharm()
    {
        var canvas = new Hangly.Core.Geometry.Size(320, 360);
        var rope = new Hangly.Core.Physics.RopeSimulation();
        rope.Fit(canvas, 1, 1);
        rope.Start();

        for (int tick = 0; tick < 1200; tick++)
        {
            rope.Step(1.0 / 120.0);
        }

        double settled = rope.Snapshot().Charms[^1].Center.X;
        rope.Push(1);
        for (int tick = 0; tick < 30; tick++)
        {
            rope.Step(1.0 / 120.0);
        }

        Assert.True(
            rope.Snapshot().Charms[^1].Center.X > settled,
            "a rightward push did not move the charm right");
    }
}
