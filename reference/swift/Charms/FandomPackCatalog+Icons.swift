//
//  FandomPackCatalog+Icons.swift
//  Hangly
//
//  Pokémon and Air Jordan.
//

import Foundation

/// Part of `FandomPackCatalog`, held in its own file for length alone.
extension CollectionCharmCatalog {
    static let iconEntries: [Entry] = [
        // Pokémon.
        Entry(
            kind: .pikachu,
            sourceFileName: "Pokemon/Pikachu.svg",
            mass: 3.99,
            radiusRatio: 0.1708,
            palette: CharmPalette(
                primary: CharmColor(0.96, 0.80, 0.01),
                secondary: CharmColor(0.60, 0.50, 0.01),
                deep: CharmColor(0.31, 0.13, 0.05),
                light: CharmColor(1.00, 0.95, 0.70)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .charmander,
            sourceFileName: "Pokemon/Charmander.svg",
            mass: 4.00,
            radiusRatio: 0.1709,
            palette: CharmPalette(
                primary: CharmColor(0.91, 0.49, 0.08),
                secondary: CharmColor(0.56, 0.30, 0.05),
                deep: CharmColor(0.37, 0.09, 0.03),
                light: CharmColor(0.99, 0.92, 0.76)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .squirtle,
            sourceFileName: "Pokemon/Squirtle.svg",
            mass: 4.01,
            radiusRatio: 0.1711,
            palette: CharmPalette(
                primary: CharmColor(0.19, 0.51, 0.66),
                secondary: CharmColor(0.12, 0.32, 0.41),
                deep: CharmColor(0.07, 0.08, 0.11),
                light: CharmColor(0.91, 0.95, 0.97)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .bulbasaur,
            sourceFileName: "Pokemon/Bulbasaur.svg",
            mass: 4.02,
            radiusRatio: 0.1712,
            palette: CharmPalette(
                primary: CharmColor(0.35, 0.48, 0.30),
                secondary: CharmColor(0.22, 0.30, 0.19),
                deep: CharmColor(0.05, 0.10, 0.04),
                light: CharmColor(0.87, 0.93, 0.88)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .charizard,
            sourceFileName: "Pokemon/Charizard.svg",
            mass: 4.03,
            radiusRatio: 0.1713,
            palette: CharmPalette(
                primary: CharmColor(0.79, 0.44, 0.15),
                secondary: CharmColor(0.49, 0.27, 0.09),
                deep: CharmColor(0.20, 0.08, 0.06),
                light: CharmColor(0.97, 0.85, 0.72)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .dragonair,
            sourceFileName: "Pokemon/Dragonair.svg",
            mass: 4.04,
            radiusRatio: 0.1715,
            palette: CharmPalette(
                primary: CharmColor(0.04, 0.56, 0.77),
                secondary: CharmColor(0.02, 0.35, 0.48),
                deep: CharmColor(0.06, 0.21, 0.35),
                light: CharmColor(0.90, 0.96, 0.97)
            ),
            sound: .glass,
            beadCount: 0
        ),
        Entry(
            kind: .mewtwo,
            sourceFileName: "Pokemon/Mewtwo.svg",
            mass: 4.06,
            radiusRatio: 0.1716,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.44, 0.67),
                secondary: CharmColor(0.35, 0.27, 0.42),
                deep: CharmColor(0.29, 0.18, 0.34),
                light: CharmColor(0.97, 0.92, 0.99)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .umbreon,
            sourceFileName: "Pokemon/Umbreon.svg",
            mass: 4.07,
            radiusRatio: 0.1717,
            palette: CharmPalette(
                primary: CharmColor(0.48, 0.47, 0.57),
                secondary: CharmColor(0.30, 0.29, 0.35),
                deep: CharmColor(0.01, 0.01, 0.02),
                light: CharmColor(0.84, 0.84, 0.87)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .ashAndPikachu,
            sourceFileName: "Pokemon/Ash and Pikachu.svg",
            mass: 4.08,
            radiusRatio: 0.1719,
            palette: CharmPalette(
                primary: CharmColor(0.83, 0.58, 0.20),
                secondary: CharmColor(0.51, 0.36, 0.12),
                deep: CharmColor(0.07, 0.04, 0.04),
                light: CharmColor(0.94, 0.90, 0.78)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .pokeBall,
            sourceFileName: "Pokemon/Poke Ball.svg",
            mass: 4.09,
            radiusRatio: 0.1720,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.22, 0.22),
                secondary: CharmColor(0.36, 0.14, 0.14),
                deep: CharmColor(0.07, 0.01, 0.01),
                light: CharmColor(0.92, 0.91, 0.91)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Air Jordan.
        Entry(
            kind: .airJordan1Chicago,
            sourceFileName: "Air Jordan/Air Jordan 1 Chicago.svg",
            mass: 3.90,
            radiusRatio: 0.1697,
            palette: CharmPalette(
                primary: CharmColor(0.63, 0.17, 0.10),
                secondary: CharmColor(0.39, 0.11, 0.06),
                deep: CharmColor(0.04, 0.01, 0.01),
                light: CharmColor(0.93, 0.90, 0.88)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .airJordan1Bred,
            sourceFileName: "Air Jordan/Air Jordan 1 Bred Patent.svg",
            mass: 3.92,
            radiusRatio: 0.1698,
            palette: CharmPalette(
                primary: CharmColor(0.72, 0.15, 0.11),
                secondary: CharmColor(0.45, 0.09, 0.07),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.89, 0.83, 0.82)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .airJordan4FireRed,
            sourceFileName: "Air Jordan/Air Jordan 4 Fire Red.svg",
            mass: 3.93,
            radiusRatio: 0.1699,
            palette: CharmPalette(
                primary: CharmColor(0.71, 0.22, 0.18),
                secondary: CharmColor(0.44, 0.14, 0.11),
                deep: CharmColor(0.04, 0.01, 0.01),
                light: CharmColor(0.92, 0.91, 0.91)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .airJordan11Concord,
            sourceFileName: "Air Jordan/Air Jordan 11 Concord.svg",
            mass: 3.94,
            radiusRatio: 0.1701,
            palette: CharmPalette(
                primary: CharmColor(0.39, 0.37, 0.40),
                secondary: CharmColor(0.24, 0.23, 0.25),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.92, 0.90, 0.91)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .airJordan3,
            sourceFileName: "Air Jordan/Air Jordan 3.svg",
            mass: 3.95,
            radiusRatio: 0.1703,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.34, 0.26),
                secondary: CharmColor(0.36, 0.21, 0.16),
                deep: CharmColor(0.04, 0.02, 0.02),
                light: CharmColor(0.93, 0.90, 0.89)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .airJordan6Carmine,
            sourceFileName: "Air Jordan/Air Jordan 6 Carmine.svg",
            mass: 3.96,
            radiusRatio: 0.1704,
            palette: CharmPalette(
                primary: CharmColor(0.61, 0.25, 0.19),
                secondary: CharmColor(0.38, 0.15, 0.12),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.92, 0.90, 0.90)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .airJordan5,
            sourceFileName: "Air Jordan/Air Jordan 5.svg",
            mass: 3.97,
            radiusRatio: 0.1705,
            palette: CharmPalette(
                primary: CharmColor(0.63, 0.29, 0.22),
                secondary: CharmColor(0.39, 0.18, 0.14),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.83, 0.76, 0.72)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .offWhiteJordan1,
            sourceFileName: "Air Jordan/Off-White Air Jordan 1.svg",
            mass: 3.98,
            radiusRatio: 0.1707,
            palette: CharmPalette(
                primary: CharmColor(0.67, 0.25, 0.17),
                secondary: CharmColor(0.42, 0.15, 0.11),
                deep: CharmColor(0.03, 0.01, 0.01),
                light: CharmColor(0.94, 0.87, 0.82)
            ),
            sound: .soft,
            beadCount: 0
        )
    ]
}
