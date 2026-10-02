//
//  FandomPackCatalog+Anime.swift
//  Hangly
//
//  One Piece, Attack on Titan and Naruto.
//

import Foundation

/// Part of `FandomPackCatalog`, held in its own file for length alone.
extension CollectionCharmCatalog {
    static let animeEntries: [Entry] = [
        // One Piece.
        Entry(
            kind: .luffy,
            sourceFileName: "One Piece/Monkey D. Luffy.svg",
            mass: 2.76,
            radiusRatio: 0.1607,
            palette: CharmPalette(
                primary: CharmColor(0.66, 0.39, 0.10),
                secondary: CharmColor(0.41, 0.24, 0.06),
                deep: CharmColor(0.03, 0.01, 0.01),
                light: CharmColor(0.94, 0.80, 0.69)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .zoro,
            sourceFileName: "One Piece/Roronoa Zoro.svg",
            mass: 3.30,
            radiusRatio: 0.1608,
            palette: CharmPalette(
                primary: CharmColor(0.72, 0.48, 0.30),
                secondary: CharmColor(0.45, 0.30, 0.19),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.85, 0.78, 0.67)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .nami,
            sourceFileName: "One Piece/Nami.svg",
            mass: 2.79,
            radiusRatio: 0.1611,
            palette: CharmPalette(
                primary: CharmColor(0.88, 0.41, 0.06),
                secondary: CharmColor(0.55, 0.25, 0.04),
                deep: CharmColor(0.10, 0.03, 0.05),
                light: CharmColor(0.95, 0.82, 0.69)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .usopp,
            sourceFileName: "One Piece/Usopp.svg",
            mass: 2.86,
            radiusRatio: 0.1612,
            palette: CharmPalette(
                primary: CharmColor(0.72, 0.48, 0.15),
                secondary: CharmColor(0.45, 0.30, 0.09),
                deep: CharmColor(0.04, 0.01, 0.00),
                light: CharmColor(0.91, 0.83, 0.75)
            ),
            sound: .wood,
            beadCount: 0
        ),
        Entry(
            kind: .sanji,
            sourceFileName: "One Piece/Sanji.svg",
            mass: 2.73,
            radiusRatio: 0.1613,
            palette: CharmPalette(
                primary: CharmColor(0.97, 0.41, 0.01),
                secondary: CharmColor(0.60, 0.25, 0.01),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.96, 0.78, 0.50)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .chopper,
            sourceFileName: "One Piece/Tony Tony Chopper.svg",
            mass: 2.81,
            radiusRatio: 0.1614,
            palette: CharmPalette(
                primary: CharmColor(0.34, 0.48, 0.44),
                secondary: CharmColor(0.21, 0.30, 0.27),
                deep: CharmColor(0.07, 0.04, 0.04),
                light: CharmColor(0.89, 0.84, 0.80)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .robin,
            sourceFileName: "One Piece/Nico Robin.svg",
            mass: 2.82,
            radiusRatio: 0.1615,
            palette: CharmPalette(
                primary: CharmColor(0.76, 0.30, 0.23),
                secondary: CharmColor(0.47, 0.19, 0.14),
                deep: CharmColor(0.02, 0.01, 0.01),
                light: CharmColor(0.95, 0.75, 0.66)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .ace,
            sourceFileName: "One Piece/Portgas D. Ace.svg",
            mass: 2.88,
            radiusRatio: 0.1616,
            palette: CharmPalette(
                primary: CharmColor(0.89, 0.33, 0.02),
                secondary: CharmColor(0.55, 0.20, 0.01),
                deep: CharmColor(0.05, 0.01, 0.00),
                light: CharmColor(0.95, 0.75, 0.60)
            ),
            sound: .soft,
            beadCount: 0
        ),
        // Attack on Titan.
        Entry(
            kind: .erenYeager,
            sourceFileName: "Attack on Titan/Eren Yeager.svg",
            mass: 3.42,
            radiusRatio: 0.1642,
            palette: CharmPalette(
                primary: CharmColor(0.66, 0.45, 0.29),
                secondary: CharmColor(0.41, 0.28, 0.18),
                deep: CharmColor(0.02, 0.01, 0.00),
                light: CharmColor(0.80, 0.71, 0.63)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .mikasaAckerman,
            sourceFileName: "Attack on Titan/Mikasa Ackerman.svg",
            mass: 3.44,
            radiusRatio: 0.1643,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.33, 0.20),
                secondary: CharmColor(0.36, 0.20, 0.12),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.79, 0.70, 0.62)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .leviAckerman,
            sourceFileName: "Attack on Titan/Levi Ackerman.svg",
            mass: 3.47,
            radiusRatio: 0.1644,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.40, 0.25),
                secondary: CharmColor(0.35, 0.25, 0.15),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.76, 0.69, 0.61)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .arminArlert,
            sourceFileName: "Attack on Titan/Armin Arlert.svg",
            mass: 3.49,
            radiusRatio: 0.1646,
            palette: CharmPalette(
                primary: CharmColor(0.65, 0.44, 0.21),
                secondary: CharmColor(0.40, 0.27, 0.13),
                deep: CharmColor(0.03, 0.01, 0.00),
                light: CharmColor(0.83, 0.72, 0.60)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .colossalTitan,
            sourceFileName: "Attack on Titan/Colossal Titan.svg",
            mass: 3.50,
            radiusRatio: 0.1647,
            palette: CharmPalette(
                primary: CharmColor(0.64, 0.20, 0.08),
                secondary: CharmColor(0.40, 0.12, 0.05),
                deep: CharmColor(0.13, 0.01, 0.00),
                light: CharmColor(0.86, 0.78, 0.73)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .attackTitan,
            sourceFileName: "Attack on Titan/Attack Titan.svg",
            mass: 3.51,
            radiusRatio: 0.1648,
            palette: CharmPalette(
                primary: CharmColor(0.73, 0.51, 0.36),
                secondary: CharmColor(0.45, 0.32, 0.22),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.87, 0.73, 0.64)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .surveyCorpsEmblem,
            sourceFileName: "Attack on Titan/Survey Corps Emblem.svg",
            mass: 3.52,
            radiusRatio: 0.1649,
            palette: CharmPalette(
                primary: CharmColor(0.25, 0.27, 0.30),
                secondary: CharmColor(0.15, 0.17, 0.19),
                deep: CharmColor(0.02, 0.02, 0.06),
                light: CharmColor(0.81, 0.77, 0.73)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .cadetCorpsEmblem,
            sourceFileName: "Attack on Titan/Cadet Corps Emblem.svg",
            mass: 3.54,
            radiusRatio: 0.1650,
            palette: CharmPalette(
                primary: CharmColor(0.57, 0.47, 0.35),
                secondary: CharmColor(0.35, 0.29, 0.22),
                deep: CharmColor(0.04, 0.03, 0.01),
                light: CharmColor(0.84, 0.78, 0.71)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Naruto.
        Entry(
            kind: .narutoUzumaki,
            sourceFileName: "Naruto/Naruto Uzumaki.svg",
            mass: 3.55,
            radiusRatio: 0.1651,
            palette: CharmPalette(
                primary: CharmColor(0.87, 0.59, 0.02),
                secondary: CharmColor(0.54, 0.37, 0.01),
                deep: CharmColor(0.02, 0.01, 0.01),
                light: CharmColor(0.93, 0.79, 0.54)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .sasukeUchiha,
            sourceFileName: "Naruto/Sasuke Uchiha.svg",
            mass: 3.56,
            radiusRatio: 0.1653,
            palette: CharmPalette(
                primary: CharmColor(0.59, 0.48, 0.43),
                secondary: CharmColor(0.37, 0.30, 0.27),
                deep: CharmColor(0.00, 0.00, 0.01),
                light: CharmColor(0.82, 0.74, 0.70)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .kakashiHatake,
            sourceFileName: "Naruto/Kakashi Hatake.svg",
            mass: 3.57,
            radiusRatio: 0.1654,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.38, 0.22),
                secondary: CharmColor(0.36, 0.24, 0.14),
                deep: CharmColor(0.01, 0.01, 0.00),
                light: CharmColor(0.83, 0.79, 0.76)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .itachiUchiha,
            sourceFileName: "Naruto/Itachi Uchiha.svg",
            mass: 3.59,
            radiusRatio: 0.1655,
            palette: CharmPalette(
                primary: CharmColor(0.56, 0.25, 0.19),
                secondary: CharmColor(0.35, 0.15, 0.12),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.74, 0.64, 0.58)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .minatoNamikaze,
            sourceFileName: "Naruto/Minato Namikaze.svg",
            mass: 3.60,
            radiusRatio: 0.1657,
            palette: CharmPalette(
                primary: CharmColor(0.87, 0.63, 0.05),
                secondary: CharmColor(0.54, 0.39, 0.03),
                deep: CharmColor(0.01, 0.01, 0.02),
                light: CharmColor(0.89, 0.83, 0.72)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .kurama,
            sourceFileName: "Naruto/Kurama.svg",
            mass: 3.62,
            radiusRatio: 0.1658,
            palette: CharmPalette(
                primary: CharmColor(0.82, 0.35, 0.01),
                secondary: CharmColor(0.51, 0.22, 0.01),
                deep: CharmColor(0.14, 0.01, 0.00),
                light: CharmColor(0.87, 0.60, 0.43)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .akatsukiCloud,
            sourceFileName: "Naruto/Akatsuki Cloud.svg",
            mass: 3.63,
            radiusRatio: 0.1659,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.08, 0.03),
                secondary: CharmColor(0.36, 0.05, 0.02),
                deep: CharmColor(0.27, 0.03, 0.01),
                light: CharmColor(0.85, 0.80, 0.75)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .sharingan,
            sourceFileName: "Naruto/Mangekyo Sharingan.svg",
            mass: 3.64,
            radiusRatio: 0.1661,
            palette: CharmPalette(
                primary: CharmColor(0.72, 0.10, 0.00),
                secondary: CharmColor(0.45, 0.06, 0.00),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.73, 0.64, 0.60)
            ),
            sound: .metal,
            beadCount: 0
        )
    ]
}
