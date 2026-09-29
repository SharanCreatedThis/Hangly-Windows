//
//  Centring.cs
//  Hangly
//
//  Where a window goes to be in the middle of a display.
//

namespace Hangly.Core.Geometry;

/// <summary>The top-left corner that centres a window in a display's work area. macOS: <c>WindowCentering.origin</c>.</summary>
public static class Centring
{
    /// <summary>The origin, in the same units as the area (physical pixels, y down).</summary>
    /// <remarks>
    /// A window bigger than the area keeps its top-left corner in it, so the title bar can
    /// still be grabbed.
    /// </remarks>
    public static (int X, int Y) Origin(int areaX, int areaY, int areaWidth, int areaHeight, int width, int height) =>
        (areaX + Math.Max(0, (areaWidth - width) / 2), areaY + Math.Max(0, (areaHeight - height) / 2));
}
