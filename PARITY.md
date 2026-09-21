# Parity audit: Windows against macOS 2.0.0 (build 200)

What the Windows build still differs from in the shipping macOS app, how each
difference was established, and what it would take to close.

Audited 20 September 2026 against macOS Hangly **2.0.0 (200)** installed at
`/Applications/Hangly.app`, running, and against the Windows build at commit
`82d27b3`. Both are version 2.0.0, so this is a like-for-like comparison.

---

## 0. What this audit could and could not check

This matters more than anything below it, because it bounds how much of the rest is
measurement and how much is inference.

**Checked directly**

| Source | What it settled |
|---|---|
| `reference/swift/` — physics, models, charm catalogues | Every number and data structure the rope and the catalogue use |
| `reference/swift/Docs/` — 1,312 lines across four documents | The artwork split, the bead model, the import pipeline, the Studio's stages, the Library's composition |
| `/Applications/Hangly.app` bundle and binary | Which features actually ship, and their user-facing strings |
| Screenshots of both apps, light and dark | The visible differences, measured |
| Windows source | What the port actually does |

**Not available, and this is the blocker**

`reference/swift/` carries the model and physics layers only. There is **no macOS
view-layer source in this repository** — no `CharmRenderer`, no rope-style renderer,
no Library, About or Studio views. Those are exactly the files that would answer:

- the drop shadow's radius, offset, colour and opacity
- the specular bloom's geometry
- the rope's gradients, texture and lighting
- every Library layout metric: card size, spacing, hover and selection treatment
- the About page's composition and the creator surfaces

Where this audit states a macOS rendering parameter, it comes from the documentation
or from the picture — never from the macOS code, because that code is not here.
**Everything in §1 marked *inferred* would become exact if `CharmRenderer.swift` and
the rope-style renderer were added to `reference/swift/`.** That is the single
highest-value thing that could be handed over.

---

## 1. Rendering audit

### 1.1 Beads — root cause found, and it is not a quality problem

This is the largest visible difference and the cheapest to fix, because the Windows
build already does all the hard parts.

**What macOS does.** From `Docs/SVG-Import.md`:

> `VectorImage` then rasterises **any region of the asset on its own**, at the size it
> will appear; because the source is vector, a region blown up to fill its target is
> as sharp as the whole asset would be.

> `hangingArtwork()` returns the charm with **its beads handed to the rope**.

> Their sizes and places **come straight from the artwork**, which is what makes a
> rope at rest look exactly like the picture the designer drew.

So the answer to each question asked:

| Question | Answer |
|---|---|
| Does macOS crop bead regions directly from the SVG? | **Yes.** `VectorImage` rasterises an arbitrary region of the vector asset at target size. |
| Does it create bead sprites from SVG regions? | Yes — one raster per region, cached by size, same as the body. |
| Are bead layers extracted separately? | Not layers. The split is **measured from the rendering** by row profile, not read from SVG structure. |
| Is any simplification happening? | On macOS, none. On Windows, total. |

**What Windows does.** The pipeline is correct right up to the last step:

1. `CharmArtworkSplitter.Split` measures the artwork and returns
   `CharmArtworkRegions(Rect Body, IReadOnlyList<Rect> Beads)` — the bead rectangles
   are *already computed*, and by the same row-profile method macOS uses.
2. `CharmCatalog.BeadsFor` converts each rectangle into `CharmBead(Size, Offset, Mass)`
   — and **discards the rectangle**. Position, size and mass survive; the pointer back
   into the artwork does not.
3. `RopeRenderer.DrawBeads` therefore has nothing to draw from, and fills a generic
   ellipse plus one highlight ellipse.

That is the whole defect. The artwork is measured and then thrown away.

**Why one bead becomes three.** Both catalogues declare `beadCount: 1` for Nazar —
verified line by line, the port is faithful. One *run* above the body is correct: the
three beads the designer drew touch each other, so the row profile sees them as one
solid run. macOS rasterises that run's rectangle and you see all three beads. Windows
draws one ellipse sized to the run's bounding box and you see one flat oval. Same
measurement, different renderer.

**The fix is small.** `CharmArtworkCache.Raster(fileName, pixels, region)` already
takes an arbitrary region — it was built for exactly this and is currently only ever
called with the body. Carrying the source rectangle alongside each `CharmBead` and
calling the same rasteriser from `DrawBeads` is the whole change. Estimated half a
day including tests.

### 1.2 Shadow and glow — Windows draws the wrong thing entirely

**macOS.** From `Docs/SVG-Import.md`: "The renderer adds **only a drop shadow** to
vector artwork: it carries its own shading, and a specular bloom on top of it would
read as a smudge." From `Docs/Charm-System.md`, `CharmRenderer` owns "shadow, body
gradient, details, specular bloom, rim", and for bitmaps "the shadow is cast by
drawing the image into a shadow layer, so a cut-out subject casts the shape of itself
and not of its bounding box."

So: a **shape-accurate drop shadow**, cast from the artwork's own alpha.

**Windows.** `RopeRenderer.DrawCharms` draws no shadow at all. Instead it fills an
ellipse of `radius × CharmHaloExtent` (1.7×) at **6% alpha** in the charm's light
colour. That is the visible circle in the screenshots: a hard-edged disc, centred on
the charm, that has no macOS counterpart.

This single substitution explains both reported symptoms — "shadows minimal or
absent" and "hard edge glow, circular blur visible". They are the same object.

*Inferred, not measured:* the shadow's exact radius, offset, colour and opacity. Not
in the repository.

**Note on cost.** `RopeRenderer`'s own comment explains that Win2D's Gaussian blur was
rejected because "a blur is an off-screen pass per frame, and at 120 Hz over a window
this size it costs more than the entire solver". That reasoning was about the *cord's*
glow, where three strokes read the same. It does not transfer to a charm drop shadow,
which is one shadow per charm, at most three per frame, on a raster that is already
cached. A shadow is affordable; the note should not be read as forbidding it.

### 1.3 Rope and beads material quality

The cord itself is drawn with the right *structure* — `RopeRenderer` is a transcription
of the macOS style renderer's approach, with dashes for twist, paired strokes for
braid, notches for chain, layered strokes for neon, and the per-style tables match.

What it lacks is any *gradient across the cord's width*. Every stroke is a flat colour:
deep underneath, primary over it, then texture. macOS reads as lit because the cord
has cross-sectional shading.

*Inferred.* The macOS cord renderer is not in the repository, so the exact gradient
construction is unknown. This is the weakest-evidenced item in the audit and should
not be implemented from guesswork.

### 1.4 SVG rasterisation quality — now correct

Fixed and verified this session. Artwork was rasterised in points while the surface
is at the display's DPI, so at 200% each source pixel was drawn to four. Now sized in
device pixels. Measured on Captain America at identical on-screen size: mean gradient
24.3 → 36.6 per pixel, peak 121 → 397. Side-by-side at
`hangly-shots/compare-artwork.png`.

Remaining difference after that fix is attributable to §1.1 and §1.2, not to
rasterisation.

---

## 2. Library parity

Windows has the skeleton; macOS has roughly twice the surface.

| Element | macOS | Windows | Gap |
|---|---|---|---|
| Detail sidebar | Preview, name, region, description, tags, "On the rope", "Favorite" | None | **Whole pane missing** |
| Collection packs | Cards with artwork, charm count and description (Marvel, DC, Tamil Spiritual, BTS, Football Legends, Music Legends, Friends, Breaking Bad, Stranger Things) | Flat category chips | **Whole browse mode missing** |
| Charms / Ropes toggle | Segmented control; ropes browsable in the same page | Rope style lives on a separate Appearance page | Structural |
| Current rope strip | Thumbnails, drag to reorder, per-charm size slider, "Add Charm" tile | Radio buttons 1/2/3 and a button per slot | **Reorder and per-charm sizing missing** |
| Category list | All, Favorites, Protection, Luck & Fortune, Ritual & Home, Classic, Seasonal, **Imported**, Marvel… | Same minus naming ("Yours" vs "Imported") | Cosmetic |
| Search | "Search charms" | "Search charms, places, materials" | Windows is arguably better |
| Create page | Present in sidebar | Absent | See §6 |

`Docs/Charm-System.md` confirms the detail pane is load-bearing: "Selecting a card
sets the charm manager's selection and nothing else: the rope changes on its next
frame and **the detail pane, which always shows the selection**, follows."

The data for the sidebar is already on Windows — `CharmCatalogEntry` carries
`Region`, `Description` and `Tags`, and they are populated for all 81 charms. Nothing
needs generating; it needs displaying.

*Inferred:* exact card dimensions, spacing, hover and selection treatment.

---

## 3. Multi-charm layout — no defect found

Reported as "Windows: fixed sizing, macOS: dynamic scaling". **I could not reproduce
this, and the code says it should not happen.**

`CharmStackLayout` is identical between the two, line for line: same reference ruler,
same three ceilings applied in the same order (bead reach above, `charmClearance`
against each neighbour, `charmHeadroom / charmHaloExtent` for the lowest), same
`max(0, min(reference × radiusRatio × scale, ceiling))`. Windows charm radii *do*
shrink as charms are added — measured earlier this session: three charms at the
shipped settings resolve to smaller radii than one, exactly as the ceilings dictate.

My reading is that this symptom is §1.1 in disguise: flat elliptical beads do not
visually rescale the way real bead artwork does, so a three-charm rope reads as
badly proportioned even though the geometry is right. **Recommend fixing beads first
and re-assessing**, rather than changing layout maths that currently matches the
reference exactly. Changing those numbers without a failing test would also breach
the standing rule on `Hangly.Core`.

---

## 4. Feature matrix

Verified against the macOS binary's symbols and user-facing strings, the reference
documentation, and the Windows source. Not estimated.

| Feature | macOS 2.0.0 | Windows | Status |
|---|---|---|---|
| Physics / solver | Full | Full | **Parity** — 666 tests, ported from the same source |
| Rope styles | 10 | 10 | **Parity** |
| Charm catalogue | 81 | 81 | **Parity** |
| Overlay rendering | Shadow, gradients, bloom, rim, artwork beads | Flat beads, no shadow, halo disc | **Gap** — §1.1, §1.2 |
| Library browse | Packs, detail pane, reorder, per-charm size | Grid, chips, search, favourites, recents | **Partial** — §2 |
| About page | Statistics, Secrets, creator, coffee, release notes | Links, analytics inspector | **Partial** — §7 |
| Analytics | PostHog, 25 events | PostHog, 25 events, inspector | **Parity** (Windows adds the inspector) |
| Custom import — SVG | Yes | Yes | **Parity** |
| Custom import — photo | Vision subject extraction | **None** | **Missing** — §5 |
| Charm Studio | Full window, staged pipeline | **None**; the **Create** tab is v1.0's answer | **v1.1** — §6 |
| Sound | `CharmSound`, per-charm, volume | Removed rather than left as a control that does nothing | **v1.1** |
| Welcome popup | `Welcome*` strings present | None | **Missing** |
| Follow popup | `Follow*` strings present | None | **Missing** |
| Coffee flow | `CreatorUPIQR.png/jpg` ships in the bundle | Link only | **Partial** |
| Secrets button | `AboutSecrets`, `SecretVault`, "Tell me a secret" | None | **Missing** |
| Release notes | `Release Notes` strings | Link to GitHub | **Partial** |
| Statistics view | `AboutStatistics` | None | **Missing** |
| Weather charms | `WeatherService`, settings UI, analytics event | None | **Removed from the roadmap** — not a gap, §8 |
| Seasonal packs | `SeasonalCoordinator`, `SeasonalSettings` | None; the category and its eleven charms were cut | **Removed from the roadmap** — not a gap, §8 |

Sound is worth calling out: `CharmSound` is already ported and every catalogue entry
carries its value, and `soundEnabled`/`soundVolume` are already in the settings
document and in the Customize UI. **Windows currently offers a control that does
nothing.** That is a correctness problem, not just a gap.

---

## 5. Photo import

Fully documented in `Docs/Charm-System.md`, so no guessing is needed about behaviour:

```
load → isolate subject → fit to square → analyse → encode PNG
```

- **Load** — ImageIO; PNG, JPEG, WebP; downsamples anything over 2048px first.
- **Isolate** — three strategies in order: existing transparency is trusted; otherwise
  Vision's `VNGenerateForegroundInstanceMaskRequest`; if that sees nothing, a corner
  flood fill compared against the **seed corner** (not the neighbouring pixel, so a
  soft edge cannot let it creep into the subject).
- **Fit** — crop to visible pixels, scale into a 512px transparent square with margin.
- **Analyse** — mass from coverage; knot inset from the top-most visible row; cord
  palette from the alpha-weighted mean colour.

**Windows position.** Stages 1, 3 and 4 port directly — `CharmImporter` already
implements the mass, palette and knot arithmetic for SVG and the same functions apply
to a bitmap. Stage 2 is the only hard part: there is no in-box Windows equivalent of
`VNGenerateForegroundInstanceMaskRequest`.

Options, in order of preference:

1. **Transparency + flood fill only.** Ships in about two days, handles cut-outs and
   clip art on flat backgrounds, fails on photographs. Two of macOS's three strategies.
2. **ONNX Runtime with a segmentation model** (U²-Net or similar, ~4–5 MB). Handles
   photographs. Adds a native dependency and per-architecture model assets, and
   roughly doubles download size. Perhaps a week.
3. **Windows ML.** Avoids shipping a runtime but the API surface is in flux and the
   floor is above Windows 10 1809.

Recommendation: ship (1) labelled honestly, and treat (2) as its own milestone.

---

## 6. Studio

From `Docs/Charm-System.md`: `CharmStudioPipeline` is the importer "cut at its joints"
— `load` and `detectSubjects` run once per source, `isolate` when the background
method changes, `buildDraft` when a slider moves. Subject detection keeps every cut-out
Vision can produce, all instances then each alone, so switching subjects never re-runs
Vision. Every stage is a `nonisolated` async function over `Sendable` values, run
detached. Hosted as an `NSWindow` around an `NSHostingController`.

The binary confirms the UI surface: `CharmStudioView`, `CharmStudioViewModel`,
`StudioAdjustments`, `StudioBackgroundControls`, `StudioCharmDraft`, `StudioDropZone`,
`StudioHeader`, `StudioPreviewMode`, `StudioPreviewPanel`, `StudioRopePreview`,
`StudioSavedOverlay`, `StudioStatusBar`.

**Complexity: high, and gated on §5.** The Studio is a front end for the photo
pipeline; without subject detection there is little for it to adjust. Roughly two to
three weeks after §5 lands, and it should not start before then.

---

## 7. Creator experience

Concrete, evidenced gaps:

- **`CreatorUPIQR.png` / `.jpg` ship in the macOS bundle.** The coffee flow shows a
  real UPI QR code. Windows links out. This is the clearest single piece of
  personality that is missing.
- **Secrets** — `AboutSecrets`, `SecretVault`, "Tell me a secret", "No secret revealed
  yet". An easter-egg vault with revealed state. Entirely absent on Windows.
- **Statistics** — `AboutStatistics`. Absent.
- **Release notes** — in-app on macOS; Windows opens a browser.
- **Welcome and Follow popups** — first-run and follow prompts. Absent.

*Inferred:* the copy and layout of all of these. The strings are in the binary but
their arrangement is not.

---

## 8. Weather and Seasonal packs — removed from the roadmap

> **Settled 21 September 2026: neither will be built for Windows, at any version.** They
> are not deferred, not backlogged and not counted as parity gaps in
> `Docs/RELEASE-READINESS.md`. What follows is kept because it is the evidence of what
> macOS has, which is worth recording; it is not a plan.

The earlier note in this section argued for building them, on the evidence that both are
complete, shipping production features of macOS 2.0.0:

- `WeatherService`, `WeatherFetching`, `WeatherSettings`, `WeatherStatusRow`,
  `CurrentWeather`, `WeatherCondition`, `WeatherMood`, `WeatherPlace`,
  `WeatheredImageCache`
- A settings UI: *"Set a location in Settings to use weather here."*
- User-visible section: *"Live Weather and Seasonal Packs"*
- Error handling: *"Could not reach the weather service."*, *"Checking the weather"*
- An analytics event: `weather_effect_toggled`
- Privacy copy: *"Your location. Weather uses a city name you can see and change, and
  it is never sent here."* — a city name, never coordinates, never transmitted
- `SeasonalCoordinator`, `SeasonalSettings`, `SeasonalPack`

These are not experimental or hidden. **They stay on the roadmap**, and the privacy
design is already decided for us: city name only, user-visible and user-editable,
never sent to analytics. PRIVACY.md will need a section when it lands.

---

## 9. Recommended order

Ranked by visible-difference-per-day, with correctness before features.

| # | Work | Effort | Why here |
|---|---|---|---|
| 1 | **Beads from artwork** (§1.1) | ~0.5 day | Largest visible difference; machinery already exists; likely also resolves §3 |
| 2 | **Charm drop shadow, remove halo disc** (§1.2) | ~1 day | Second largest; removes a construct with no macOS counterpart |
| 3 | ~~**Sound playback** (§4)~~ | — | **Deferred to v1.1.** The control was removed rather than left doing nothing |
| 4 | **Library detail sidebar** (§2) | ~2 days | Data is already there for all 81 charms |
| 5 | **Collection pack cards** (§2) | ~2 days | Catalogue already carries pack membership |
| 6 | **Reorder + per-charm size** (§2) | ~2 days | Completes the Library |
| 7 | **About: statistics, secrets, UPI coffee, release notes** (§7) | ~3 days | Personality; self-contained |
| 8 | **Welcome / Follow popups** (§7) | ~1 day | Small, first-run polish |
| 9 | **Photo import, strategies 1 and 3** (§5) | ~2 days | Ships value without a model dependency |
| 10 | ~~**Weather + Seasonal** (§8)~~ | — | **Removed from the roadmap permanently.** Not deferred, not backlogged, not a parity gap |
| 11 | **Photo import via ONNX** (§5) | ~1 week | Unlocks 12 |
| 12 | **Creator Studio** (§6) | ~2–3 weeks | **v1.1**, not a v1.0 blocker. The Create tab is v1.0's answer |

Items 1–3 are correctness and should land before anything in §2 onward.

## 10. What would make the rest exact

Adding these to `reference/swift/`, read-only as the rest is, would convert every
*inferred* item above into a measurement:

- `CharmRenderer.swift` — settles §1.2 and §1.3 completely
- the rope-style renderer — settles §1.3
- the Library views — settles §2's metrics
- `AboutView` and the creator surfaces — settles §7's copy and layout

Without them, §1.2, §1.3, §2 metrics and §7 can only be approached by eye.
