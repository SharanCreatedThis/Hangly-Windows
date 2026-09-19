//
//  Size.cs
//  Hangly
//

namespace Hangly.Core.Geometry;

/// <summary>A width and a height in points, standing in for <c>CGSize</c>.</summary>
public readonly struct Size : IEquatable<Size>
{
    public double Width { get; }

    public double Height { get; }

    public Size(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public static Size Zero => new(0, 0);

    public bool Equals(Size other) => Width.Equals(other.Width) && Height.Equals(other.Height);

    public override bool Equals(object? obj) => obj is Size other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(Size a, Size b) => a.Equals(b);

    public static bool operator !=(Size a, Size b) => !a.Equals(b);

    public override string ToString() => $"{Width:0.##} × {Height:0.##}";
}
