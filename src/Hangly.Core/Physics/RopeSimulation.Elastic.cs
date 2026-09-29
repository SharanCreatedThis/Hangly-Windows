//
//  RopeSimulation.Elastic.cs
//  Hangly
//
//  Rope → Elastic: rigid at rest, springy under a real pull.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

public sealed partial class RopeSimulation
{
    /// <summary>Standard or Elastic. See <see cref="ElasticTable"/>.</summary>
    public RopePhysics Physics { get; private set; } = RopePhysics.Standard;

    /// <summary>Each link's accumulated XPBD multiplier this step; reset at the top of every step.</summary>
    private double[] elasticLambdas = [];

    /// <summary>Live: a rope already swinging keeps swinging and simply gains or loses its give.</summary>
    public void SetPhysics(RopePhysics physics)
    {
        if (physics == Physics)
        {
            return;
        }

        Physics = physics;
        Wake();
    }

    /// <summary>The stretch ceiling in force: the style's, or Elastic's.</summary>
    private double StretchCeiling => Physics == RopePhysics.Elastic
        ? Math.Max(ElasticTable.Ceiling, Configuration.MaxStretchRatio)
        : Configuration.MaxStretchRatio;

    /// <summary>How much shorter each link's rest length is, so that under the weight below it the spring hangs at exactly the Standard length.</summary>
    private double[] elasticSag = [];

    /// <summary>Clears this step's multipliers and works out each link's load. Called once per fixed step, before relaxation.</summary>
    /// <remarks>
    /// <b>Why the rest length moves.</b> A spring soft enough to bounce visibly must sag under
    /// what hangs from it — that is what a bounce period of half a second means. Left alone
    /// the charm would hang lower on Elastic than on Standard. At rest an XPBD link stretches
    /// by exactly its compliance times the force through it, and the force through a link is
    /// gravity times the mass of everything below it, so each link's rest length is shortened
    /// by precisely that: at rest the rope is the Standard rope, to the solver's tolerance.
    /// </remarks>
    private void BeginElasticStep()
    {
        if (Physics != RopePhysics.Elastic)
        {
            return;
        }

        int links = Math.Max(0, Points.Length - 1);
        if (elasticLambdas.Length != links)
        {
            elasticLambdas = new double[links];
            elasticSag = new double[links];
        }
        else
        {
            Array.Clear(elasticLambdas);
        }

        double below = 0;
        for (int index = links - 1; index >= 0; index--)
        {
            double inverseMass = Points[index + 1].InverseMass;
            below += inverseMass > 0 ? 1 / inverseMass : 0;
            elasticSag[index] = ElasticTable.Compliance * Configuration.Gravity * below;
        }
    }

    /// <summary>One link of an elastic rope: a spring when stretched, rigid when pushed together.</summary>
    /// <returns>The magnitude of the correction applied to this link.</returns>
    private double SolveElasticLink(int index, double restLength, double timeStep)
    {
        int indexA = index, indexB = index + 1;
        double target = Math.Max(restLength * 0.5, restLength - elasticSag[index]);
        Vec2 delta = Points[indexB].Position - Points[indexA].Position;
        double distance = delta.Magnitude;
        if (distance <= target)
        {
            return SolveLink(indexA, indexB, target);
        }

        double inverseA = EffectiveInverseMass(indexA);
        double inverseB = EffectiveInverseMass(indexB);
        double alphaTilde = ElasticTable.Compliance / (timeStep * timeStep);
        double denominator = inverseA + inverseB + alphaTilde;
        if (denominator <= 0 || distance <= Precision.UlpOfOne)
        {
            return 0;
        }

        // XPBD: the multiplier carries what earlier passes this step already did, so more
        // passes converge on the same spring rather than a stiffer one.
        double constraint = distance - target;
        double deltaLambda = (-constraint - (alphaTilde * elasticLambdas[index])) / denominator;
        elasticLambdas[index] += deltaLambda;

        Vec2 direction = delta / distance;
        Vec2 moveA = direction * (-inverseA * deltaLambda);
        Vec2 moveB = direction * (inverseB * deltaLambda);
        Points[indexA].Position += moveA;
        Points[indexB].Position += moveB;
        return Math.Max(moveA.Magnitude, moveB.Magnitude);
    }
}
