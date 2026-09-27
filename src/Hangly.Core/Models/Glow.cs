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
public readonly record struct GlowStrength(double Reach, double Intensity);

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

    /// <summary>The halo for <paramref name="level"/>, or null for none.</summary>
    public static GlowStrength? StrengthOf(GlowLevel level) => level switch
    {
        GlowLevel.Off => null,
        GlowLevel.Strong => new GlowStrength(Reach: 1.65, Intensity: 2.4),
        _ => new GlowStrength(Reach: 1.35, Intensity: 1),
    };
}
