//
//  ScreenPlacement.cs
//  Hangly
//
//  Pure geometry for positioning the overlay. Deliberately free of Win32.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

/// <summary>Computes the overlay's frame inside a display's usable bounds.</summary>
/// <remarks>
/// Kept free of Win32 so the placement rules can be unit-tested on any machine, with no
/// attached display and no window manager. All rectangles use the convention described
/// on <see cref="Rect"/>: origin top left, <c>y</c> growing downward.
/// </remarks>
public static class ScreenPlacement
{
    /// <param name="size">Desired overlay size in points.</param>
    /// <param name="anchor">Which corner or edge to hang from.</param>
    /// <param name="bounds">
    /// The display region to place within, in virtual-desktop coordinates. This should be
    /// the monitor's <em>work area</em>, so the overlay hangs below a top-docked taskbar
    /// rather than behind it.
    /// </param>
    /// <param name="offset">User nudge. <c>x</c> positive moves right, <c>y</c> positive moves down.</param>
    /// <param name="edgeInset">
    /// Margin kept between the overlay and the left/right display edges, for a leading- or
    /// trailing-anchored overlay.
    /// </param>
    /// <param name="topInset">
    /// Margin kept above the overlay, at the top of <paramref name="bounds"/>. Separate
    /// from <paramref name="edgeInset"/> because the rope hangs from the top on every
    /// anchor, so this is what decides how close it comes to the display's own edge;
    /// <paramref name="edgeInset"/> only ever matters for the sides. Null means "use
    /// <paramref name="edgeInset"/>", so a caller that does not care still gets one
    /// uniform margin.
    /// </param>
    /// <returns>A frame in virtual-desktop coordinates.</returns>
    public static Rect Frame(
        Size size,
        OverlayAnchor anchor,
        Rect bounds,
        Vec2 offset = default,
        double edgeInset = 0,
        double? topInset = null)
    {
        double originX = AnchoredOriginX(anchor, size, bounds, edgeInset);

        // Top-anchored: the overlay's top edge sits just under the top of `bounds`.
        double originY = bounds.Top + (topInset ?? edgeInset);

        var proposed = new Rect(
            originX + offset.X,
            originY + offset.Y,
            size.Width,
            size.Height);

        return ClampAnchorPoint(proposed, bounds);
    }

    private static double AnchoredOriginX(OverlayAnchor anchor, Size size, Rect bounds, double edgeInset) =>
        anchor switch
        {
            OverlayAnchor.TopLeading => bounds.Left + edgeInset,
            OverlayAnchor.TopTrailing => bounds.Right - size.Width - edgeInset,
            _ => bounds.Left + ((bounds.Width - size.Width) / 2),
        };

    /// <summary>Keeps the point the charm hangs from on screen, rather than the whole window.</summary>
    /// <remarks>
    /// The overlay is mostly empty: a wide, tall, transparent canvas with a rope down the
    /// middle of it, sized so the charm has room to swing. Insisting that all of it stay
    /// on screen therefore stops the charm about half a window short of either edge —
    /// which is exactly the "cannot reach the left or right edge" that made the position
    /// control feel like it had dead zones. What has to stay on screen is the rope, and
    /// the rope is at the top centre.
    /// </remarks>
    public static Rect ClampAnchorPoint(Rect rect, Rect bounds)
    {
        if (bounds.IsEmpty)
        {
            return rect;
        }

        double column = Math.Clamp(rect.MidX, bounds.Left, bounds.Right);
        double top = Math.Clamp(rect.Top, bounds.Top, bounds.Bottom);
        return new Rect(column - (rect.Width / 2), top, rect.Width, rect.Height);
    }

    /// <summary>
    /// Keeps <paramref name="rect"/> fully inside <paramref name="bounds"/> when it is
    /// small enough to fit. Oversized rects are returned untouched so the caller can
    /// decide what to do.
    /// </summary>
    public static Rect Clamp(Rect rect, Rect bounds)
    {
        if (rect.Width > bounds.Width || rect.Height > bounds.Height)
        {
            return rect;
        }

        double x = Math.Min(Math.Max(rect.Left, bounds.Left), bounds.Right - rect.Width);
        double y = Math.Min(Math.Max(rect.Top, bounds.Top), bounds.Bottom - rect.Height);
        return new Rect(x, y, rect.Width, rect.Height);
    }
}
