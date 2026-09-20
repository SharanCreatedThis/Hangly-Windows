# Release readiness

Where the Windows port stands against a v1 release, 21 September 2026, at commit
`dc819fc`. Evidence classes as in VISUAL-PARITY.md.

---

## 1. How the percentages below are worked out

Two numbers, because they answer different questions and quoting one alone would
mislead.

- **v1 scope** — everything the app has to do to ship, *after* the 21 September scope
  cut removed weather, the seasonal pack and sound.
- **macOS feature parity** — the same measured against everything macOS 2.0.0 does,
  including the three features deliberately cut. This number can never reach 100% and is
  not meant to.

Areas are weighted by remaining effort, not by count: "Studio" is one row and six weeks.

## 2. v1 scope

| Area | State | Weight | Done |
|---|---|---|---|
| Solver, rope styles, catalogue | Parity, 674 tests | 15 | 100% |
| Overlay rendering | Beads, shadow, DPI, swing envelope all closed | 12 | 100% |
| Library | Grid, search, favourites, recents, chips, detail panel, collection cards | 12 | 85% |
| About and creator | Hero, creator card, milestones, secrets, coffee sheet | 8 | 100% |
| Analytics | 24 events, inspector; Windows ahead of macOS here | 6 | 100% |
| Custom import (SVG) | Import, sanitise, store, render | 8 | 100% |
| Settings and persistence | Survives updates, clamps, forward-compatible | 5 | 100% |
| Photo import | Not started | 10 | 0% |
| Studio | Not started | 12 | 0% |
| Welcome and follow flows | Not started | 4 | 0% |
| Drop a file on the charm | Not started; events already named | 3 | 0% |
| Packaging, signing, release | Architecture done, never exercised for real | 5 | 40% |

**v1 scope: 71% complete.**

Deferring Studio and photo import to 1.1 — see §6 — takes the same work to
**91% of a v1 that ships**.

## 3. macOS feature parity

Adding back the three cut features at their macOS weight (weather 8, seasonal 5,
sound 4, against a new total of 117):

**macOS feature parity: 64%.** It will not go higher than about **85%** while the three
cut features stay cut, and that is the intended outcome, not a shortfall.

## 4. Remaining visual differences

| Difference | Evidence | Severity |
|---|---|---|
| Cord has no cross-sectional shading | **[LIVE]** macOS cord reads as lit; Windows strokes are flat colour | Cosmetic, visible side by side |
| Rasterisation keeps 87% of a Lanczos reference | **[MEAS]** mean gradient 36.6 vs 42.1 | Barely visible; cause hypothesised only |
| Library spacing, radii and type scale | **[INFER]** — macOS view layer not in the repo | Cosmetic |
| No Ropes tab in the Library | **[LIVE]** macOS has a Charms/Ropes segmented control; Windows puts rope style on Appearance | Structural, not a defect |
| No drag-to-reorder, no per-charm size slider | **[LIVE]** both present in macOS's current-rope strip | Functional gap |

Closed this cycle: bead artwork, drop shadow, halo removal, DPI rasterisation, swing
envelope, launch whip, detail panel, collection cards, creator surfaces.

## 5. Remaining feature differences

| Feature | macOS | Windows | Note |
|---|---|---|---|
| Photo import | Vision subject extraction | None | §6 |
| Studio | Full staged pipeline | None | Gated on photo import |
| Welcome popup | Present **[BINARY]** | None | Small |
| Follow popup | Present **[BINARY]** | None | Small; 5 events already named |
| Drop a file on the charm | Present **[DOC]** | None | 3 events already named |
| Reorder / per-charm size | Present **[LIVE]** | None | Library |
| Weather, seasonal, sound | Present | **Cut from v1** | Deliberate |

Five analytics events remain defined but never fired: `airdrop_drag_entered`,
`airdrop_file_dropped`, `airdrop_picker_opened`, `charm_reordered`,
`follow_popup_*`. That is intentional — the names are reserved so both platforms report
the same act under the same name — but it should not grow.

## 6. Release blockers

Ordered by what actually stops a release.

### Blocking

1. **Nothing has ever been signed.** SignPath Foundation requires a released artifact
   before it will sign, so the first release is unsigned by construction and will raise
   SmartScreen. The enquiry drafted at `~/Documents/hangly-shots/signpath-enquiry.md`
   **has still not been sent**, and it gates everything downstream. This is the single
   longest lead time in the project and the least technical.
2. **The release pipeline has never produced a real release.** `vpk pack` works and an
   install-over preserves settings **[MEAS]**, but no tag, no feed, no update has ever
   been published or consumed.
3. **The manual verification matrix is unrun.** 100% and 150% scaling, native x64
   hardware, two monitors, hot-unplug, wake from sleep, and the Windows 10 1809 floor the
   manifest declares. The floor is the sharpest: it is claimed and never tested. Either
   test it or raise it — claiming it is the one option not available.

### Should fix before v1

4. **`WM_DPICHANGED` is not handled.** **[WIN]** Nothing in the window layer listens for
   it, so moving the overlay between displays of different scale, or changing scale while
   running, leaves `scale` stale until something else repositions the window. Known,
   never fixed, and now the most likely first-run bug on a multi-display machine.

### Not blocking

5. Studio and photo import. Large, self-contained, and nothing else depends on them.

## 7. Recommended path to v1

**Defer Studio and photo import to 1.1.** They are 22 of the 100 points of v1 scope and
roughly five to eight weeks; everything else is days. Shipping without them is a coherent
product — an ornament with seventy charms, a library, custom SVG import and a creator
page. Holding v1 for them costs two months for a feature most users will never open.

| # | Step | Effort | Blocks |
|---|---|---|---|
| 1 | **Send the SignPath enquiry** | an hour | everything |
| 2 | Handle `WM_DPICHANGED` | 1 day | — |
| 3 | Run the manual matrix; decide the OS floor | 1 day | the manifest's claim |
| 4 | Library: reorder and per-charm size | 2 days | — |
| 5 | Welcome and follow flows | 1 day | fires 5 named events |
| 6 | Drop a file on the charm | 1 day | fires 3 named events |
| 7 | Cut **v0.9.0 pre-release** on GitHub, unsigned | 1 day | SignPath's precondition |
| 8 | Apply to SignPath with that artifact | — | signed v1 |
| 9 | **v1.0**, signed, on the website | 1 day | — |

Steps 2–6 are six working days. Steps 1, 7 and 8 are calendar time, not work, and step 1
has been outstanding for weeks — **it is the critical path and it is one email**.

## 8. What is still inferred

- The shadow's parameters are fitted to measurements, not read from macOS source.
- The residual rasterisation gap has a hypothesis, not a cause.
- Library layout metrics are eyeballed from screenshots at a different scale factor.
- macOS's sound implementation is unknown — no audio ships in its bundle.

Adding `CharmRenderer.swift`, the rope-style renderer and the Library views to
`reference/swift/` would settle the first three.
