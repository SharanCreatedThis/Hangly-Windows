using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>A running Hangly follows the clock: the next change, from anywhere in the day.</summary>
public class TimeBoundaryTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Theory]
    [InlineData("2026-09-27T00:30:00Z", "2026-09-27T05:00:00Z")]
    [InlineData("2026-09-27T05:00:00Z", "2026-09-27T12:00:00Z")]
    [InlineData("2026-09-27T11:59:59Z", "2026-09-27T12:00:00Z")]
    [InlineData("2026-09-27T17:00:00Z", "2026-09-27T18:00:00Z")]
    [InlineData("2026-09-27T23:10:00Z", "2026-09-28T05:00:00Z")]
    public void TheNextChangeIsTheNextBoundary(string now, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), RopeTimeProfileTable.NextBoundary(DateTimeOffset.Parse(now), Utc));

    [Fact]
    public void AtTheBoundaryTheProfileHasChanged()
    {
        DateTimeOffset boundary = RopeTimeProfileTable.NextBoundary(DateTimeOffset.Parse("2026-09-27T11:00:00Z"), Utc);

        Assert.Equal(RopeTimeProfile.Morning, RopeTimeProfileTable.ForHour(boundary.AddSeconds(-1).Hour));
        Assert.Equal(RopeTimeProfile.Afternoon, RopeTimeProfileTable.ForHour(boundary.Hour));
    }

    [Fact]
    public void ADaylightSavingNightStillLandsOnFiveInTheMorningLocally()
    {
        // Europe/London springs forward at 01:00 UTC on 29 March 2026.
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "GMT Standard Time" : "Europe/London");
        DateTimeOffset next = RopeTimeProfileTable.NextBoundary(DateTimeOffset.Parse("2026-03-28T23:30:00Z"), london);

        Assert.Equal(5, TimeZoneInfo.ConvertTime(next, london).Hour);
        Assert.Equal(DateTimeOffset.Parse("2026-03-29T04:00:00Z"), next);
    }
}
