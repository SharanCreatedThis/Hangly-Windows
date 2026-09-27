//
//  RopeSimulation.Intro.cs
//  Hangly
//
//  The Spider-Man entrance, as the rope itself: reeled out from the anchor.
//

using Hangly.Core.Models;

namespace Hangly.Core.Physics;

public sealed partial class RopeSimulation
{
    /// <summary>Seconds into the Spider-Man entrance, or null when it is not playing.</summary>
    public double? IntroElapsed { get; private set; }

    /// <summary>The links' rest length as a fraction of their real one: 1, except while the entrance reels the rope out.</summary>
    public double ReelFraction { get; private set; } = 1;

    /// <summary>Starts the entrance: the rope hanging straight, gathered up almost to the anchor, still. From here the solver does everything.</summary>
    public void BeginIntro()
    {
        ResetToHanging();
        IntroElapsed = 0;
        ReelFraction = IntroTable.Reel(0);
        for (int index = 0; index < Points.Length; index++)
        {
            var gathered = Anchor + ((Points[index].Position - Anchor) * ReelFraction);
            Points[index].Position = gathered;
            Points[index].PreviousPosition = gathered;
        }

        RebuildBeads(preservingMotion: false);
        Wake();
    }

    /// <summary>One fixed step of the entrance. When it ends the rope is simply the rope.</summary>
    private void AdvanceIntro(double timeStep)
    {
        if (IntroElapsed is not double elapsed)
        {
            return;
        }

        double now = elapsed + timeStep;
        if (now >= IntroTable.Duration)
        {
            IntroElapsed = null;
            ReelFraction = 1;
        }
        else
        {
            IntroElapsed = now;
            ReelFraction = IntroTable.Reel(now);
        }
    }

    /// <summary>The web at the anchor, while the entrance plays.</summary>
    public WebBloom? Bloom => IntroElapsed is double elapsed
        ? new WebBloom(
            Anchor,
            IntroTable.BloomSize(CharmLayout.Slots.Count > 0 ? CharmLayout.Slots[^1].Radius : 30),
            IntroTable.BloomGrowth(elapsed),
            IntroTable.BloomOpacity(elapsed))
        : null;
}
