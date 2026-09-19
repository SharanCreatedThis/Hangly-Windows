//
//  CircleCharm.swift
//  Hangly
//

import CoreGraphics

/// A plain bead, drawn from geometry rather than artwork.
///
/// No longer offered in the menu — `.circle` is `Bead.svg` now, like every other
/// built-in charm. This is what the app falls back to when a charm cannot be
/// resolved at all: an import whose file has gone, or a kind with no asset. It is
/// geometry precisely because it has to draw when artwork is what went wrong.
struct CircleCharm: BuiltInCharm {
    let kind = CharmKind.circle

    let sound = CharmSound.glass

    let metrics = CharmMetrics.default

    let palette = CharmPalette(
        primary: CharmColor(0.49, 0.42, 0.95),
        secondary: CharmColor(0.33, 0.26, 0.78),
        deep: CharmColor(0.22, 0.16, 0.55),
        light: CharmColor(0.78, 0.74, 1.00)
    )

    func artwork() -> CharmArtwork {
        CharmArtwork(silhouette: CGPath(ellipseIn: CGRect(x: 0, y: 0, width: 1, height: 1), transform: nil))
    }
}
