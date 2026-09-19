//
//  CharmColor.cs
//  Hangly
//

namespace Hangly.Core.Models;

/// <summary>An sRGB colour, kept free of WinUI so charms stay in the model layer.</summary>
public readonly record struct CharmColor(double Red, double Green, double Blue, double Alpha = 1)
{
    public CharmColor WithAlpha(double value) => this with { Alpha = value };

    /// <summary>Multiplies the colour channels, keeping alpha. Below one darkens.</summary>
    public CharmColor Scaled(double factor) => new(
        Math.Clamp(Red * factor, 0, 1),
        Math.Clamp(Green * factor, 0, 1),
        Math.Clamp(Blue * factor, 0, 1),
        Alpha);

    public static CharmColor Interpolate(CharmColor start, CharmColor end, double progress)
    {
        double amount = Math.Clamp(progress, 0, 1);
        double Mix(double first, double second) => first + ((second - first) * amount);
        return new CharmColor(
            Mix(start.Red, end.Red),
            Mix(start.Green, end.Green),
            Mix(start.Blue, end.Blue),
            Mix(start.Alpha, end.Alpha));
    }
}

/// <summary>The four inks a charm's artwork is drawn with.</summary>
public readonly record struct CharmPalette(
    CharmColor Primary,
    CharmColor Secondary,
    CharmColor Deep,
    CharmColor Light)
{
    public static CharmPalette Interpolate(CharmPalette start, CharmPalette end, double progress) => new(
        CharmColor.Interpolate(start.Primary, end.Primary, progress),
        CharmColor.Interpolate(start.Secondary, end.Secondary, progress),
        CharmColor.Interpolate(start.Deep, end.Deep, progress),
        CharmColor.Interpolate(start.Light, end.Light, progress));
}
