//
//  FandomPackCatalog+Sagas.swift
//  Hangly
//
//  Harry Potter, Game of Thrones and Ben 10.
//

import Foundation

/// Part of `FandomPackCatalog`, held in its own file for length alone.
extension CollectionCharmCatalog {
    static let sagaEntries: [Entry] = [
        // Harry Potter.
        Entry(
            kind: .harryPotter,
            sourceFileName: "Harry Potter/Harry Potter.svg",
            mass: 2.93,
            radiusRatio: 0.1618,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.30, 0.28),
                secondary: CharmColor(0.35, 0.19, 0.17),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.75, 0.65, 0.62)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .hermioneGranger,
            sourceFileName: "Harry Potter/Hermione Granger.svg",
            mass: 2.94,
            radiusRatio: 0.1619,
            palette: CharmPalette(
                primary: CharmColor(0.60, 0.40, 0.31),
                secondary: CharmColor(0.37, 0.25, 0.19),
                deep: CharmColor(0.03, 0.01, 0.01),
                light: CharmColor(0.84, 0.72, 0.65)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .ronWeasley,
            sourceFileName: "Harry Potter/Ron Weasley.svg",
            mass: 2.95,
            radiusRatio: 0.1621,
            palette: CharmPalette(
                primary: CharmColor(0.69, 0.45, 0.33),
                secondary: CharmColor(0.43, 0.28, 0.20),
                deep: CharmColor(0.04, 0.01, 0.01),
                light: CharmColor(0.84, 0.69, 0.61)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .albusDumbledore,
            sourceFileName: "Harry Potter/Albus Dumbledore.svg",
            mass: 2.99,
            radiusRatio: 0.1622,
            palette: CharmPalette(
                primary: CharmColor(0.59, 0.46, 0.36),
                secondary: CharmColor(0.37, 0.29, 0.22),
                deep: CharmColor(0.08, 0.05, 0.03),
                light: CharmColor(0.90, 0.86, 0.82)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .lordVoldemort,
            sourceFileName: "Harry Potter/Lord Voldemort.svg",
            mass: 3.00,
            radiusRatio: 0.1623,
            palette: CharmPalette(
                primary: CharmColor(0.33, 0.31, 0.35),
                secondary: CharmColor(0.20, 0.19, 0.22),
                deep: CharmColor(0.00, 0.00, 0.01),
                light: CharmColor(0.78, 0.75, 0.77)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .hedwig,
            sourceFileName: "Harry Potter/Hedwig.svg",
            mass: 3.04,
            radiusRatio: 0.1624,
            palette: CharmPalette(
                primary: CharmColor(0.46, 0.42, 0.38),
                secondary: CharmColor(0.29, 0.26, 0.24),
                deep: CharmColor(0.21, 0.19, 0.19),
                light: CharmColor(0.97, 0.98, 0.98)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .goldenSnitch,
            sourceFileName: "Harry Potter/Golden Snitch.svg",
            mass: 3.32,
            radiusRatio: 0.1626,
            palette: CharmPalette(
                primary: CharmColor(0.68, 0.50, 0.23),
                secondary: CharmColor(0.42, 0.31, 0.14),
                deep: CharmColor(0.29, 0.16, 0.02),
                light: CharmColor(1.00, 0.98, 0.89)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .deathlyHallows,
            sourceFileName: "Harry Potter/Deathly Hallows.svg",
            mass: 3.34,
            radiusRatio: 0.1627,
            palette: CharmPalette(
                primary: CharmColor(0.40, 0.51, 0.59),
                secondary: CharmColor(0.25, 0.32, 0.37),
                deep: CharmColor(0.02, 0.03, 0.07),
                light: CharmColor(0.95, 0.97, 0.97)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Game of Thrones.
        Entry(
            kind: .jonSnow,
            sourceFileName: "Game of Thrones/Jon Snow.svg",
            mass: 3.68,
            radiusRatio: 0.1665,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.43, 0.34),
                secondary: CharmColor(0.35, 0.27, 0.21),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.81, 0.73, 0.68)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .daenerysTargaryen,
            sourceFileName: "Game of Thrones/Daenerys Targaryen.svg",
            mass: 3.69,
            radiusRatio: 0.1666,
            palette: CharmPalette(
                primary: CharmColor(0.67, 0.24, 0.11),
                secondary: CharmColor(0.42, 0.15, 0.07),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.90, 0.79, 0.70)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .tyrionLannister,
            sourceFileName: "Game of Thrones/Tyrion Lannister.svg",
            mass: 3.70,
            radiusRatio: 0.1667,
            palette: CharmPalette(
                primary: CharmColor(0.69, 0.45, 0.28),
                secondary: CharmColor(0.43, 0.28, 0.17),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.84, 0.68, 0.56)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .nightKing,
            sourceFileName: "Game of Thrones/Night King.svg",
            mass: 3.72,
            radiusRatio: 0.1669,
            palette: CharmPalette(
                primary: CharmColor(0.18, 0.42, 0.55),
                secondary: CharmColor(0.11, 0.26, 0.34),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.75, 0.79, 0.81)
            ),
            sound: .glass,
            beadCount: 0
        ),
        Entry(
            kind: .houseStark,
            sourceFileName: "Game of Thrones/House Stark.svg",
            mass: 3.73,
            radiusRatio: 0.1671,
            palette: CharmPalette(
                primary: CharmColor(0.55, 0.45, 0.36),
                secondary: CharmColor(0.34, 0.28, 0.22),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.90, 0.87, 0.84)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .houseTargaryen,
            sourceFileName: "Game of Thrones/House Targaryen.svg",
            mass: 3.74,
            radiusRatio: 0.1673,
            palette: CharmPalette(
                primary: CharmColor(0.71, 0.13, 0.04),
                secondary: CharmColor(0.44, 0.08, 0.02),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.86, 0.72, 0.67)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .houseLannister,
            sourceFileName: "Game of Thrones/House Lannister.svg",
            mass: 3.75,
            radiusRatio: 0.1674,
            palette: CharmPalette(
                primary: CharmColor(0.67, 0.43, 0.15),
                secondary: CharmColor(0.42, 0.27, 0.09),
                deep: CharmColor(0.06, 0.00, 0.00),
                light: CharmColor(0.97, 0.89, 0.78)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .ironThrone,
            sourceFileName: "Game of Thrones/Iron Throne.svg",
            mass: 3.76,
            radiusRatio: 0.1675,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.44, 0.32),
                secondary: CharmColor(0.36, 0.27, 0.20),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.86, 0.81, 0.74)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Ben 10.
        Entry(
            kind: .benTennyson,
            sourceFileName: "Ben 10/Ben Tennyson.svg",
            mass: 3.07,
            radiusRatio: 0.1628,
            palette: CharmPalette(
                primary: CharmColor(0.76, 0.58, 0.27),
                secondary: CharmColor(0.47, 0.36, 0.17),
                deep: CharmColor(0.05, 0.03, 0.01),
                light: CharmColor(0.95, 0.86, 0.73)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .fourArms,
            sourceFileName: "Ben 10/Four Arms.svg",
            mass: 3.10,
            radiusRatio: 0.1629,
            palette: CharmPalette(
                primary: CharmColor(0.78, 0.29, 0.10),
                secondary: CharmColor(0.48, 0.18, 0.06),
                deep: CharmColor(0.02, 0.01, 0.01),
                light: CharmColor(0.90, 0.85, 0.78)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .heatblast,
            sourceFileName: "Ben 10/Heatblast.svg",
            mass: 3.12,
            radiusRatio: 0.1631,
            palette: CharmPalette(
                primary: CharmColor(1.00, 0.64, 0.00),
                secondary: CharmColor(0.62, 0.40, 0.00),
                deep: CharmColor(0.25, 0.03, 0.01),
                light: CharmColor(0.99, 0.96, 0.64)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .xlr8,
            sourceFileName: "Ben 10/XLR8.svg",
            mass: 3.17,
            radiusRatio: 0.1632,
            palette: CharmPalette(
                primary: CharmColor(0.06, 0.62, 0.78),
                secondary: CharmColor(0.04, 0.38, 0.48),
                deep: CharmColor(0.01, 0.00, 0.01),
                light: CharmColor(0.80, 0.88, 0.85)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .diamondhead,
            sourceFileName: "Ben 10/Diamondhead.svg",
            mass: 3.19,
            radiusRatio: 0.1634,
            palette: CharmPalette(
                primary: CharmColor(0.04, 0.77, 0.65),
                secondary: CharmColor(0.02, 0.48, 0.40),
                deep: CharmColor(0.02, 0.02, 0.02),
                light: CharmColor(0.85, 0.95, 0.83)
            ),
            sound: .glass,
            beadCount: 0
        ),
        Entry(
            kind: .cannonbolt,
            sourceFileName: "Ben 10/Cannonbolt.svg",
            mass: 3.20,
            radiusRatio: 0.1636,
            palette: CharmPalette(
                primary: CharmColor(0.92, 0.70, 0.02),
                secondary: CharmColor(0.57, 0.43, 0.01),
                deep: CharmColor(0.03, 0.01, 0.01),
                light: CharmColor(0.98, 0.92, 0.78)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .upgrade,
            sourceFileName: "Ben 10/Upgrade.svg",
            mass: 3.35,
            radiusRatio: 0.1637,
            palette: CharmPalette(
                primary: CharmColor(0.46, 0.70, 0.09),
                secondary: CharmColor(0.29, 0.43, 0.06),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.83, 0.85, 0.58)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .wildmutt,
            sourceFileName: "Ben 10/Wildmutt.svg",
            mass: 3.37,
            radiusRatio: 0.1638,
            palette: CharmPalette(
                primary: CharmColor(0.86, 0.48, 0.07),
                secondary: CharmColor(0.53, 0.30, 0.04),
                deep: CharmColor(0.06, 0.03, 0.02),
                light: CharmColor(0.97, 0.82, 0.60)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .omnitrix,
            sourceFileName: "Ben 10/Omnitrix.svg",
            mass: 3.39,
            radiusRatio: 0.1639,
            palette: CharmPalette(
                primary: CharmColor(0.65, 0.84, 0.00),
                secondary: CharmColor(0.40, 0.52, 0.00),
                deep: CharmColor(0.02, 0.02, 0.01),
                light: CharmColor(0.83, 0.87, 0.55)
            ),
            sound: .metal,
            beadCount: 0
        )
    ]
}
