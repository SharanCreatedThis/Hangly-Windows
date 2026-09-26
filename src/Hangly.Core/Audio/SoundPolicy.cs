//
//  SoundPolicy.cs
//  Hangly
//
//  Which sound, how loud, and whether at all.
//

using Hangly.Core.Models;

namespace Hangly.Core.Audio;

/// <summary>Every decision about sound that is not the speaker's.</summary>
/// <remarks>
/// The same numbers as the macOS <c>SoundPolicy</c>, so the two platforms make the same
/// sound at the same moment. Three events make a sound:
///
/// <list type="bullet">
/// <item><b>Throw</b> — letting go of a charm fast. The macOS build's original sound:
/// above 500 points a second, louder the faster, full at 3100.</item>
/// <item><b>Attach</b> — a charm arriving on the rope, at a fixed 0.6.</item>
/// <item><b>Collision</b> — two charms on one cord meeting above 300 points a second,
/// quieter than a throw (0.7 of it at most). Measured: a hard fold produces knocks at
/// 600–5000 points a second and a tail of brushes below 250, which stay silent.</item>
/// </list>
///
/// <para>Under reduced motion, collisions are silent: they are the rope's secondary motion,
/// which is what reduced motion asks to calm. Throw and attach still sound, because the
/// person caused them.</para>
/// </remarks>
public static class SoundPolicy
{
    /// <summary>Two sounds inside this window collapse into one.</summary>
    public const double Cooldown = 0.12;

    /// <summary>Silence for this long unloads the audio engine.</summary>
    public const double IdleTimeout = 3;

    public const double ThrowThreshold = 500;
    public const double ThrowFullSpeed = 2600;
    public const double AttachIntensity = 0.6;
    public const double CollisionThreshold = 300;
    public const double CollisionFullSpeed = 2600;
    public const double CollisionGain = 0.7;

    /// <summary>Below this, a sound is not worth waking the engine for.</summary>
    public const double AudibleVolume = 0.005;

    /// <summary>How loud a throw is, or null for no sound.</summary>
    public static double? ThrowIntensity(double speed) =>
        speed > ThrowThreshold ? Math.Clamp((speed - ThrowThreshold) / ThrowFullSpeed, 0.25, 1) : null;

    /// <summary>How loud a collision is, or null for no sound.</summary>
    public static double? CollisionIntensity(double speed, RopeMotion motion) =>
        motion == RopeMotion.Reduced || speed <= CollisionThreshold
            ? null
            : Math.Clamp((speed - CollisionThreshold) / CollisionFullSpeed, 0.2, 1) * CollisionGain;

    /// <summary>When two charms meet, the one whose material carries: a bell over glass over metal over wood over cloth.</summary>
    public static CharmSound Carrier(CharmSound first, CharmSound second) =>
        Rank(first) >= Rank(second) ? first : second;

    /// <summary>The volume to play at, or null when it should not play.</summary>
    /// <param name="enabled">Sound effects switched on.</param>
    /// <param name="userVolume">The Appearance slider, 0–1.</param>
    /// <param name="intensity">From the event, 0–1.</param>
    /// <param name="sinceLast">Seconds since the last sound played.</param>
    /// <param name="systemQuiet">The system is keeping things quiet (full-screen, presenting, a game).</param>
    public static double? Volume(bool enabled, double userVolume, double intensity, double sinceLast, bool systemQuiet)
    {
        if (!enabled || systemQuiet || sinceLast < Cooldown)
        {
            return null;
        }

        double volume = Math.Clamp(userVolume, 0, 1) * Math.Clamp(intensity, 0, 1);
        return volume > AudibleVolume ? volume : null;
    }

    private static int Rank(CharmSound sound) => sound switch
    {
        CharmSound.Bell => 4,
        CharmSound.Glass => 3,
        CharmSound.Metal => 2,
        CharmSound.Wood => 1,
        _ => 0,
    };
}
