# Ship checklist

What must be true before Hangly for Windows is a thing a stranger can download.

This is the **one** list. `RELEASE-CHECKLIST.md` is the procedure for cutting any release;
`SIGNPATH-CHECKLIST.md` is the signing track, which runs on somebody else's calendar and
should be started first. Readiness numbers and their weighting live in
`Docs/RELEASE-READINESS.md`.

Evidence tags, used throughout the repository: **[SWIFT] [DOC] [BINARY] [LIVE] [MEAS]
[WIN] [INFER]**. A box is ticked when there is evidence, not when there is confidence.
*Green CI is not evidence that the app works* — four fatal bugs in this project have
passed green builds.

---

## 1. Done, with evidence

These are closed. They are listed so that the open list below is read as the whole of what
is left.

- [x] **Physics, rope styles and the catalogue** — 70 charms, 759 tests. **[LIVE]**
- [x] **Overlay rendering** — artwork beads, shape-accurate shadow, cord shading, device-pixel
      rasterisation, the swing-and-drag envelope, always-on-top re-asserted. **[MEAS]**
- [x] **Library** — grid, search, detail panel, hero cards, reorder, per-place sizing,
      favourites, recents. **[LIVE]**
- [x] **Create** — a PNG, a JPG or an SVG becomes a charm through the existing import path. **[LIVE]**
- [x] **Import security** — a 42-file hostile corpus, two real defects found and fixed. **[MEAS]**
- [x] **Onboarding** — welcome with a mandatory display name, follow card. **[LIVE]**
- [x] **Analytics** — the shared PostHog project, macOS event names kept exactly, HTTP 200
      verified against production. **[LIVE]**
- [x] **Persistence across an upgrade** — settings, favourites, recents, imported artwork and
      milestones all survive an install over the top. **[LIVE]**
- [x] **Packaging** — both architectures pack; install, upgrade and uninstall all run. **[LIVE]**
- [x] **Update plumbing** — per-architecture channels, a silent check twenty seconds after
      launch, release notes carried from `CHANGELOG.md` into the package and onto the
      About page, `FileVersion` derived from the package version. **[LIVE]** for everything
      except the live fetch, which is §2.1.
- [x] **Public-beta hardening** — one instance at a time, and a tray icon that survives an
      Explorer restart. **[MEASURED]**, `tools/hardening-checks.ps1`.

## 2. Blocking the first public release

### 2.0 The release hardening audit's blockers

`RELEASE-HARDENING-AUDIT.md`, 21 September 2026. Its verdict is **no-go until these four
are done**, and its reasoning is not repeated here.

- [x] **B1** — nothing stops a second copy running, and the two overwrite each other's
      settings. **Fixed** at `ae7b927`; `tools/hardening-checks.ps1` proves it.
- [x] **B2** — the tray icon does not survive an Explorer restart, which leaves the app
      running with no way to reach its own menu. **Fixed** at `ae7b927`; same harness.
- [ ] **B3** — the existing `v0.9.0` draft predates the rendering fixes and both blocker
      fixes; delete it and re-cut. **[MEASURED]**
- [x] **B4** — `PRIVACY.md` and `Docs/DISTRIBUTION.md` described an update check that
      fetches a static file; the code asks the GitHub Releases API. **Corrected.**


### 2.1 The update path has never run against a published feed

Everything either side of it is verified. `UpdateManager` has never fetched a real
release, because none exists — which makes publishing v0.9.0 the next action rather than a
later one.

- [ ] Publish **v0.9.0**, both architectures, following `RELEASE-CHECKLIST.md`.
- [ ] Install 0.9.0 from the published `Setup.exe`.
- [ ] Publish **v0.9.1**.
- [ ] The installed 0.9.0 offers *Update to 0.9.1…* in the tray, within a minute of launch,
      without interrupting anything.
- [ ] That line opens About showing the 0.9.1 release notes.
- [ ] *Install and restart* completes and comes back as 0.9.1.
- [ ] Settings, imported charms and milestones survive it. **[LIVE]**

### 2.2 Signing has not been started

- [ ] **Send the SignPath enquiry.** It has been drafted for weeks and is still unsent.
      `SIGNPATH-CHECKLIST.md` §3. Nothing downstream can start until it goes.

The first release ships unsigned and SmartScreen will warn. That is expected and is
written on the download page, not hidden.

### 2.3 The manual QA matrix is unrun

A once-per-release manual pass, run by hand on the VM's own display settings. **Do not
automate this and do not probe display APIs from inside the guest.**

- [ ] 100%, 125%, 150%, 175% scaling — the charm is sharp, and the swing envelope does not
      clip at the extremes.
- [ ] Dual monitor, and mixed DPI between the two.
- [ ] Sleep and wake.
- [ ] Monitor hot-unplug while the charm is on the second display.
- [ ] Drag a file from Explorer onto the charm.
- [ ] **Windows 10 1809.** The manifest claims it and nothing has ever tested it. Either
      test it or raise the floor — a claimed minimum nobody has run is a promise to
      strangers.
- [ ] x64: install and run the x64 package. It builds and packs; it has never been
      installed. **[WIN]**

### 2.4 The download page

- [ ] Links to the `Setup.exe` for **both** architectures, labelled so someone knows which
      they want.
- [ ] Says what the app does — SignPath requires a download page that describes the
      functionality.
- [ ] Says plainly that the build is unsigned and what Windows will show, rather than
      promising a clean install.
- [ ] Links `PRIVACY.md`.

## 3. Not blocking, and deliberately so

Recorded so they are decisions rather than oversights.

- [ ] **Download size.** 120 MB the first time, 0.24 MB every time after. A
      first-impression problem only. `PublishTrimmed` is **not** safe without proof: WinUI
      and Win2D reach for types through reflection and COM activation, and a trimmed build
      that launches can still fail on a path nobody exercised until a user did.
- [ ] **A Library visit costs 153 MB of working set and 599 handles and gives none of it
      back** — **[MEASURED]**, `RELEASE-HARDENING-AUDIT.md` §5.3. macOS reclaims its
      share on close.
- [ ] **Uninstall leaves `%APPDATA%\Hangly`**, so settings survive an uninstall and
      reinstall. Whether that is wanted is a decision nobody has made.
- [ ] **Launch at login across an update** — the registry entry should name Velopack's
      stub, which is stable across versions, rather than `current\`. Unverified. **[INFER]**

## 4. Out of scope for v1.0

Not missing. Removed, or scheduled.

- **Weather and seasonal charms** — **removed from the roadmap permanently.** Complete,
  shipping macOS features, and not Windows features at any version. No placeholder,
  disabled, hidden or dormant code remains.
- **Sound** — **v1.1.** Removed from the source rather than left as a control that does
  nothing, so bringing it back is a port.
- **Creator Studio and photo subject extraction** — **v1.1.** The **Create** tab is
  v1.0's answer to making your own charm and is finished; the Studio is not a v1.0
  blocker.

## 5. The one-line test

Before publishing anything: *has it been run on Windows and looked at?*

If the answer is "it compiles" or "CI is green", it is not ready. Run it, look at it, then
say so.
