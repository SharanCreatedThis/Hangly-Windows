//
//  StoryPackCatalog+Screen.swift
//  Hangly
//
//  Friends, Breaking Bad and Stranger Things: the three story collections that
//  came off a screen rather than a pitch or a stage.
//

import Foundation

/// The second half of `StoryPackCatalog`, held in its own file for length alone.
/// Everything said there about beads, folders and sampled palettes applies here
/// unchanged.
extension CollectionCharmCatalog {
    static let screenEntries: [Entry] = [
        // Friends.
        Entry(
            kind: .rachelGreen,
            sourceFileName: "Friends/Rachel Green.svg",
            mass: 2.87,
            radiusRatio: 0.1692,
            palette: CharmPalette(
                primary: CharmColor(0.64, 0.44, 0.32),
                secondary: CharmColor(0.40, 0.28, 0.20),
                deep: CharmColor(0.06, 0.04, 0.03),
                light: CharmColor(0.73, 0.60, 0.52)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .monicaGeller,
            sourceFileName: "Friends/Monica Geller.svg",
            mass: 2.92,
            radiusRatio: 0.1696,
            palette: CharmPalette(
                primary: CharmColor(0.66, 0.47, 0.37),
                secondary: CharmColor(0.41, 0.29, 0.23),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.69, 0.51, 0.41)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .rossGeller,
            sourceFileName: "Friends/Ross Geller.svg",
            mass: 2.98,
            radiusRatio: 0.1702,
            palette: CharmPalette(
                primary: CharmColor(0.65, 0.46, 0.35),
                secondary: CharmColor(0.41, 0.29, 0.21),
                deep: CharmColor(0.03, 0.02, 0.01),
                light: CharmColor(0.71, 0.54, 0.43)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .joeyTribbiani,
            sourceFileName: "Friends/Joey Tribbiani.svg",
            mass: 3.03,
            radiusRatio: 0.1706,
            palette: CharmPalette(
                primary: CharmColor(0.64, 0.44, 0.33),
                secondary: CharmColor(0.40, 0.27, 0.20),
                deep: CharmColor(0.04, 0.03, 0.02),
                light: CharmColor(0.74, 0.64, 0.58)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .chandlerBing,
            sourceFileName: "Friends/Chandler Bing.svg",
            mass: 3.09,
            radiusRatio: 0.1710,
            palette: CharmPalette(
                primary: CharmColor(0.61, 0.43, 0.34),
                secondary: CharmColor(0.38, 0.27, 0.21),
                deep: CharmColor(0.04, 0.03, 0.02),
                light: CharmColor(0.69, 0.54, 0.47)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .phoebeBuffay,
            sourceFileName: "Friends/Phoebe Buffay.svg",
            mass: 3.14,
            radiusRatio: 0.1714,
            palette: CharmPalette(
                primary: CharmColor(0.68, 0.45, 0.26),
                secondary: CharmColor(0.42, 0.28, 0.16),
                deep: CharmColor(0.08, 0.04, 0.02),
                light: CharmColor(0.77, 0.60, 0.48)
            ),
            sound: .soft,
            beadCount: 0
        ),
        // Breaking Bad.
        Entry(
            kind: .walterWhite,
            sourceFileName: "Breaking Bad/Walter White.svg",
            mass: 3.28,
            radiusRatio: 0.1718,
            palette: CharmPalette(
                primary: CharmColor(0.78, 0.56, 0.14),
                secondary: CharmColor(0.48, 0.35, 0.09),
                deep: CharmColor(0.07, 0.05, 0.04),
                light: CharmColor(0.89, 0.73, 0.47)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .jessePinkman,
            sourceFileName: "Breaking Bad/Jesse Pinkman.svg",
            mass: 3.33,
            radiusRatio: 0.1722,
            palette: CharmPalette(
                primary: CharmColor(0.74, 0.51, 0.11),
                secondary: CharmColor(0.46, 0.32, 0.07),
                deep: CharmColor(0.02, 0.02, 0.01),
                light: CharmColor(0.80, 0.62, 0.35)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .saulGoodman,
            sourceFileName: "Breaking Bad/Saul Goodman.svg",
            mass: 3.38,
            radiusRatio: 0.1726,
            palette: CharmPalette(
                primary: CharmColor(0.76, 0.57, 0.34),
                secondary: CharmColor(0.47, 0.35, 0.21),
                deep: CharmColor(0.03, 0.02, 0.02),
                light: CharmColor(0.81, 0.62, 0.41)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .gusFring,
            sourceFileName: "Breaking Bad/Gus Fring.svg",
            mass: 3.43,
            radiusRatio: 0.1730,
            palette: CharmPalette(
                primary: CharmColor(0.54, 0.33, 0.21),
                secondary: CharmColor(0.33, 0.21, 0.13),
                deep: CharmColor(0.01, 0.02, 0.02),
                light: CharmColor(0.60, 0.47, 0.40)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .mikeEhrmantraut,
            sourceFileName: "Breaking Bad/Mike Ehrmantraut.svg",
            mass: 3.48,
            radiusRatio: 0.1734,
            palette: CharmPalette(
                primary: CharmColor(0.70, 0.47, 0.36),
                secondary: CharmColor(0.43, 0.29, 0.22),
                deep: CharmColor(0.01, 0.01, 0.01),
                light: CharmColor(0.76, 0.55, 0.44)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .heisenberg,
            sourceFileName: "Breaking Bad/Heisenberg.svg",
            mass: 3.53,
            radiusRatio: 0.1738,
            palette: CharmPalette(
                primary: CharmColor(0.47, 0.39, 0.32),
                secondary: CharmColor(0.29, 0.24, 0.20),
                deep: CharmColor(0.01, 0.01, 0.01),
                light: CharmColor(0.55, 0.47, 0.41)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .rv,
            sourceFileName: "Breaking Bad/RV.svg",
            mass: 3.58,
            radiusRatio: 0.1766,
            palette: CharmPalette(
                primary: CharmColor(0.81, 0.64, 0.40),
                secondary: CharmColor(0.50, 0.40, 0.25),
                deep: CharmColor(0.03, 0.02, 0.01),
                light: CharmColor(0.94, 0.82, 0.66)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Stranger Things.
        Entry(
            kind: .eleven,
            sourceFileName: "Stranger Things/Eleven.svg",
            mass: 2.24,
            radiusRatio: 0.1742,
            palette: CharmPalette(
                primary: CharmColor(0.69, 0.45, 0.34),
                secondary: CharmColor(0.43, 0.28, 0.21),
                deep: CharmColor(0.07, 0.07, 0.08),
                light: CharmColor(0.78, 0.65, 0.61)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .mikeWheeler,
            sourceFileName: "Stranger Things/Mike Wheeler.svg",
            mass: 2.28,
            radiusRatio: 0.1746,
            palette: CharmPalette(
                primary: CharmColor(0.61, 0.42, 0.32),
                secondary: CharmColor(0.38, 0.26, 0.20),
                deep: CharmColor(0.02, 0.02, 0.01),
                light: CharmColor(0.71, 0.57, 0.49)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .dustinHenderson,
            sourceFileName: "Stranger Things/Dustin Henderson.svg",
            mass: 2.33,
            radiusRatio: 0.1750,
            palette: CharmPalette(
                primary: CharmColor(0.65, 0.43, 0.31),
                secondary: CharmColor(0.40, 0.27, 0.19),
                deep: CharmColor(0.04, 0.02, 0.01),
                light: CharmColor(0.80, 0.69, 0.64)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .lucasSinclair,
            sourceFileName: "Stranger Things/Lucas Sinclair.svg",
            mass: 2.38,
            radiusRatio: 0.1754,
            palette: CharmPalette(
                primary: CharmColor(0.48, 0.26, 0.15),
                secondary: CharmColor(0.30, 0.16, 0.09),
                deep: CharmColor(0.04, 0.02, 0.01),
                light: CharmColor(0.66, 0.54, 0.45)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .willByers,
            sourceFileName: "Stranger Things/Will Byers.svg",
            mass: 2.44,
            radiusRatio: 0.1758,
            palette: CharmPalette(
                primary: CharmColor(0.65, 0.42, 0.23),
                secondary: CharmColor(0.40, 0.26, 0.15),
                deep: CharmColor(0.06, 0.03, 0.01),
                light: CharmColor(0.77, 0.61, 0.43)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .demogorgon,
            sourceFileName: "Stranger Things/Demogorgon.svg",
            mass: 2.48,
            radiusRatio: 0.1762,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.24, 0.19),
                secondary: CharmColor(0.36, 0.15, 0.12),
                deep: CharmColor(0.09, 0.02, 0.01),
                light: CharmColor(0.67, 0.52, 0.47)
            ),
            sound: .soft,
            beadCount: 0
        )
    ]
}
