//
//  Charm.swift
//  Hangly
//
//  The charm abstraction: what hangs on the end of the rope.
//

import CoreGraphics
import Foundation

/// Identity of any charm, built-in or imported.
///
/// Built-ins are a closed set; imported charms are open-ended, so identity has to be
/// more than an enum. Storage is a single string so the settings document stays
/// readable: a built-in is written as its plain kind name, exactly as before Phase 4,
/// and an import is written as `custom:` followed by its UUID. That keeps every
/// existing settings document decoding unchanged.
enum CharmID: Hashable, Sendable {
    case builtIn(CharmKind)
    case custom(UUID)

    private static let customPrefix = "custom:"

    var isCustom: Bool {
        if case .custom = self { return true }
        return false
    }

    /// The import's identifier, or `nil` for a built-in.
    var customID: UUID? {
        if case .custom(let uuid) = self { return uuid }
        return nil
    }

    var storageValue: String {
        switch self {
        case .builtIn(let kind): kind.rawValue
        case .custom(let uuid): Self.customPrefix + uuid.uuidString
        }
    }

    init?(storageValue: String) {
        if let kind = CharmKind(rawValue: storageValue) {
            self = .builtIn(kind)
        } else if storageValue.hasPrefix(Self.customPrefix),
                  let uuid = UUID(uuidString: String(storageValue.dropFirst(Self.customPrefix.count))) {
            self = .custom(uuid)
        } else {
            return nil
        }
    }
}

extension CharmID: Codable {
    init(from decoder: any Decoder) throws {
        let raw = try decoder.singleValueContainer().decode(String.self)
        guard let id = CharmID(storageValue: raw) else {
            throw DecodingError.dataCorrupted(DecodingError.Context(
                codingPath: decoder.codingPath,
                debugDescription: "Unrecognised charm identifier '\(raw)'."
            ))
        }
        self = id
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(storageValue)
    }
}

/// The physical properties a charm contributes to the rope.
struct CharmMetrics: Codable, Equatable, Sendable {
    /// Mass relative to a plain rope node. Heavier charms swing with more authority
    /// and pull the rope straighter.
    var mass: Double

    /// Bounding radius as a fraction of the rope's total length.
    var radiusRatio: Double

    /// Where the cord terminates, as a fraction of the bounding radius measured back
    /// along the final link. One puts the knot on the bounding circle.
    var knotInset: Double

    /// The shipped default, matching the plain bead.
    static let `default` = CharmMetrics(mass: 2.6, radiusRatio: 0.126, knotInset: 0.90)

    init(mass: Double, radiusRatio: Double, knotInset: Double = 0.9) {
        self.mass = mass
        self.radiusRatio = radiusRatio
        self.knotInset = knotInset
    }

    /// Linear blend, used to make a charm change resize smoothly instead of popping.
    static func interpolate(from start: CharmMetrics, to end: CharmMetrics, progress: Double) -> CharmMetrics {
        let clamped = progress.clamped(to: 0...1)
        return CharmMetrics(
            mass: start.mass + ((end.mass - start.mass) * clamped),
            radiusRatio: start.radiusRatio + ((end.radiusRatio - start.radiusRatio) * clamped),
            knotInset: start.knotInset + ((end.knotInset - start.knotInset) * clamped)
        )
    }
}

/// An sRGB colour, kept free of SwiftUI so charms stay in the model layer.
struct CharmColor: Codable, Equatable, Hashable, Sendable {
    var red: Double
    var green: Double
    var blue: Double
    var alpha: Double

    init(_ red: Double, _ green: Double, _ blue: Double, alpha: Double = 1) {
        self.red = red
        self.green = green
        self.blue = blue
        self.alpha = alpha
    }

    func withAlpha(_ value: Double) -> CharmColor {
        CharmColor(red, green, blue, alpha: value)
    }

    /// Multiplies the colour channels, keeping alpha. Below one darkens.
    func scaled(by factor: Double) -> CharmColor {
        CharmColor(
            (red * factor).clamped(to: 0...1),
            (green * factor).clamped(to: 0...1),
            (blue * factor).clamped(to: 0...1),
            alpha: alpha
        )
    }

    static func interpolate(from start: CharmColor, to end: CharmColor, progress: Double) -> CharmColor {
        let amount = progress.clamped(to: 0...1)
        func mix(_ first: Double, _ second: Double) -> Double {
            first + ((second - first) * amount)
        }
        return CharmColor(
            mix(start.red, end.red),
            mix(start.green, end.green),
            mix(start.blue, end.blue),
            alpha: mix(start.alpha, end.alpha)
        )
    }
}

/// Named roles a charm's artwork draws with, resolved through its palette.
enum CharmInk: Equatable, Sendable {
    case primary
    case secondary
    case deep
    case light
    case glint
}

/// The four tones a charm is built from.
struct CharmPalette: Codable, Equatable, Hashable, Sendable {
    var primary: CharmColor
    var secondary: CharmColor
    var deep: CharmColor
    var light: CharmColor

    /// Derives a full palette from one representative colour, for imported images.
    static func derived(from base: CharmColor) -> CharmPalette {
        CharmPalette(
            primary: base,
            secondary: base.scaled(by: 0.72),
            deep: base.scaled(by: 0.45),
            light: .interpolate(from: base, to: CharmColor(1, 1, 1), progress: 0.55)
        )
    }

    /// Blends two palettes, so the cord can follow a charm change instead of
    /// snapping to the new colour halfway through the cross-fade.
    static func interpolate(from start: CharmPalette, to end: CharmPalette, progress: Double) -> CharmPalette {
        CharmPalette(
            primary: .interpolate(from: start.primary, to: end.primary, progress: progress),
            secondary: .interpolate(from: start.secondary, to: end.secondary, progress: progress),
            deep: .interpolate(from: start.deep, to: end.deep, progress: progress),
            light: .interpolate(from: start.light, to: end.light, progress: progress)
        )
    }

    func color(for ink: CharmInk) -> CharmColor {
        switch ink {
        case .primary: primary
        case .secondary: secondary
        case .deep: deep
        case .light: light
        case .glint: CharmColor(1, 1, 1, alpha: 0.85)
        }
    }
}

/// One shape drawn on top of a charm's silhouette.
struct CharmDetail {
    enum Style {
        case fill(CharmInk)
        /// Line width is in unit space and scaled with the charm.
        case stroke(CharmInk, width: Double)
    }

    var path: CGPath
    var style: Style
}

/// A charm's complete drawing description, in a unit square.
///
/// A charm is one of three things. Geometry: a silhouette that carries the shadow,
/// body gradient, specular highlight and rim, with details drawn over it. A bitmap:
/// an imported image fitted to the square and lit through its own alpha. Or a
/// vector asset: an SVG rasterised at exact device pixels for every size it is
/// drawn at, so it is crisp on any display and carries its own shading. The
/// renderer owns the lighting in every case.
struct CharmArtwork {
    var silhouette: CGPath
    var details: [CharmDetail]

    /// When set, this is the visual. The silhouette is then only its bounds.
    var bitmap: CGImage?

    /// When set, this is the visual, rasterised on demand.
    var vector: VectorImage?

    /// The part of `vector` holding the charm itself, when its artwork also draws
    /// beads that hang separately. `nil` means the whole asset is the charm.
    var bodyRegion: CGRect?

    /// The parts of `vector` holding each bead, nearest the anchor first. Drawn on
    /// the cord at the positions the simulation gives them.
    var beadRegions: [CGRect] = []

    /// Fraction of the square a whole vector asset spans; the rest is margin. A
    /// body region carries its own framing and is drawn without it.
    static let vectorFill = 0.96

    init(silhouette: CGPath, details: [CharmDetail] = []) {
        self.silhouette = silhouette
        self.details = details
    }

    init(bitmap: CGImage) {
        self.silhouette = CGPath(rect: CGRect(x: 0, y: 0, width: 1, height: 1), transform: nil)
        self.details = []
        self.bitmap = bitmap
    }

    init(vector: VectorImage, body: CGRect? = nil, beads: [CGRect] = []) {
        self.silhouette = CGPath(rect: CGRect(x: 0, y: 0, width: 1, height: 1), transform: nil)
        self.details = []
        self.vector = vector
        self.bodyRegion = body
        self.beadRegions = beads
    }
}

/// Something that can hang on the end of the rope.
///
/// Conformers are value types describing geometry and physical properties. They own
/// no rendering: `CharmRenderer` turns an artwork into pixels, which is what keeps
/// the set visually coherent and lets a new charm be added by describing shapes alone.
///
/// `Sendable` because the registry of built-ins is a static constant and imported
/// charms cross into the rendering pass. `CGImage` is immutable and `Sendable`, so a
/// bitmap charm qualifies too.
protocol Charm: Sendable {
    var id: CharmID { get }
    var displayName: String { get }

    /// SF Symbol shown beside the name in the menu.
    var symbolName: String { get }

    var metrics: CharmMetrics { get }
    var palette: CharmPalette { get }

    /// Material for sound effects. Defaults to `.soft`.
    var sound: CharmSound { get }

    /// Beads threaded onto the cord above the charm. Defaults to none.
    var beads: [CharmBead] { get }

    /// Shapes in a unit square, with `(0, 0)` at the top left. The complete piece,
    /// which is what the Library, the Studio and the previews show.
    func artwork() -> CharmArtwork

    /// The artwork as it hangs on the rope.
    ///
    /// Separate from `artwork()` because a charm drawn with beads above it hangs as
    /// several pieces: the rope carries the beads and the charm hangs below them.
    /// Working that out means measuring the asset, so only the overlay asks for it.
    func hangingArtwork() -> CharmArtwork
}

/// A charm from the closed, shipped set. Identity and naming come from its kind.
protocol BuiltInCharm: Charm {
    var kind: CharmKind { get }
}

extension Charm {
    var beads: [CharmBead] { [] }

    /// Most charms are one piece, and hang exactly as they are drawn.
    func hangingArtwork() -> CharmArtwork { artwork() }
}

extension BuiltInCharm {
    var id: CharmID { .builtIn(kind) }
    var displayName: String { kind.displayName }
    var symbolName: String { kind.symbolName }
}
