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
    /// <param name="position">
    /// Where along the top edge the rope hangs: 0 hard left, 0.5 centred, 1 hard right.
    /// </param>
    /// <param name="bounds">
    /// The display region to place within, in virtual-desktop coordinates. This should be
    /// the monitor's <em>work area</em>, so the overlay hangs below a top-docked taskbar
    /// rather than behind it.
    /// </param>
    /// <param name="offset">User nudge. <c>x</c> positive moves right, <c>y</c> positive moves down.</param>
    /// <param name="edgeInset">Margin kept at the display edges.</param>
    /// <param name="topInset">
    /// Margin kept above the overlay. Null means "use <paramref name="edgeInset"/>".
    /// </param>
    /// <returns>A frame in virtual-desktop coordinates.</returns>
    /// <remarks>
    /// <b>A fraction rather than a corner.</b> Top Left, Top Center and Top Right were
    /// three of the thousands of places a charm can hang, and the two somebody did not
    /// pick were rarely the one they wanted.
    ///
    /// <para>The fraction places the <em>rope</em>, not the window. The window is mostly
    /// empty — a wide transparent canvas with a cord down the middle — so placing its left
    /// edge would put the charm half a window away from where the number says. At 0 the
    /// cord sits against the left edge of the display and half the canvas hangs off it,
    /// which is what "hard left" has to mean for a thing drawn in the middle of its own
    /// window.</para>
    /// </remarks>
    public static Rect Frame(
        Size size,
        double position,
        Rect bounds,
        Vec2 offset = default,
        double edgeInset = 0,
        double? topInset = null)
    {
        double along = bounds.Left + (bounds.Width * Math.Clamp(position, 0, 1));
        double originX = along - (size.Width / 2);

        var placed = new Rect(
            originX + offset.X,
            bounds.Top + (topInset ?? edgeInset) + offset.Y,
            size.Width,
            size.Height);

        return ClampAnchorPoint(placed, bounds);
    }

    /// <summary>
    /// The old three-corner placement, kept so a settings file written before positions
    /// existed still opens with the charm where it was.
    /// </summary>
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
