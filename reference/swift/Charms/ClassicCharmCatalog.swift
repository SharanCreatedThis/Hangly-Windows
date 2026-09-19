//
//  ClassicCharmCatalog.swift
//  Hangly
//
//  The five classics: the plain shapes the app opened with.
//

import Foundation

extension CollectionCharmCatalog {

    /// The five classics: plain shapes rather than charms with a story.
    ///
    /// They are artwork like everything else. They used to be hand-written
    /// `CGPath` geometry, which is why they were held apart from the catalogue —
    /// there was nothing to look up. Repainted with the rest of the collection,
    /// they have assets, so there is no longer a second kind of built-in charm.
    static let classicEntries: [Entry] = [
        Entry(
            kind: .circle,
            sourceFileName: "Bead.svg",
            mass: 2.6,
            radiusRatio: 0.140,
            palette: CharmPalette(
                primary: CharmColor(0.12, 0.20, 0.66),
                secondary: CharmColor(0.07, 0.12, 0.44),
                deep: CharmColor(0.03, 0.06, 0.24),
                light: CharmColor(0.46, 0.58, 0.95)
            ),
            sound: .glass,
            beadCount: 0
        ),
        Entry(
            kind: .camera,
            sourceFileName: "Camera.svg",
            mass: 4.2,
            radiusRatio: 0.170,
            palette: CharmPalette(
                primary: CharmColor(0.36, 0.38, 0.43),
                secondary: CharmColor(0.20, 0.22, 0.26),
                deep: CharmColor(0.09, 0.10, 0.12),
                light: CharmColor(0.74, 0.77, 0.82)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .star,
            sourceFileName: "Star.svg",
            mass: 2.2,
            radiusRatio: 0.157,
            palette: CharmPalette(
                primary: CharmColor(1.00, 0.78, 0.25),
                secondary: CharmColor(0.93, 0.58, 0.10),
                deep: CharmColor(0.62, 0.35, 0.03),
                light: CharmColor(1.00, 0.93, 0.70)
            ),
            sound: .soft,
            beadCount: 1
        ),
        Entry(
            kind: .heart,
            sourceFileName: "Heart.svg",
            mass: 2.9,
            radiusRatio: 0.149,
            // Rose quartz in a silver bezel, where the drawn one was pillar-box red.
            palette: CharmPalette(
                primary: CharmColor(0.93, 0.70, 0.74),
                secondary: CharmColor(0.82, 0.51, 0.57),
                deep: CharmColor(0.54, 0.29, 0.34),
                light: CharmColor(1.00, 0.90, 0.92)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .diamond,
            sourceFileName: "Diamond.svg",
            mass: 3.4,
            radiusRatio: 0.142,
            palette: CharmPalette(
                primary: CharmColor(0.62, 0.88, 0.97),
                secondary: CharmColor(0.35, 0.68, 0.88),
                deep: CharmColor(0.18, 0.42, 0.62),
                light: CharmColor(0.90, 0.98, 1.00)
            ),
            sound: .glass,
            beadCount: 0
        )
    ]
}
