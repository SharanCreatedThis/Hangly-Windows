//
//  CollectionPackCatalog.swift
//  Hangly
//
//  The four collections' place in the charm catalogue.
//

import Foundation

/// Marvel, DC, Tamil Spiritual and BTS, described exactly as the hand-drawn
/// collection and the seasonal packs are.
///
/// Two things about these are worth knowing before changing any number here.
///
/// **The artwork lives in folders.** `sourceFileName` carries the subdirectory —
/// `Avengers/Spider-Man.svg` — because these arrived as four folders under
/// `Assets/Charms` rather than loose beside the hand-drawn set. `SyncCharmAssets`
/// appends the whole path, so nothing else had to change for it.
///
/// **Only Tamil Spiritual has beads.** Its five are drawn hanging on a strung cord,
/// the way the hand-drawn collection is, so they declare `beadCount: 3` and the
/// splitter hands those three to the rope. The other fifteen are drawn as bare
/// pendants with a bail and no cord, so they declare `beadCount: 0` and hang whole —
/// the same case as `himmeli`, and the reason the splitter has one.
///
/// Palettes are sampled from the artwork rather than chosen: `primary` is the mean
/// of its most saturated tenth, `deep` and `light` its darkest and lightest twelfths.
/// That is what the ambient glow and the weather tinting read, so a palette that
/// disagrees with the picture shows up as a halo in the wrong colour.
extension CollectionCharmCatalog {
    static let collectionPackEntries: [Entry] = [
        Entry(
            kind: .spiderMan,
            sourceFileName: "Avengers/Spider-Man.svg",
            mass: 2.96,
            radiusRatio: 0.1525,
            palette: CharmPalette(
                primary: CharmColor(0.53, 0.17, 0.26),
                secondary: CharmColor(0.33, 0.11, 0.16),
                deep: CharmColor(0.05, 0.04, 0.11),
                light: CharmColor(0.61, 0.50, 0.58)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .captainAmericaShield,
            sourceFileName: "Avengers/Captain-America.svg",
            mass: 3.46,
            radiusRatio: 0.1515,
            palette: CharmPalette(
                primary: CharmColor(0.70, 0.13, 0.17),
                secondary: CharmColor(0.43, 0.08, 0.11),
                deep: CharmColor(0.09, 0.04, 0.09),
                light: CharmColor(0.73, 0.74, 0.74)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .ironManHelmet,
            sourceFileName: "Avengers/Iron-Man.svg",
            mass: 3.26,
            radiusRatio: 0.1495,
            palette: CharmPalette(
                primary: CharmColor(0.69, 0.34, 0.29),
                secondary: CharmColor(0.43, 0.21, 0.18),
                deep: CharmColor(0.16, 0.03, 0.04),
                light: CharmColor(0.98, 0.92, 0.87)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .thorHammer,
            sourceFileName: "Avengers/Thor Mjölnir.svg",
            mass: 3.91,
            radiusRatio: 0.1535,
            palette: CharmPalette(
                primary: CharmColor(0.43, 0.51, 0.56),
                secondary: CharmColor(0.26, 0.32, 0.34),
                deep: CharmColor(0.11, 0.12, 0.14),
                light: CharmColor(0.85, 0.88, 0.88)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .hulkFist,
            sourceFileName: "Avengers/Hulk.svg",
            mass: 3.71,
            radiusRatio: 0.1550,
            palette: CharmPalette(
                primary: CharmColor(0.19, 0.54, 0.24),
                secondary: CharmColor(0.12, 0.34, 0.15),
                deep: CharmColor(0.06, 0.09, 0.06),
                light: CharmColor(0.39, 0.63, 0.42)
            ),
            sound: .wood,
            beadCount: 0
        ),
        Entry(
            kind: .spiderManSwinging,
            sourceFileName: "Avengers/Spider-Man Swinging.svg",
            mass: 3.01,
            radiusRatio: 0.1528,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.26, 0.33),
                secondary: CharmColor(0.36, 0.16, 0.20),
                deep: CharmColor(0.07, 0.04, 0.08),
                light: CharmColor(0.67, 0.54, 0.56)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .batmanSymbol,
            sourceFileName: "DC/Bat-Man.svg",
            mass: 3.31,
            radiusRatio: 0.1575,
            palette: CharmPalette(
                primary: CharmColor(0.33, 0.38, 0.47),
                secondary: CharmColor(0.20, 0.23, 0.29),
                deep: CharmColor(0.04, 0.05, 0.06),
                light: CharmColor(0.64, 0.61, 0.59)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .supermanShield,
            sourceFileName: "DC/Super-Man.svg",
            mass: 3.36,
            radiusRatio: 0.1590,
            palette: CharmPalette(
                primary: CharmColor(0.48, 0.21, 0.15),
                secondary: CharmColor(0.30, 0.13, 0.10),
                deep: CharmColor(0.17, 0.01, 0.01),
                light: CharmColor(0.85, 0.81, 0.76)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .wonderWomanEmblem,
            sourceFileName: "DC/Wonder Women.svg",
            mass: 3.21,
            radiusRatio: 0.1605,
            palette: CharmPalette(
                primary: CharmColor(0.72, 0.54, 0.38),
                secondary: CharmColor(0.45, 0.34, 0.23),
                deep: CharmColor(0.06, 0.02, 0.03),
                light: CharmColor(0.92, 0.83, 0.66)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .shazamLightning,
            sourceFileName: "DC/Flash.svg",
            mass: 2.91,
            radiusRatio: 0.1485,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.15, 0.10),
                secondary: CharmColor(0.35, 0.09, 0.06),
                deep: CharmColor(0.31, 0.03, 0.03),
                light: CharmColor(0.94, 0.87, 0.70)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .greenLanternRing,
            sourceFileName: "DC/Green Lantern.svg",
            mass: 3.06,
            radiusRatio: 0.1610,
            palette: CharmPalette(
                primary: CharmColor(0.07, 0.73, 0.40),
                secondary: CharmColor(0.04, 0.45, 0.25),
                deep: CharmColor(0.08, 0.20, 0.16),
                light: CharmColor(0.86, 0.92, 0.88)
            ),
            sound: .glass,
            beadCount: 0
        ),
        Entry(
            kind: .vel,
            sourceFileName: "Tamil Gods/Muruga Vel.svg",
            mass: 3.11,
            radiusRatio: 0.1620,
            palette: CharmPalette(
                primary: CharmColor(0.72, 0.56, 0.33),
                secondary: CharmColor(0.44, 0.35, 0.21),
                deep: CharmColor(0.28, 0.16, 0.10),
                light: CharmColor(0.86, 0.76, 0.54)
            ),
            sound: .metal,
            beadCount: 1
        ),
        Entry(
            kind: .vinayagarCoin,
            sourceFileName: "Tamil Gods/Vinayaga.svg",
            mass: 3.16,
            radiusRatio: 0.1468,
            palette: CharmPalette(
                primary: CharmColor(0.71, 0.54, 0.29),
                secondary: CharmColor(0.44, 0.34, 0.18),
                deep: CharmColor(0.24, 0.13, 0.06),
                light: CharmColor(0.79, 0.65, 0.40)
            ),
            sound: .metal,
            beadCount: 1
        ),
        Entry(
            kind: .omSymbol,
            sourceFileName: "Tamil Gods/OM.svg",
            mass: 2.97,
            radiusRatio: 0.1630,
            palette: CharmPalette(
                primary: CharmColor(0.67, 0.43, 0.33),
                secondary: CharmColor(0.42, 0.27, 0.20),
                deep: CharmColor(0.33, 0.15, 0.13),
                light: CharmColor(0.98, 0.92, 0.74)
            ),
            sound: .metal,
            beadCount: 1
        ),
        Entry(
            kind: .karuppuStatue,
            sourceFileName: "Tamil Gods/Karuppu.svg",
            mass: 3.61,
            radiusRatio: 0.1635,
            palette: CharmPalette(
                primary: CharmColor(0.51, 0.38, 0.30),
                secondary: CharmColor(0.32, 0.24, 0.18),
                deep: CharmColor(0.06, 0.05, 0.04),
                light: CharmColor(0.59, 0.49, 0.43)
            ),
            sound: .wood,
            beadCount: 1
        ),
        Entry(
            kind: .templeBell,
            sourceFileName: "Tamil Gods/Mani.svg",
            mass: 3.41,
            radiusRatio: 0.1645,
            palette: CharmPalette(
                primary: CharmColor(0.64, 0.47, 0.32),
                secondary: CharmColor(0.39, 0.29, 0.20),
                deep: CharmColor(0.24, 0.15, 0.10),
                light: CharmColor(0.78, 0.66, 0.53)
            ),
            sound: .bell,
            beadCount: 1
        ),
        Entry(
            kind: .btsMemberOne,
            sourceFileName: "BTS/RM.svg",
            mass: 2.65,
            radiusRatio: 0.1593,
            palette: CharmPalette(
                primary: CharmColor(0.53, 0.34, 0.63),
                secondary: CharmColor(0.33, 0.21, 0.39),
                deep: CharmColor(0.11, 0.07, 0.14),
                light: CharmColor(0.85, 0.79, 0.79)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .btsMemberTwo,
            sourceFileName: "BTS/Jin.svg",
            mass: 2.68,
            radiusRatio: 0.1601,
            palette: CharmPalette(
                primary: CharmColor(0.66, 0.50, 0.41),
                secondary: CharmColor(0.41, 0.31, 0.25),
                deep: CharmColor(0.04, 0.03, 0.04),
                light: CharmColor(0.86, 0.73, 0.65)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .btsMemberThree,
            sourceFileName: "BTS/SUGA.svg",
            mass: 2.71,
            radiusRatio: 0.1609,
            palette: CharmPalette(
                primary: CharmColor(0.73, 0.55, 0.53),
                secondary: CharmColor(0.45, 0.34, 0.33),
                deep: CharmColor(0.55, 0.40, 0.46),
                light: CharmColor(0.89, 0.83, 0.83)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .btsMemberFour,
            sourceFileName: "BTS/j-hope.svg",
            mass: 2.74,
            radiusRatio: 0.1617,
            palette: CharmPalette(
                primary: CharmColor(0.44, 0.39, 0.48),
                secondary: CharmColor(0.27, 0.24, 0.30),
                deep: CharmColor(0.10, 0.08, 0.11),
                light: CharmColor(0.86, 0.84, 0.86)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .btsMemberFive,
            sourceFileName: "BTS/Jimin.svg",
            mass: 2.77,
            radiusRatio: 0.1625,
            palette: CharmPalette(
                primary: CharmColor(0.52, 0.29, 0.67),
                secondary: CharmColor(0.32, 0.18, 0.41),
                deep: CharmColor(0.12, 0.07, 0.18),
                light: CharmColor(0.79, 0.62, 0.78)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .btsMemberSix,
            sourceFileName: "BTS/V.svg",
            mass: 2.80,
            radiusRatio: 0.1633,
            palette: CharmPalette(
                primary: CharmColor(0.70, 0.46, 0.33),
                secondary: CharmColor(0.43, 0.29, 0.21),
                deep: CharmColor(0.53, 0.32, 0.23),
                light: CharmColor(0.94, 0.85, 0.78)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .btsMemberSeven,
            sourceFileName: "BTS/Jungkook.svg",
            mass: 2.83,
            radiusRatio: 0.1641,
            palette: CharmPalette(
                primary: CharmColor(0.55, 0.35, 0.67),
                secondary: CharmColor(0.34, 0.22, 0.42),
                deep: CharmColor(0.03, 0.02, 0.05),
                light: CharmColor(0.87, 0.74, 0.83)
            ),
            sound: .soft,
            beadCount: 0
        )
    ]
}
