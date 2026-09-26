//
//  CharmSound.cs
//  Hangly
//
//  What a charm is made of, as far as the ear is concerned.
//

namespace Hangly.Core.Audio;

/// <summary>A charm's material, which decides the sound it makes.</summary>
/// <remarks>
/// The macOS <c>CharmSound</c>, case for case. Every catalogue entry carries one,
/// generated from the Swift catalogue, so a charm sounds the same on both platforms. A
/// charm somebody made is <see cref="Soft"/>, as on macOS.
/// </remarks>
public enum CharmSound
{
    /// <summary>A struck bell: a clear fundamental over inharmonic partials, ringing out.</summary>
    Bell,

    /// <summary>Painted wood or clay: a short, dry knock.</summary>
    Wood,

    /// <summary>Glass or enamel: a bright, brief tink.</summary>
    Glass,

    /// <summary>Iron or brass: a clink with a little sustain.</summary>
    Metal,

    /// <summary>Fabric, straw or fruit: barely a thud.</summary>
    Soft,
}
