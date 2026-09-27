//
//  Interaction.cs
//  Hangly
//
//  Whether the charm notices the pointer coming towards it.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

/// <summary>Appearance → Motion → Interaction. The same two choices, in the same order and words, as macOS.</summary>
public enum InteractionMode
{
    /// <summary>What Hangly has always done: the charm moves when it is moved.</summary>
    Normal,

    /// <summary>The charm drifts away from a pointer moving towards it.</summary>
    Reactive,
}

/// <summary>The one rule behind Reactive. Identical numbers to macOS's <c>ReactiveTable</c>, pinned by the same tests.</summary>
/// <remarks>
/// <b>Approach, not presence.</b> The push follows how fast the pointer is moving towards
/// the charm, never where it is: a pointer resting beside the charm exerts nothing, so the
/// charm settles back under gravity and the rope can sleep. Below
/// <see cref="ApproachThreshold"/> nothing happens at all, which is what lets somebody
/// reach for the charm deliberately and take hold of it; inside the grab radius the push
/// is zero for the same reason.
///
/// <para><b>Lively, but never a jump.</b> The push is a target speed for the charm's own node,
/// fed through the solver's impulse — gravity, damping and the stretch ceiling do the rest,
/// so nothing can teleport. The first numbers moved the charm about fifteen points and read
/// as too timid; these reach further, answer slower pointers and push harder, so a pass
/// sends the charm swinging clear. At the cap, stacked charms can knock audibly, as they do
/// when flicked.</para>
/// </remarks>
public static class ReactiveTable
{
    /// <summary>How far the charm notices the pointer, in charm radii from its centre.</summary>
    public const double Reach = 3.5;

    /// <summary>Approach speed, in points per second, below which nothing happens.</summary>
    public const double ApproachThreshold = 150;

    /// <summary>Push speed per point per second of approach above the threshold.</summary>
    public const double Gain = 0.6;

    /// <summary>The fastest a push sends the charm, in points per second.</summary>
    public const double MaximumPush = 450;

    public static string TitleOf(InteractionMode mode) => mode == InteractionMode.Reactive ? "Reactive" : "Normal";

    /// <summary>The velocity the charm should have away from the pointer, or null for none.</summary>
    /// <param name="charm">The charm's centre.</param>
    /// <param name="radius">Its radius.</param>
    /// <param name="grabRadius">How close the pointer is when it can take hold of it.</param>
    /// <param name="cursor">Where the pointer is.</param>
    /// <param name="cursorVelocity">How it is moving, in points per second.</param>
    public static Vec2? Push(Vec2 charm, double radius, double grabRadius, Vec2 cursor, Vec2 cursorVelocity)
    {
        Vec2 away = charm - cursor;
        double distance = away.Magnitude;
        double reach = radius * Reach;
        if (distance <= grabRadius || distance >= reach || reach <= grabRadius)
        {
            return null;
        }

        Vec2 direction = away / distance;
        double approach = (cursorVelocity.X * direction.X) + (cursorVelocity.Y * direction.Y);
        if (approach <= ApproachThreshold)
        {
            return null;
        }

        // Strongest just outside the grab radius, nothing at the edge of the reach.
        double nearness = (reach - distance) / (reach - grabRadius);
        double speed = Math.Min(MaximumPush, Gain * (approach - ApproachThreshold)) * nearness;
        return direction * speed;
    }
}
