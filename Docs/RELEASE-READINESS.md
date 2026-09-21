# Release readiness

21 September 2026, recalculated after the release hardening audit
(`RELEASE-HARDENING-AUDIT.md`). Evidence tags: **[SWIFT] [DOC] [BINARY] [LIVE] [MEAS]
[WIN] [INFER]**.

**Roadmap, as settled on 21 September 2026.** Weather charms and seasonal charms are
**removed permanently** — not deferred, not backlogged, and not counted in any number on
this page. Sound is **deferred to v1.1**. The **Create tab is the v1.0 answer** to making
your own charm; **Creator Studio is a v1.1 item and is not a v1.0 blocker**.

---

## 1. How the numbers are reached

Areas are weighted by **remaining effort**, not counted — "Studio" is one row and six
weeks. Three numbers, because they answer different questions:

- **v1.0 completion** — what a shippable v1.0 needs. Creator Studio and photo-AI import
  are v1.1 and are not counted here.
- **Windows feature completion** — everything Windows intends to have eventually, which
  is v1.0 plus the v1.1 list.
- **macOS parity** — measured against the macOS feature set Windows intends to have.
  Weather and seasonal charms are **not** in the denominator: they were removed from the
  roadmap, so counting them would report a gap nobody intends to close.

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
| Analytics | Shared project, macOS names, HTTP 200 verified, inspector. One orphan event | 6 | 95% |
| Settings and persistence | Survives upgrade, verified end to end | 5 | 100% |
| Drop-on-charm | Implemented; shell drag not verifiable here | 4 | 80% |
| Packaging and update | Both channels package and publish, both feeds carry notes, a draft release exists; the live update path has never run | 10 | 85% |
| Signing | Not started, and blocked on a release existing | 6 | 0% |
| Manual QA matrix | Scaling, multi-monitor, Win10, sleep/wake — all unrun | 8 | 0% |
| Release hardening | Audited 21 Sep. Two blockers open: the tray icon does not survive an Explorer restart, and the Library costs 153 MB it never returns | 6 | 20% |

**v1.0: 80% complete.** It was 83% before the hardening audit; the audit added a weighted
area that is almost entirely open, and lowered analytics for the orphan event it found.

## 3. Windows feature completion

Adding the v1.1 list at its own weight — Creator Studio (12) and photo-AI import (10) —
against 128:

**Windows feature completion: 66%.**

## 4. macOS parity

Adding sound (4), the one deferred feature, against 132:

**macOS parity: 64%**, with a ceiling of **100%** once sound ships in v1.1.

The ceiling used to be quoted as 88%, because weather and seasonal charms were counted as
gaps. They are not gaps: they are not being built. Neither appears in this number again.

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

4. **The tray icon does not survive an Explorer restart.** Nothing handles the
   `TaskbarCreated` message, so the icon goes and does not come back — **[MEASURED]**,
   and the tray menu is the only way to reach Customize. Found by the hardening audit;
   the full account is in `RELEASE-HARDENING-AUDIT.md`.

### Should fix, not blocking

5. **A Library visit costs 153 MB of working set and gives none of it back** —
   **[MEASURED]**, three times the ~45 MB this page used to claim. macOS reclaims its
   share on close.
6. Downloads are ~120 MB. Deltas fix the repeat case at 0.24 MB, not the first one.
7. `app_quit` is posted fire-and-forget as the process exits, so it is unlikely ever to
   arrive. **[INFERRED]**
8. `collection_charm_selected` is defined and never fired.

## 6. Required before v1.0

| # | Task | Effort | Blocks |
|---|---|---|---|
| 1 | **Send the SignPath enquiry** | an hour | everything downstream |
| 2 | Tag and publish **v0.9.0**, both architectures | half a day | 3 |
| 3 | Verify a live update against the published feed | half a day | v1.0 |
| 4 | Run the manual QA matrix; settle the OS floor | 1 day | the manifest's claim |
| 5 | Re-add the tray icon on `TaskbarCreated` | an hour | the app being reachable |
| 6 | Release the Library's artwork when the window hides | half a day | — |

`FileVersion` from the build and the silent background update check were on this list and
are done.

Five working days of engineering. Items 1 and 2 are calendar time, and item 1 has been
outstanding for weeks.

## 7. Recommended scope

### v1.0 — ship this
Overlay and physics · 70 charms · Library with detail panel, hero cards, reorder and
per-place sizing · **Create** · SVG and raster import with the hostile corpus behind it ·
About, creator card, secrets, milestones, UPI coffee · onboarding with a display name ·
analytics into the shared project · auto-update · signed, once SignPath has answered.

### v1.1 — the next thing
**Creator Studio** · photo import with subject extraction · **sound** · releasing
interface artwork on close · trimming the download.

In-app release notes were on this list and shipped in v0.9.0.

### Removed permanently
**Weather charms** and **seasonal charms**. Both are complete, shipping macOS features.
Neither is a Windows roadmap item at any version: they are not deferred, not backlogged
and not counted as parity gaps. Nothing here is waiting for them.

## 8. What cannot be verified on this hardware

Unchanged, and still not claimed: dual monitor and mixed DPI (the host has one display),
scaling other than 200% (not automatable, and driving display APIs in the guest is
forbidden after the earlier incident), Windows 10 1809, sleep/wake, hot-unplug, and a
drag from Explorer onto the charm (an OLE modal loop that synthetic input cannot drive).

These are listed in STATUS.md §7 as unverified rather than assumed, and they are the
manual pass in §6 item 4.
