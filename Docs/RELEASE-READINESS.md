# Release readiness

21 September 2026, at commit `8f7249f`. Evidence tags: **[SWIFT] [DOC] [BINARY] [LIVE]
[MEAS] [WIN] [INFER]**.

---

## 1. How the numbers are reached

Areas are weighted by **remaining effort**, not counted — "Studio" is one row and six
weeks. Three numbers, because they answer different questions:

- **v1.0 completion** — what a shippable v1.0 needs, with Studio and photo-AI out of scope.
- **Windows feature completion** — everything Windows intends to have eventually.
- **macOS parity** — measured against everything macOS 2.0.0 does, including the three
  features deliberately removed. This one can never reach 100% and is not meant to.

## 2. v1.0 completion

| Area | State | Weight | Done |
|---|---|---|---|
| Solver, rope styles, catalogue | 70 charms, 751 tests | 10 | 100% |
| Overlay rendering | Artwork beads, shape shadow, cord shading, DPI, swing envelope, always-on-top | 12 | 100% |
| Library | Grid, search, detail panel, hero cards, reorder, per-place size, favourites, recents | 12 | 100% |
| Create | PNG/JPG/SVG → charm, via the existing import path | 8 | 100% |
| Import + security | 42-file hostile corpus, two defects fixed, filesystem guarded | 8 | 100% |
| About and creator | Hero, creator card, milestones, secrets, UPI coffee sheet | 6 | 100% |
| Onboarding | Welcome with mandatory name, follow card | 5 | 100% |
| Analytics | Shared project, macOS names, HTTP 200 verified, inspector | 6 | 100% |
| Settings and persistence | Survives upgrade, verified end to end | 5 | 100% |
| Drop-on-charm | Implemented; shell drag not verifiable here | 4 | 80% |
| Packaging and update | Packages, installs, upgrades, uninstalls; updater written, feed unpublished | 10 | 70% |
| Signing | Not started, and blocked on a release existing | 6 | 0% |
| Manual QA matrix | Scaling, multi-monitor, Win10, sleep/wake — all unrun | 8 | 0% |

**v1.0: 83% complete.**

## 3. Windows feature completion

Adding Studio (12) and photo-AI import (10) at their own weight, against 122:

**Windows feature completion: 69%.**

## 4. macOS parity

Adding weather (8), seasonal (5) and sound (4) back, against 139:

**macOS parity: 61%**, with a ceiling of about **88%** while those three stay removed.
That ceiling is the intended outcome, not a shortfall.

## 5. Remaining blockers

### Blocking v1.0

1. **The update path has never run against a published feed.** Everything either side of
   it is verified — packaging, install, upgrade, uninstall, delta generation — but
   `UpdateManager` has never fetched a real release, because none exists. **It cannot be
   tested until v0.9.0 is published**, which makes publishing v0.9.0 the next action, not
   a later one.
2. **Nothing is signed, and SignPath will not sign a project that has not released.** The
   first release is unsigned by construction and SmartScreen will warn. The enquiry
   drafted at `~/Documents/hangly-shots/signpath-enquiry.md` **has still not been sent**;
   it is the longest lead time in the project and the least technical thing on this list.
3. **The manual QA matrix is unrun**: 100/125/150/175/200% scaling, dual monitor, mixed
   DPI, sleep/wake, hot-unplug, Windows 10 1809. The 1809 floor is the sharpest — the
   manifest claims it and nothing has ever tested it. Either test it or raise it.

### Should fix, not blocking

4. `FileVersion` is hard-coded to 2.0.0.0, so a 0.9.0 package reports 2.0.0.0. One line.
5. A Library visit costs ~45 MB and is never released; macOS reclaims its share on close.
6. Downloads are ~120 MB. Deltas fix the repeat case at 0.24 MB, not the first one.

## 6. Required before v1.0

| # | Task | Effort | Blocks |
|---|---|---|---|
| 1 | **Send the SignPath enquiry** | an hour | everything downstream |
| 2 | Tag and publish **v0.9.0**, both architectures | half a day | 3 |
| 3 | Verify a live update against the published feed | half a day | v1.0 |
| 4 | Run the manual QA matrix; settle the OS floor | 1 day | the manifest's claim |
| 5 | `FileVersion` from the build | minutes | — |
| 6 | Silent background update check | half a day | — |

Five working days of engineering. Items 1 and 2 are calendar time, and item 1 has been
outstanding for weeks.

## 7. Recommended scope

### v1.0 — ship this
Overlay and physics · 70 charms · Library with detail panel, hero cards, reorder and
per-place sizing · **Create** · SVG and raster import with the hostile corpus behind it ·
About, creator card, secrets, milestones, UPI coffee · onboarding with a display name ·
analytics into the shared project · auto-update · signed, once SignPath has answered.

### v1.1 — the next thing
Studio · photo import with subject extraction · in-app release notes · releasing interface
artwork on close · trimming the download.

### v2.0 — the ones that were cut
Weather · seasonal packs · sound. All three are complete, shipping macOS features and all
three were removed from v1 deliberately. They return when there is a reason, not because
macOS has them.

## 8. What cannot be verified on this hardware

Unchanged, and still not claimed: dual monitor and mixed DPI (the host has one display),
scaling other than 200% (not automatable, and driving display APIs in the guest is
forbidden after the earlier incident), Windows 10 1809, sleep/wake, hot-unplug, and a
drag from Explorer onto the charm (an OLE modal loop that synthetic input cannot drive).

These are listed in STATUS.md §7 as unverified rather than assumed, and they are the
manual pass in §6 item 4.
