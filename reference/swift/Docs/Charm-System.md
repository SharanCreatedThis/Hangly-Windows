# The charm system

What hangs on the end of the rope: the charm abstraction, the shipped collection,
the Library that browses it, and the Studio that turns an image into a charm.

Part of the [architecture documentation](Architecture.md). The artwork pipeline
that feeds it is described separately in [SVG import](SVG-Import.md).

---

## The abstraction

### Three pieces

- **`Charm`** is a protocol over stateless value types. A charm knows its identity,
  its physical metrics and its shapes. It owns no rendering at all.
- **`CharmRenderer`** decides how any charm is lit: shadow, body gradient, details,
  specular bloom, rim. Implementing lighting once is why a camera and a heart look
  like they came from the same box, and why adding a charm needs no drawing code.
- **`CharmManager`** is the registry and the selection, backed by `SettingsStore` so
  the choice persists.

`CharmKind` is deliberately separate from the protocol. The protocol carries geometry
and is not something you would persist or put in a menu; the enum is a stable string
that survives in a settings document and enumerates for the UI.

### Geometry in a unit square

Charms describe shapes in a unit square using `CGPath`, not SwiftUI `Path`. That keeps
the model layer free of SwiftUI, consistent with the rest of the project, and lets a
test assert that every charm's silhouette is non-empty and stays inside its own
bounds. The renderer applies one affine transform to place and scale it, so charms are
resolution independent and stay crisp at any size.

### Physics

Charm metrics are not part of `RopeConfiguration`. They live on the simulation as a
separate property, because `RopeConfiguration.fitted(to:)` rebuilds itself when the
overlay resizes and would otherwise wipe the charm's mass and size.

`setCharmMetrics(_:)` updates the final node's inverse mass in place rather than
rebuilding the rope, which is what lets a charm change interpolate frame by frame
without the rope losing its motion.

### Switching

`OverlayViewModel` polls the manager's selection once per frame rather than observing
it. Polling puts the change on the same clock as the animation that follows, so there
is no ordering question between "the selection changed" and "the frame that animates
it".

A change produces two `CharmLayer`s for about a quarter of a second: the outgoing one
fading and shrinking, the incoming one fading in and growing. Mass, radius and the
cord's palette interpolate alongside, so the grab region and the swing weight always
match what is on screen. The transition starts from the rope's *current* metrics, not
the outgoing charm's nominal ones, so interrupting a change mid-way stays continuous.

The renderer knows nothing about any of this. It draws the list of layers it is given.

## Custom charms

### Identity had to open up

Phase 3 noted that `CharmKind`, a closed enum, was the one place the design would
have to give. It gave here. `CharmID` is now the identity: `.builtIn(CharmKind)` or
`.custom(UUID)`. Its storage form is a single string, and a built-in is written as
its plain kind name exactly as before, so every settings document written by Phase 3
still decodes unchanged. Imports are written as `custom:` plus a UUID. The compatibility
is asserted in a test.

### The pipeline

`CharmImageProcessor` is a chain of pure functions, each testable alone:

```
load  →  isolate subject  →  fit to square  →  analyse  →  encode PNG
```

- **Load** goes through ImageIO, which reads PNG, JPEG and WebP natively, and
  downsamples anything over 2048 pixels first so a 50-megapixel photo costs the same
  as a screenshot.
- **Isolate** tries three things in order. Existing transparency is trusted, because
  an image someone has already cut out must not be cut again. Otherwise Vision's
  `VNGenerateForegroundInstanceMaskRequest` lifts the subject, which handles
  photographs. If it sees nothing, a corner flood fill clears a flat background,
  which handles clip art on white. The flood fill compares to the *seed corner*, not
  the neighbouring pixel, so a soft edge cannot let it creep into the subject.
- **Fit** crops to the visible pixels and scales them into a 512-pixel transparent
  square with a margin, so every charm bitmap maps to the unit square the same way
  the vector charms do.
- **Analyse** derives what the rope needs. Mass follows coverage, so a solid shape is
  heavier than a sparse one. The knot inset follows the top-most visible row, so the
  cord's loop lands on the image's real edge. The cord palette is derived from the
  alpha-weighted mean colour.

The whole chain runs in a detached task. Inputs and outputs are `Sendable`, which
`CGImage` is, so nothing crosses the actor boundary that should not.

### Storage

`CustomCharmStore` keeps a JSON manifest beside one processed PNG per import, under
Application Support. The manifest is the source of truth and holds everything the
menu needs, so listing a hundred imports loads no pixels; bitmaps are loaded on first
use and cached. An entry whose PNG has gone is pruned at load rather than carried as
a dead link, and a corrupt manifest starts the store empty rather than blocking launch.

### Rendering a bitmap

`CharmRenderer` branches on `CharmArtwork.bitmap`. The shadow is cast by drawing the
image into a shadow layer, so a cut-out subject casts the shape of itself and not of
its bounding box. The specular bloom is clipped with `clipToLayer` to the image's own
alpha, so it never spills onto the background. Imported charms therefore share the
built-ins' light direction, shadow softness and highlight, which is what makes them
read as members of the set rather than stickers.

### Dropping and importing

The charm's hit disc is already the only part of the overlay that takes the mouse, so
it doubles as the drop target with no change to click-through elsewhere. While a file
is held over it the panel keeps mouse events regardless of cursor drift, the same rule
that keeps a drag alive. `CharmImportCoordinator` is the single entry point for both
the drop and the menu, so the rules — one import at a time, unsupported files refused
with a message, failures shown — are written once.

## The collection and the Library

### Words in JSON, shapes in Swift

Each built-in charm now has two halves. Its geometry, mass and palette are Swift,
where they are typed, unit-tested for bounds and rendered by the shared lighting
model. Its name, region, description, tags and preview reference are entries in
`CharmLibrary.json`, where they can be edited and reviewed without a rebuild. The
two are joined by `CharmKind`'s raw value, and a test insists that every kind has an
entry and that the two agree on the name, so they cannot drift apart.

### The Library

`CharmLibraryViewModel` merges the JSON entries with the import store's entries, the
latter under a synthetic "Yours" category, into one list of `CharmLibraryItem`s.
Search folds case and diacritics once per item. Favourites are a `Set<CharmID>` in
`AppSettings`, decoded one identifier at a time so an unrecognised entry drops itself
rather than the whole set, and deleting an import removes its star. Selecting a card
sets the charm manager's selection and nothing else: the rope changes on its next
frame and the detail pane, which always shows the selection, follows.

The Library is one page of Customize, which is an `NSWindow` owned by
`CustomizeWindowController` rather than a SwiftUI scene — see
`Docs/Architecture.md` §9 for why. Nothing opens at launch.

### What a window's artwork costs, and getting it back

Browsing the Library rasterises every charm in the collection at card size, About
draws the current one larger than life at the top of the page, and Create previews
every import ever made. All of it lands in the same caches the rope draws from, and
none of it used to be released.

Every rasterisation is now tagged with an `ArtworkUsage`: `.overlay` for the rope,
`.interface` for everything drawn in a window. The tag rides in the SwiftUI
environment — the overlay's root view sets it, every other surface leaves it at the
default — so it reaches `CharmRenderer` through `GraphicsContext.environment`
without a single drawing call having to be told twice which it is. It is part of the
`VectorImage` cache key, so the same charm at the same size can be held twice: once
for the rope, once for a card.

Closing Customize runs `ArtworkMemory.reclaim()`, which drops every `.interface`
bitmap, every imported bitmap that is not on the rope, every weathered copy whose
source has gone, and Create's workspace if the charm in it was already saved. The
measured regions — where a charm's beads end and its body begins — are kept: they
are rectangles, the rope reads them on every step, and each costs a rasterisation to
work out again.

What that recovers, and what it does not, is measured in
[`RC2-Audit.md`](RC2-Audit.md) §2. The short version: releasing the window and its
artwork gives back about 9 MB of the 37 a visit costs, and the rest is not the
caches. It is the Swift metadata, ObjC method caches and AppKit machinery that a
window of this size instantiates once and the process then keeps, plus the parsed
SVG documents, which AppKit caches per bundle and an app cannot evict. Measured
with `heap`: the live object graph goes from 7.7 MB idle to 22.5 MB after a visit,
and stays there however long you wait.


---

## The Charm Studio

### The pipeline, staged

`CharmStudioPipeline` is the Phase 4 importer cut at its joints. `load` and
`detectSubjects` run once per source; `isolate` runs when the background method
changes; `buildDraft` runs when a slider moves. Subject detection keeps every cut-out
Vision can produce — all instances, then each on its own — so switching subjects
never touches Vision again. Each stage is a `nonisolated` async function over
`Sendable` values, run detached, so the window stays responsive while a large photo
is processed.

### One window, hosted by AppKit

The Studio is an `NSWindow` around an `NSHostingController`, not a SwiftUI `Window`
scene, for the same reason the overlay is an `NSPanel`: any service can open it and
hand it a file without threading an `OpenWindowAction` through the view hierarchy.
That is what lets a drop on the overlay, the menu, and the Library's toolbar all
land in the same place. When the window closes its view tree is released, so a hidden
Studio cannot keep its rope preview ticking.

### Undo as a value

`StudioAdjustments` is a plain struct, so an undo step is a copy. `UndoStack` holds
the past and, after an undo, the future; recording a change discards the future,
which is the behaviour every editor has. A slider drag is one step, not one per tick:
the view calls `beginEditing` when a drag starts and `endEditing` when it ends, and
only the difference between those two snapshots is recorded. ⌘Z and ⇧⌘Z are
keyboard shortcuts on the header's buttons, which is how they work in a window that
has no application menu.

### Reprocessing that cannot go stale

Adjustments debounce for sixty milliseconds and every reprocess carries a
generation number. A result whose generation is no longer current is dropped, so a
fast sequence of slider moves can never leave a preview from a superseded value.
Isolation is cached against the method that produced it, so moving the size slider
re-fits without re-isolating.

### The rope preview

`StudioRopePreview` runs its own `RopeSimulation` under a `TimelineView` that pauses
whenever the rope has settled, so an idle Studio costs nothing. Its charm carries the
draft's real mass, which is the point: the user sees how the weight they chose
actually swings before committing. Under Reduce Motion the preview hangs still and
the Swing button is disabled.

### What the tests cover

HEIC decoding through a real HEIC fixture, since ImageIO on Apple Silicon can encode
one; subject detection producing one cut-out per instance; each removal method's
observable effect; the draft applying size, weight and fill; adjustment clamping;
name derivation including the dotfile edge; the undo stack including branch
discard and the cap; and the whole workflow through the view model — open, adjust,
undo, redo, a drag as one step, method changes including the failure path and its
undo, save with and without selecting, and bad input leaving the Studio empty.

Two real defects surfaced along the way. A file named only `.png` produced the name
`.png`, because Foundation treats a leading dot as no extension. And a corrupt PNG
was reported as an unsupported *type* rather than unreadable: ImageIO cannot identify
garbage at all, and that case now reads as unreadable, with "unsupported" reserved
for a real format outside the list.

