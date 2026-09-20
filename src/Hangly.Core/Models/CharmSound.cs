//
//  CharmSound.cs
//  Hangly
//
//  What a charm is made of, as far as the ear is concerned.
//

namespace Hangly.Core.Models;

/// <summary>The material a charm sounds like when it knocks against something.</summary>
/// <remarks>
/// Carried in the catalogue now although nothing plays it yet: the synthesiser is a
/// later milestone, and a catalogue generated from the Swift may as well bring the
/// field across with everything else rather than be regenerated for it later.
/// </remarks>
public enum CharmSound
{
    Bell,
    Wood,
    Glass,
    Metal,
    Soft,
}
