# Public beta readiness

21 September 2026. Written after the public-beta hardening pass, and based only on what
was measured on this machine or read out of an artefact. Where something is not
established, it says so rather than reading as though it is.

Labels are as in `RELEASE-HARDENING-AUDIT.md`: **[MEASURED]**, **[VERIFIED]**,
**[INFERRED]**, **[UNVERIFIED]**.

---

## Recommendation

**Go for a public beta of v0.9.0, with two conditions, and no-go for calling it v1.0.**

The conditions are both on the download page rather than in the code:

1. **Say it is unsigned and say what Windows will do.** SmartScreen will warn with no
   publisher name. Somebody who is not expecting that will read it as a virus warning.
2. **Say which file is which.** Two architectures, four downloads, and an ARM64 machine
   that installs the x64 build will run it under emulation and never be told.

The reasoning is in §6, and what is still unknown is in §5. Nothing in §5 is a reason to
wait, and one of them — the live update path — can only be closed *by* releasing.

---

## 1. Priority 1 — one Hangly at a time

**Commit `ae7b927`. CI: Build green on `ae7b927` (Solver and models, App win-x64, App
win-arm64).**

### Root cause

There was no single-instance check of any kind. `grep -rn "Mutex\|SingleInstance" src/`
returned nothing before this change. Every launch built a full application: its own
overlay thread, its own tray icon, its own `SettingsStore`.

The damage was not the second window. It was that `SettingsStore` reads `settings.json`
once at construction and writes the whole document on every change, and nothing watches
the file — so two processes each held a private idea of the settings and the last one to
write erased the other's. **[MEASURED]** two processes at 146.2 MB and 145.5 MB, both
live, both owning the same file.

### What was implemented

A named mutex, `Local\Hangly.SingleInstance`, claimed in `Program.Main`:

- **After `VelopackApp.Build().Run()`.** Velopack drives install, update and uninstall by
  relaunching the app with its own arguments in a short-lived process. Those must never be
  turned away, so the claim happens after they have had their turn.
- **After the development switches.** `--check-artwork` and the rest do their work without
  a window and exit.
- **`Local\`, not `Global\`.** Session-scoped, so two people signed in to the same machine
  each get their own Hangly, which is what a per-user install means.
- **Nothing is released by hand.** Windows drops the mutex when the process ends, however
  it ends, so a crash or a kill leaves no stale lock. The handle is held in a static field
  only to keep the finaliser away from it.
- **An `AbandonedMutexException` is treated as success**, because it means the previous
  holder died and this process is now the only Hangly.
- **If the kernel object cannot be created at all**, the app starts anyway and says so in
  the log. One Hangly is better than two; a running Hangly is better than neither.

**One thing this uncovered.** `Diagnostics.Install` truncated the log, and it runs before
the instance check — so a second launch erased the running copy's record of its own
startup on the way to discovering it should exit. Starting the log is now its own step,
`Diagnostics.StartLog`, taken only by a process that is going to be the running Hangly.
The second copy appends one line explaining itself and changes nothing else.

### Verification

`tools/hardening-checks.ps1`, which is repeatable and exits non-zero on failure:

```
== 1. Single instance ==
  PASS  a copy starts - 1 running, pid 9524
  PASS  the second copy exits - exit code 0
  PASS  one process remains - 1 running
  PASS  it is the original process - pid 9524 -> 9524
  PASS  settings are untouched - sha256 unchanged
  PASS  the log was not truncated - 11 lines before, 13 after
  PASS  the refusal is recorded - log names the second copy
```

**The startup race, specifically. [MEASURED]** Two processes started as close to together
as the shell allows, five times:

```
round 1 : started pid 4188 and 4204 together -> 1 running (pid 4188)
round 2 : started pid 9444 and 5168 together -> 1 running (pid 9444)
round 3 : started pid  160 and 8188 together -> 1 running (pid  160)
round 4 : started pid 6808 and 3596 together -> 1 running (pid 6808)
round 5 : started pid 6028 and 4848 together -> 1 running (pid 6028)
```

One survivor every time, and it is always the first of the pair. No duplicate overlay and
no duplicate tray icon follow, because there is no second process for them to belong to.

---

## 2. Priority 2 — surviving an Explorer restart

**Commit `ae7b927`. Same CI run.**

### Root cause

Two of them, and the second only appeared once the first was fixed.

**A notification icon belongs to the Explorer process that draws it.** When Explorer
restarts, every icon in the notification area is gone, and the new Explorer broadcasts
`TaskbarCreated` so applications can ask again. Nothing in this app listened:
`grep -rn "TaskbarCreated" src/` returned nothing. **[MEASURED]** before the fix — Hangly
running, a charm on screen, and no icon and no overflow chevron in the notification area.

Because the overlay is click-through except over the charm, **the tray menu is the only
way to reach Customize, the Library, Create, About and Quit.** The app was left running
with Task Manager as the only way out.

**And listening was not enough.** The first attempt registered the message, handled it
correctly, and the icon still did not come back: the tray's window was `HWND_MESSAGE`, and
**broadcast messages are delivered to top-level windows only.** A message-only window is
not one. The icon's own callbacks had always arrived because those are sent to a specific
window; the one message that had to arrive was the one that could not.

### What was implemented

- `RegisterWindowMessage("TaskbarCreated")` at construction, checked before the switch in
  the window procedure.
- On receipt, the icon is re-added with `NIM_ADD` — not `NIM_MODIFY`, because the icon
  Explorer knew about went with the Explorer that knew about it — using the tooltip the
  icon currently has. A failure is logged rather than thrown, as at startup.
- The tray window is now an ordinary top-level window that is never shown:
  `WS_EX_TOOLWINDOW` so it is out of Alt-Tab and off the taskbar, and no `WS_VISIBLE`.
- It is titled **"Hangly tray"**, not "Hangly". Now that it is top-level it can be found
  by anything that enumerates windows, and a second top-level window with the app's name
  is one an accessibility tool — or this project's own UI tests — would pick up instead of
  the real one.

### Verification

From the same harness:

```
== 2. Explorer restart recovery ==
  PASS  explorer came back - explorer.exe running
  PASS  hangly survived - pid 9524 still running
  PASS  the icon was re-added - Shell_NotifyIcon(NIM_ADD) succeeded after TaskbarCreated
  PASS  nothing failed - no FAIL lines in the log
```

And by hand, on the copy that survived the restart — not a fresh one:

| Step | Result | Evidence |
|---|---|---|
| Explorer killed and restarted | Hangly keeps the same pid | **[MEASURED]** |
| Notification area | The overflow chevron is back. Before the fix there was **no chevron at all**, which is what proved the icon was gone rather than hidden | **[MEASURED]**, screenshots |
| Tray icon found by UI Automation | `'Hangly'` at 2987,1958 | **[MEASURED]** |
| Right-click | The full menu: Hide Charm · Customize… · Rope ▸ · Position ▸ · Launch at Login · Quit Hangly | **[MEASURED]**, screenshot |
| Customize opened from that menu | Window present, `WinUIDesktopWin32WindowClass` | **[MEASURED]** |
| The Library page inside it | Live: detail panel reading *Daruma · Japan · "A round, weighted doll…" · On the rope*, the favourite button, "3 charms hang on the cord, from the top down.", the reorder buttons, the size slider, the search box, and the charm grid including an imported charm | **[MEASURED]**, UI Automation dump |
| Overlay | Still drawing three charms over the foreground window | **[MEASURED]**, screenshot |

**What was not separately driven, and why.** Create, About and Appearance were not each
navigated to after the restart. WinUI removes a collapsed page's subtree from the
automation tree, so reaching them means driving the navigation pane, which this harness
could not do reliably. It is a limit of the harness, not a finding about the app: an
Explorer restart does not touch the app's own window, and the window opened from the
rebuilt icon and rendered a fully populated page. **Quit was not exercised either**, for
the same reason — the entry is visible in the menu screenshot and was not clicked.

---

## 3. Priority 3 — the release, regenerated

**Draft rebuilt from tag `v0.9.0` at commit `416b85d`. Release workflow green: `Package
(win-arm64)`, `Package (win-x64)`, `Publish to GitHub Releases`. Tooling committed as
`fc17d7f`.**

The previous draft was built at `12a5a34` — before five rendering fixes and before both
blocker fixes. It was deleted, tag and all, and cut again. **It is a draft. Nothing is
public.**

`tools/validate-release.ps1` checks the artefacts rather than the pipeline's own claims.
Twenty-two checks, all passing:

| | |
|---|---|
| Feeds | `releases.win-arm64.json` and `releases.win-x64.json` both present |
| Channel separation | **No `releases.win.json`** — the failure mode that would offer an ARM64 machine an x64 package |
| Feed contents | Each names one package, for its own architecture, at version 0.9.0 |
| Release notes | 461 characters of `NotesMarkdown` in each feed, matching the 0.9.0 section of `CHANGELOG.md`, and the same text on the draft's page with the unsigned-build warning appended |
| Velopack metadata | nuspec id `Hangly`, version `0.9.0`, title `Hangly`, authors `sharancreatedthis`; channel `win-arm64` / `win-x64` and machineArchitecture `arm64` / `x64` respectively |
| Assets | Setup, Portable, full nupkg, `RELEASES-*`, `assets.*.json` and the feed, for both architectures |

Two results worth stating on their own:

- **`FileVersion` reads `0.9.0.0`** inside the shipped executable, for both architectures.
  The last package anybody installed reported `2.0.0.0`; this is the first time the fix has
  been checked in an artefact rather than in a build directory. **[MEASURED]**
- **`ProductVersion` reads `0.9.0+416b85d8be2c4dc25f211d357ee3f3359f5deb4f`**, so the
  package says which commit it came from. **[MEASURED]**

---

## 4. Priority 4 — documentation

**Commit `416b85d`. Build green.**

| Document | What changed |
|---|---|
| `PRIVACY.md` | The update check is described as it actually works — GitHub's public releases API, then the feed asset for this build's channel — instead of "a request for one static file". The promise underneath is unchanged and restated: no identifier, no name, no profile, no Hangly server. The analytics table went from eleven of the twenty-five events to all of them, the one event that exists and is never sent is named, and **`airdrop_file_dropped` carrying the file's extension and a coarse size bucket is now stated where somebody reading about imports will see it** — the document had disclosed it further down and contradicted itself higher up. Weather and seasonal charms are described as removed permanently |
| `Docs/DISTRIBUTION.md` | §4 matches `PRIVACY.md` and explains why it is the API and not a URL. Two questions marked open are closed: the feed host is GitHub Releases, and x64 is built and published — never installed, which remains true and is now said that way |
| `README.md` | Was advertising 25% ported, 67 tests and an overlay that had never been launched. Now 759 tests, both architectures, the Create tab, v1.0 at 83%, and the roadmap |
| `Docs/RELEASE-READINESS.md` | Both blockers recorded as fixed and verified. v1.0 83%, Windows feature completion 68%, macOS parity 66% against a ceiling of 100% |
| `STATUS.md` | The blockers closed; the stale entries about beads-as-ellipses, SVG-only import and a bare Library grid were already corrected in the previous pass |
| `src/Hangly.Core/Settings/SettingsStore.cs` | Its own remarks said the settings file lives under `%LOCALAPPDATA%` and then defined the path as roaming `%APPDATA%` two paragraphs later |

**Roadmap, stated identically everywhere:** weather charms and seasonal charms are
**removed permanently** — not deferred, not backlogged, not counted as parity gaps. Sound
is **v1.1**. The **Create tab is the v1.0 answer** to making your own charm; **Creator
Studio is v1.1** and is not a v1.0 blocker.

---

## 5. Measurements

All **[MEASURED]** on Windows 11 ARM64 (build 26200) under Parallels on Apple silicon,
one display, 200% scaling, three charms on the rope. One machine, one configuration.

### Startup and cost of the changes

| | Before (`3bf63ba`) | After (`ae7b927`) |
|---|---|---|
| Process start to `overlay window shown` | 319 ms | **325 ms** |
| Idle CPU, 60 s, settled | 0.65% | **1.15%** |
| Working set | 153.4 MB | **142.1 MB** |
| Private bytes | 70.0 MB | 59.2 MB |
| Handles | 538 | 539 |

**Startup impact: none worth reporting.** Six milliseconds is one mutex creation and a
`RegisterWindowMessage` call, and is inside run-to-run variation.

**Memory impact: none.** The working set is lower after than before, which is variation
rather than an improvement, and nothing here would have improved it.

**The idle figure needs its context.** 1.15% against 0.65% looks like a regression and is
not: the previous pass measured six consecutive ten-second samples at 0.47, 0.47, 1.09,
0.78, 0.62 and 1.09 per cent. 1.15% sits at the top of a spread that was already there.
Neither change adds per-frame work — a mutex is held, not polled, and `TaskbarCreated`
arrives at most once per Explorer restart.

### The Library, re-measured

| | Audit (`6f129d3`) | Now (`ae7b927`) |
|---|---|---|
| Overlay only | 153.4 MB / 538 handles | 141.5 MB / 539 handles |
| Customize open | 306.8 MB / 1137 | 283.2 MB / 1148 |
| Customize hidden | 305.9 MB / 1128 | 263.4 MB / 1134 |

**Still the largest open performance finding, and unchanged in shape:** opening the
Library costs about 140 MB and roughly 600 handles, and closing it gives back a fraction.
The window hides rather than closes, deliberately. Nothing in this pass addressed it and
nothing in this pass was meant to.

### New tests

- **`tools/hardening-checks.ps1`** — eleven checks over the two blockers, two screenshots,
  non-zero exit on failure. It restarts Explorer, so it is a deliberate thing to run rather
  than something for CI.
- **`tools/validate-release.ps1`** — twenty-two checks over a draft release's feeds,
  packages, notes, nuspec metadata and stamped versions. It cannot publish.
- **No new unit tests.** Neither fix has logic that can be exercised without Windows: one
  is a kernel object and the other is a window message. Adding a Core test that asserted a
  mutex name string would test nothing. The suite stays at **759 passing**.

### CI

| Commit | What | Build |
|---|---|---|
| `ae7b927` | Single instance, `TaskbarCreated`, the fold | **green** |
| `416b85d` | Documentation | **green** |
| `fc17d7f` | Release validation tooling | **green** |
| tag `v0.9.0` → `416b85d` | Release workflow, both architectures, publish | **green** |

Every commit in this pass is green. This report is itself a commit, and a documentation
commit's Build run is not listed here for the obvious reason.

---

## 6. Go / no-go

### What is measured and good

- One Hangly at a time, including when two are started together, five times out of five.
- The tray icon comes back after an Explorer restart, the menu works on the surviving
  process, and Customize opens from it with a fully populated Library.
- 325 ms to first frame. ~1% of one core at idle. No growth at rest.
- A draft release whose artefacts have been read rather than assumed: both channels
  separated, notes carried through to both feeds, version stamped correctly in both
  executables, and each package naming the commit it came from.
- 759 tests green, and CI green on every commit in this pass.

### What is still open, and why none of it blocks a beta

| | State | Why it does not block |
|---|---|---|
| The live update path has never run against a published feed | **[UNVERIFIED]** | It cannot be closed before publishing. Publishing the beta is how it gets closed |
| No rollback | **[VERIFIED]** as absent | A bad beta is replaced by a better one. Worth knowing, not worth waiting for |
| The Library costs ~140 MB it does not return | **[MEASURED]** | Unpleasant, not harmful, and not a beta blocker |
| Settings corruption is silent | **[VERIFIED]** | Rare, and the failure is "starts with defaults" rather than anything worse |
| Uninstall leaves a `Run` registry value | **[VERIFIED]** | Affects people who uninstall, which by definition is after the beta has been tried |
| `app_quit` probably never arrives | **[INFERRED]** | Distorts one metric. No user impact |
| `collection_charm_selected` is never fired | **[VERIFIED]** | No user impact |
| Multi-monitor, mixed DPI, sleep/wake, lock/unlock, scaling other than 200%, Windows 10 1809, x64 installed | **[UNVERIFIED]** | This is what a beta is for. **They stop being acceptable at v1.0** |
| Unsigned; SmartScreen warns with no publisher name | **[VERIFIED]**, deliberate | Condition 1 on the download page. SignPath will not sign a project that has not released |

### The recommendation, stated plainly

**Go, as a public beta.** The two things that could leave somebody stuck with an app they
could not use are fixed and measured. What remains is either cosmetic, rare, invisible to
the person using it, or unknowable until other people run it on hardware that is not this
one — which is the argument for a beta rather than against it.

**No-go for v1.0**, on the same evidence. The unverified row above is most of a v1.0 QA
matrix, the app is unsigned, and the update path has never completed once. v1.0 needs
those three closed; a beta needs them written down, which they now are.

**The two conditions are both on the download page**, because a beta that a stranger
cannot get installed is not a beta, and both failure modes here are failures of
explanation rather than of code:

1. It is unsigned, and Windows will say *"Windows protected your PC"* with no publisher
   name. Say so before they see it.
2. Four downloads, two architectures. Say which is which.
