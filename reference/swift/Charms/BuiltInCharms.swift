//
//  BuiltInCharms.swift
//  Hangly
//
//  The shipped charm set.
//

/// Every built-in charm, in menu order: the Hangly collection, then the classics.
///
/// The collection leads because it is what the app is for; the classics are plain
/// shapes and sit underneath it. All of it is drawn from SVG assets resolved
/// through `SVGArtworkSource`, so the order is the catalogue's own. A free-standing
/// lookup rather than a method on a service, so previews, scripts and tests can
/// resolve a charm without building the whole object graph.
enum BuiltInCharms {
    static let all: [any Charm] = CollectionCharmCatalog.charms(
        source: SVGArtworkSource.resolveDefault()
    )

    /// Falls back to the circle, so an unknown kind can never leave the rope bare.
    static func charm(for kind: CharmKind) -> any Charm {
        all.first { $0.id == .builtIn(kind) } ?? CircleCharm()
    }

    /// Collection charms whose SVG asset could not be found.
    ///
    /// This is the one place that asks, and asking opens every asset — which is why
    /// it is only ever called from the development-only launch check. A shipped app
    /// loads a charm's artwork when it draws it and not before.
    static var missingArtwork: [CharmKind] {
        all.compactMap { charm in
            guard let svg = charm as? SVGCharm, svg.vector?.isAvailable != true else { return nil }
            return svg.kind
        }
    }
}
