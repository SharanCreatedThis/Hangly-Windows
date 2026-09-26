//
//  RopeMotion.cs
//  Hangly
//
//  How much the rope moves, for people who asked it to move less.
//

namespace Hangly.Core.Models;

/// <summary>What the person chose in Appearance → Motion.</summary>
/// <remarks>
/// The same three words and the same stored names on macOS and Windows. Follow System is
/// the default: Windows' "Animation effects" and macOS's "Reduce motion" are where people
/// already said what they want, and Hangly should not make them say it twice.
/// </remarks>
public enum MotionPreference
{
    FollowSystem,
    Reduced,
    Full,
}

/// <summary>How the rope actually moves, once the preference has met the system.</summary>
public enum RopeMotion
{
    Full,
    Reduced,
}

/// <summary>What reduced motion does to the solver.</summary>
/// <param name="EnergyLossScale">
/// Multiplier on <c>1 - damping</c>, composed after the style and the time of day in the
/// same way the time of day is. Four times the energy loss: a throw that swings for about
/// forty-five seconds at full motion settles in about ten.
/// </param>
/// <param name="ThrowScale">
/// Multiplier on the speed a charm carries when it is let go. A flick stays a flick, and
/// lands as a nudge.
/// </param>
/// <param name="SwingsOnLaunch">
/// Whether the rope greets you with a swing when it appears. Under reduced motion it does
/// not: it hangs still and moves only when it is moved, which is what the macOS build has
/// always done under the system's Reduce Motion.
/// </param>
/// <remarks>
/// <b>What it leaves alone, on purpose:</b> gravity, length, segment count and the stretch
/// ceiling. Those decide what the rope looks like and how it hangs, and reduced motion is a
/// promise that it looks exactly the same — it only moves less, and for less long. The
/// beads share the rope's damping, so their secondary wobble calms with it.
/// </remarks>
public readonly record struct RopeMotionPhysics(
    double EnergyLossScale,
    double ThrowScale,
    bool SwingsOnLaunch);

/// <summary>The two motion profiles and how the preference resolves to one.</summary>
public static class RopeMotionTable
{
    public static RopeMotionPhysics PhysicsOf(RopeMotion motion) => motion switch
    {
        RopeMotion.Reduced => new RopeMotionPhysics(EnergyLossScale: 4, ThrowScale: 0.55, SwingsOnLaunch: false),
        _ => new RopeMotionPhysics(EnergyLossScale: 1, ThrowScale: 1, SwingsOnLaunch: true),
    };

    /// <summary>What the rope does, given what the person chose and what the system says.</summary>
    /// <param name="preference">The Appearance setting.</param>
    /// <param name="systemReducesMotion">
    /// Windows: animation effects switched off. macOS: Reduce motion switched on.
    /// </param>
    public static RopeMotion Resolve(MotionPreference preference, bool systemReducesMotion) => preference switch
    {
        MotionPreference.Reduced => RopeMotion.Reduced,
        MotionPreference.Full => RopeMotion.Full,
        _ => systemReducesMotion ? RopeMotion.Reduced : RopeMotion.Full,
    };
}
