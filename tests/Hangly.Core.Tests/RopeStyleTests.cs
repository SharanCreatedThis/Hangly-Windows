//
//  RopeStyleTests.cs
//  Hangly.Core.Tests
//

using Hangly.Core.Models;
using Hangly.Core.Physics;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The style and time-of-day tables, and the invariants that hold them still.</summary>
public class RopeStyleTests
{
    [Fact(DisplayName = "Thread is exactly the shipped configuration")]
    public void ThreadIsTheBaseline()
    {
        RopePhysicsProfile thread = RopeStyleTable.PhysicsOf(RopeStyle.Thread);
        RopeConfiguration defaults = RopeConfiguration.Default;

        // The whole defence against the rope Hangly shipped with drifting as styles are
        // added: thread is not a style so much as the name for what the rope already did.
        Assert.Equal(1.0, thread.GravityScale);
        Assert.Equal(defaults.Damping, thread.Damping);
        Assert.Equal(defaults.MaxStretchRatio, thread.MaxStretchRatio);
        Assert.Equal(defaults.ConstraintIterations, thread.ConstraintIterations);
        Assert.Equal(defaults.StretchPasses, thread.StretchPasses);
        Assert.Equal(1.0, thread.CharmMassScale);
    }

    [Fact(DisplayName = "Applying thread to the defaults changes nothing")]
    public void ApplyingThreadIsTheIdentity()
    {
        RopeConfiguration defaults = RopeConfiguration.Default;

        Assert.Equal(defaults, defaults.Applying(RopeStyle.Thread));
    }

    [Fact(DisplayName = "Applying a style is idempotent and order-independent")]
    public void ApplyingIsOrderIndependent()
    {
        RopeConfiguration defaults = RopeConfiguration.Default;

        // Applying leather and then thread gives thread, not a rope that remembers
        // being leather.
        RopeConfiguration viaLeather = defaults.Applying(RopeStyle.Leather).Applying(RopeStyle.Thread);

        Assert.Equal(defaults.Applying(RopeStyle.Thread), viaLeather);
    }

    [Fact(DisplayName = "Every style is distinct in the way it swings")]
    public void StylesAreNotNineNamesForOneRope()
    {
        var profiles = new HashSet<RopePhysicsProfile>();

        foreach (RopeStyle style in RopeStyleTable.All)
        {
            RopePhysicsProfile physics = RopeStyleTable.PhysicsOf(style);
            profiles.Add(physics);

            // A style may not make the solver unstable whatever else it does.
            Assert.InRange(physics.Damping, 0.9, 1.0);
            Assert.InRange(physics.MaxStretchRatio, 1.0, 1.1);
            Assert.True(physics.ConstraintIterations > RopeConfiguration.Default.SegmentCount);
            Assert.True(physics.GravityScale > 0);
            Assert.True(physics.CharmMassScale > 0);
        }

        // The real risk in this feature is not that it crashes but that it ships as nine
        // names for one rope.
        //
        // Asserted on the whole profile rather than on any single field, because no one
        // field separates all nine: Thread and Silver Cord share a damping of 0.9990 and
        // are told apart by gravity and charm mass. Requiring nine distinct dampings
        // would be a claim about the table that the shipped table does not make.
        Assert.Equal(RopeStyleTable.All.Count, profiles.Count);
    }

    [Fact(DisplayName = "A heavier cord swings more slowly than a lighter one")]
    public void WeightReadsAsPeriod()
    {
        RopePhysicsProfile gold = RopeStyleTable.PhysicsOf(RopeStyle.GoldChain);
        RopePhysicsProfile neon = RopeStyleTable.PhysicsOf(RopeStyle.Neon);

        // Period goes as 1/sqrt(gravityScale), so the heavy cord must have the lower
        // scale, and the heavy cord's charm must hold its line against the rope.
        Assert.True(gold.GravityScale < neon.GravityScale);
        Assert.True(gold.CharmMassScale > neon.CharmMassScale);
    }
}

/// <summary>The time of day, which scales what the style already decided.</summary>
public class RopeTimeProfileTests
{
    [Fact(DisplayName = "The afternoon is the identity")]
    public void AfternoonIsTheIdentity()
    {
        RopeTimePhysics afternoon = RopeTimeProfileTable.PhysicsOf(RopeTimeProfile.Afternoon);

        // The rope that shipped is the rope at three in the afternoon, to the last bit.
        Assert.Equal(1, afternoon.EnergyLossScale);
        Assert.Equal(1, afternoon.ReleaseAngleScale);
        Assert.Equal(1, afternoon.RestSpeedScale);
        Assert.Equal(
            RopeConfiguration.Default.Applying(RopeStyle.Thread),
            RopeConfiguration.Default.Applying(RopeStyle.Thread, RopeTimeProfile.Afternoon));
    }

    [Fact(DisplayName = "A time of day scales a style rather than replacing it")]
    public void TimeScalesTheStyle()
    {
        RopeConfiguration leatherNoon = RopeConfiguration.Default
            .Applying(RopeStyle.Leather, RopeTimeProfile.Afternoon);
        RopeConfiguration neonNoon = RopeConfiguration.Default
            .Applying(RopeStyle.Neon, RopeTimeProfile.Afternoon);
        RopeConfiguration leatherNight = RopeConfiguration.Default
            .Applying(RopeStyle.Leather, RopeTimeProfile.Night);
        RopeConfiguration neonNight = RopeConfiguration.Default
            .Applying(RopeStyle.Neon, RopeTimeProfile.Night);

        // Night is the quicker version of whichever rope you are on: leather stays the
        // quick one, and the gap between the styles survives the profile.
        Assert.True(leatherNight.Damping < leatherNoon.Damping);
        Assert.True(neonNight.Damping < neonNoon.Damping);
        Assert.True(leatherNight.Damping < neonNight.Damping);
    }

    [Theory(DisplayName = "Morning from five, afternoon from noon, night from six")]
    [InlineData(0, RopeTimeProfile.Night)]
    [InlineData(4, RopeTimeProfile.Night)]
    [InlineData(5, RopeTimeProfile.Morning)]
    [InlineData(11, RopeTimeProfile.Morning)]
    [InlineData(12, RopeTimeProfile.Afternoon)]
    [InlineData(17, RopeTimeProfile.Afternoon)]
    [InlineData(18, RopeTimeProfile.Night)]
    [InlineData(23, RopeTimeProfile.Night)]
    public void HoursMapToProfiles(int hour, RopeTimeProfile expected) =>
        Assert.Equal(expected, RopeTimeProfileTable.ForHour(hour));
}
