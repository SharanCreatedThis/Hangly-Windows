//
//  Precision.cs
//  Hangly
//
//  The one floating-point constant the solver compares against.
//

namespace Hangly.Core.Geometry;

/// <summary>
/// Shared floating-point tolerances.
/// </summary>
public static class Precision
{
    /// <summary>
    /// Machine epsilon for <see cref="double"/>: the gap between 1 and the next
    /// representable value above it.
    /// </summary>
    /// <remarks>
    /// This is the port of Swift's <c>Double.ulpOfOne</c>, which the solver uses
    /// throughout as "too small to divide by".
    ///
    /// It is deliberately <em>not</em> <see cref="double.Epsilon"/>. The two names
    /// describe different numbers: .NET's <c>Epsilon</c> is the smallest denormal,
    /// about 5e-324, which is some 300 orders of magnitude below what every one of
    /// these guards means. Using it would let the solver divide by a distance of
    /// 1e-300 and hand the rope an infinity.
    /// </remarks>
    public const double UlpOfOne = 2.2204460492503131e-16;
}
