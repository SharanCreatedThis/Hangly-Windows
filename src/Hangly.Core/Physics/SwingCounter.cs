//
//  SwingCounter.cs
//  Hangly
//
//  What counts as one swing, for "Swings survived".
//

namespace Hangly.Core.Physics;

/// <summary>Counts swings of the hanging charm. The macOS <c>SwingCounter</c> is the same rule.</summary>
/// <remarks>
/// One swing is one crossing of the vertical below the anchor, which is what a pendulum
/// does. A dead band of a fiftieth of the rope keeps a charm resting a hair off centre from
/// ticking over forever on floating-point noise: well inside the smallest swing anyone can
/// see, well outside that noise. Fed once per simulation tick with the charm's horizontal
/// offset from the anchor; allocates nothing.
/// </remarks>
public struct SwingCounter
{
    private int lastSide;

    /// <summary>Takes one tick. True when that tick completed a swing.</summary>
    public bool Observe(double offsetFromAnchor, double ropeLength)
    {
        double band = ropeLength / 50;
        int side = offsetFromAnchor > band ? 1 : offsetFromAnchor < -band ? -1 : 0;
        if (side == 0)
        {
            return false;
        }

        bool crossed = lastSide != 0 && side != lastSide;
        lastSide = side;
        return crossed;
    }
}
