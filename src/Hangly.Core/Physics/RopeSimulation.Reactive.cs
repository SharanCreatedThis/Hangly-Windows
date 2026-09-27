//
//  RopeSimulation.Reactive.cs
//  Hangly
//
//  Interaction → Reactive: the charm drifts away from a pointer moving towards it.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

public sealed partial class RopeSimulation
{
    /// <summary>Nudges every charm the pointer is heading for, through the solver's own impulse.</summary>
    /// <remarks>
    /// Only ever adds the speed that is missing along the push, never replaces the charm's
    /// motion: a charm already moving away fast enough is left alone, and one swinging across
    /// the push keeps its swing. So repeated polls while the pointer keeps coming cannot
    /// build up, and there is nothing to oscillate against. Does nothing under reduced
    /// motion or mid-drag.
    /// </remarks>
    /// <returns>Whether any charm was nudged.</returns>
    public bool Repel(Vec2 cursor, Vec2 cursorVelocity)
    {
        if (Motion == RopeMotion.Reduced || IsDragging)
        {
            return false;
        }

        bool nudged = false;
        double timeStep = Configuration.FixedTimeStep;
        foreach (CharmStackLayout.Slot charm in CharmLayout.Slots)
        {
            if (charm.Node < 0 || charm.Node >= Points.Length)
            {
                continue;
            }

            Vec2 center = Points[charm.Node].Position;
            if (ReactiveTable.Push(center, charm.Radius, charm.Radius + RopeConfiguration.Layout.GrabPadding, cursor, cursorVelocity) is not Vec2 push)
            {
                continue;
            }

            double target = push.Magnitude;
            Vec2 direction = push / target;
            Vec2 current = Points[charm.Node].Displacement / timeStep;
            double along = (current.X * direction.X) + (current.Y * direction.Y);
            if (along >= target)
            {
                continue;
            }

            ApplyImpulse(current + (direction * (target - along)), charm.Node);
            nudged = true;
        }

        return nudged;
    }
}
