# Status

As of the first run on real Windows hardware. Build health and feature completeness are
reported separately here, because they are separate questions and conflating them is how
a project convinces itself it is nearly finished.

- **Build:** green. All three CI jobs pass on `windows-latest`.
- **Runs:** yes — Windows 11 ARM64 at 200%. The rope hangs on the desktop, is transparent,
  and can be thrown.
- **Public beta:** the two blockers the hardening audit found — a second copy running
  alongside the first, and a tray icon that did not survive an Explorer restart — are
  **fixed and verified** (`ae7b927`, `tools/hardening-checks.ps1`).
- **Features:** **v1.0 is 83% complete** — see `Docs/RELEASE-READINESS.md` for the
  weighting. Charms, the Library, Create, importing your own, customization, About,
  onboarding, analytics and auto-update are in. Signing, the manual QA matrix and the
  hardening findings are what is left.
- **Roadmap, settled 21 September 2026:** weather and seasonal charms are **removed
  permanently**. Sound is **v1.1**. The **Create tab is the v1.0 answer** to making your
  own charm and **Creator Studio is v1.1**, not a v1.0 blocker.

---

## 1. Build health

| Job | Result |
|---|---|
| Solver and models (tests) | ✅ 751 / 751 passing on Windows |
| Customize window (UI smoke) | ✅ 91 / 91 checks, driven through UI Automation in the VM |
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
- **The rope swung out of its own window.** The canvas was narrower than the envelope,
  and `Resize` flung the rope whenever the anchor moved — 236 points of excursion on a
  220-point canvas at launch. The canvas is now derived from the **drag** reach, which is
  what bounds how far the charm can go: sizing for the release angle was a first attempt
  and still left the charm cut in half at the end of a drag. A re-fit translates the rope
  with its anchor instead of yanking it. `EnvelopeTests` covers all ten rope styles × one
  to three charms × both sliders at 0.5, 1.0 and 2.0, for both the release-and-settle and
  the full drag circle. Cost: idle is unchanged; a sustained drag goes from 9.4% to 19.8%
  of one core at the shipped settings, 27.9% at both sliders maxed.

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
- The **charm catalogue — all seventy**, generated from the Swift by
  `tools/generate-catalogue.py` rather than transcribed. Every one of them opens,
  rasterises and measures: `Hangly.exe --check-artwork` reports *81 measured, 0 missing,
  0 unmeasurable*, on both architectures.
- The **artwork splitter**, which is what makes a charm more than a picture: it finds the
  beads in the artwork's own silhouette and measures where the knot sits, so a rope at
  rest is laid out as the designer drew it. Charms are drawn cropped to their measured
  body, and their beads ride the cord as the solver's own particles.
- **One, two or three charms** chosen from the tray or the Customize window.
- The **Library**: all seventy charms grouped by pack, search that reaches names,
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
  seventy as artwork grouped by pack, the number on the cord, the cord itself with its
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
- **A Library visit costs 153 MB of working set and returns none of it.** The window
  hides rather than closes, deliberately, but the cost of that decision is three times
  what was previously recorded. **[MEASURED]**
- **There is no Creator Studio**, and no macOS-style "open it in the Studio before it
  lands" step: an import goes straight into the Library. That is the v1.0 design — the
  **Create** tab is how you make a charm here — and the Studio is a v1.1 item.

## 4. What remains to be ported

Everything v1.0 needs is written. What is left is one piece of macOS's app that v1.0 does
not need, one that is deferred, and one small thing.

| Subsystem | Swift lines | When |
|---|---:|---|
| **Creator Studio** | ~2,400 | **v1.1.** Editor, pipeline, undo stack. The Create tab is v1.0's answer and is finished. |
| **Photo import with subject extraction** | ~600 | **v1.1.** Raster import works; macOS's subject cut-out does not exist here. |
| **Sound** | ~500 | **v1.1.** Deferred deliberately; no playback has ever been written. |
| **Menu bar artwork** | ~400 | Unscheduled. The animated tray icon; currently static. |

Weather and seasonal charms are not on this list and will not be. See the scope section
at the end.

## 5. Completion

**v1.0 is 83% complete** by the weighting in `Docs/RELEASE-READINESS.md`, which is the one
place these numbers are worked out. By weighted line count of the macOS source the port is
**roughly 84%**.

Line count is the weaker of the two numbers and is kept only because it is comparable
with where this started. What is left of it is Creator Studio, which is a v1.1 item, so
the line-count figure can stall at 84% without v1.0 being any further away.

| | Ported |
|---|---|
| Physics | ~100% |
| App shell and services | ~90% |
| Models | ~95% |
| Views | ~80% |

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
| **x64, under ARM64 emulation** | ✅ PE machine AMD64, launches, renders, 70 charms measured |
| Transparency, click-through, drag, throw, settle | ✅ |
| Tray icon, menu, keyboard navigation | ✅ |
| All 70 charms load, rasterise and measure | ✅ |
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
| A file dragged from Explorer onto the charm | ⚠️ implemented, end-to-end drag not exercised. `RegisterDragDrop` succeeds and the replacement is unit-tested, but an OLE drag is a shell-driven modal loop that synthetic input cannot complete — two attempts hung on `DoDragDrop`. Manual-pass item |
| A DPI change *while running* | ⚠️ handled, never exercised. `WM_DPICHANGED` refits the overlay, and the scale is also compared against the window's own DPI once a second so a missed message cannot leave it stale. Neither path can be triggered from outside the process — Windows refuses a synthetic `WM_DPICHANGED` (`PostMessage` → ERROR_MESSAGE_SYNC_ONLY, `SendMessage` dropped) — so this needs the manual scaling pass |

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
| Events defined but never fired | **One: `collection_charm_selected`.** Found by the hardening audit; the other twenty-four all have a call site. This row used to claim every event had one |
| Import input format | SVG, PNG and JPG here. macOS additionally cuts the subject out of a photograph; that is a v1.1 item |
| Import review step | macOS opens every interactive import in the Studio first. Here the **Create** tab is the review step, and an import from the menu or a drop goes straight into the Library |
| About page | Hero, statistics, secrets, creator card, milestones, the UPI coffee sheet and in-app release notes are all there now. This row used to say none of them were |
| Library layout | Grid, search, collection hero cards, a detail panel and reorder are all there now. macOS's tag cloud and rope shelf are not |
| Charm metadata | macOS keeps physics in the Swift catalogue and Library facts in `CharmLibrary.json`. Here the generator merges both into one table by id, so a charm is described in exactly one place |
| Recently used | **An addition, not a port.** macOS has favourites and no recents |
| Rope composition | macOS has no count control at all — the number of charms is a consequence of how many places are filled. Here it is still a one-two-three picker |

## 8. Next milestone

**Release hardening, and nothing else.** Everything a person does with Hangly day to day
exists: install it, pick a charm, decide how many hang, choose a cord, make one of their
own, see what it is collecting and switch that off. The remaining work is making sure it
holds up, not adding to it.

`PRIVACY.md` is in this repository and describes this build rather than the macOS one,
including the features it does not have. It should be updated **before** anything it
describes ships, not after.

What remains for v1.0, in the order that unblocks the most:

1. **Publish v0.9.0 and verify a live update against the published feed.** Nothing else
   can prove the update path, because there has never been a release to update from.
3. **Send the SignPath enquiry**, which has been drafted and unsent for weeks and is the
   longest lead time in the project.
4. **The manual QA matrix** — scaling, multi-monitor, sleep/wake, and the Windows 10 1809
   floor the manifest claims and nothing has tested.

**Creator Studio, photo subject extraction and sound are v1.1.** None of them is a v1.0
blocker.

---

## Scope: what this build deliberately does not have

Cut on 21 September 2026, and cut rather than hidden. There is no disabled code, no
feature flag and no dormant branch for any of these — they are gone from the source, the
settings document, the analytics table, the tests and the docs.

**Weather and seasonal charms are removed permanently.** They are not deferred to a later
version, they are not in any backlog, and they are not counted as parity gaps. **Sound is
deferred to v1.1** — the code below was still removed, so bringing it back is a port and
not an un-hiding.

| Cut | What went with it |
|---|---|
| **Weather** | The `weather_effect_toggled` analytics event, and the reference to a weather city in the privacy copy. Nothing else existed; the feature was never ported. |
| **Seasonal packs** | The `seasonal` category, and the eleven charms filed under it — Snowflake, Bell, Candy Cane, Pumpkin, Ghost, Bat, Diya, Lotus, Lantern, Firework and Lucky Coin — with their artwork. The catalogue is **70 charms**, not macOS's 81. |
| **Sound** — *deferred to v1.1, not removed* | `CharmSound`, the `Sound` field on every catalogue entry, and `SoundEnabled` / `SoundVolume` in the settings document. No playback had ever been written. |

The generator is where the catalogue difference is expressed: `SOURCES` no longer reads
`SeasonalCharmCatalog.swift` and `DROPPED_CATEGORIES` names the category. `reference/swift/`
is untouched, as always — it still describes the macOS app, which still has all eighty-one.

**Settings written by an older build** still load. Unknown keys have always been ignored,
so `soundEnabled`, `soundVolume` and anything seasonal are dropped on read; a rope
carrying a deleted charm falls back to the bead, which is the same path a deleted import
already took.
