//
//  RopeCharm.swift
//  Hangly
//
//  A charm and how big it is in the place it hangs.
//

import Foundation

/// One place on the rope: which charm is in it, and how large that charm is drawn
/// relative to the size its artwork asks for.
///
/// The size belongs to the *place*, not to the charm. Swapping the middle charm of
/// three keeps the middle small, because what was chosen was a composition — one
/// large piece with two smaller ones either side of it — and that composition should
/// survive changing one's mind about which charm is in it.
///
/// This is a relative trim, not an absolute size. `OverlaySettings.charmSize` is the
/// master: it says how big the whole ornament is, and this says how the charms on it
/// are balanced against each other. One control answers "how big is Hangly", the
/// other answers "which of these is the centrepiece", and neither can answer the
/// other's question.
struct RopeCharm: Codable, Equatable, Sendable {
    /// How far a single place may be trimmed from the size its artwork asks for.
    ///
    /// Narrower than the master size range on purpose. This exists to make a
    /// composition read, not to make one charm enormous: past about a third either
    /// way the small ones stop looking like part of the same string and start
    /// looking like a mistake.
    static let sizeRange: ClosedRange<Double> = 0.6...1.6

    /// Which charm hangs here.
    var charm: CharmID

    /// How large it is drawn, as a multiple of the size its artwork asks for.
    var size: Double

    init(_ charm: CharmID, size: Double = 1) {
        self.charm = charm
        self.size = size.clamped(to: Self.sizeRange)
    }

    /// Tolerant of a document that names a charm without sizing it, which is every
    /// document written before places could be sized.
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.init(
            try container.decode(CharmID.self, forKey: .charm),
            size: try container.decodeIfPresent(Double.self, forKey: .size) ?? 1
        )
    }
}

extension CharmMetrics {
    /// This charm at a different size.
    ///
    /// Radius and mass both move, and they move together: a charm drawn twice the
    /// size that swung with the same authority as before would read as a picture of
    /// a charm rather than an object on a string. Linear rather than cubed — the
    /// rope is tuned for how heavy a charm *looks*, and a cubed mass makes a large
    /// charm behave like a wrecking ball long before it looks like one.
    ///
    /// ``knotInset`` is a proportion of the radius and so is already correct at any
    /// size; scaling it too would move the cord off the artwork.
    func scaled(by size: Double) -> CharmMetrics {
        guard size != 1 else { return self }
        return CharmMetrics(
            mass: mass * size,
            radiusRatio: radiusRatio * size,
            knotInset: knotInset
        )
    }
}
