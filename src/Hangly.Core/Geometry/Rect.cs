//
//  Rect.cs
//  Hangly
//

namespace Hangly.Core.Geometry;

/// <summary>A rectangle in display coordinates.</summary>
/// <remarks>
/// <b>Y grows downward</b>, with the origin at the top left, which is Win32's
/// convention for both monitor bounds and window positions.
///
/// <para>This is the one place the port deliberately departs from the original rather
/// than transcribing it. The macOS <c>ScreenPlacement</c> works in AppKit's y-up global
/// space, where the "top" of a rect is its <c>maxY</c>; on Windows the top is
/// <see cref="Top"/> and the arithmetic inverts. Converting at the boundary and keeping
/// the y-up rules would have meant every placement bug living in a coordinate flip
/// nobody could see, so the rules are restated in the platform's own convention and
/// tested in it — including the case the original has a test for, a second display
/// positioned left of the primary at a negative origin.</para>
/// </remarks>
public readonly record struct Rect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public double MidX => Left + (Width / 2);

    public double MidY => Top + (Height / 2);

    public static Rect Zero => new(0, 0, 0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => $"({Left:0.##}, {Top:0.##}) {Width:0.##}×{Height:0.##}";
}
