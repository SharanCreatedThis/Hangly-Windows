//
//  AnalyticsValue.cs
//  Hangly
//
//  The kinds of value the app is allowed to send.
//

namespace Hangly.Core.Analytics;

/// <summary>A property value, as a closed set of types.</summary>
/// <remarks>
/// Deliberately not <see cref="object"/>. An event built out of <c>object</c> cannot be
/// compared and cannot be asserted on in a test without casting — and the whole safety
/// argument for an analytics layer is that you can look at an event and see exactly what
/// leaves the machine. This is the list of things that may.
/// </remarks>
public abstract record AnalyticsValue
{
    private AnalyticsValue()
    {
    }

    public static AnalyticsValue Of(string value) => new Text(value);

    public static AnalyticsValue Of(int value) => new Integer(value);

    public static AnalyticsValue Of(double value) => new Number(value);

    public static AnalyticsValue Of(bool value) => new Flag(value);

    public static AnalyticsValue Of(IReadOnlyList<string> value) => new List(value);

    /// <summary>A property that is sent as JSON null rather than omitted.</summary>
    /// <remarks>
    /// The difference matters for exactly one property. PostHog reads the sending
    /// address and derives a country, a region and a city from it unless the payload
    /// carries <c>$ip</c> set to null — omitting the key is how you get the geolocation,
    /// not how you decline it. Everything else that has no value is simply left out.
    /// </remarks>
    public static AnalyticsValue Null { get; } = new Absent();

    public sealed record Text(string Value) : AnalyticsValue;

    /// <summary>Explicitly nothing. See <see cref="Null"/>.</summary>
    public sealed record Absent : AnalyticsValue;

    public sealed record Integer(int Value) : AnalyticsValue;

    public sealed record Number(double Value) : AnalyticsValue;

    public sealed record Flag(bool Value) : AnalyticsValue;

    /// <remarks>Compared by contents, because the generated equality would compare the list by reference.</remarks>
    public sealed record List(IReadOnlyList<string> Value) : AnalyticsValue
    {
        public bool Equals(List? other) =>
            other is not null && Value.SequenceEqual(other.Value, StringComparer.Ordinal);

        public override int GetHashCode()
        {
            var hash = default(HashCode);
            foreach (string item in Value)
            {
                hash.Add(item, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
