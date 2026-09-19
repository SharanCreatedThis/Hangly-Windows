//
//  CollectionCharmCatalog.swift
//  Hangly
//
//  The Hangly collection: identity, physics and sound per charm, with the artwork
//  supplied as SVG.
//

import Foundation

/// A built-in charm drawn from an SVG asset.
///
/// Physics, palette and sound are data from the catalog; the visual is a vector
/// image rasterised on demand. The palette still tints the ambient glow, while the
/// cord takes the collection's gold so it continues the cord drawn in the artwork.
struct SVGCharm: BuiltInCharm {
    let kind: CharmKind
    let mass: Double
    let radiusRatio: Double
    let palette: CharmPalette
    let sound: CharmSound

    /// How the artwork divides into beads and charm; see `CharmArtworkSplitter`.
    let beadCount: Int
    let bodyRun: Int

    /// `nil` when the asset is missing; the charm then draws a placeholder bead so
    /// the rope is never bare, and the omission is reported at launch.
    let vector: VectorImage?

    /// Measured once per asset and cached by `VectorImage`, so the repeated reads
    /// below cost a dictionary lookup rather than a rasterisation.
    private var regions: CharmArtworkRegions? {
        vector?.regions(beadCount: beadCount, bodyRun: bodyRun)
    }

    /// The knot sits where the artwork's own loop begins, which is the top of the
    /// charm's body — measured from the asset rather than guessed at.
    var metrics: CharmMetrics {
        CharmMetrics(
            mass: mass,
            radiusRatio: radiusRatio,
            knotInset: regions?.knotInset ?? CollectionCharmCatalog.fallbackKnotInset
        )
    }

    /// The beads the artwork draws above the charm, described in proportions of the
    /// charm's radius so they survive a rescale.
    ///
    /// Their places and sizes come straight from the artwork, so a rope at rest is
    /// laid out exactly as the designer drew it; the simulation only lets them slide
    /// from there.
    var beads: [CharmBead] {
        guard let regions else { return [] }
        let longest = max(regions.body.width, regions.body.height)
        guard longest > 0 else { return [] }
        // Unit-square distances become multiples of the charm's radius.
        let scale = 2 / longest

        return regions.beads.map { rect in
            let size = CGSize(width: rect.width * scale, height: rect.height * scale)
            // The mean of the two sides, so a long bead is not weighed as a sphere
            // of its length.
            let radius = (size.width * size.height).squareRoot() / 2
            return CharmBead(
                size: size,
                offset: (regions.body.minY - rect.midY) * scale,
                // Weight for a solid bead of this size beside the charm's own,
                // floored so the lightest still registers on the rope.
                mass: max(
                    CollectionCharmCatalog.minimumBeadMass,
                    mass * pow(radius, 3) * CollectionCharmCatalog.beadDensity
                )
            )
        }
    }

    func artwork() -> CharmArtwork {
        guard let vector else { return CircleCharm().artwork() }
        return CharmArtwork(vector: vector)
    }

    func hangingArtwork() -> CharmArtwork {
        guard let vector else { return CircleCharm().artwork() }
        guard let regions else { return CharmArtwork(vector: vector) }
        return CharmArtwork(vector: vector, body: regions.body, beads: regions.beads)
    }
}

/// The twelve collection charms as data.
enum CollectionCharmCatalog {
    struct Entry: Sendable {
        let kind: CharmKind
        /// The designer's file name in `Assets/Charms`.
        let sourceFileName: String
        let mass: Double
        let radiusRatio: Double
        let palette: CharmPalette
        let sound: CharmSound

        /// How many solid parts of the artwork, from the top, are beads on the cord.
        let beadCount: Int

        /// Which solid part the charm itself starts at. Equal to `beadCount` unless
        /// the artwork's own cord is thick enough to read as a part of its own, in
        /// which case the parts in between are dropped: the simulated cord replaces
        /// them.
        let bodyRun: Int

        init(
            kind: CharmKind,
            sourceFileName: String,
            mass: Double,
            radiusRatio: Double,
            palette: CharmPalette,
            sound: CharmSound,
            beadCount: Int,
            bodyRun: Int? = nil
        ) {
            self.kind = kind
            self.sourceFileName = sourceFileName
            self.mass = mass
            self.radiusRatio = radiusRatio
            self.palette = palette
            self.sound = sound
            self.beadCount = beadCount
            self.bodyRun = bodyRun ?? beadCount
        }
    }

    /// Used only when an asset cannot be measured, which means it is missing: the
    /// knot then sits on the placeholder bead's bounding circle.
    static let fallbackKnotInset = 0.96

    /// How much heavier a bead is than the charm for its size. Beads are solid
    /// glass or metal; a charm is mostly hollow.
    static let beadDensity = 6.0

    /// Floor on a bead's weight, so the smallest still pulls on the cord.
    static let minimumBeadMass = 0.05

    /// The hand-drawn collection, the seasonal packs, the four collections, the
    /// five story collections, then the classics — the order the charm menu offers
    /// them in.
    static let entries: [Entry] =
        collectionEntries + seasonalEntries + collectionPackEntries + storyPackEntries + classicEntries

    private static let collectionEntries: [Entry] = [
        Entry(
            kind: .nazar,
            sourceFileName: "Nazar Boncuğu.svg",
            mass: 2.75,
            radiusRatio: 0.145,
            palette: CharmPalette(
                primary: CharmColor(0.10, 0.22, 0.68),
                secondary: CharmColor(0.06, 0.12, 0.42),
                deep: CharmColor(0.03, 0.06, 0.25),
                light: CharmColor(0.42, 0.58, 0.95)
            ),
            sound: .glass,
            beadCount: 1
        ),
        Entry(
            kind: .hamsa,
            sourceFileName: "Hamsa.svg",
            mass: 3.05,
            radiusRatio: 0.158,
            palette: CharmPalette(
                primary: CharmColor(0.15, 0.24, 0.60),
                secondary: CharmColor(0.09, 0.14, 0.42),
                deep: CharmColor(0.05, 0.08, 0.26),
                light: CharmColor(0.50, 0.62, 0.95)
            ),
            sound: .metal,
            beadCount: 2
        ),
        Entry(
            kind: .nimbuMirchi,
            sourceFileName: "Nimbu-mirchi.svg",
            mass: 2.85,
            radiusRatio: 0.152,
            palette: CharmPalette(
                primary: CharmColor(0.98, 0.84, 0.18),
                secondary: CharmColor(0.85, 0.62, 0.08),
                deep: CharmColor(0.55, 0.38, 0.03),
                light: CharmColor(1.00, 0.96, 0.62)
            ),
            sound: .soft,
            beadCount: 0
        ),
        Entry(
            kind: .ghanta,
            sourceFileName: "Ghanta.svg",
            mass: 4.05,
            radiusRatio: 0.15,
            palette: CharmPalette(
                primary: CharmColor(0.76, 0.45, 0.22),
                secondary: CharmColor(0.55, 0.30, 0.13),
                deep: CharmColor(0.32, 0.17, 0.07),
                light: CharmColor(0.98, 0.78, 0.55)
            ),
            sound: .bell,
            beadCount: 1
        ),
        Entry(
            kind: .drishtiBommai,
            sourceFileName: "Dhrishti bomma.svg",
            mass: 3.25,
            radiusRatio: 0.164,
            palette: CharmPalette(
                primary: CharmColor(0.86, 0.14, 0.12),
                secondary: CharmColor(0.62, 0.08, 0.08),
                deep: CharmColor(0.36, 0.04, 0.05),
                light: CharmColor(1.00, 0.55, 0.45)
            ),
            sound: .wood,
            beadCount: 1
        ),
        Entry(
            kind: .panchangJie,
            sourceFileName: "Pánchang Jié.svg",
            mass: 2.45,
            radiusRatio: 0.16,
            palette: CharmPalette(
                primary: CharmColor(0.88, 0.14, 0.18),
                secondary: CharmColor(0.62, 0.08, 0.10),
                deep: CharmColor(0.36, 0.04, 0.06),
                light: CharmColor(1.00, 0.62, 0.60)
            ),
            sound: .soft,
            beadCount: 1
        ),
        Entry(
            kind: .daruma,
            sourceFileName: "Daruma.svg",
            mass: 3.65,
            radiusRatio: 0.154,
            palette: CharmPalette(
                primary: CharmColor(0.88, 0.12, 0.12),
                secondary: CharmColor(0.62, 0.06, 0.06),
                deep: CharmColor(0.38, 0.03, 0.03),
                light: CharmColor(1.00, 0.50, 0.40)
            ),
            sound: .wood,
            beadCount: 3
        ),
        Entry(
            kind: .manekiNeko,
            sourceFileName: "Maneki-neko.svg",
            mass: 3.45,
            radiusRatio: 0.167,
            palette: CharmPalette(
                primary: CharmColor(0.97, 0.95, 0.90),
                secondary: CharmColor(0.82, 0.78, 0.70),
                deep: CharmColor(0.55, 0.50, 0.42),
                light: CharmColor(1.00, 1.00, 1.00)
            ),
            sound: .wood,
            beadCount: 4
        ),
        Entry(
            kind: .horseshoe,
            sourceFileName: "Horseshoe.svg",
            mass: 3.85,
            radiusRatio: 0.151,
            palette: CharmPalette(
                primary: CharmColor(0.58, 0.60, 0.64),
                secondary: CharmColor(0.38, 0.40, 0.44),
                deep: CharmColor(0.20, 0.21, 0.24),
                light: CharmColor(0.88, 0.90, 0.93)
            ),
            sound: .metal,
            beadCount: 0
        ),
        Entry(
            kind: .scarab,
            sourceFileName: "Scarab.svg",
            mass: 3.15,
            radiusRatio: 0.146,
            palette: CharmPalette(
                primary: CharmColor(0.20, 0.74, 0.76),
                secondary: CharmColor(0.10, 0.50, 0.55),
                deep: CharmColor(0.05, 0.30, 0.34),
                light: CharmColor(0.70, 0.95, 0.94)
            ),
            sound: .glass,
            beadCount: 1
        ),
        Entry(
            kind: .himmeli,
            sourceFileName: "Himmeli.svg",
            mass: 2.35,
            radiusRatio: 0.169,
            palette: CharmPalette(
                primary: CharmColor(0.82, 0.64, 0.26),
                secondary: CharmColor(0.60, 0.44, 0.14),
                deep: CharmColor(0.36, 0.26, 0.07),
                light: CharmColor(0.99, 0.90, 0.60)
            ),
            sound: .soft,
            beadCount: 1
        ),
        Entry(
            kind: .dreamCatcher,
            sourceFileName: "Dream Catcher.svg",
            mass: 2.52,
            radiusRatio: 0.1686,
            palette: CharmPalette(
                primary: CharmColor(0.62, 0.49, 0.32),
                secondary: CharmColor(0.38, 0.30, 0.20),
                deep: CharmColor(0.07, 0.03, 0.01),
                light: CharmColor(0.84, 0.75, 0.63)
            ),
            sound: .wood,
            beadCount: 0
        )
    ]

    static var kinds: [CharmKind] {
        entries.map(\.kind)
    }

    static func entry(for kind: CharmKind) -> Entry? {
        entries.first { $0.kind == kind }
    }

    static func sourceFileName(for kind: CharmKind) -> String? {
        entry(for: kind)?.sourceFileName
    }

    /// Builds the collection against a source of artwork.
    static func charms(source: SVGArtworkSource) -> [any Charm] {
        entries.map { entry in
            SVGCharm(
                kind: entry.kind,
                mass: entry.mass,
                radiusRatio: entry.radiusRatio,
                palette: entry.palette,
                sound: entry.sound,
                beadCount: entry.beadCount,
                bodyRun: entry.bodyRun,
                vector: source.vectorImage(for: entry.kind)
            )
        }
    }

}
