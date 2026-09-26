//
//  RopeSimulation.Collisions.cs
//  Hangly
//
//  When two charms on one cord meet, and how hard.
//

using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>Two charms meeting.</summary>
/// <param name="First">The upper charm, by its place on the cord from the anchor down.</param>
/// <param name="Second">The lower charm.</param>
/// <param name="Speed">How fast they were closing, in points per second.</param>
public readonly record struct CharmCollision(int First, int Second, double Speed);

/// <summary>Collision events, for sound.</summary>
/// <remarks>
/// The charms already cannot pass through each other: <see cref="SeparateCharms"/> is a
/// minimum-distance constraint solved with the links. A collision is the moment that
/// constraint engages for a pair that was clear of each other the substep before, and its
/// speed is how deep they had gone into each other when it did, over the substep that took
/// them there — the closing speed, read from the constraint rather than guessed.
///
/// <para>Only the transition counts. Two charms that stay in contact while the rope swings
/// make one event, not one per substep, and a rope at rest makes none. The macOS solver
/// reports the same events by the same rule.</para>
///
/// <para>Buffered in a list that is reused, and capped, so a rope nobody is listening to
/// cannot grow it, and listening costs no allocation per frame.</para>
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>More than a frame ever produces; the rest are dropped rather than kept.</summary>
    private const int CollisionBufferLimit = 8;

    private static readonly int MaximumPairs = CharmStack.MaximumCount * (CharmStack.MaximumCount - 1) / 2;

    private readonly bool[] touching = new bool[MaximumPairs];
    private readonly double[] contactDepth = CreateNoContact();
    private readonly List<CharmCollision> collisions = [];

    /// <summary>Moves the collisions since the last call into <paramref name="into"/>.</summary>
    public void TakeCollisions(List<CharmCollision> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.AddRange(collisions);
        collisions.Clear();
    }

    /// <summary>The pair's slot in the bookkeeping arrays.</summary>
    private static int PairIndex(int first, int second, int count) =>
        (first * ((2 * count) - first - 1) / 2) + (second - first - 1);

    /// <summary>Called by the separation pass: the first depth a substep sees for a pair.</summary>
    private void NoteContact(int pair, double depth)
    {
        if (pair < contactDepth.Length && contactDepth[pair] < 0)
        {
            contactDepth[pair] = depth;
        }
    }

    /// <summary>After relaxation: new contacts become events, and the substep's notes clear.</summary>
    private void CloseContacts(double timeStep)
    {
        int count = CharmLayout.Slots.Count;
        for (int first = 0; first < count - 1; first++)
        {
            for (int second = first + 1; second < count; second++)
            {
                int pair = PairIndex(first, second, count);
                if (pair >= MaximumPairs)
                {
                    continue;
                }

                bool now = contactDepth[pair] >= 0;
                if (now && !touching[pair] && timeStep > 0 && collisions.Count < CollisionBufferLimit)
                {
                    collisions.Add(new CharmCollision(first, second, contactDepth[pair] / timeStep));
                }

                touching[pair] = now;
                contactDepth[pair] = -1;
            }
        }
    }

    /// <summary>Forgets who was touching whom: a new stack is new pairs.</summary>
    private void ForgetContacts()
    {
        Array.Clear(touching);
        Array.Fill(contactDepth, -1);
        collisions.Clear();
    }

    private static double[] CreateNoContact()
    {
        var depths = new double[MaximumPairs];
        Array.Fill(depths, -1);
        return depths;
    }
}
