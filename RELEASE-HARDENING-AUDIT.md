# Release hardening audit

21 September 2026, at commit `6f129d3`. No code was changed by this audit — it reports,
it does not fix.

> **What happened next.** B1, B2 and B4 were fixed at `ae7b927` and B3 is the release
> regeneration. The findings below are left exactly as they were measured, because an
> audit rewritten after the fact stops being evidence; `tools/hardening-checks.ps1` is the
> standing check that B1 and B2 do not come back, and `Docs/RELEASE-READINESS.md` carries
> the current state.
>
> One thing found during the fix and worth recording here: handling `TaskbarCreated` was
> not enough on its own. The tray's window was `HWND_MESSAGE`, and **broadcast messages
> are delivered to top-level windows only** — so the message that had to arrive was the
> one that could not. The window is now top-level and never shown.

Every finding carries one label, and the label is about **how it is known**, not how bad
it is:

| | |
|---|---|
| **[MEASURED]** | A number or an observation taken off this machine, with the method written down |
| **[VERIFIED]** | Established by reading the code or the artefact it describes, and true by inspection |
| **[INFERRED]** | Follows from something verified, but the consequence itself was not observed |
| **[UNVERIFIED]** | Not established. Listed so it is not mistaken for established |

**Everything measured here was measured on one machine**: Windows 11 ARM64 (build
26200) running under Parallels on Apple silicon, one display, 200% scaling. That is a
real Windows machine and the numbers are real numbers, but it is one configuration. Where
a second configuration would change the answer, the finding says so.

---

## 1. Verdict

**No-go for publishing v0.9.0 today. Go after two fixes, both small.**

The two blockers are §2.1 and §2.2: the app can be launched twice and the tray icon does
not survive an Explorer restart. Neither is exotic, both are reachable by an ordinary
person on an ordinary day, and one of them leaves the app running with no way to reach
its own settings. Both are hours of work, not days.

Everything else on this page is either a risk to accept knowingly, a number to record, or
something that cannot be verified until v0.9.0 exists. The full reasoning is in §8.

---

## 2. Release blockers

### 2.1 Nothing stops a second copy running **[MEASURED]**

Started `Hangly.exe` twice, twelve seconds apart:

```
instances running: 2
  pid 5712  ws 146.2 MB
  pid 9164  ws 145.5 MB
```

Two overlays, two tray icons, two ropes, 292 MB. There is no mutex, no named event and no
single-instance check anywhere in the source — `grep -rn "Mutex\|SingleInstance"` returns
nothing.

The consequences compound:

- **Settings are lost silently.** Both processes own `settings.json` and neither watches
  it. `SettingsStore` reads the file once at construction and writes the whole document
  on every change, so whichever instance writes last erases whatever the other did. A
  person who changes their charm in one window and their rope style in the other keeps
  one of the two. **[VERIFIED]**
- **The log becomes unreadable.** `Diagnostics` truncates the log at launch, so the
  second instance wipes the first instance's record of its own startup. Both then append
  to the same file under a lock that is per-process and therefore not a lock at all.
  **[VERIFIED]**
- **Analytics double-counts.** Two `app_launch` events, two launch counters, two sets of
  rope properties, from one person starting the app once too often. **[VERIFIED]**

**Why this is a blocker rather than a rough edge**: launch-at-login is a setting, the
Start menu shortcut stays clickable, and an installer relaunches the app. A second copy
is an ordinary accident, not an abuse.

### 2.2 The tray icon does not come back after an Explorer restart **[MEASURED]**

Killed `explorer.exe`, let Windows restart it, and photographed the notification area
before and after with Hangly running throughout:

| | Notification area |
|---|---|
| Hangly running, Explorer restarted | Defender, network, volume, battery. **No overflow chevron at all** — nothing hidden |
| Hangly relaunched | The overflow chevron appears; Hangly's icon is behind it |

The chevron is the proof: it is absent when there is nothing hidden, and the only thing
that changed between the two photographs is Hangly being restarted.

The cause is in the source. When Explorer restarts it broadcasts a registered window
message, `TaskbarCreated`, and every application that owns a notification icon must
re-add it in response. `grep -rn "TaskbarCreated" src/` returns nothing. **[VERIFIED]**

**Why this is a blocker**: the overlay is click-through except over the charm itself, so
**the tray menu is the only way to reach Customize, the Library, the Create tab, the About
page and Quit**. After an Explorer restart the app is still running, still drawing a
charm, and has no user interface. The only way out is Task Manager.

---

## 3. Stability

### 3.1 Settings corruption is invisible **[VERIFIED]**

`SettingsStore` has a `WasRecovered` property documented as "whether the document on disk
was unreadable and had to be replaced". Three things are wrong with it:

1. The constructor throws the answer away: `storage = initial ?? Load(path, out _);`. A
   corrupted document is silently replaced with defaults and the flag stays false.
2. The only place that sets it is the *write* path, so the flag actually means "a save
   failed" — a different condition wearing the same name.
3. Nothing reads it. `grep -rn WasRecovered src/` finds the declaration, one assignment,
   and no consumer.

**[INFERRED]** A person whose settings file is truncated by a power loss loses their
name, their charms and their milestones, sees the app start with defaults, and neither
the app nor the log says anything happened.

### 3.2 Uninstall leaves the "run at login" entry behind **[VERIFIED]**

`RegistryLaunchAtLogin` writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Hangly`
pointing at `Environment.ProcessPath`. Velopack's uninstaller removes the install
directory. Nothing removes the registry value: `Program.cs` calls
`VelopackApp.Build().Run()` with no uninstall hook.

**[INFERRED]** Somebody who enabled launch-at-login and then uninstalls keeps a Run entry
pointing at a deleted executable, for as long as their Windows install lasts.

Also left behind, both by design and worth stating in one place:

| Path | After uninstall | Deliberate? |
|---|---|---|
| `%APPDATA%\Hangly` — settings, log, imported charms | Kept | Yes, `Docs/DISTRIBUTION.md` §5 records it as an open decision |
| `%TEMP%\Hangly\thumbnails` — 3.71 MB **[MEASURED]** | Kept | Incidental; Windows clears `%TEMP%` eventually |
| `HKCU\…\Run\Hangly` | Kept | **No** — §3.2 |

### 3.3 The artwork cache never evicts anything **[VERIFIED]**

`CharmArtworkCache` holds three dictionaries — rasterised bitmaps keyed by
`(file, pixel size, region)`, parsed SVG documents, and measured regions — and the only
code that removes an entry is `Dispose`. Each distinct rounded pixel size is a new
`CanvasBitmap`.

**[INFERRED]** Dragging the charm-size slider rasterises a fresh bitmap at every rounded
size it passes through and keeps all of them for the life of the process. Not measured,
because the slider needs a UI driver this audit did not build; it is arithmetic from the
cache key, not a suspicion.

### 3.4 The renderer now owns device resources and has no way to release them **[VERIFIED]**

`RopeRenderer` caches one `CanvasRadialGradientBrush` per charm for the ambient glow,
keyed by charm id, disposed only when the drawing device changes. The class has no
`Dispose`, and `OverlayWindow.Dispose` disposes the artwork cache but not the renderer.

Bounded by the number of distinct charms hung in one session, so small — but it is a
device resource with no owner, and it was introduced today.

### 3.5 `app_quit` is posted into a process that is about to exit **[VERIFIED]**

`AnalyticsManager.Stop` calls `Track(Events.AppQuit)`, which hands the payload to
`PostHogProvider.Track`, which does `_ = SendAsync(payload)` and returns. `Flush()` is
documented as having nothing to flush, because nothing is queued. Then the process exits.

**[INFERRED]** the request rarely survives long enough to be sent, so session length and
retention will read differently on Windows than on macOS even though the event name
matches. Not measured: measuring it means a clean quit through the tray menu and a look at
the log, which is worth doing before deciding what to change.

### 3.6 What was checked and is sound

Recorded because an audit that lists only faults is not an audit.

- **Thread safety at the overlay boundary is clean. [VERIFIED]** The overlay runs its own
  STA thread with its own message pump. Every cross-thread entry point — `Apply`,
  `SetCharms`, `Nudge`, `TakeSwings` — hands over through `Interlocked.Exchange` and the
  loop takes ownership on its own thread. No locks are held across the boundary, so there
  is no deadlock to find, and a second change arriving mid-read replaces the pending value
  rather than being dropped.
- **No `lock` is taken while calling out to anything. [VERIFIED]** The only monitor in the
  app is the one-line gate around the log write.
- **The settings write is atomic. [VERIFIED]** Written to `settings.json.tmp` and moved
  over the original, so a crash mid-write leaves the previous document rather than half of
  a new one. There is no `fsync`, so a power loss in the wrong millisecond could still
  leave an empty file — a real but small risk, and the same one every editor takes.
- **Analytics failures are contained. [VERIFIED]** `SendAsync` catches inside the task, so
  there is no unobserved task exception and no network error can reach the UI.
- **No memory growth at rest. [MEASURED]** Working set flat at 149.1 MB across six
  consecutive ten-second samples, and unchanged across a twelve-second drag.

---

## 4. Overlay behaviour

| Behaviour | Result | Evidence |
|---|---|---|
| Always on top | **Holds.** Charm drawn over a maximised terminal, over Notepad and over Edge; re-asserted every 1000 ms by `HoldTopmost` | **[MEASURED]** |
| Fullscreen | **Draws over it.** The charm is visible over a borderless-fullscreen browser window; there is no suppression logic of any kind | **[MEASURED]**, and see §6.3 |
| Startup, launch at login | Writes `Environment.ProcessPath`, which for an installed copy is `…\Hangly\current\Hangly.exe`. Velopack keeps `current` stable across updates, so the path should survive one | **[VERIFIED]** for the write; **[UNVERIFIED]** across an actual update |
| Explorer restart | **Fails.** The icon does not come back | **[MEASURED]** — §2.2 |
| Monitor disconnect / reconnect | Not established. This host has one display | **[UNVERIFIED]** |
| Sleep / wake | Not established | **[UNVERIFIED]** |
| Lock / unlock | Not established | **[UNVERIFIED]** |
| DPI change | `WM_DPICHANGED` is handled, and a 1000 ms poll (`ScaleDrifted`) catches the cases the message cannot reach — a cross-process synthetic `WM_DPICHANGED` cannot be delivered at all, which is why the poll exists | **[VERIFIED]** in code; **[UNVERIFIED]** live, because only 200% is available here and changing scaling from inside the guest is forbidden after the earlier incident |
| Taskbar auto-hide | The overlay reads the display's **work area** only inside `Reposition`, and `Reposition` runs on a settings change or a scale change. Turning auto-hide on changes the work area and raises `WM_SETTINGCHANGE`, which nothing handles | **[INFERRED]**: the overlay keeps its old position until something else moves it. Consequence not observed |

---

## 5. Performance

All **[MEASURED]** on the machine described at the top, on the build at `6f129d3`, with
three charms on the rope.

### 5.1 Startup

From process start to `overlay window shown`, read out of the log's own timestamps:

```
12:27:55.796  process start
12:27:55.933  tray icon registered      (+137 ms)
12:27:56.066  charms measured and hung  (+270 ms)
12:27:56.115  overlay window shown      (+319 ms)
```

**319 ms.** Nothing in that sequence is waiting on anything else.

### 5.2 CPU

| State | CPU, as a share of one core |
|---|---|
| Idle, settled, 60 s | **0.65%** |
| Idle, six consecutive 10 s samples | 0.47, 0.47, 1.09, 0.78, 0.62, 1.09 |
| Dragging, 12 s of continuous motion | **27.08%** |
| The 15 s after a 12 s fling | **25.52%** |

The idle number is the one that matters for a thing that hangs on a desktop all day, and
it is good: the frame loop still wakes at the compositor's rate, but `SimulationClock`
passes one tick in four and the rope does no work. The first version of this measurement
read 7.86% and was wrong — it sampled the first thirty seconds after launch, while the
rope was still swinging. Both numbers are in this document because the difference between
them is the whole reason to measure twice.

The 25.52% after the fling is not a leak: a rope fed energy for twelve seconds keeps
swinging, and swinging is drawn. Worth knowing that a hard fling costs a quarter of a core
for the best part of half a minute.

### 5.3 Memory, and the Library

| State | Working set | Private | Handles |
|---|---|---|---|
| Fresh launch, overlay only | **153.4 MB** | 70.0 MB | 538 |
| Customize open, Library shown | **306.8 MB** | 127.0 MB | 1137 |
| Customize closed | 305.9 MB | 126.0 MB | 1128 |
| Ten seconds later | **310.8 MB** | 131.0 MB | 1131 |

**Opening the Library costs 153 MB of working set and 599 handles, and closing it returns
none of it.** Repeated twice with the same shape.

Part of this is deliberate: `OpenCustomize` builds the window once and hides it on close
"so there is nothing to rebuild and the window comes back where it was left". The
decision is defensible. The recorded price was not — `Docs/RELEASE-READINESS.md` said
"a Library visit costs ~45 MB"; it costs three times that. The doc has been corrected.

### 5.4 On disk

| | |
|---|---|
| Published payload | 402.1 MB across 604 files |
| Installer download | 120.8 MB |
| Thumbnail cache, `%TEMP%\Hangly\thumbnails\256` | 71 files, **3.71 MB**, pruned against live ids at each open ("pruned 0 stale thumbnail(s) of 71 live") |
| Imported charms, `%APPDATA%\Hangly\Charms` | 2 files, 491.4 KB |
| Settings and log, `%APPDATA%\Hangly` | 2 files, 2.7 KB |

Nothing here is wasteful except the payload itself, which is mostly locale satellites and
unused runtime and is already recorded in `Docs/DISTRIBUTION.md` §5 as untrimmed. The
thumbnail cache is small, bounded by the catalogue, and cleans up after itself.

---

## 6. Release

### 6.1 What is verified

- **The workflow runs and produces everything. [VERIFIED]** Tagging `v0.9.0` ran all
  three jobs green: `Package (win-arm64)`, `Package (win-x64)`, `Publish to GitHub
  Releases`.
- **Channel separation holds. [VERIFIED]** The draft release carries
  `releases.win-arm64.json` and `releases.win-x64.json` and **no `releases.win.json`**,
  which is the failure this workflow was written to prevent — `vpk` defaults to a channel
  called `win` and would have offered an ARM64 machine an x64 package.
- **Release notes reach both places. [VERIFIED]** The release page body is the 0.9.0
  section of `CHANGELOG.md` plus the unsigned-build warning, and the feed's
  `NotesMarkdown` is 461 characters of the same text. One source, two destinations.
- **The feed is fetched the way GitHub actually serves it. [VERIFIED]** The updater uses
  `GithubSource`. Handing the repository URL to `UpdateManager` as a string builds a
  `SimpleWebSource`, which would have requested
  `github.com/SharanCreatedThis/Hangly-Windows/releases.win-arm64.json` — a page that has
  never existed. That defect was found and fixed this week and would have been invisible
  until the first release was published.
- **Version consistency. [VERIFIED]** `Directory.Build.props` and the app project both
  say `0.9.0`; the workflow overrides both from the tag and refuses a tag that is not
  `vMAJOR.MINOR.PATCH`; `FileVersion` is derived rather than stated, so a 0.9.0 package
  reports 0.9.0.0. The last part is **[UNVERIFIED]** on a freshly installed build — the
  only install measured so far reported `2.0.0.0`, which is the bug this replaced.

### 6.2 The draft that exists is stale **[MEASURED]**

The `v0.9.0` draft was built from `12a5a34`. Five commits of rendering fixes have landed
since, including the one that drew a separate rope for every charm. **The artefacts in
that draft do not represent what anybody should install.** It must be deleted and re-cut,
not published.

### 6.3 Risks carried into the release

- **The live update path has never run. [UNVERIFIED]** Packaging, install, upgrade,
  uninstall and delta generation are all verified; `UpdateManager` fetching a real
  release is not, and cannot be until one is published. This is the reason to publish
  v0.9.0, and the reason not to trust the updater until v0.9.1 has been offered to a real
  installed copy.
- **There is no rollback. [VERIFIED]** Velopack keeps the previous package in
  `%LOCALAPPDATA%\Hangly\packages` — two were present after the upgrade test — and its
  API exposes `IsDowngrade`, which this app never reads. A bad release can be replaced
  only by a newer good one, or by uninstalling and reinstalling by hand. Acceptable for
  v0.9.0; it should be a conscious decision and not a discovery.
- **Portable copies are never updated. [VERIFIED]** `CheckAsync` returns "Updates apply
  to installed copies only" when Velopack reports the copy is not installed, which is the
  honest answer for the `Portable.zip`. The download page should say so.
- **x64 has been packaged and never installed. [VERIFIED]** Both architectures build and
  both feeds exist; only ARM64 has ever been installed and run.

---

## 7. Analytics

- **Twenty-five events are defined. Twenty-four have a call site. [VERIFIED]** The
  orphan is **`collection_charm_selected`**. `STATUS.md` claimed every defined event had
  a call site; it did not, and that row has been corrected.
- **No duplicate firings. [VERIFIED]** Seven events have more than one call site and each
  was read: `charm_imported`/`charm_saved` fire once from the menu-and-drop path and once
  from the Create tab, which are different paths and cannot both run; `charm_reordered`
  fires from the arrow buttons and from drag-reorder; `airdrop_picker_opened` from the two
  places a picker can be opened; `follow_instagram_clicked` from the follow card and the
  About page. None of them double-fire for one action.
- **One selection produces two events. [VERIFIED]** Choosing a charm fires both
  `charm_selected` and `charm_added`, by design, from `ReportCharmChange`. Whether macOS
  does the same is **[UNVERIFIED]** — the reference has no analytics source, so this
  cannot be settled from the repository.
- **The display name is mandatory and never inferred. [VERIFIED]** It comes only from
  what is typed in onboarding or changed in Customize → Appearance. Nothing reads
  `Environment.UserName`, the Microsoft account, the machine name or any other OS source.
  A payload was checked against a deliberately distinct name earlier in the port, after a
  first check gave a false pass because the tester's Windows account happened to share
  the name being looked for.
- **Disabled means nothing is built, not just nothing sent. [VERIFIED]** `Track` returns
  before assembling properties when analytics is off, so there is no payload in memory and
  no request.
- **`app_quit` is unlikely to arrive. [INFERRED]** §3.5.

### 7.1 Privacy documentation against reality

**One real mismatch, and it is in a privacy claim. [VERIFIED]**

`PRIVACY.md` says of the update check: *"The check is a request for one static file"* and
*"The update feed is a plain file on a web server. There is no server-side component"*.
That was true of the design and is no longer true of the code. The updater uses
`GithubSource`, which calls the **GitHub Releases API** and then downloads an asset from
the URL that API returns.

The substance of the promise survives — no identifier is sent, nothing is reported to, and
Velopack's own statement about collecting no telemetry still applies — but the mechanism
described is not the mechanism used, and a privacy document that describes the wrong
mechanism is worth less than one that says nothing. `Docs/DISTRIBUTION.md` §4 repeats the
same description and has the same problem.

Everything else in `PRIVACY.md` matches the code: the event table, the "what is never
sent" list, the imported-charm promise, the permissions section, and the statement that
there is no other network use.


---

## 7.2 Documentation, read against itself and against the code

Seven documents were read against each other and against the source. One contradiction is
a blocker (§7.1); the rest are stale rather than wrong-in-substance, and every one listed
as *corrected* was corrected by this audit rather than left for later, because a planning
document nobody trusts is worse than no planning document.

| # | Contradiction | State |
|---|---|---|
| D1 | `PRIVACY.md` and `Docs/DISTRIBUTION.md` §4 both describe the update check as a GET for a static file with "no server-side component". The code calls the GitHub Releases API | **Open — blocker B4.** §7.1 |
| D2 | `Docs/DISTRIBUTION.md` §5: *"Where the feed is hosted. Static files; the host is not chosen."* It is chosen. The release workflow publishes to GitHub Releases and the updater reads it with `GithubSource` | **Open.** One line, and it is the same line D1 touches |
| D3 | `Docs/DISTRIBUTION.md` §5: *"The x64 package has never been built or installed."* It is built and sits in the `v0.9.0` draft. Never installed remains true | **Open.** Half of the sentence is now wrong |
| D4 | `Docs/RELEASE-READINESS.md` listed `FileVersion` hard-coded to 2.0.0.0 as an open item | **Corrected** — it was fixed this week and the row now records the fix |
| D5 | `Docs/RELEASE-READINESS.md` and `SHIP-CHECKLIST.md` both said a Library visit costs ~45 MB. It costs 153 MB | **Corrected** in both, and the original measurement in `Docs/LIBRARY-AUDIT-2.md` is marked superseded rather than rewritten |
| D6 | `STATUS.md` §7 claimed every defined analytics event had a call site | **Corrected.** One orphan, named |
| D7 | `STATUS.md` §3 and §4 still described beads as ellipses, imports as SVG-only, the Library as a bare grid and the About page as a single band. All four had been superseded by shipped work | **Corrected** |
| D8 | `README.md` advertised 25% ported, 67 tests and an overlay that had never been launched | **Corrected**: 759 tests, runs on both architectures, 80% of v1.0 |
| D9 | `SettingsStore`'s own remarks say the document lives "under `%LOCALAPPDATA%`" and then define `DefaultPath` as roaming `%APPDATA%`, which is the decision the rest of the paragraph argues for | **Open.** A comment contradicting the code two paragraphs below it |

`RELEASE-CHECKLIST.md` and `SIGNPATH-CHECKLIST.md` were read for the same treatment and
contradict neither each other nor the code. `SIGNPATH-CHECKLIST.md` §2 and
`Docs/DISTRIBUTION.md` §3 ask the same open question about pre-releases in the same terms,
which is what agreement looks like.

The roadmap itself is now stated identically in `STATUS.md`, `Docs/RELEASE-READINESS.md`,
`SHIP-CHECKLIST.md`, `PARITY.md`, `VISUAL-PARITY.md`, `PORTING.md`, `README.md` and
`PRIVACY.md`: **weather and seasonal charms removed permanently, sound deferred to v1.1,
Creator Studio v1.1 with the Create tab as v1.0's answer.**

---

## 8. The lists

### Release blockers — fix before v0.9.0 is published

| # | Finding | Evidence | Effort |
|---|---|---|---|
| B1 | A second instance runs happily, and the two overwrite each other's settings | **[MEASURED]** §2.1 | ~2 hours |
| B2 | The tray icon does not survive an Explorer restart, leaving the app unreachable | **[MEASURED]** §2.2 | ~1 hour |
| B3 | The existing `v0.9.0` draft was built before five rendering fixes and must be re-cut | **[MEASURED]** §6.2 | minutes |
| B4 | `PRIVACY.md` and `DISTRIBUTION.md` describe an update check the code no longer performs | **[VERIFIED]** §7.1 | ~30 minutes |

B4 is on this list and not the next one because it is a privacy claim in a document that
ships with the product, and because it costs half an hour.

### Remaining issues — should fix, not blocking v0.9.0

| # | Finding | Evidence |
|---|---|---|
| R1 | Settings corruption is silent; `WasRecovered` is discarded, overloaded and unread | **[VERIFIED]** §3.1 |
| R2 | Uninstall leaves the `Run` registry value pointing at a deleted executable | **[VERIFIED]** §3.2 |
| R3 | The Library costs 153 MB and 599 handles and returns none of it | **[MEASURED]** §5.3 |
| R4 | The artwork cache never evicts; the size slider allocates a bitmap per rounded size | **[VERIFIED]** §3.3 |
| R5 | `RopeRenderer` holds device resources with no `Dispose` | **[VERIFIED]** §3.4 |
| R6 | `app_quit` is posted as the process exits and probably never arrives | **[INFERRED]** §3.5 |
| R7 | `collection_charm_selected` is defined and never fired | **[VERIFIED]** §7 |
| R8 | Taskbar auto-hide changes the work area and nothing repositions the overlay | **[INFERRED]** §4 |
| R9 | `DISTRIBUTION.md` §5 says the feed host is not chosen, and that x64 has never been built. Both are now out of date | **[VERIFIED]** §7.2 |
| R10 | `SettingsStore`'s remarks name `%LOCALAPPDATA%` where the code uses roaming `%APPDATA%` | **[VERIFIED]** §7.2 |

### Release risks — accept knowingly, or test

| # | Risk | State |
|---|---|---|
| K1 | The live update path has never run against a published feed | **[UNVERIFIED]**, and cannot be tested before publishing |
| K2 | No rollback. A bad release is replaced only by a newer one | **[VERIFIED]** as absent |
| K3 | The charm draws over fullscreen applications, including games and video | **[MEASURED]**; no suppression exists, and whether it should is a product decision |
| K4 | Multi-monitor, mixed DPI, sleep/wake, lock/unlock, monitor hot-unplug | **[UNVERIFIED]** — one display, one scaling factor on this host |
| K5 | Scaling other than 200% | **[UNVERIFIED]** — manual, once per release, and not to be automated from inside the guest |
| K6 | Windows 10 1809, which the manifest claims as the floor | **[UNVERIFIED]** and never tested. Test it or raise the floor |
| K7 | x64 packaged, never installed | **[VERIFIED]** as packaged; install **[UNVERIFIED]** |
| K8 | The first release is unsigned; SmartScreen will warn with no publisher name | **[VERIFIED]**, and deliberate — see `SIGNPATH-CHECKLIST.md` |
| K9 | A hard fling costs ~25% of a core for the best part of half a minute | **[MEASURED]**; inherent to drawing a swinging rope |

### Recommended v0.9.0 go / no-go

**No-go today. Go once B1–B4 are done.**

The reasoning, plainly:

- The product is good and the evidence for it is real. Startup is 319 ms, idle is 0.65% of
  a core, nothing leaks at rest, the thread boundary is clean, the packaging pipeline
  produces both architectures with separated channels and real release notes, and the
  analytics table is one orphan away from complete.
- **B1 and B2 both end with a person unable to use the app they installed**, and neither
  needs unusual behaviour to reach. That is the line between a rough first release and a
  broken one.
- B3 is free and B4 is half an hour, and shipping a stale artefact or an inaccurate
  privacy claim is not worth the time saved by skipping either.
- Everything in the risk list is either unknowable before publishing (K1), a decision
  rather than a defect (K2, K3, K8), or a manual pass somebody has to sit down and do
  (K4–K7). None of them argues for waiting; K6 argues for either running one test or
  changing one number in the manifest.

Publishing v0.9.0 is also the only way to close K1, which is the single largest unknown in
the project. The sequence that gets the most certainty for the least risk is: fix B1–B4,
re-cut the draft, publish it, install it, publish v0.9.1, and watch a real copy update
itself.
