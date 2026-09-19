//
//  BuiltInCharms.cs
//  Hangly
//
//  The one charm that is wired up, until the catalogue is transcribed.
//

using Hangly.Core.Models;

namespace Hangly.App.Overlay;

/// <summary>The charms this build knows how to hang.</summary>
/// <remarks>
/// <b>There is one, and the real catalogue has eighty-two.</b> This is a bootstrap, not
/// a design: it exists so the overlay has something to draw while
/// <c>reference/swift/Charms/</c> is still untranscribed, and it should be deleted
/// outright the moment the real catalogue lands rather than grown entry by entry.
///
/// <para>The plain bead is the one chosen, and deliberately: it is the charm the app
/// opened with, its numbers are the ones <see cref="CharmMetrics.Default"/> was written
/// from, and — the part that matters here — it threads <b>no beads</b>. Every other charm
/// in the catalogue derives its beads by splitting its own artwork into regions, which is
/// a subsystem of its own (<c>CharmArtworkSplitter</c>) and is not ported. Picking a charm
/// that needs none keeps this file honest: it is a table entry, not a quarter of the
/// splitter reimplemented badly.</para>
///
/// <para>The numbers are transcribed from <c>ClassicCharmCatalog.swift</c>. The knot inset
/// is not written there either, because that entry takes the default of 0.90.</para>
/// </remarks>
public static class BuiltInCharms
{
    /// <summary>Rose-blue glass in a plain round bead — the charm Hangly opened with.</summary>
    public static CharmDescriptor PlainBead { get; } = new(
        Id: "circle",
        DisplayName: "Bead",
        FileName: "Bead.svg",
        Metrics: new CharmMetrics(Mass: 2.6, RadiusRatio: 0.140, KnotInset: 0.90),
        Palette: new CharmPalette(
            Primary: new CharmColor(0.12, 0.20, 0.66),
            Secondary: new CharmColor(0.07, 0.12, 0.44),
            Deep: new CharmColor(0.03, 0.06, 0.24),
            Light: new CharmColor(0.46, 0.58, 0.95)),
        Beads: []);

    /// <summary>What hangs on the rope at launch.</summary>
    public static IReadOnlyList<CharmDescriptor> Default { get; } = [PlainBead];
}
