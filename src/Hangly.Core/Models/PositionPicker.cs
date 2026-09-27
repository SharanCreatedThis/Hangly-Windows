//
//  PositionPicker.cs
//  Hangly
//
//  Where the charm hangs, as a point on a picture of the display.
//

namespace Hangly.Core.Models;

/// <summary>The arithmetic behind the miniature desktop on Appearance → Where it hangs.</summary>
/// <remarks>
/// The macOS <c>DesktopPositionPicker</c>'s behaviour and range (decision B3): drag the
/// charm on a picture of the display; a point on the picture is a point on the display.
/// Across is the stored fraction of the work area, as it always was here; down is
/// <see cref="Settings.OverlaySettings.OffsetY"/> in points below the top of the work area,
/// within the macOS range. The picture offers the band macOS offers — the top third of
/// the screen, since the charm hangs from the top — and the Vertical slider offers the rest.
/// </remarks>
public static class PositionPicker
{
    /// <summary>The stored range, in points: the macOS <c>OverlaySettings.Limits.verticalOffset</c>.</summary>
    /// <remarks>
    /// Below zero is macOS tucking the knot under its menu bar. Windows has no menu bar
    /// over the rope, and placement stops at the top of the work area, so those values
    /// hang at the top here.
    /// </remarks>
    public const double MinimumOffsetY = -400;

    public const double MaximumOffsetY = 1200;

    /// <summary>How far down the picture the charm can be dragged, as a fraction of its height. The macOS band.</summary>
    public const double VerticalBand = 0.34;

    /// <summary>Where the rope is pinned on a picture of a work area <paramref name="areaHeight"/> points tall, 0–1 each way.</summary>
    public static (double X, double Y) UnitPoint(double position, double offsetY, double areaHeight) =>
        (Math.Clamp(position, 0, 1), areaHeight <= 0 ? 0 : Math.Clamp(offsetY / areaHeight, 0, 1));

    /// <summary>The settings a point on the picture means: the position across, and the offset down in points.</summary>
    public static (double Position, double OffsetY) FromUnit(double x, double y, double areaHeight) =>
        (Math.Clamp(x, 0, 1), Math.Round(Math.Clamp(y, 0, VerticalBand) * Math.Max(0, areaHeight)));
}
