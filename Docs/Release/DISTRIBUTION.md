# Distribution

How a Windows copy of Hangly is built, packaged, installed, updated and signed — and
which of those are decided, which are measured, and which are still open.

Nothing here is implemented as a release process yet. The application is *architected*
for it: every decision below is either already reflected in the code or is a number the
release workflow will need. Publishing itself comes later.

---

## 1. The shape

| | |
|---|---|
| Channel | Direct download from the website, as on macOS. No Microsoft Store. |
| Installer | Velopack — one tool produces the installer, the update feed and the deltas |
| Signing | SignPath Foundation (free, open-source). No paid certificate, ever. |
| Packaged? | No. Unpackaged, self-contained, per-user. |

The Store is not a target, so nothing here is shaped by packaging identity, and
`WindowsPackageType=None` stays. `EnableMsixTooling` stays regardless — it owns PRI
generation and the compiled XAML lives inside the PRI, so removing it produces an app
with no XAML at all. That is not a packaging decision; it is load-bearing.

---

## 2. Velopack

### Where the app installs

Velopack installs per-user, with no elevation, to:

```
%LOCALAPPDATA%\Hangly\
    Hangly.exe          a stub that launches the current version
    Update.exe          the updater
    current\            the application
    packages\           the downloaded package for the installed version
```

Per-user and unelevated is the right default for an ornament: it means a stranger can
install it without an administrator, and uninstall removes one directory.

### Where the app's own data lives

```
%APPDATA%\Hangly\
    settings.json
    hangly.log
```

**Roaming, and deliberately not `%LOCALAPPDATA%\Hangly`.** That is Velopack's install
directory, and installing over an existing copy clears it. This was measured, not
reasoned about: the first spike installed over a running copy and the settings file was
gone afterwards — not moved, not backed up, gone. Preferences do not live inside the
program that reads them.

It is verified rather than assumed. A settings file written with a recognisable value,
an install of a later version over the top, and the same value still there afterwards and
still being read by the app.

There is no migration from the old location and there does not need to be: nothing has
ever been released, so no copy of Hangly for Windows has ever written one.

**Roaming has one consequence worth writing down.** Settings follow the user to another
machine, and that includes `displayIndex` — so a charm configured on the second monitor of
a two-monitor desk will be asked for on a laptop that has one. It degrades safely:
`DisplayObserver.DisplayAt` falls back to the primary display when the index is out of
range, which is the same path a monitor being unplugged takes. It is written down here so
that a later report of "my charm is on the wrong monitor" is read as roaming doing exactly
what it says, and not as a bug in placement.

### Versions and channels

| | |
|---|---|
| Scheme | SemVer, `MAJOR.MINOR.PATCH`, matching the macOS build's user-facing version |
| Channel | one per architecture: `win-arm64`, `win-x64` |
| Feed | `releases.{channel}.json` plus `.nupkg` files, served as static files |

One channel per architecture, because a channel is a single line of releases and an ARM64
machine must never be offered an x64 package. The channel name is part of the feed's file
name, so the two live side by side in one directory.

The feed is **static files over HTTPS**, published as GitHub release assets. There is no
Hangly server, which is what keeps the update check anonymous. See §4.

### Pre-releases, and the flag that has to change at v1.0

`Updater.Manager()` builds its `GithubSource` with **`prerelease: true`**, and that has to
be revisited before v1.0 ships.

With it false, Velopack asks GitHub for `/releases/latest`. That endpoint **excludes
pre-releases** and returns **404** when every release is one — which Velopack raises as an
exception, so the update check does not report "nothing new", it fails. Measured the day
v0.9.0 was published as a pre-release: `update check failed: HttpRequestException`, on
every launch.

With it true the source enumerates `/releases`, which lists everything, and the highest
version wins — so a stable v1.0 is still preferred over any 0.9.x beta.

**The cost arrives later.** Once people are running a stable release, publishing any
pre-release will offer it to them. Before v1.0 either stop publishing pre-releases, or set
the flag back to false — by which time there will be a stable release for
`/releases/latest` to find, and the 404 that forced this cannot happen.

### Sizes, measured

Measured on win-arm64, 0.9.0, before any trimming:

| | |
|---|---|
| Published payload | 400.1 MB across 610 files |
| Installer download | **120.8 MB** |
| Installed on disk | 407.5 MB |
| Delta to next version | **0.2 MB** |

Two things follow. The first download is 120.8 MB rather than 400 MB, because the
installer is compressed — the 400 MB figure is the on-disk number, not the number that
decides whether somebody waits.

**The delta figure does not apply to releases built by CI, and that was measured the hard
way.** `vpk pack` computes a delta by diffing against the previous package *in its output
directory*, and the release workflow starts every run in a clean checkout with an empty
`releases/`. There is nothing to diff against, so each release carries a full package and
no delta. The v0.9.0 → v0.9.1 update downloaded **122,864,582 bytes** from GitHub with
`deltas to target: 0`, taking 68 seconds. The 0.2 MB delta measured earlier came from
packing two versions into the same directory by hand, which is what CI does not do.

Fixing it means fetching the previous release's `.nupkg` into `releases/` before the pack
step so `vpk` has a base. Worth doing before there are many users; it is not a
correctness problem, only a size one, and it is written here rather than left as a
pleasant assumption.

Most of the 400 MB is satellite locale directories (`af-ZA`, `am-ET`, `ar-SA`, …) and
unused runtime. Trimming is tracked in §5 and has not been done.

---

## 3. Signing

SignPath Foundation signs open-source releases at no cost. Their conditions, from their
own terms:

- an OSI-approved licence with no commercial dual-licensing — **MIT, present as a
  `LICENSE` file**;
- a public repository — **yes**;
- the project must **already be released in the form that should be signed**;
- actively maintained, functionality described on its download page;
- no malware or security-circumvention tools;
- the binary must be a verifiable automated build from the source in that repository,
  which for GitHub means the SignPath GitHub App is installed and the artifact is a
  workflow artifact rather than something uploaded by a token holder;
- **every release needs manual approval** before it is signed.

The last two shape the release workflow: signing is a step *inside* the GitHub Actions
run that produced the artifact, and a human approves it. `vpk pack` already has the seam
for it — `--signTemplate` takes a command with `{{file}}` substituted, and when it is
absent the pack logs exactly what it skipped:

```
[WRN] No signing parameters provided, 289 file(s) will not be signed.
```

So signing drops into the existing pack step rather than restructuring it.

**Open question — does a GitHub pre-release satisfy "already released"?** SignPath's terms
and their GitHub integration documentation are both silent on pre-releases. This is not
something to assume in either direction; it needs asking them directly before the release
plan depends on the answer.

### What users see before reputation exists

SignPath issues **OV** certificates. Since 2024 an EV certificate no longer bypasses
SmartScreen either, so nothing cheaper is being settled for — but signing is not instant
trust:

- A signed download from a publisher with no history still shows *"Windows protected your
  PC"*, with the verified publisher name displayed.
- Reputation accrues against a consistent signing identity, across versions, as downloads
  accumulate. Microsoft gives no threshold and describes it as weeks.
- An unsigned download shows the same warning but with no publisher name, and builds no
  reputation at all — every new version starts from zero.

The download page should say so plainly rather than promise a clean install on day one.

---

## 4. The privacy promise

`PRIVACY.md` on macOS says the update check "carries no identifier". Velopack honours
that, and it was checked rather than assumed — from their FAQ:

> "The Velopack runtime library and the binaries shipped with your application
> (Setup.exe, Update.exe) collect no telemetry, analytics, or tracking data."

There is nothing for an identifier to be sent *to*, because there is no Hangly server.
The check asks **GitHub's public releases API** which releases exist for this repository,
then requests the feed asset for this build's channel — `releases.win-arm64.json` or
`releases.win-x64.json`. GitHub sees a request for a public file with an IP address, as it
does for anyone reading the repository in a browser.

**It is the API and not a plain static URL, and that is not an accident.** Release assets
live under a tag, so there is no fixed path a feed can be fetched from; handing the
repository URL to `UpdateManager` as a string builds a `SimpleWebSource` that would
request `github.com/SharanCreatedThis/Hangly-Windows/releases.win-arm64.json`, which has
never existed. `Updater.Manager()` builds a `GithubSource` for exactly this reason. Both
this document and `PRIVACY.md` described the static-file version for a while after the
code stopped doing it; the hardening audit caught the mismatch.

One scoped exception, which does not touch the promise: the `vpk` command-line tool
performs its own update check when it runs. That is a developer tool on a developer's
machine and is never shipped to a user.

---

## 5. Still open

- **Trimming.** 400 MB on disk, mostly locale satellites and unused runtime. The safe
  wins go first. `PublishTrimmed` is *not* safe here without proof: WinUI and Win2D reach
  for types through reflection and COM activation, and a trimmed build that launches
  correctly can still fail on a path nobody exercised until a user did. Anything trimmed
  must be exercised through the whole app in the VM, not merely launched.
- **x64.** Everything measured here is ARM64. The x64 package **is built and published**
  — both architectures come out of the release workflow and both feeds are in the draft —
  but it has never been installed or run.
- **Launch at login across an update.** Reconciled from the registry at startup, and the
  registry entry names a path. Velopack's stub at `%LOCALAPPDATA%\Hangly\Hangly.exe` is
  stable across versions where `current\` is not, so the entry should point at the stub —
  unverified.
- **Uninstall.** Removes the install directory. It does not remove `%APPDATA%\Hangly`,
  so settings survive an uninstall/reinstall. Whether that is wanted is a decision.
- ~~**Where the feed is hosted.**~~ **Decided: GitHub Releases.** The workflow publishes
  both architectures' assets there and the updater reads them with `GithubSource`. Nothing
  else has to be hosted, which is the whole appeal.
