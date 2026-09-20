//
//  RopeCharm.cs
//  Hangly
//
//  A charm and how big it is in the place it hangs.
//

namespace Hangly.Core.Models;

/// <summary>
/// One place on the rope: which charm is in it, and how large that charm is drawn
/// relative to the size its artwork asks for.
/// </summary>
/// <remarks>
/// A transcription of the macOS <c>RopeCharm</c>, comments included, because the reasoning
/// is the specification here.
///
/// <para>The size belongs to the <em>place</em>, not to the charm. Swapping the middle
/// charm of three keeps the middle small, because what was chosen was a composition — one
/// large piece with two smaller ones either side of it — and that composition should
/// survive changing one's mind about which charm is in it.</para>
///
/// <para>This is a relative trim, not an absolute size. <c>OverlaySettings.CharmSize</c>
/// is the master: it says how big the whole ornament is, and this says how the charms on
/// it are balanced against each other. One control answers "how big is Hangly", the other
/// answers "which of these is the centrepiece", and neither can answer the other's
/// question.</para>
/// </remarks>
public readonly record struct RopeCharm(string Id, double Size)
{
    /// <summary>The smallest a single place may be trimmed to.</summary>
    /// <remarks>
    /// Narrower than the master size range on purpose. This exists to make a composition
    /// read, not to make one charm enormous: past about a third either way the small ones
    /// stop looking like part of the same string and start looking like a mistake.
    /// </remarks>
    public const double MinimumSize = 0.6;

    public const double MaximumSize = 1.6;

    public RopeCharm(string id)
        : this(id, 1)
    {
    }

    /// <summary>This place, with its size brought inside the allowed range.</summary>
    public RopeCharm Clamped() => this with { Size = Math.Clamp(Size, MinimumSize, MaximumSize) };
}
