//
//  RopeSimulation.Constraints.cs
//  Hangly
//
//  The projection passes: what the rope is not allowed to do.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Physics;

/// <summary>Everything the solver enforces after it has integrated.</summary>
/// <remarks>
/// Position-based dynamics in the plainest form: the integrator moves nodes wherever
/// gravity and momentum take them, and these passes move them back until the rope's
/// rules hold again. Every one of them is idempotent on a rope that already obeys it,
/// which is what lets them be run to convergence and what makes a settled rope free.
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>One relaxation pass.</summary>
    /// <returns>The largest correction applied, so the caller can stop early.</returns>
    internal double SolveDistanceConstraints()
    {
        double restLength = Configuration.SegmentLength;
        double largestCorrection = 0;
        for (int index = 0; index < Points.Length - 1; index++)
        {
            largestCorrection = Math.Max(largestCorrection, SolveLink(index, index + 1, restLength));
        }

        return largestCorrection;
    }

    /// <returns>The magnitude of the correction applied to this link.</returns>
    private double SolveLink(int indexA, int indexB, double restLength)
    {
        double inverseA = EffectiveInverseMass(indexA);
        double inverseB = EffectiveInverseMass(indexB);
        double totalInverseMass = inverseA + inverseB;
        if (totalInverseMass <= 0)
        {
            return 0;
        }

        Vec2 delta = Points[indexB].Position - Points[indexA].Position;
        double distance = delta.Magnitude;
        if (distance <= Precision.UlpOfOne)
        {
            return 0;
        }

        // Split the error between the two nodes in proportion to their mobility.
        Vec2 correction = delta * ((distance - restLength) / distance / totalInverseMass);
        Points[indexA].Position += correction * inverseA;
        Points[indexB].Position -= correction * inverseB;
        return Math.Max((correction * inverseA).Magnitude, (correction * inverseB).Magnitude);
    }

    /// <summary>Keeps two charms on one cord from passing through each other.</summary>
    /// <remarks>
    /// A minimum-distance constraint between the nodes the charms hang from, solved in
    /// the same relaxation as the links so that the rope gives way rather than the
    /// charms. Spacing the attachments along the cord is not enough on its own: a hard
    /// throw folds the rope, and a fold brings two attachment nodes far closer together
    /// than the cord between them — measured at up to eighty points of overlap before
    /// this existed.
    ///
    /// <para>Every pair, not just neighbours, because a deep fold can bring the top charm
    /// down onto the bottom one with the middle charm nowhere near either.</para>
    ///
    /// <para>One-sided, like the stretch ceiling: charms already far enough apart are left
    /// alone, so this costs nothing on a hanging rope and cannot perturb one.</para>
    /// </remarks>
    /// <returns>The largest correction applied, so relaxation can stop early.</returns>
    internal double SeparateCharms()
    {
        IReadOnlyList<CharmStackLayout.Slot> slots = CharmLayout.Slots;
        if (slots.Count <= 1)
        {
            return 0;
        }

        double largestCorrection = 0;
        for (int first = 0; first < slots.Count - 1; first++)
        {
            for (int second = first + 1; second < slots.Count; second++)
            {
                double correction = Separate(
                    slots[first].Node,
                    slots[second].Node,
                    slots[first].Radius + slots[second].Radius);
                largestCorrection = Math.Max(largestCorrection, correction);
            }
        }

        return largestCorrection;
    }

    /// <summary>
    /// Pushes two nodes apart until they are <paramref name="minimum"/> apart, sharing the
    /// correction between them in proportion to their mobility exactly as the link solver
    /// does.
    /// </summary>
    /// <returns>The magnitude of the correction applied.</returns>
    private double Separate(int lower, int upper, double minimum)
    {
        if (lower < 0 || lower >= Points.Length || upper < 0 || upper >= Points.Length)
        {
            return 0;
        }

        double inverseLower = EffectiveInverseMass(lower);
        double inverseUpper = EffectiveInverseMass(upper);
        double totalInverseMass = inverseLower + inverseUpper;
        if (totalInverseMass <= 0)
        {
            return 0;
        }

        Vec2 delta = Points[upper].Position - Points[lower].Position;
        double distance = delta.Magnitude;
        if (distance >= minimum)
        {
            return 0;
        }

        // Two charms exactly on top of one another have no direction to separate along,
        // so they take the cord's: the later one hangs below.
        Vec2 direction = distance > Precision.UlpOfOne ? delta / distance : new Vec2(0, 1);
        Vec2 correction = direction * ((distance - minimum) / totalInverseMass);
        Points[lower].Position += correction * inverseLower;
        Points[upper].Position -= correction * inverseUpper;
        return Math.Max((correction * inverseLower).Magnitude, (correction * inverseUpper).Magnitude);
    }

    /// <summary>The hard guarantee behind "never stretches unrealistically".</summary>
    /// <remarks>
    /// Relaxation targets the rest length and is iterative, so it can leave a link long
    /// after a violent frame. This pass enforces the ceiling as a one-sided constraint:
    /// links inside the limit are untouched, and links over it are pulled back.
    ///
    /// <para>An earlier version snapped the offending node straight onto the limit. That
    /// oscillated rather than converged, because a chain pinned at both ends had each
    /// sweep undo the last one's work. Splitting the correction between the two ends,
    /// exactly as the distance solver does, converges instead.</para>
    /// </remarks>
    internal void EnforceMaximumStretch()
    {
        double limit = Configuration.SegmentLength * Configuration.MaxStretchRatio;

        for (int pass = 0; pass < Configuration.StretchPasses; pass++)
        {
            bool corrected = false;
            for (int index = 0; index < Points.Length - 1; index++)
            {
                if (ClampLink(index, limit))
                {
                    corrected = true;
                }
            }

            // Converged: every link is inside the limit.
            if (!corrected)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Pulls one over-long link back to <paramref name="limit"/>, sharing the correction
    /// between its ends in proportion to their mobility.
    /// </summary>
    /// <returns>Whether the link was over its limit.</returns>
    private bool ClampLink(int index, double limit)
    {
        int lower = index;
        int upper = index + 1;

        double inverseLower = EffectiveInverseMass(lower);
        double inverseUpper = EffectiveInverseMass(upper);
        double totalInverseMass = inverseLower + inverseUpper;
        if (totalInverseMass <= 0)
        {
            return false;
        }

        Vec2 delta = Points[upper].Position - Points[lower].Position;
        double distance = delta.Magnitude;
        if (distance <= limit || distance <= Precision.UlpOfOne)
        {
            return false;
        }

        Vec2 correction = delta * ((distance - limit) / distance / totalInverseMass);
        Points[lower].Position += correction * inverseLower;
        Points[upper].Position -= correction * inverseUpper;
        return true;
    }

    /// <summary>A held node is immovable for the solver, exactly like the anchor.</summary>
    internal double EffectiveInverseMass(int index) =>
        index == DragIndex ? 0 : Points[index].InverseMass;
}
