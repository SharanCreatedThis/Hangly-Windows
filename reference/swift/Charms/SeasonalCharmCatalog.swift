//
//  SeasonalCharmCatalog.swift
//  Hangly
//
//  The seasonal packs' place in the charm catalogue.
//

import Foundation

/// The eleven seasonal charms, described exactly as the hand-drawn collection is.
///
/// Kept in their own file because they were made differently: the collection was
/// illustrated, and these were constructed by `Scripts/generate-seasonal-charms.py`
/// from one shared style kit. Nothing downstream can tell — they take the same
/// `Entry`, hang on the same cord, split into the same beads and are read by the
/// same splitter — but the seam is worth being able to see, because replacing one
/// with a drawn version is a matter of dropping in an SVG and changing nothing here.
extension CollectionCharmCatalog {
    static let seasonalEntries: [Entry] = [
        Entry(
            kind: .snowflake,
            sourceFileName: "Snowflake.svg",
            mass: 2.30,
            radiusRatio: 0.1475,
            palette: CharmPalette(
                primary: CharmColor(0.42, 0.70, 0.86),
                secondary: CharmColor(0.24, 0.47, 0.63),
                deep: CharmColor(0.06, 0.27, 0.41),
                light: CharmColor(0.85, 0.95, 1.00)
            ),
            sound: .glass,
            beadCount: 2
        ),
        Entry(
            kind: .bell,
            sourceFileName: "Bell.svg",
            mass: 3.10,
            radiusRatio: 0.1530,
            palette: CharmPalette(
                primary: CharmColor(0.84, 0.60, 0.17),
                secondary: CharmColor(0.60, 0.40, 0.08),
                deep: CharmColor(0.29, 0.17, 0.02),
                light: CharmColor(1.00, 0.90, 0.65)
            ),
            sound: .bell,
            beadCount: 2
        ),
        Entry(
            kind: .candyCane,
            sourceFileName: "Candy Cane.svg",
            mass: 2.42,
            radiusRatio: 0.1435,
            palette: CharmPalette(
                primary: CharmColor(0.88, 0.16, 0.11),
                secondary: CharmColor(0.63, 0.08, 0.05),
                deep: CharmColor(0.37, 0.04, 0.03),
                light: CharmColor(1.00, 0.85, 0.82)
            ),
            sound: .wood,
            beadCount: 2
        ),
        Entry(
            kind: .pumpkin,
            sourceFileName: "Pumpkin.svg",
            mass: 3.35,
            radiusRatio: 0.1615,
            palette: CharmPalette(
                primary: CharmColor(0.94, 0.48, 0.09),
                secondary: CharmColor(0.68, 0.29, 0.03),
                deep: CharmColor(0.36, 0.14, 0.02),
                light: CharmColor(1.00, 0.77, 0.42)
            ),
            sound: .wood,
            beadCount: 2
        ),
        Entry(
            kind: .ghost,
            sourceFileName: "Ghost.svg",
            mass: 2.15,
            radiusRatio: 0.1555,
            palette: CharmPalette(
                primary: CharmColor(0.87, 0.89, 0.96),
                secondary: CharmColor(0.56, 0.60, 0.77),
                deep: CharmColor(0.23, 0.27, 0.41),
                light: CharmColor(1.00, 1.00, 1.00)
            ),
            sound: .soft,
            beadCount: 2
        ),
        Entry(
            kind: .bat,
            sourceFileName: "Bat.svg",
            mass: 2.55,
            radiusRatio: 0.1595,
            palette: CharmPalette(
                primary: CharmColor(0.42, 0.28, 0.62),
                secondary: CharmColor(0.23, 0.14, 0.38),
                deep: CharmColor(0.09, 0.05, 0.16),
                light: CharmColor(0.72, 0.60, 0.90)
            ),
            sound: .soft,
            beadCount: 2
        ),
        Entry(
            kind: .diya,
            sourceFileName: "Diya.svg",
            mass: 3.00,
            radiusRatio: 0.1545,
            palette: CharmPalette(
                primary: CharmColor(0.71, 0.34, 0.11),
                secondary: CharmColor(0.47, 0.20, 0.05),
                deep: CharmColor(0.23, 0.09, 0.02),
                light: CharmColor(1.00, 0.81, 0.42)
            ),
            sound: .wood,
            beadCount: 2
        ),
        Entry(
            kind: .lotus,
            sourceFileName: "Lotus.svg",
            mass: 2.50,
            radiusRatio: 0.1585,
            palette: CharmPalette(
                primary: CharmColor(0.94, 0.42, 0.65),
                secondary: CharmColor(0.64, 0.19, 0.42),
                deep: CharmColor(0.33, 0.06, 0.20),
                light: CharmColor(1.00, 0.84, 0.92)
            ),
            sound: .soft,
            beadCount: 2
        ),
        Entry(
            kind: .lantern,
            sourceFileName: "Lantern.svg",
            mass: 2.95,
            radiusRatio: 0.1505,
            palette: CharmPalette(
                primary: CharmColor(0.85, 0.13, 0.11),
                secondary: CharmColor(0.58, 0.07, 0.05),
                deep: CharmColor(0.30, 0.03, 0.02),
                light: CharmColor(1.00, 0.70, 0.55)
            ),
            sound: .wood,
            beadCount: 2
        ),
        Entry(
            kind: .firework,
            sourceFileName: "Firework.svg",
            mass: 2.40,
            radiusRatio: 0.1565,
            palette: CharmPalette(
                primary: CharmColor(0.95, 0.70, 0.18),
                secondary: CharmColor(0.66, 0.42, 0.05),
                deep: CharmColor(0.20, 0.09, 0.32),
                light: CharmColor(1.00, 0.94, 0.74)
            ),
            sound: .glass,
            beadCount: 2
        ),
        Entry(
            kind: .luckyCoin,
            sourceFileName: "Lucky Coin.svg",
            mass: 3.20,
            radiusRatio: 0.1465,
            palette: CharmPalette(
                primary: CharmColor(0.82, 0.63, 0.20),
                secondary: CharmColor(0.55, 0.39, 0.06),
                deep: CharmColor(0.28, 0.19, 0.02),
                light: CharmColor(1.00, 0.93, 0.72)
            ),
            sound: .metal,
            beadCount: 2
        )
    ]
}
