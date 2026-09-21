# Visual parity audit

Windows against the shipping macOS **2.0.0 (build 200)**, 20 September 2026.

Every claim below carries its evidence class, as one of:

| Tag | Meaning |
|---|---|
| **[SWIFT]** | Read from `reference/swift/` source |
| **[DOC]** | Read from `reference/swift/Docs/` |
| **[BINARY]** | Read out of `/Applications/Hangly.app` |
| **[LIVE]** | Observed in the running macOS app |
| **[WIN]** | Read from Windows source |
| **[MEAS]** | Measured from pixels, method stated |
| **[INFER]** | Reasoned, not observed — flagged every time |

---

## 1. Beads — investigated, fixed, verified

### Investigation

**Does the artwork contain the beads?** Yes, and more literally than expected. **[MEAS]**
Decoding the embedded rasters out of each SVG:

| Charm | viewBox | Embedded rasters |
|---|---|---|
| Nazar Boncuğu | 0 0 474 762 | body 474×529, **plus three beads: 69×64, 108×102, 71×67** |
| Hamsa | 0 0 453 860 | body 453×643, **plus three beads: 65×59, 100×93, 67×65** |
| Daruma | 0 0 442 855 | one 442×855 (body and ornament together) |
| Captain America | 0 0 492 556 | one 492×556, no beads |

Nazar's three beads are three separate images inside the file. The artwork is not merely
*a* source of truth for the beads; it is the only one.

**How does macOS render them?** **[DOC]** `Docs/SVG-Import.md`:

> `VectorImage` then rasterises **any region of the asset on its own**, at the size it
> will appear; because the source is vector, a region blown up to fill its target is as
> sharp as the whole asset would be.

> `hangingArtwork()` returns the charm with **its beads handed to the rope**.

> Their sizes and places **come straight from the artwork**, which is what makes a rope
> at rest look exactly like the picture the designer drew.

So: crops regions directly from the SVG; one raster per region; no separate layer
extraction; **no simplification**.

**[LIVE]** Confirmed by capturing the running macOS app: the Nazar hangs under three
distinct gold spheres with individual specular highlights.

**How did Windows render them?** **[WIN]** Three stages, correct until the last:

1. `CharmArtworkSplitter.Split` returns `CharmArtworkRegions(Rect Body, IReadOnlyList<Rect> Beads)`
   — the rectangles, by the same row-profile method macOS uses.
2. `CharmCatalog.BeadsFor` turned each rectangle into `CharmBead(Size, Offset, Mass)`
   and **discarded the rectangle**.
3. `RopeRenderer.DrawBeads` therefore had nothing to draw from and filled an ellipse
   plus a highlight ellipse.

**Why one oval instead of three beads.** **[SWIFT]+[WIN]** Both catalogues declare
`beadCount: 1` for Nazar — verified entry by entry, the port is faithful. One *run* is
correct: the three drawn beads touch, so the row profile sees one solid run. macOS
rasterises that run's rectangle and all three beads appear. Windows drew one ellipse
around the run's bounding box.

### Implementation

`CharmDescriptor` now carries `BeadRegions`, `CharmLibrary` passes them through, and
`CharmArtworkCache.DrawBead` rasterises each region through the same call the body
already used — `Raster(fileName, pixels, region)` always accepted an arbitrary region
and was only ever called with the body. Beads are rotated with the cord like the charm.

The ellipse survives only as the fallback for a charm whose artwork could not be split.

### Verification **[MEAS]**

Ten bead-carrying charms captured at charm size 1.6: `hangly-shots/beads-after.png`.
Every bead is its own artwork —

- **Nazar** three gold spheres, matching **[LIVE]** macOS exactly
- **Hamsa** graduated gold beads
- **Daruma** the ornate endless-knot ornament, not a sphere at all
- **Maneki-neko** red and gold pair
- **Ghanta** dark ridged metal
- **Panchángjié** red knot with tassel
- **Lotus** magenta sphere matching the flower
- plus Lucky Coin, Bell, Firework

Before/after of the same rope: `after-drag3.png` (flat ovals) against `launch.png`.

**Note on Dream Catcher.** Named in the brief as a bead charm. **[SWIFT]+[WIN]** Both
catalogues declare `beadCount: 0` — its beads are part of its body artwork and macOS
draws no simulated beads on it either. No gap; nothing to fix.

**Scope.** **[WIN]** 26 of 81 charms carry beads (12 with one, 12 with two, Daruma three,
Maneki-neko four). The other 55 are unaffected.

---

## 2. Shadow — measured off macOS, implemented, verified

### Investigation

**[DOC]** `Docs/SVG-Import.md`: "The renderer adds **only a drop shadow** to vector
artwork: it carries its own shading, and a specular bloom on top of it would read as a
smudge." `Docs/Charm-System.md` on bitmaps: "the shadow is cast by drawing the image into
a shadow layer, so **a cut-out subject casts the shape of itself and not of its bounding
box**." So: alpha-derived, shape-accurate.

**Parameters — measured, not guessed. [MEAS]+[LIVE]** The macOS overlay was captured over
a white backdrop and the luminance profile read outward from the charm's silhouette in
three directions. Against a charm 224 screen px across (Retina 2×, so 112 pt):

| Distance from edge | Below | Right | Above |
|---|---|---|---|
| +1 px | 21.6% | 18.0% | 11.0% |
| +10 px | 12.2% | 7.3% | 3.3% |
| +22 px | 3.1% | 2.2% | 1.0% |
| +34 px | 0.0% | 0.3% | 0.0% |

Shadow on every side, stronger below than above, reaching white again about 16 pt out.

**The fit. [INFER] from [MEAS]** A blurred silhouette offset downward reproduces that: at
the bottom edge the sample sits `offset` inside the shadow and at the top edge the same
distance outside, so the two readings sum to the opacity and their ratio gives the offset
in units of the blur. Solving:

- opacity **0.326**
- blur σ **0.098 × radius**
- offset **0.041 × radius** downward

This is a model fitted to four measurements, not a value read from macOS source — the
renderer is not in the repository. It is labelled as such in the code.

### Implementation

`CharmArtworkCache.DrawShadow` uses Win2D's `ShadowEffect` over the charm's own cached
raster, so the shadow is cast from the artwork's alpha. The blur is stated in bitmap
pixels and the result scaled into place afterwards — blurring after the scale would
soften by the DPI factor on a 200% display.

### Verification **[MEAS]**

Windows captured over a white backdrop, same method:

| Distance | macOS below | Windows below |
|---|---|---|
| +10 px | 12.2% | 16.9% |
| +18 px | 5.9% | 8.2% |
| +26 px | 1.4% | 2.7% |
| +34 px | 0.0% | 0.4% |

Same character — soft falloff to nothing, no edge — and the same order of magnitude.
Windows reads slightly heavier. Two caveats, stated rather than hidden: the two charms
are different sizes (98 px radius against 112), and near-field readings are contaminated
by the charm's own soft edge, which on a glass Nazar is dark navy. The far field, where
contamination is nil, agrees closely.

**Shape accuracy:** `win-white-hamsa.png` — the shadow follows the thumb, the gaps
between fingers and the palm. It is not a disc.

---

## 3. Glow — removed

**[WIN]** Created in `RopeRenderer.DrawCharms`: a `FillEllipse` of
`radius × CharmHaloExtent` (1.7×) at **6% alpha** in the charm's own light colour.

**[DOC]+[BINARY]** No macOS equivalent. The documentation says vector charms get a drop
shadow *and nothing else*, and explicitly rejects a bloom over them as reading like a
smudge. The bloom that does exist is for imported bitmaps and is clipped to the image's
own alpha.

**Removed entirely**, replaced by nothing. The depth it was reaching for is §2's shadow.
`CharmHaloExtent` still sizes the canvas — that is a separate job, the headroom the
layout reserves below the lowest charm.

---

## 4. SVG rasterisation — measured, and a fix that failed

### The numbers asked for **[MEAS]+[WIN]**

Captain America at charm size 1.2, 192 dpi (200%):

| | |
|---|---|
| Source SVG viewBox | 492 × 556 |
| Source raster inside it | 492 × 556 PNG |
| Red-ring diameter at source | 329 px |
| Red-ring diameter on screen | 110 device px |
| Effective scale | 0.334 — a **2.99× reduction** |
| Raster target size | `round(radius × 2 × dpi/96)` device px |
| Scaling path | SVG → Skia `SKSurface` at device px → `CanvasBitmap.CreateFromBytes` at 96 dpi → Direct2D `DrawImage` into a DIP rect on a 192 dpi target → `UpdateLayeredWindow` |

Nazar 474×762 with four rasters; Hamsa 453×860 with four; Daruma 442×855 with one.

### How close is it to ideal? **[MEAS]**

The screen output was compared against a Lanczos downscale of the same source raster to
the same size, by mean absolute gradient across the shield:

| | mean | peak |
|---|---|---|
| Windows (Skia, direct to device px) | 36.55 | 397 |
| Lanczos reference | 42.13 | 243 |
| ratio | **0.867** | — |

Windows keeps 87% of the achievable detail, but with *higher* peak gradients than the
reference — the signature of under-filtered sampling, not blur.

### The fix that did not work **[MEAS]**

Supersampling was implemented — rasterise at 2× and filter down with a Mitchell cubic —
and **measured worse**: mean gradient fell from 36.55 to 23.56 against a reference of
44.40, a ratio of 0.531. Two resampling stages and a deliberately soft cubic lose more
than Skia's single stage. **Reverted**, and the reasoning left in the code so it is not
retried blind.

**[INFER]** The residual 13% is most likely the fractional-position bilinear resample
Direct2D performs, because `pixels` is rounded to an integer while the destination
rectangle is fractional. Not isolated — stated as a hypothesis, not a finding.

---

## 5. Multi-charm layout — measured, no defect

The brief asked for measurement rather than argument. **[MEAS]** Nazar's rim diameter,
identical settings, only the charm count changing:

| Charms on the cord | Nazar diameter | Relative |
|---|---|---|
| 1 | 118 device px (59.0 pt) | 100% |
| 2 | 110 device px (55.0 pt) | 93% |
| 3 | 97 device px (48.5 pt) | 82% |

**Windows scaling is dynamic.** **[SWIFT]+[WIN]** `CharmStackLayout` matches the Swift
line for line — same reference ruler, same three ceilings in the same order, same
`max(0, min(reference × radiusRatio × scale, ceiling))`.

**[INFER]** The reported symptom was most likely §1 in disguise: flat elliptical beads do
not rescale convincingly, so a three-charm rope read as badly proportioned when the
geometry was right. Recommend re-assessing now the beads are real.

**Not measured:** the macOS side of this table. Obtaining it means changing the charms on
your running macOS rope, which I did not do uninvited. Say the word and I will back up
the macOS settings, capture 1/2/3, and restore.

---

## 6. Sound — **deferred to v1.1**, audit only

**[BINARY]** 18 sound-related strings in the macOS binary. **[SWIFT]** `CharmSound` is an
enum over material classes — `.glass`, and others — carried per catalogue entry, not a
per-charm audio file.

**[WIN]** Ported state: `CharmSound` exists, every one of the 81 entries carries its
value, and `soundEnabled` / `soundVolume` are in the settings document *and* wired into
the Customize UI. **There is no playback and there are no audio assets.** Windows ships
a control that does nothing — a correctness problem, not a gap.

**[BINARY]** No audio files ship in the macOS bundle's `Resources` either, which means
macOS synthesises or uses system sounds. **[INFER]** Not determinable without the audio
source.

**Estimate:** 2 days if synthesised (material classes map to short synthesised strikes);
4–5 days if assets must be sourced or commissioned. **Blocked on** knowing which macOS
does — this is a question for you, not something the repository can answer.

---

## 7. Weather and Seasonal — **removed from the roadmap permanently**

> Settled 21 September 2026. Neither will be built for Windows at any version, and
> neither is counted as a parity gap. What follows records what macOS has. **[BINARY]**

Both are complete production features of the shipping release, not experiments:

**Weather** — `WeatherService`, `WeatherFetching`, `WeatherSettings`, `WeatherStatusRow`,
`CurrentWeather`, `WeatherCondition`, `WeatherMood`, `WeatherPlace`, `WeatherReading`,
`WeatheredImageCache`. User-facing copy: *"Checking the weather"*, *"Could not reach the
weather service."*, *"Set a location in Settings to use weather here."*, and a section
titled *"Live Weather and Seasonal Packs"*. Analytics event `weather_effect_toggled`
with the description *"Whether weather effects were switched on or off."*

**Privacy, already decided:** *"Your location. Weather uses a city name you can see and
change, and it is never sent here."* A city name, user-visible, user-editable, never
transmitted to analytics. PRIVACY.md will need this section.

**Seasonal** — `SeasonalCoordinator`, `SeasonalSettings`, `SeasonalPack`. **[SWIFT]**
`SeasonalPack.swift` and `SeasonalCharmCatalog.swift` are already in the reference, and
the seasonal charms are already in the Windows catalogue; what is missing is the
rotation logic, not the artwork.

**[INFER]** The visual effects themselves — what a "weathered" charm looks like — are not
determinable: `WeatheredImageCache` names the mechanism but the renderer is not in the
repository.

---

## 8. Parity matrix

| Feature | macOS | Windows | Gap |
|---|---|---|---|
| Beads | Artwork regions from SVG | **Artwork regions from SVG** | **Closed this round** |
| Shadows | Alpha-derived, shape-accurate | **Alpha-derived, shape-accurate** | **Closed this round**; parameters fitted, not read |
| Glow/halo | None over vector charms | **None** | **Closed this round** |
| SVG quality | Vector at target size | Vector at target size, 87% of ideal | Small; cause hypothesised only |
| Multi-charm layout | Dynamic | **Dynamic, measured** | None found |
| Library UI | Detail pane, pack cards, reorder, per-charm size | Grid, chips, search, favourites, recents | **Large** |
| About | Statistics, Secrets, UPI coffee, release notes | Links, analytics inspector | **Large** |
| Analytics | PostHog, 25 events | PostHog, 25 events, + inspector | None (Windows ahead) |
| Sound | Plays | Settings only, no playback | **Broken control** |
| Photo import | Vision subject extraction | None | **Missing** |
| Studio | Full staged pipeline | None | **Missing** |
| Weather | Production feature | None | **Missing** |
| Seasonal | Production feature | Artwork present, no rotation | **Missing** |
| Creator experience | UPI QR, secrets, statistics | Links | **Large** |
| Physics / rope styles / catalogue | — | — | Parity, 666 tests |

---

## 9. Priority

1. ~~**Sound playback**~~ — **v1.1.** The control was removed rather than left doing nothing
2. **Library detail sidebar** — data already exists for all 81 charms
3. **Collection pack cards** — catalogue already carries pack membership
4. **Reorder and per-charm size**
5. **About: statistics, secrets, UPI coffee, release notes**
6. **Photo import** without a model dependency
7. ~~**Weather and Seasonal**~~ — **removed from the roadmap permanently**
8. **Creator Studio** — **v1.1**; the Create tab is v1.0's answer

## 10. What is still inferred, and what would fix that

Four things above are models or hypotheses rather than readings, all because
`reference/swift/` carries no view layer:

- the shadow's parameters (fitted to measurements — §2)
- the residual 13% rasterisation gap (hypothesis — §4)
- what a weathered charm looks like (unknown — §7)
- every Library and About layout metric

Adding `CharmRenderer.swift`, the rope-style renderer, and the Library and About views
to `reference/swift/` would turn all of them into measurements.
