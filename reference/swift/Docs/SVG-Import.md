# SVG artwork and the import pipeline

How the collection's artwork gets from a designer's SVG into a charm swinging on a
rope: the asset pipeline, vector rendering at every scale, and the measurement that
separates a charm from the beads drawn above it.

Part of the [architecture documentation](Architecture.md).

---

### Artwork

Every built-in charm is a professional SVG asset. `CollectionCharmCatalog`
holds each one's identity, physics, palette, sound and source file name as data,
and `SVGCharm` pairs that with a `VectorImage` resolved through `SVGArtworkSource`.
The geometry those charms were first drawn with — and the helpers only it used — is
gone, including the five classics, which were the last charms drawn in code.
`CircleCharm` survives as the placeholder for a charm that cannot be resolved at
all, which is the one case artwork cannot answer.

`VectorImage` wraps an SVG that AppKit keeps as vector data and rasterises it into
a square of exactly the pixels the renderer needs, at the display's scale and under
the current appearance, caching per size. That is what makes the artwork crisp on
Retina at any size while costing a bitmap draw per frame rather than a vector
render. The renderer adds only a drop shadow to vector artwork: it carries its own
shading, and a specular bloom on top of it would read as a smudge.

Each SVG is one picture: a cord at the top, a few beads threaded onto it, then the
charm. The overlay needs those as separate pieces — the rope carries the beads and
the charm hangs below them — and the artwork must not be edited to get them, so the
split is measured from the rendering instead. `CharmArtworkSplitter` rasterises the
asset once and reads its row profile: a row crossed only by the cord is a few percent
of the artwork's width, a row through a bead or the charm is far wider, so runs of
wide rows are the solid parts and the gaps between them are cord. Which runs are
beads, and which one begins the charm, is the single judgement a picture cannot make
— a thick cord and a fat bead look alike from below — so the catalogue states both
per charm, and the runs above the charm that are not beads are dropped because the
simulated cord replaces them. `VectorImage` then rasterises any region of the asset
on its own, at the size it will appear; because the source is vector, a region blown
up to fill its target is as sharp as the whole asset would be.

That split is only for the rope. `artwork()` returns the complete piece, which is
what the Library, the Studio and the previews show; `hangingArtwork()` returns the
charm with its beads handed to the rope, and only the overlay asks for it. The
measurement costs a rasterisation, so this keeps it off the path that draws sixteen
cards at once.

A charm's knot — where the cord stops and the artwork takes over — is measured from
the asset too, as the top of its body, rather than guessed at. `RopeCurve` finds it
as the point where the cord crosses into the charm's own radius, which is why the
cord meets the loop in the same place whether the rope hangs straight or whips: a
bent tail covers less cord than a straight one. The charm's orientation comes from
that same cut rather than from the final link, so its loop always lines up with the
cord drawn into it.

A collection charm whose SVG is missing draws a placeholder bead rather than
nothing, and the omission is logged at launch and testable through
`SVGArtworkSource.missingAssets`. A charm whose artwork cannot be split the way the
catalogue describes falls back to drawing whole, which is wrong but never blank.

### Beads on the cord

A bead is a Verlet particle exactly like a rope node: it carries its position and
its previous position, and gravity and damping act on it the same way. The one extra
constraint is that it lives on the cord. Each fixed step, after the rope is solved,
every bead is integrated freely, projected back onto the curve, pulled toward the
rest place the artwork drew it at, and hard-limited to a short travel either side;
then a separation pass pushes touching beads apart and keeps the lowest clear of the
charm. The free integration is what makes a bead lag behind a whipping rope and catch
up afterwards; the tether and the limit are the knot it is threaded against, and are
why it slides rather than migrates.

The dependency runs one way. `RopeSimulation+Beads` reads the rope and writes only
beads, so no amount of bead behaviour can disturb the rope's own solver, and the
existing guarantees about stretch and sleep hold unchanged. What beads do give back
is weight: each one's mass is shared between the two nodes it hangs between, applied
when the beads are set rather than every step, because a bead only slides a few
points and a mass that changed under the solver every step would be a source of
instability for no visible gain.

Beads are described in proportions of the charm's radius, not in points, so the same
description survives a rescale — the simulation converts against the radius each time
it is given one. Their sizes and places come straight from the artwork, which is what
makes a rope at rest look exactly like the picture the designer drew.

`RopeCurve` is the piece both halves share. The rope is drawn as a quadratic spline
through the node midpoints rather than as a polyline, so a bead placed on the chain
would sit beside the cord rather than on it. The curve builds that spline, flattens
it once, and answers where the point this far along is, how far along the nearest
point is, and what the cord looks like up to a cut. The renderer strokes the same
flattened curve, so what is drawn and what beads ride on cannot drift apart.

### The asset pipeline

There is no longer a pipeline for the artwork itself: `Assets/Charms` is a folder
reference in the project, so the designer's folder is copied into the app whole and
`SVGArtworkSource` reads the SVGs out of `Contents/Resources/Charms` by the file
name the catalogue already names. Adding a charm means dropping in the file and
naming it in `CollectionCharmCatalog`; nothing has to be synced, and the app draws
the same bytes the designer drew.

It used to go through the asset catalog, an imageset per charm, and that was worth
undoing: `actool` bakes a bitmap of every vector asset at each scale factor beside
the vector data, and none of those bitmaps is ever drawn, because `VectorImage`
rasterises the vector itself at the exact size each frame needs. They were 56 MB of
a 95 MB app. What a folder cannot do is carry a dark variant of a charm; no charm
has one, and `VectorImage` still draws under the dark appearance for anything that
responds to it.

`Scripts/generate-charm-previews.sh` renders every charm's preview PNG and the
collection sheet through the same renderer the rope uses, reading the SVGs from the
folder via `HANGLY_CHARM_SVG_DIR`. It compiles the same `CollectionCharmCatalog`
the app does, so there is one mapping from file to charm. A test checks that every
metadata entry in the collection resolves to a file inside the built bundle.

