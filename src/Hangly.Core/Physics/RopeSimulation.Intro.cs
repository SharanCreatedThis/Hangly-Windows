//
//  RopeSimulation.Intro.cs
//  Hangly
//
//  The Spider-Man entrance, as the rope itself: reeled out from the anchor.
//

using Hangly.Core.Geometry;
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
        HasWeb = true;
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

    /// <summary>Whether the entrance's web hangs from the top edge: from the entrance on, for as long as a Spider-Man charm stays on the rope.</summary>
    public bool HasWeb { get; private set; }

    /// <summary>Takes the web down, when the last Spider-Man charm leaves the rope.</summary>
    public void DetachWeb() => HasWeb = false;

    /// <summary>The web, if there is one: growing during the entrance, whole after it.</summary>
    /// <remarks>
    /// Its centre sits on the rope, <see cref="IntroTable.WebDepth"/> of its spread below the
    /// anchor, along the direction the top of the rope is hanging — so when the charm swings,
    /// the web flexes with the rope rather than the rope cutting through it.
    /// </remarks>
    public WebBloom? Bloom
    {
        get
        {
            if (!HasWeb)
            {
                return null;
            }

            double growth = IntroElapsed is double elapsed ? IntroTable.BloomGrowth(elapsed) : 1;
            double spread = IntroTable.WebSpread(CharmLayout.Slots.Count > 0 ? CharmLayout.Slots[^1].Radius : 30);
            Vec2 direction = new(0, 1);
            for (int index = 1; index < Points.Length; index++)
            {
                Vec2 offset = Points[index].Position - Anchor;
                if (offset.Magnitude > 1)
                {
                    direction = offset / offset.Magnitude;
                    break;
                }
            }

            Vec2 hub = Anchor + (direction * (spread * IntroTable.WebDepth * growth));
            return new WebBloom(Anchor, hub, 0, spread, growth);
        }
    }
}
