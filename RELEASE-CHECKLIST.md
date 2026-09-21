# Release checklist

The steps for cutting **any** release of Hangly for Windows, in order. It is a procedure,
not a discussion — the reasoning lives in `Docs/DISTRIBUTION.md`, and what must be true
before the *first* public release lives in `SHIP-CHECKLIST.md`.

Tick every box. A step that was skipped is written down as skipped, not left blank.

---

## 0. Before you start

- [ ] Working tree clean, on `main`, pushed.
- [ ] CI green on that exact commit — all three jobs: *Solver and models*, *App win-x64*,
      *App win-arm64*. Green CI is not evidence the app works; it is evidence it builds.
- [ ] `git log` since the last tag read through, so the changelog describes what shipped
      rather than what you remember shipping.

## 1. Write the release down

- [ ] Add a section to `CHANGELOG.md` at the top: `## X.Y.Z — D Month YYYY`.
- [ ] Write it for someone who uses Hangly, not for someone who wrote it. Bullets, plain
      words, no commit subjects.
- [ ] Check it renders as text: `./tools/release-notes.ps1 -Version X.Y.Z`. That is the
      exact text that reaches the release page **and** the app's About page.

## 2. Verify the build yourself

Green CI is not a release gate on its own — four fatal bugs in this project have passed
green builds. Run it.

- [ ] Publish and launch in the VM. The charm appears, swings, and settles.
- [ ] Library opens; search, a collection, a detail panel, a favourite.
- [ ] Create: a PNG and an SVG both become charms.
- [ ] About: version, milestones, the coffee sheet.
- [ ] `%APPDATA%\Hangly\hangly.log` has no `FAIL` lines.

## 3. Tag

The tag is the version. `v0.9.0` packages `0.9.0`; the workflow refuses anything that is
not `vMAJOR.MINOR.PATCH`.

- [ ] `git tag vX.Y.Z && git push origin vX.Y.Z`

## 4. Watch the workflow

`.github/workflows/release.yml`, two package jobs and one publish job.

- [ ] *Read the release notes* passes. If it fails, §1 was skipped — the workflow will not
      ship a release nobody described.
- [ ] Both `Package (win-arm64)` and `Package (win-x64)` succeed.
- [ ] *Check the feed names its own channel* prints `releases.win-arm64.json` and
      `releases.win-x64.json`. A feed called `releases.win.json` means the channel was not
      applied, and an ARM64 machine would be offered an x64 package.
- [ ] The draft release contains, for **each** architecture: `*-full.nupkg`,
      `*-Setup.exe`, `*-Portable.zip`, `RELEASES-*`, `releases.*.json`.

## 5. Check the draft before anyone can see it

The release is created as a **draft**. Nothing is public until you publish it.

- [ ] The notes on the page are the changelog section, plus the unsigned warning.
- [ ] Download the `Setup.exe` for this machine's architecture and install it.
- [ ] `%LOCALAPPDATA%\Hangly\current\Hangly.exe` → properties → **File version is X.Y.Z.0**,
      not something left over from a previous release.
- [ ] Settings from the previous version survived: name, rope, favourites, recents,
      imported charms, milestones.

## 6. Publish

- [ ] Undraft the GitHub release. This is the moment it becomes public.
- [ ] Update the download page on the website to point at the new `Setup.exe` links.

## 7. Verify the update path, from the outside

This is the step that cannot be done before publishing, and the one worth doing every
time.

- [ ] On a machine running the **previous** version, open the tray menu. Within a minute
      of launch it offers *Update to X.Y.Z…*.
- [ ] That line opens About with the release notes shown — the same words as the release
      page.
- [ ] *Install and restart* downloads, restarts, and comes back as X.Y.Z.
- [ ] Settings, imported charms and milestones all survived the update.

## 8. Afterwards

- [ ] `STATUS.md` updated: what is now verified, what is still not.
- [ ] If anything above was skipped or failed, write it into `STATUS.md` rather than
      leaving it to memory.
