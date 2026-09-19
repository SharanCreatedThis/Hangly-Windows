//
//  StoryPackCatalog.swift
//  Hangly
//
//  The five story collections' place in the charm catalogue.
//

import Foundation

/// Football Legends, Music Legends, Friends, Breaking Bad and Stranger Things,
/// described exactly as the earlier collections are.
///
/// They arrived together, in five folders under `Assets/Charms`, so like the four
/// before them `sourceFileName` carries its subdirectory and `SyncCharmAssets`
/// appends the whole path.
///
/// **None of them has beads.** Every one is drawn as a single figure or crest with
/// no cord above it, so they declare `beadCount: 0` and hang whole — the same case
/// as Marvel, DC and BTS.
///
/// Palettes are sampled from the artwork rather than chosen, by the rule the four
/// before them used: `primary` is the mean of the most saturated tenth of the
/// opaque pixels, `deep` and `light` the means of the darkest and lightest
/// twelfths, and `secondary` is `primary` at 62 per cent. The ambient glow and the
/// weather tinting read these, so a palette that disagrees with the picture shows
/// up on screen as a halo in the wrong colour.
extension CollectionCharmCatalog {
    /// The five, in the order the Library offers them. Split across two files so
    /// that neither runs past the length the linter allows.
    static let storyPackEntries: [Entry] = legendEntries + screenEntries

    /// Football Legends and Music Legends: the two the collections opened with.
    static let legendEntries: [Entry] = [
        // Football Legends.
        Entry(
            kind: .ronaldoJersey,
            sourceFileName: "Football/Ronaldo 7.svg",
            mass: 3.02,
            radiusRatio: 0.1652,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.06, 0.09),
                secondary: CharmColor(0.36, 0.03, 0.06),
                deep: CharmColor(0.42, 0.01, 0.03),
                light: CharmColor(0.93, 0.83, 0.64)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .messiJersey,
            sourceFileName: "Football/Messi 10.svg",
            mass: 3.08,
            radiusRatio: 0.1656,
            palette: CharmPalette(
                primary: CharmColor(0.35, 0.58, 0.72),
                secondary: CharmColor(0.22, 0.36, 0.44),
                deep: CharmColor(0.11, 0.14, 0.22),
                light: CharmColor(0.90, 0.90, 0.90)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .neymarJersey,
            sourceFileName: "Football/Neymar 10.svg",
            mass: 3.13,
            radiusRatio: 0.1660,
            palette: CharmPalette(
                primary: CharmColor(0.87, 0.71, 0.02),
                secondary: CharmColor(0.54, 0.44, 0.01),
                deep: CharmColor(0.14, 0.38, 0.15),
                light: CharmColor(0.93, 0.83, 0.40)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .realMadridCrest,
            sourceFileName: "Football/Real Madrid.svg",
            mass: 3.18,
            radiusRatio: 0.1664,
            palette: CharmPalette(
                primary: CharmColor(0.71, 0.53, 0.31),
                secondary: CharmColor(0.44, 0.33, 0.19),
                deep: CharmColor(0.11, 0.09, 0.32),
                light: CharmColor(0.92, 0.91, 0.89)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .fcBarcelonaCrest,
            sourceFileName: "Football/FC Barcelona.svg",
            mass: 3.23,
            radiusRatio: 0.1668,
            palette: CharmPalette(
                primary: CharmColor(0.46, 0.15, 0.29),
                secondary: CharmColor(0.28, 0.09, 0.18),
                deep: CharmColor(0.30, 0.04, 0.19),
                light: CharmColor(0.91, 0.86, 0.76)
            ),
            sound: .metal,
            beadCount: 0
        ),
        // Music Legends.
        Entry(
            kind: .billieEilish,
            sourceFileName: "Music/Billie Eilish.svg",
            mass: 2.62,
            radiusRatio: 0.1672,
            palette: CharmPalette(
                primary: CharmColor(0.64, 0.62, 0.37),
                secondary: CharmColor(0.40, 0.38, 0.23),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.73, 0.71, 0.49)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .xxxtentacion,
            sourceFileName: "Music/XXXTentacion.svg",
            mass: 2.67,
            radiusRatio: 0.1676,
            palette: CharmPalette(
                primary: CharmColor(0.66, 0.49, 0.34),
                secondary: CharmColor(0.41, 0.30, 0.21),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.73, 0.57, 0.42)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .michaelJackson,
            sourceFileName: "Music/Michael Jackson.svg",
            mass: 2.72,
            radiusRatio: 0.1680,
            palette: CharmPalette(
                primary: CharmColor(0.55, 0.30, 0.24),
                secondary: CharmColor(0.34, 0.18, 0.15),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.76, 0.72, 0.68)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .taylorSwift,
            sourceFileName: "Music/Taylor Swift.svg",
            mass: 2.78,
            radiusRatio: 0.1684,
            palette: CharmPalette(
                primary: CharmColor(0.71, 0.52, 0.39),
                secondary: CharmColor(0.44, 0.32, 0.24),
                deep: CharmColor(0.06, 0.04, 0.03),
                light: CharmColor(0.82, 0.73, 0.67)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .juiceWrld,
            sourceFileName: "Music/Juice WRLD.svg",
            mass: 2.84,
            radiusRatio: 0.1688,
            palette: CharmPalette(
                primary: CharmColor(0.51, 0.34, 0.24),
                secondary: CharmColor(0.32, 0.21, 0.15),
                deep: CharmColor(0.01, 0.00, 0.00),
                light: CharmColor(0.68, 0.64, 0.60)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .theWeeknd,
            sourceFileName: "Music/The Weeknd.svg",
            mass: 2.89,
            radiusRatio: 0.1694,
            palette: CharmPalette(
                primary: CharmColor(0.61, 0.26, 0.24),
                secondary: CharmColor(0.38, 0.16, 0.15),
                deep: CharmColor(0.00, 0.00, 0.00),
                light: CharmColor(0.64, 0.40, 0.31)
            ),
            sound: .soft,
            beadCount: 0
        )
    ]
}
