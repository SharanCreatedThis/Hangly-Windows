//
//  Glow.cs
//  Hangly
//
//  How much of the charm's own colour spills onto the desktop behind it.
//

namespace Hangly.Core.Models;

/// <summary>Appearance → Glow. The same three choices, in the same order and words, as macOS.</summary>
/// <remarks>
/// Soft is what every charm has always had, so it is the default and nothing changes on
/// update. The colour is the charm's own, from its palette — built-in or imported alike.
/// </remarks>
public enum GlowLevel
{
    Off,
    Soft,
    Strong,
}

/// <summary>How the halo is drawn: how far it reaches, and how much colour it carries.</summary>
/// <param name="Reach">Where the glow has faded to nothing, in charm radii from the centre.</param>
/// <param name="Intensity">Multiplier on the Soft halo's opacity at every point.</param>
public readonly record struct GlowStrength(double Reach, double Intensity)
{
    /// <summary>
    /// How far the glow spreads past the charm's own outline, as the blur's standard deviation against the charm's
    /// shorter side: it fades out three of those past the edge. macOS's <c>GlowStrength.spread</c>.
    /// </summary>
    /// <remarks>
    /// Soft's 0.06 fades out 0.18 of the charm's size past its edge — on a round charm, 1.36 radii from its centre,
    /// where the disc it replaces faded out — and Strong's 0.11 at 1.66 radii, inside the 1.7 the layout reserves.
    /// </remarks>
    public double Spread => 0.06 * (Reach - 1) / 0.35;
}

/// <summary>The numbers behind each level. Identical to macOS's <c>GlowLevel.strength</c>, pinned by the same tests.</summary>
/// <remarks>
/// <b>Soft</b> reaches 1.35 radii, which is where both builds' glows fade out — the
/// Windows gradient runs to <see cref="Physics.RopeConfiguration.Layout.CharmHaloExtent"/>
/// but is shaped to reach zero at 0.79 of it, matched by measurement against macOS.
/// <b>Strong</b> reaches 1.65, inside the 1.7 the layout reserves for the halo, so changing
/// the glow never moves or shrinks a charm and never outgrows the redrawn area; and it
/// carries 2.4 times the colour, which reads clearly on a dark desktop and as a warm tint
/// on a light one without turning into a coloured disc.
/// </remarks>
public static class GlowTable
{
    public static string TitleOf(GlowLevel level) => level switch
    {
        GlowLevel.Off => "Off",
        GlowLevel.Strong => "Strong",
        _ => "Soft",
    };

    /// <summary>The glow's alpha where the charm is solid, at Soft; Strong multiplies it by its intensity.</summary>
    /// <remarks>Most of it is under the charm: what shows is its edge fading out, at about half this.</remarks>
    public const double Opacity = 0.3;

    /// <summary>How much of the glow's colour is the artwork's own; the rest is the palette's.</summary>
    public const double OwnColourShare = 0.5;

    /// <summary>Light added to every glow, so a dark part of a charm never casts a dark halo.</summary>
    public const double Lift = 0.12;

    /// <summary>
    /// The palette colour a glow leans towards: the palette's primary, brightened to <c>0.85</c> at its brightest
    /// channel — a neutral light for a black palette — so the glow is light, not paint.
    /// </summary>
    /// <remarks>
    /// The glow used to be a disc of the palette's colour whatever the charm's shape — a ring round a tall figure, a
    /// moon behind a slender bell. It is now the charm's own outline blurred out past its edge, in its own colours
    /// (red off a red suit, purple off a purple skirt) leaning towards this one: macOS's <c>RGBABitmap.glow</c>.
    /// </remarks>
    public static CharmColor TintOf(CharmColor primary)
    {
        double brightest = Math.Max(primary.Red, Math.Max(primary.Green, primary.Blue));
        if (brightest <= 0.001)
        {
            return new CharmColor(0.85, 0.85, 0.85);
        }

        double lift = 0.85 / brightest;
        return new CharmColor(
            Math.Min(1, primary.Red * lift), Math.Min(1, primary.Green * lift), Math.Min(1, primary.Blue * lift));
    }

    /// <summary>
    /// The glow's colour over a part of the charm coloured <paramref name="own"/>: half its own, half
    /// <see cref="TintOf"/> the palette's, lifted. What the overlay's colour matrix computes on the GPU.
    /// </summary>
    public static CharmColor GlowColour(CharmColor own, CharmColor primary)
    {
        CharmColor tint = TintOf(primary);
        double Mix(double mine, double theirs) =>
            Math.Min(1, (mine * OwnColourShare) + (theirs * (1 - OwnColourShare)) + Lift);
        return new CharmColor(Mix(own.Red, tint.Red), Mix(own.Green, tint.Green), Mix(own.Blue, tint.Blue));
    }

    /// <summary>The halo for <paramref name="level"/>, or null for none.</summary>
    public static GlowStrength? StrengthOf(GlowLevel level) => level switch
    {
        GlowLevel.Off => null,
        GlowLevel.Strong => new GlowStrength(Reach: 1.65, Intensity: 2.4),
        _ => new GlowStrength(Reach: 1.35, Intensity: 1),
    };
}
