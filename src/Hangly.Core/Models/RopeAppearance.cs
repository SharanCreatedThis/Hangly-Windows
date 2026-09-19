//
//  RopeAppearance.cs
//  Hangly
//
//  What each cord looks like.
//

namespace Hangly.Core.Models;

/// <summary>The pattern worked along a cord.</summary>
/// <remarks>
/// Every one is drawn with ordinary strokes of the path the cord already has: a dashed
/// pass is a twist, a wider flatter pair is a braid, a heavy notch with a lit rim is a
/// chain. Nothing uses a filter, because a blur over a 240 Hz overlay is a per-frame
/// off-screen pass for an effect that reads the same as three strokes.
/// </remarks>
public enum RopeTextureKind
{
    Smooth,
    Twist,
    Braid,
    Links,
    Web,
}

/// <param name="Kind">Which pattern.</param>
/// <param name="Pitch">How often it repeats, relative to the cord's width.</param>
/// <param name="Offset">How far the pattern is inset, as a fraction of the width.</param>
/// <param name="Thickness">Link thickness, for <see cref="RopeTextureKind.Links"/>.</param>
public readonly record struct RopeTexture(
    RopeTextureKind Kind,
    double Pitch = 0,
    double Offset = 0,
    double Thickness = 0)
{
    public static RopeTexture Smooth => new(RopeTextureKind.Smooth);

    public static RopeTexture Twist(double pitch, double offset) =>
        new(RopeTextureKind.Twist, pitch, offset);

    public static RopeTexture Braid(double pitch, double offset) =>
        new(RopeTextureKind.Braid, pitch, offset);

    public static RopeTexture Links(double pitch, double thickness) =>
        new(RopeTextureKind.Links, pitch, Thickness: thickness);

    public static RopeTexture Web(double pitch, double offset) =>
        new(RopeTextureKind.Web, pitch, offset);
}

/// <summary>How a cord is drawn.</summary>
/// <param name="Palette">Its four inks.</param>
/// <param name="WidthScale">Cord width as a fraction of the charm's radius.</param>
/// <param name="MinimumWidth">Floor on that width, in points, so a small charm's cord stays visible.</param>
/// <param name="Texture">The pattern worked along it.</param>
/// <param name="GlowStrength">How much light it throws, zero for every ordinary cord.</param>
/// <param name="BeadTint">
/// The palette its beads are recoloured through, or null to leave the artwork alone.
/// Thread and Gold Chain leave it alone because the artwork's beads are already that
/// gold, and a recolour of a colour it already is can only lose detail.
/// </param>
public readonly record struct RopeAppearance(
    CharmPalette Palette,
    double WidthScale,
    double MinimumWidth,
    RopeTexture Texture,
    double GlowStrength,
    CharmPalette? BeadTint);

public static partial class RopeStyleAppearanceTable
{
    /// <summary>
    /// The antique burnished gold the collection was drawn on: dark enough to read as
    /// cord against a bright desktop rather than as a drawn line.
    /// </summary>
    private static readonly CharmPalette ThreadPalette = new(
        new CharmColor(0.47, 0.34, 0.11),
        new CharmColor(0.31, 0.22, 0.06),
        new CharmColor(0.16, 0.11, 0.03),
        new CharmColor(0.78, 0.62, 0.30));

    private static readonly CharmPalette LeatherPalette = new(
        new CharmColor(0.36, 0.22, 0.13),
        new CharmColor(0.25, 0.15, 0.08),
        new CharmColor(0.11, 0.06, 0.03),
        new CharmColor(0.63, 0.45, 0.30));

    private static readonly CharmPalette GoldPalette = new(
        new CharmColor(0.72, 0.55, 0.19),
        new CharmColor(0.50, 0.36, 0.10),
        new CharmColor(0.24, 0.16, 0.04),
        new CharmColor(0.97, 0.86, 0.53));

    private static readonly CharmPalette SilverPalette = new(
        new CharmColor(0.69, 0.72, 0.77),
        new CharmColor(0.47, 0.50, 0.55),
        new CharmColor(0.21, 0.23, 0.27),
        new CharmColor(0.97, 0.98, 1.00));

    private static readonly CharmPalette NeonPalette = new(
        new CharmColor(0.29, 0.92, 1.00),
        new CharmColor(0.16, 0.60, 0.95),
        new CharmColor(0.05, 0.16, 0.40),
        new CharmColor(0.88, 1.00, 1.00));

    private static readonly CharmPalette SilkPalette = new(
        new CharmColor(0.94, 0.95, 0.97),
        new CharmColor(0.74, 0.77, 0.82),
        new CharmColor(0.33, 0.36, 0.42),
        new CharmColor(1.00, 1.00, 1.00));

    private static readonly CharmPalette MidnightPalette = new(
        new CharmColor(0.16, 0.18, 0.29),
        new CharmColor(0.09, 0.10, 0.18),
        new CharmColor(0.03, 0.03, 0.07),
        new CharmColor(0.42, 0.46, 0.62));

    private static readonly CharmPalette TemplePalette = new(
        new CharmColor(0.87, 0.51, 0.09),
        new CharmColor(0.66, 0.20, 0.07),
        new CharmColor(0.29, 0.07, 0.03),
        new CharmColor(1.00, 0.82, 0.38));

    /// <summary>
    /// Brighter and cooler than <see cref="RopeStyle.SilverChain"/>'s, so the two read as
    /// different metals rather than as the same one twice.
    /// </summary>
    private static readonly CharmPalette SilverCordPalette = new(
        new CharmColor(0.80, 0.83, 0.88),
        new CharmColor(0.58, 0.62, 0.69),
        new CharmColor(0.26, 0.28, 0.33),
        new CharmColor(1.00, 1.00, 1.00));

    public static RopeAppearance AppearanceOf(RopeStyle style) => style switch
    {
        RopeStyle.Thread => new RopeAppearance(
            ThreadPalette, 0.046, 1.5, RopeTexture.Twist(1.5, 0.20), 0, null),

        RopeStyle.Leather => new RopeAppearance(
            LeatherPalette, 0.058, 1.7, RopeTexture.Braid(2.3, 0.26), 0, LeatherPalette),

        RopeStyle.GoldChain => new RopeAppearance(
            GoldPalette, 0.088, 2.2, RopeTexture.Links(2.05, 0.62), 0, null),

        RopeStyle.SilverChain => new RopeAppearance(
            SilverPalette, 0.082, 2.1, RopeTexture.Links(1.95, 0.60), 0, SilverPalette),

        RopeStyle.Neon => new RopeAppearance(
            NeonPalette, 0.040, 1.4, RopeTexture.Smooth, 1.0, NeonPalette),

        // Thinner than anything else, including neon: silk is the one cord whose whole
        // character is that you can hardly see it. The floor matters more here than the
        // scale — the texture pass refuses to run under 1.4 points, where any pattern
        // turns to mud, and silk sat exactly on that floor, so the web drew on the rope
        // and vanished in the Library card. The glow is not neon's: a quarter of one,
        // three faint wide strokes under a white cord reading as light caught along it,
        // and also what keeps a white cord visible against a white window.
        RopeStyle.SpiderThread => new RopeAppearance(
            SilkPalette, 0.038, 1.55, RopeTexture.Web(1.55, 0.28), 0.26, SilkPalette),

        RopeStyle.MidnightCord => new RopeAppearance(
            MidnightPalette, 0.068, 1.9, RopeTexture.Braid(2.1, 0.24), 0, MidnightPalette),

        // Coarser than thread's twist: cotton rolled on the palm, not drawn.
        RopeStyle.TempleThread => new RopeAppearance(
            TemplePalette, 0.052, 1.6, RopeTexture.Twist(1.15, 0.26), 0, TemplePalette),

        RopeStyle.SilverCord => new RopeAppearance(
            SilverCordPalette, 0.054, 1.7, RopeTexture.Twist(1.35, 0.22), 0, SilverCordPalette),

        _ => AppearanceOf(RopeStyle.Thread),
    };

    /// <summary>
    /// A cord's drawn width for a charm of this radius, never below the style's floor.
    /// </summary>
    public static double WidthFor(RopeStyle style, double charmRadius)
    {
        RopeAppearance appearance = AppearanceOf(style);
        return Math.Max(appearance.MinimumWidth, charmRadius * appearance.WidthScale);
    }
}
