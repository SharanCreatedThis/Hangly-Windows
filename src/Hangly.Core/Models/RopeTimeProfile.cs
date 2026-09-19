//
//  RopeTimeProfile.cs
//  Hangly
//
//  How the rope feels at different times of day.
//

namespace Hangly.Core.Models;

/// <summary>The three moods the rope moves in.</summary>
/// <remarks>
/// The whole design goal in one sentence: someone should think <em>the rope feels a
/// little different today</em>, never <em>this is a different rope</em>. That rules
/// out most of what could have been changed. Gravity is untouched, because it is the
/// only knob in a pendulum that alters its period, and a changed period is precisely
/// what would read as a different rope. So is the length, the segment count and the
/// stretch ceiling. What changes is how long motion <em>persists</em> — felt rather
/// than seen, and the one property of a swing a person could not name if asked but
/// would notice if it were wrong.
///
/// <para><see cref="Afternoon"/> is the identity. Every one of its scales is exactly
/// one, so an afternoon rope is the rope that shipped, to the last bit.</para>
/// </remarks>
public enum RopeTimeProfile
{
    Morning,
    Afternoon,
    Night,
}

/// <summary>What a time of day does to the solver.</summary>
/// <remarks>
/// Three scales, and not one of them is an absolute value. That is deliberate: the
/// rope already has nine styles with damping four times apart, and a profile that
/// wrote a fixed number would flatten them into each other — a leather rope at night
/// would settle at the same rate as a neon one. Scaling what each style already does
/// keeps leather the quick one and makes night the quicker version of whatever the
/// rope happens to be made of.
/// </remarks>
/// <param name="EnergyLossScale">
/// Multiplier on how fast the rope loses energy — that is, on <c>1 - damping</c>
/// rather than on damping itself. The right quantity to scale: damping is a number
/// just under one, and the interesting part is the sliver below it. Halving
/// <em>that</em> halves the rate a swing dies away at, whatever the style set it to,
/// where halving the damping itself would stop the rope dead.
/// </param>
/// <param name="ReleaseAngleScale">
/// Multiplier on the angle the rope is released at when it first appears. The
/// morning's greeting is a slightly wider swing; the night's a narrower one. Costs
/// nothing, because it happens once.
/// </param>
/// <param name="RestSpeedScale">
/// Multiplier on the speed below which the rope counts as still. Above one the rope
/// decides it has finished sooner — the same thing a calmer evening does to a person.
/// </param>
public readonly record struct RopeTimePhysics(
    double EnergyLossScale,
    double ReleaseAngleScale,
    double RestSpeedScale);

/// <summary>The time-of-day table, and the clock rule that picks a row.</summary>
public static class RopeTimeProfileTable
{
    /// <summary>The rope as it shipped, and the profile every scale is measured against.</summary>
    public const RopeTimeProfile Baseline = RopeTimeProfile.Afternoon;

    public static RopeTimePhysics PhysicsOf(RopeTimeProfile profile) => profile switch
    {
        // Keeps a swing going about a quarter longer than the afternoon, and starts
        // wider. Nothing moves faster; it simply carries on.
        RopeTimeProfile.Morning => new RopeTimePhysics(0.80, 1.14, 0.94),

        // The identity, asserted as such by RopeTimeProfileTests. The rope that
        // shipped is the rope at three in the afternoon.
        RopeTimeProfile.Afternoon => new RopeTimePhysics(1, 1, 1),

        RopeTimeProfile.Night => new RopeTimePhysics(1.30, 0.88, 1.10),

        _ => new RopeTimePhysics(1, 1, 1),
    };

    public static string DisplayNameOf(RopeTimeProfile profile) => profile switch
    {
        RopeTimeProfile.Morning => "Morning",
        RopeTimeProfile.Afternoon => "Afternoon",
        RopeTimeProfile.Night => "Night",
        _ => "Afternoon",
    };

    /// <summary>Which profile a local hour falls in.</summary>
    /// <remarks>
    /// Morning from five, afternoon from noon, night from six — the ordinary meanings
    /// of the words, and the hours the rest of the day is described by. A pure function
    /// of the hour, so "what is the rope doing at 3am" is a question with an answer
    /// rather than an experiment.
    /// </remarks>
    public static RopeTimeProfile ForHour(int hour) => hour switch
    {
        >= 5 and < 12 => RopeTimeProfile.Morning,
        >= 12 and < 18 => RopeTimeProfile.Afternoon,
        _ => RopeTimeProfile.Night,
    };

    /// <summary>
    /// Which profile a moment falls in, read in the machine's own time zone. Local,
    /// because the point is the time where the person is.
    /// </summary>
    public static RopeTimeProfile ForDate(DateTimeOffset date) => ForHour(date.ToLocalTime().Hour);
}
