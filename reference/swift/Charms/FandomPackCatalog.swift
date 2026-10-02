//
//  FandomPackCatalog.swift
//  Hangly
//
//  The fandom collections' place in the charm catalogue, and the charms that
//  joined collections already there.
//

import Foundation

/// The charms that arrived in Hangly 2.2: eight new collections, and charms added to
/// Marvel, Stranger Things, Football Legends and Spirituality.
///
/// Described exactly as the collections before them, with three differences worth
/// knowing before changing anything here.
///
/// **Nothing is added to the artwork.** A figure drawn without a loop hangs as it was
/// drawn, and the cord meets it where the splitter measures its top on the centre
/// line. The Golden Snitch's centre line first meets the ball, below its middle, so
/// it hangs from just above its centre and the drawn cord runs on behind it, between
/// the wings, to the ball (`cordInset`).
///
/// **The sneakers and a few sigils hang from loops drawn off to one side.** Hung by
/// such a loop a real charm tips until the loop is over its middle, so those assets
/// are turned in the SVG by exactly that much, and hang from the loop they were drawn
/// with.
///
/// **Spider-Man and Gwen is drawn on its own rope.** The rope above Spider-Man is cord
/// furniture the splitter drops; the simulated cord takes over at his grip.
///
/// Palettes are sampled from the artwork by the rule the earlier collections used:
/// `primary` is the mean of the most saturated tenth of the opaque pixels by chroma,
/// `deep` and `light` the means of the darkest and lightest twelfths, and `secondary`
/// is `primary` at 62 per cent.
extension CollectionCharmCatalog {
    /// All of them, in the order the Library offers them.
    static let fandomPackEntries: [Entry] =
        additionEntries + animeEntries + sagaEntries + iconEntries

    /// The charms added to collections that were already here.
    static let additionEntries: [Entry] = [
        // Marvel.
        Entry(
            kind: .spiderManGwen,
            sourceFileName: "Avengers/Spider-Man and Gwen.svg",
            mass: 2.70,
            radiusRatio: 0.1790,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.30, 0.26),
                secondary: CharmColor(0.36, 0.19, 0.16),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.78, 0.70, 0.64)
            ),
            sound: .soft,
            beadCount: 0,
            hangsByOwnCord: true
        ),
        Entry(
            kind: .eyeOfAgamotto,
            sourceFileName: "Avengers/Eye of Agamotto.svg",
            mass: 3.22,
            radiusRatio: 0.1602,
            palette: CharmPalette(
                primary: CharmColor(0.38, 0.71, 0.44),
                secondary: CharmColor(0.24, 0.44, 0.27),
                deep: CharmColor(0.06, 0.04, 0.01),
                light: CharmColor(0.85, 0.90, 0.77)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .stormbreaker,
            sourceFileName: "Avengers/Stormbreaker.svg",
            mass: 3.24,
            radiusRatio: 0.1603,
            palette: CharmPalette(
                primary: CharmColor(0.51, 0.37, 0.28),
                secondary: CharmColor(0.32, 0.23, 0.17),
                deep: CharmColor(0.13, 0.10, 0.11),
                light: CharmColor(0.88, 0.88, 0.84)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .doctorDoom,
            sourceFileName: "Avengers/Doctor Doom.svg",
            mass: 3.27,
            radiusRatio: 0.1604,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.43, 0.33),
                secondary: CharmColor(0.35, 0.27, 0.20),
                deep: CharmColor(0.00, 0.00, 0.01),
                light: CharmColor(0.75, 0.69, 0.62)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .deadpoolWolverine,
            sourceFileName: "Avengers/Deadpool and Wolverine.svg",
            mass: 3.29,
            radiusRatio: 0.1606,
            palette: CharmPalette(
                primary: CharmColor(0.85, 0.61, 0.17),
                secondary: CharmColor(0.53, 0.38, 0.11),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.93, 0.89, 0.79)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Stranger Things.
        Entry(
            kind: .maxMayfield,
            sourceFileName: "Stranger Things/Max Mayfield.svg",
            mass: 3.66,
            radiusRatio: 0.1662,
            palette: CharmPalette(
                primary: CharmColor(0.74, 0.21, 0.05),
                secondary: CharmColor(0.46, 0.13, 0.03),
                deep: CharmColor(0.04, 0.01, 0.01),
                light: CharmColor(0.81, 0.63, 0.54)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .steveHarrington,
            sourceFileName: "Stranger Things/Steve Harrington.svg",
            mass: 3.67,
            radiusRatio: 0.1663,
            palette: CharmPalette(
                primary: CharmColor(0.66, 0.36, 0.23),
                secondary: CharmColor(0.41, 0.22, 0.14),
                deep: CharmColor(0.01, 0.00, 0.01),
                light: CharmColor(0.74, 0.63, 0.59)
            ),
            sound: .wood,
            beadCount: 0
        ),
        // Football Legends.
        Entry(
            kind: .ronaldoPortugal,
            sourceFileName: "Football/Ronaldo Portugal.svg",
            mass: 4.10,
            radiusRatio: 0.1721,
            palette: CharmPalette(
                primary: CharmColor(0.81, 0.15, 0.06),
                secondary: CharmColor(0.50, 0.09, 0.04),
                deep: CharmColor(0.19, 0.01, 0.00),
                light: CharmColor(0.95, 0.89, 0.85)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .ronaldoBicycleKick,
            sourceFileName: "Football/Ronaldo Bicycle Kick.svg",
            mass: 4.11,
            radiusRatio: 0.1723,
            palette: CharmPalette(
                primary: CharmColor(0.70, 0.45, 0.23),
                secondary: CharmColor(0.43, 0.28, 0.14),
                deep: CharmColor(0.10, 0.07, 0.07),
                light: CharmColor(0.98, 0.97, 0.96)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .ballonDor,
            sourceFileName: "Football/Ballon d'Or.svg",
            mass: 4.12,
            radiusRatio: 0.1724,
            palette: CharmPalette(
                primary: CharmColor(0.82, 0.54, 0.09),
                secondary: CharmColor(0.51, 0.33, 0.06),
                deep: CharmColor(0.04, 0.01, 0.00),
                light: CharmColor(0.99, 0.96, 0.88)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .championsLeagueTrophy,
            sourceFileName: "Football/Champions League Trophy.svg",
            mass: 4.13,
            radiusRatio: 0.1725,
            palette: CharmPalette(
                primary: CharmColor(0.79, 0.55, 0.25),
                secondary: CharmColor(0.49, 0.34, 0.15),
                deep: CharmColor(0.08, 0.04, 0.02),
                light: CharmColor(0.99, 0.99, 0.98)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Spirituality.
        Entry(
            kind: .rudraksha,
            sourceFileName: "Spirituality/Rudraksha.svg",
            mass: 3.77,
            radiusRatio: 0.1677,
            palette: CharmPalette(
                primary: CharmColor(0.68, 0.32, 0.07),
                secondary: CharmColor(0.42, 0.20, 0.04),
                deep: CharmColor(0.03, 0.00, 0.00),
                light: CharmColor(0.90, 0.76, 0.61)
            ),
            sound: .wood,
            beadCount: 0
        ),
        Entry(
            kind: .shivaLingam,
            sourceFileName: "Spirituality/Shiva Lingam.svg",
            mass: 3.78,
            radiusRatio: 0.1678,
            palette: CharmPalette(
                primary: CharmColor(0.64, 0.42, 0.20),
                secondary: CharmColor(0.40, 0.26, 0.12),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.90, 0.84, 0.74)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .buddha,
            sourceFileName: "Spirituality/Buddha.svg",
            mass: 3.79,
            radiusRatio: 0.1679,
            palette: CharmPalette(
                primary: CharmColor(0.80, 0.51, 0.14),
                secondary: CharmColor(0.50, 0.32, 0.09),
                deep: CharmColor(0.02, 0.01, 0.00),
                light: CharmColor(0.95, 0.81, 0.63)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .hanuman,
            sourceFileName: "Spirituality/Hanuman.svg",
            mass: 3.80,
            radiusRatio: 0.1681,
            palette: CharmPalette(
                primary: CharmColor(0.78, 0.27, 0.06),
                secondary: CharmColor(0.48, 0.17, 0.04),
                deep: CharmColor(0.17, 0.02, 0.00),
                light: CharmColor(0.96, 0.83, 0.67)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .kaaba,
            sourceFileName: "Spirituality/Kaaba.svg",
            mass: 3.81,
            radiusRatio: 0.1682,
            palette: CharmPalette(
                primary: CharmColor(0.79, 0.57, 0.28),
                secondary: CharmColor(0.49, 0.35, 0.17),
                deep: CharmColor(0.01, 0.01, 0.01),
                light: CharmColor(0.93, 0.80, 0.57)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .greenDome,
            sourceFileName: "Spirituality/Green Dome.svg",
            mass: 3.82,
            radiusRatio: 0.1683,
            palette: CharmPalette(
                primary: CharmColor(0.61, 0.47, 0.21),
                secondary: CharmColor(0.38, 0.29, 0.13),
                deep: CharmColor(0.01, 0.05, 0.01),
                light: CharmColor(0.95, 0.91, 0.85)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .crescentAndStar,
            sourceFileName: "Spirituality/Crescent and Star.svg",
            mass: 3.83,
            radiusRatio: 0.1685,
            palette: CharmPalette(
                primary: CharmColor(0.70, 0.46, 0.13),
                secondary: CharmColor(0.43, 0.29, 0.08),
                deep: CharmColor(0.03, 0.02, 0.00),
                light: CharmColor(0.98, 0.91, 0.79)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .allahPendant,
            sourceFileName: "Spirituality/Allah Pendant.svg",
            mass: 3.84,
            radiusRatio: 0.1687,
            palette: CharmPalette(
                primary: CharmColor(0.73, 0.49, 0.16),
                secondary: CharmColor(0.45, 0.30, 0.10),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.96, 0.86, 0.69)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .cross,
            sourceFileName: "Spirituality/Cross.svg",
            mass: 3.86,
            radiusRatio: 0.1689,
            palette: CharmPalette(
                primary: CharmColor(0.82, 0.50, 0.07),
                secondary: CharmColor(0.51, 0.31, 0.04),
                deep: CharmColor(0.20, 0.05, 0.00),
                light: CharmColor(0.98, 0.95, 0.88)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .dove,
            sourceFileName: "Spirituality/Dove of Peace.svg",
            mass: 3.87,
            radiusRatio: 0.1691,
            palette: CharmPalette(
                primary: CharmColor(0.81, 0.55, 0.20),
                secondary: CharmColor(0.50, 0.34, 0.12),
                deep: CharmColor(0.50, 0.31, 0.12),
                light: CharmColor(0.97, 0.94, 0.90)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .prayingAngel,
            sourceFileName: "Spirituality/Praying Angel.svg",
            mass: 3.88,
            radiusRatio: 0.1693,
            palette: CharmPalette(
                primary: CharmColor(0.75, 0.45, 0.13),
                secondary: CharmColor(0.46, 0.28, 0.08),
                deep: CharmColor(0.08, 0.04, 0.05),
                light: CharmColor(0.98, 0.91, 0.84)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .sacredHeart,
            sourceFileName: "Spirituality/Sacred Heart.svg",
            mass: 3.89,
            radiusRatio: 0.1695,
            palette: CharmPalette(
                primary: CharmColor(0.87, 0.43, 0.04),
                secondary: CharmColor(0.54, 0.27, 0.02),
                deep: CharmColor(0.13, 0.01, 0.00),
                light: CharmColor(0.98, 0.93, 0.82)
            ),
            sound: .metal,
            beadCount: 0
        )
    ]
}
