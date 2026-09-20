# Status

As of the first run on real Windows hardware. Build health and feature completeness are
reported separately here, because they are separate questions and conflating them is how
a project convinces itself it is nearly finished.

- **Build:** green. All three CI jobs pass on `windows-latest`.
- **Runs:** yes — Windows 11 ARM64 at 200%. The rope hangs on the desktop, is transparent,
  and can be thrown.
- **Features:** roughly 71% ported. Charms, the Library, importing your own, customization, About and analytics are in.

---

## 1. Build health

| Job | Result |
|---|---|
| Solver and models (tests) | ✅ 423 / 423 passing on Windows |
| Customize window (UI smoke) | ✅ 63 / 63 checks, driven through UI Automation in the VM |
| App — `win-x64` Release | ✅ builds and publishes |
| App — `win-arm64` Release | ✅ builds and publishes |

### Build problems that were found and fixed

Every one of these came from code written on a Mac, where none of it could be compiled.

| Problem | Cause | Fix |
|---|---|---|
| Wrong package versions across the board | Guessed from memory. Windows App SDK is at **2.5.1**, not 1.6.x; Win2D 1.4.0; Svg.Skia 5.2.3; SkiaSharp 4.152.1 | Verified each against the NuGet API and pinned |
| `CanvasDashStyle.Custom` does not exist | Assumed an enum member by analogy | Setting `CustomDashStyle` is itself the switch; the `DashStyle` assignment was removed |
| `Microsoft.Graphics.Canvas.DirectX` not found | `DirectXPixelFormat` is a WinRT type | Corrected to `Windows.Graphics.DirectX.DirectXPixelFormat` |
| `WMC9999: Object reference not set` | **Not a XAML bug.** `WMC1509` showed MarkupCompilePass2 had no local assembly, which is what it reports when the C# compile it depends on already failed | Disappeared once the C# errors were fixed |
| `PRI175` / `PRI277` duplicate resource | `hangly.ico` was both `ApplicationIcon` (embedded) and `Content` (a file) | Ships once, embedded; the tray reads it back out of the executable |
| Warnings-as-errors broke the XAML compiler's own output | Generated code cannot satisfy the analyzers this repo turns on, and cannot be edited | Rule switched off for `Hangly.App` only; still enforced in `Hangly.Core` |
| Missing `using`, `using var` on a non-disposable | Ordinary mistakes no compiler had yet seen | Fixed |

### Build problems still possible

Nothing is failing. The three risks this section used to list have all been settled on a
real machine — Windows 11 ARM64 at 200% scaling:

- **Transparency.** Was wrong, and could not be fixed where it was written. A WinUI 3
  window owns an opaque redirection surface created with its HWND, and no XAML property
  replaces it, so the overlay drew a correct rope inside a white rectangle. The window
  layer is now a plain Win32 layered window; see PORTING.md §3.
- **The P/Invoke surface.** Exercised. Two real defects came out of it — the wrong export
  name for `Shell_NotifyIconW`, and a `NOTIFYICONDATA` declared short enough that the
  shell refused it silently — and both are fixed.
- **SkiaSharp's native binary on ARM64.** Loads, and rasterises the charm.

### Two rendering defects, found by looking and fixed

Both shipped green. Neither was caught by the test suite or by CI, and both were reported
as the Windows build looking worse than macOS rather than as bugs — which is what they
were. Full write-up in PORTING.md §3.

- **Artwork was soft on every display above 100%.** Charm rasters were sized in points
  while the surface was at the display's DPI, so at 200% each source pixel was drawn to
  four. Sized in device pixels now. Measured at the same on-screen size, mean gradient
  across the shield went from 24.3 to 36.6 per pixel and the peak from 121 to 397.
- **The rope swung out of its own window.** The canvas was narrower than the swing
  envelope, and `Resize` flung the rope whenever the anchor moved — 236 points of
  excursion on a 220-point canvas at launch. The canvas is now derived from the envelope,
  and a re-fit translates the rope with its anchor instead of yanking it. `EnvelopeTests`
  covers all ten rope styles × one to three charms × both sliders at 0.5, 1.0 and 2.0.

---

## 2. What works

- The **rope solver**, complete and verified: Verlet integration, relaxation to
  convergence, the one-sided stretch ceiling, charm separation, the bead pass, the cord
  curve, sleeping and waking, the fixed 240 Hz timestep.
- **Nine rope styles** and **three time-of-day profiles**, as data tables.
- **One, two or three charms** on one cord, with the layout guarantees intact.
- The **settings document**: tolerant decoding, clamping, atomic writes, one write path.
- **Placement geometry**, restated in Win32's coordinate convention and tested in it.
- Both architectures **publish as self-contained applications**.
- The **overlay**, confirmed by watching it rather than by inferring it: a transparent,
  click-through, always-on-top window; the charm grabbed, dragged, thrown and left to
  settle; the tray icon, its menu, and the menu's keyboard navigation.
- The **charm catalogue — all eighty-one**, generated from the Swift by
  `tools/generate-catalogue.py` rather than transcribed. Every one of them opens,
  rasterises and measures: `Hangly.exe --check-artwork` reports *81 measured, 0 missing,
  0 unmeasurable*, on both architectures.
- The **artwork splitter**, which is what makes a charm more than a picture: it finds the
  beads in the artwork's own silhouette and measures where the knot sits, so a rope at
  rest is laid out as the designer drew it. Charms are drawn cropped to their measured
  body, and their beads ride the cord as the solver's own particles.
- **One, two or three charms** chosen from the tray or the Customize window.
- The **Library**: all eighty-one charms grouped by pack, search that reaches names,
  places, materials and tags and folds accents so *pancha* finds *Pánchángjié*, filter
  chips for favourites, recents and all fourteen categories, starring, and an empty state
  that says which nothing it is.
- **Importing your own charm**: an SVG is checked, stripped of everything that is not
  drawing, measured by the same splitter the shipped charms use, and stored beside the
  settings where an update cannot reach it. It searches, stars, goes in recents, hangs in
  a stack of three and works with every cord.
- **About**, with the app icon read back out of the executable, the version and build, the
  copyright, links to the website, GitHub, the release notes and Instagram, and the
  coffee button.
- **Analytics**, with the macOS build's event names exactly, and the inspector
  `PRIVACY.md` promises: whether sharing is on, where it would go, the installation
  identifier masked, the last event, and how many have been sent.
- The **Customize window**: a WinUI settings window with a charm picker showing all
  eighty-one as artwork grouped by pack, the number on the cord, the cord itself with its
  description, size, reach and opacity, where it hangs, and the two behaviour switches.
  Every control writes straight through to the store and the rope changes as you watch.

## 3. What does not work

- **The window layer is new, and has one machine's worth of evidence behind it.** Launch,
  transparency, click-through, dragging and the tray menu have all been watched working on
  Windows 11 ARM64 at 200%. None of it has been seen at 100%, on x64, on a second display,
  or across a DPI change — and the overlay recomputes its scale only when it repositions.
- **Nothing is packaged or signed yet.** Velopack produces an installer that installs and
  runs — measured, not assumed — but no release has been cut, no feed is hosted, and
  SignPath has not been applied to. `Docs/DISTRIBUTION.md` has the decisions and the
  numbers.
- **A presented frame costs a read-back**, and that is now the honest cost rather than a
  suspected one. `UpdateLayeredWindow` wants the pixels in system memory, so each drawn
  frame is two 1.27 MB copies out of the render target. It allocates nothing: profiled
  over a thirty-second drag, allocation fell from 154 MB/s to 0.7 MB/s and gen-2
  collections from 1,040 to 7. What remains is memory bandwidth, and it is only spent
  while the rope is awake.
- **Beads are drawn as ellipses, not as sprites cut from the artwork.** Their number,
  size, position and weight are all measured from the artwork and are correct — the
  solver carries exactly the beads the designer drew. What differs from macOS is only
  how they are *painted*: a disc tinted with the cord's palette, rather than that part of
  the SVG. It reads well because a bead is a bead, and it is a parity gap all the same.
- **Imports are SVG only.** macOS imports photographs; this build does not. See
  PORTING.md — it is the largest single parity gap left.
- **There is no Studio**, and no macOS-style "open it in the Studio before it lands"
  step: an import goes straight into the Library.

## 4. What remains to be ported

Ordered by what unblocks the most.

| Subsystem | Swift lines | Notes |
|---|---:|---|
| **Charm Library** | ~1,800 | Browser, search, categories, favourites. |
| **Charm Studio** | ~2,400 | Editor, pipeline, undo stack. **Deferred: explicitly out of scope for v1.** |
| **Custom charm import** | ~1,200 | Image processor, store, dialogs. |
| **Weather** | ~700 | Open-Meteo client, moods, effects. |
| **Seasons** | ~600 | Seasonal packs and the coordinator. |
| **Sound** | ~500 | Synthesiser and per-material charm sounds. |
| **Menu bar artwork** | ~400 | The animated tray icon. Currently a static icon. |
| **Welcome, updates, analytics** | ~900 | |

## 5. Completion

**Roughly 71%** by weighted line count of the macOS source.

That number understates progress in one way and overstates it in another, and both are
worth saying:

- It **understates** it because the hardest and least forgiving part is finished. The
  solver is the piece where being approximately right is the same as being wrong, and it
  is done and asserted to 1e-9 against the original.
- It **overstates** it because the remaining 75% is mostly UI, and UI is where a port
  stops being a transcription and starts being design work. Those lines will not come as
  fast as the solver's did.

| | Ported |
|---|---|
| Physics | ~100% |
| App shell and services | ~40% |
| Models | ~60% |
| Views | ~52% |

## 6. Distribution

Decided and documented in `Docs/DISTRIBUTION.md`; measured on win-arm64 at 0.9.0.

| | |
|---|---|
| Velopack packages self-contained win-arm64 WinUI | ✅ installs, runs, renders |
| Installer download | 120.8 MB (from a 400.1 MB payload) |
| Delta to the next version | 0.2 MB |
| Settings survive installing over an existing copy | ✅ once moved out of the install directory |
| Signing | not started — SignPath needs a released artifact first |
| Trimming | not started |
| x64 package | never built |

## 7. Verified, and not

Build health and feature completeness are separate questions, and so are "it compiles"
and "somebody watched it work". This is the second list.

### Watched working

| | |
|---|---|
| Windows 11 ARM64 at 200% | ✅ the configuration everything below was seen on |
| Windows 11 ARM64 at 250% | ✅ incidentally, during a display-scaling incident |
| **x64, under ARM64 emulation** | ✅ PE machine AMD64, launches, renders, 81 charms measured |
| Transparency, click-through, drag, throw, settle | ✅ |
| Tray icon, menu, keyboard navigation | ✅ |
| All 81 charms load, rasterise and measure | ✅ |
| Charms drawn cropped to their measured body, beads on the cord | ✅ one, two and three at a time |
| Velopack install, run, and settings surviving an install-over | ✅ |

### Not verified, and not claimed

Deferred to a manual pass before a release, because automating display changes inside the
guest cost an incident once already and is not worth a second:

| | |
|---|---|
| 100% and 150% scaling | ❌ never seen |
| **Native x64 hardware** | ❌ emulation exercises the binary, not the silicon |
| Two monitors | ❌ the host has one display; not testable here |
| Monitor hot-unplug | ❌ the fallback exists in `DisplayObserver.DisplayAt` and is untested end to end |
| Wake from sleep | ❌ never tried |
| **Windows 10 1809**, the floor the manifest declares | ❌ never tried. Either test it or raise the floor; claiming it is the one option that is not available |
| A DPI change *while running* | ❌ and expected to be wrong — nothing handles `WM_DPICHANGED`, so `scale` is stale until something repositions the window |

Customize, About and analytics are covered by `tools/ui-smoke.ps1`, which drives the
built app through UI Automation inside the guest and checks the settings file afterwards.
It is not part of CI — CI has no desktop — so it is a command somebody runs, and the
rhythm is to run it whenever any of that changes.

### Analytics, verified

| | |
|---|---|
| Launches with sharing **off**: nothing captured, no identifier minted | ✅ |
| Launches with sharing **on**: identifier minted, two events, no more | ✅ |
| The launch is counted either way | ✅ |
| No duplicate events — `Start` is idempotent | ✅ asserted in tests; inspector read 2 after launch |
| No event spam: forty seconds idle, no events and no rewrite of the settings file | ✅ |
| The toggle switches off, discards the identifier and zeroes the counters | ✅ |
| Switching back on mints a **different** identifier | ✅ |
| No property describing the desktop appears on any event | ✅ asserted against a recording provider |

### The Library, verified

| | |
|---|---|
| All 81 shown, grouped by pack | ✅ |
| Search narrows the grid, and writes nothing to disk | ✅ asserted against the file, not inferred |
| Accent-folding: *pancha* → *Pánchángjié*, *boncugu* → *Nazar boncuğu* | ✅ unit tested |
| Category chips — all fourteen, none of them empty | ✅ a test fails if a chip would lead nowhere |
| Starring persists; favourites filter shows what was starred | ✅ |
| Hanging a charm records it as recent, newest first, no repeats | ✅ |
| Empty states for "no favourites", "nothing hung", "nothing matches" | ✅ |
| Filter survives closing and reopening the window | ✅ |
| Thumbnails rendered once to disk and reused across launches | ✅ 81 PNGs, not re-rendered on reopen |

### Startup, measured

Median of the app's own timestamps, from launch to the rope being on screen, over nine
runs on the ARM64 guest at 200%.

| | Wall clock | App internal |
|---|---|---|
| Before the Library | 317 ms | 236 ms |
| After the Library | 341 ms | 255 ms |
| After custom import | 331 ms | 268 ms |

**A real regression of about 19 ms, or 8%.** It is the generated catalogue getting bigger:
every charm now carries its region, description and tags, and the whole table is built the
first time anything touches it — which at launch is resolving the charms on the rope.

Not optimised, deliberately. The fix is to split the Library-only strings into a second
generated table that nothing touches until the Library opens, and 19 ms on a 255 ms
startup does not yet pay for a second table. Written down here so that if startup ever
does matter, the first place to look is known rather than guessed at.

Custom import added another **13 ms**, and would have added 31 ms: opening the imports
folder and parsing its manifest happened on every launch until it was made lazy. It now
happens only when a custom charm is on the rope or the Library is opened, so a person who
has imported nothing pays nothing for the feature. The 13 ms that remains was not
attributed to a specific cause — it is small, the medians are tight, and guessing at it
would be worse than saying so.

### Custom charm import, verified

| | |
|---|---|
| A valid SVG imports and appears without a restart | ✅ |
| Hostile SVG accepted as a drawing, with script, handlers, `foreignObject`, `@import` and external references stripped | ✅ 6 removals, and a check that none reached disk |
| XXE / entity-expansion refused outright | ✅ the DTD is prohibited |
| Empty, zero-byte, non-SVG and oversize files refused with a plain message | ✅ |
| Survives a restart, and an **installer upgrade** | ✅ file, manifest and settings all intact |
| Hangs in a stack of three, with any cord, under the real solver | ✅ screenshotted |
| Searchable, favouritable, recent, in its own "Yours" category | ✅ |
| Deleting removes the file, the rope reference and the favourite | ✅ |
| Same behaviour on **x64** under emulation | ✅ identical accept/reject on all six files |

### Known differences from macOS

| | |
|---|---|
| `macos_version` → `windows_version` | The same key holding a different kind of number would make the two datasets disagree about what the word means |
| Transport | Hand-written against PostHog's capture endpoint rather than their SDK. The macOS build wraps the SDK behind the same provider seam; here the wrapper was the whole job, and a file this size can be read to check what leaves |
| No batching | Each event is its own request. macOS lets the SDK queue; at a handful of events per session there is nothing to gain and a queue is something to lose on a crash |
| Events defined but never fired | `charm_imported`, `charm_saved`, `charm_reordered`, `weather_effect_toggled`, `collection_opened`, `collection_charm_selected`, `follow_popup_*`, `airdrop_*`, `coffee_copy_upi`, `coffee_qr_viewed` — the features do not exist yet. Named now so both platforms report the same act under the same name later |
| Import input format | **SVG here, photographs on macOS.** The biggest gap in this milestone; see PORTING.md |
| Import review step | macOS opens every interactive import in the Studio first. Here it goes straight into the Library |
| About page | One page, not the macOS four-band layout: no statistics, no secrets button, no creator card, no in-app release-notes or coffee sheets — both links open a browser |
| Library layout | A grid with chips above it. macOS has hero cards for the collections, a detail panel describing the charm you are reading about, a tag cloud, and a rope shelf with its own swatches — none of which are here. Choosing a charm hangs it; there is no "read about it without hanging it" |
| Charm metadata | macOS keeps physics in the Swift catalogue and Library facts in `CharmLibrary.json`. Here the generator merges both into one table by id, so a charm is described in exactly one place |
| Recently used | **An addition, not a port.** macOS has favourites and no recents |
| Rope composition | macOS has no count control at all — the number of charms is a consequence of how many places are filled. Here it is still a one-two-three picker |

## 8. Next milestone

Everything a person does with Hangly day to day now exists: install it, pick a charm,
decide how many hang, choose a cord, see what it is collecting and switch that off.

`PRIVACY.md` is in this repository and describes this build rather than the macOS one,
including the features it does not have. It should be updated **before** anything it
describes ships, not after.

What remains for v1, in the order that unblocks the most:

1. **Raster import** — the formats macOS actually accepts, and drag-and-drop from
   Explorer. The SVG path is done; the photograph path is the gap.
2. **Weather and seasons**, if they are kept at all — see the roadmap reassessment.
3. **Sound**, the welcome flow, the follow card, and the tray artwork.
4. **Packaging, signing and the v0.9.0 pre-release**, which is blocked on SignPath
   answering whether a pre-release satisfies "already released".

**Charm Studio is deferred — explicitly out of scope for v1.**
