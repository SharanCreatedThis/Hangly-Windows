//
//  RopeSimulation.Style.cs
//  Hangly
//
//  Changing what the rope is made of, without stopping it.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>The rope style's grip on the solver.</summary>
/// <remarks>
/// Almost all of a style is a set of replacements for numbers
/// <see cref="RopeConfiguration"/> already had, applied by
/// <see cref="RopeConfiguration.Applying"/>. What lives here is the part that has to
/// happen <em>to a rope that is already moving</em>.
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>Changes the cord, mid-swing if need be.</summary>
    /// <remarks>
    /// Applied on the spot rather than eased in. The rope is not paused, not reset and not
    /// re-fitted: every node keeps its position <em>and its history</em>, so the velocity
    /// Verlet infers from that history is untouched and the swing carries straight through
    /// the change into the new cord's behaviour. Only the numbers the next step reads are
    /// different. The look is what eases.
    /// </remarks>
    public void SetStyle(RopeStyle newStyle)
    {
        if (newStyle == Style)
        {
            return;
        }

        Style = newStyle;
        Configuration = Configuration.Applying(newStyle, TimeProfile);
        RefreshLayout();
        ApplyMasses();
        Wake();
    }

    /// <summary>Changes how far the charm hangs.</summary>
    /// <remarks>
    /// Re-fits the rope rather than rebuilding it: the links get longer or shorter and the
    /// solver pulls the chain to suit over the next few frames, so dragging the slider
    /// makes the charm <em>descend</em> rather than jump. Nothing about the charm changes —
    /// not its radius, not its beads, not its mass.
    /// </remarks>
    public void SetRopeLength(double newLength, Size canvasSize)
    {
        if (newLength.Equals(RopeLength))
        {
            return;
        }

        RopeLength = newLength;
        Resize(canvasSize);
    }

    /// <summary>Changes how large the charm is drawn.</summary>
    /// <remarks>
    /// The counterpart of <see cref="SetRopeLength"/> and the exact complement of it: the
    /// charm grows where it hangs, the rope above it keeps every link the length it had,
    /// and the charm does not move up or down a point.
    /// </remarks>
    public void SetCharmSize(double newSize, Size canvasSize)
    {
        if (newSize.Equals(CharmSize))
        {
            return;
        }

        CharmSize = newSize;
        Resize(canvasSize);
    }

    /// <summary>Changes the time of day the rope moves in.</summary>
    /// <remarks>
    /// Applied in place like a style, and for the same reasons — but deliberately
    /// <em>without</em> waking the rope. A style change is something a person just asked
    /// for and should see; noon arriving is not. Waking a settled rope to tell it the
    /// afternoon has started would cost a second of simulation, twice a day, to show nobody
    /// anything: the new numbers are read by the next step whenever that step happens to
    /// come.
    /// </remarks>
    public void SetTimeProfile(RopeTimeProfile newProfile)
    {
        if (newProfile == TimeProfile)
        {
            return;
        }

        TimeProfile = newProfile;
        Configuration = Configuration.Applying(Style, newProfile);
    }
}
