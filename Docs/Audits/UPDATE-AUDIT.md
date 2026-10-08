# Velopack and the update path

21 September 2026, exercised end to end on Windows 11 ARM64. Evidence tags as elsewhere.
Nothing below is inferred: every package was built, installed, upgraded and uninstalled.

---

## 1. What existed before this audit **[WIN]**

| | |
|---|---|
| `VelopackApp.Build().Run()` in `Program.Main` | ✅ present, first thing that runs |
| Update client | ❌ **none** — no `UpdateManager`, no check, no download, no apply |
| Release workflow | ❌ **none** — only `build.yml` |
| Packages ever produced | ✅ 0.9.0/0.9.1/0.9.2 ARM64, from the earlier spike |

So the install and uninstall halves of Velopack worked and had done for a while; the
half that makes an installed copy become a newer one did not exist at all.

## 2. Packaging, measured **[MEAS]**

| | ARM64 | x64 |
|---|---|---|
| Full package | 117.2 MB | 120.2 MB |
| Setup.exe | 121.4 MB | 124.4 MB |
| Portable zip | 117.2 MB | 120.2 MB |
| **Delta 0.9.0 → 0.9.1** | **0.24 MB** | not built |
| Installed on disk | 409.8 MB | — |

The delta is the number that matters for anyone on a metered connection: an update is a
quarter of a megabyte against a hundred and twenty.

## 3. The channel defect, found here **[MEAS]**

`vpk pack --runtime win-arm64` produces a feed called **`releases.win.json`**. The runtime
identifier does not set the channel; `--channel` does, and it defaults to `win`.

DISTRIBUTION.md commits to one channel per architecture precisely so "an ARM64 machine
must never be offered an x64 package". Packaging both architectures without `--channel`
would have put them on one line of releases and done exactly that.

With `--channel win-arm64` the feed becomes `releases.win-arm64.json` alongside
`RELEASES-win-arm64`, and the x64 build produces `releases.win-x64.json`. The release
workflow passes it explicitly and **fails the build if the expected feed file is not
produced**, so this cannot regress quietly.

## 4. The feed **[MEAS]**

`releases.win-arm64.json`, after two versions:

```json
{"Assets":[
 {"PackageId":"Hangly","Version":"0.9.1","Type":"Full","FileName":"Hangly-0.9.1-win-arm64-full.nupkg","SHA1":"8371E0…","SHA256":"0E2AEA…","Size":122890214},
 {"PackageId":"Hangly","Version":"0.9.1","Type":"Delta","FileName":"Hangly-0.9.1-win-arm64-delta.nupkg","SHA1":"19A689…","SHA256":"12764D…","Size":252035},
 {"PackageId":"Hangly","Version":"0.9.0","Type":"Full","FileName":"Hangly-0.9.0-win-arm64-full.nupkg","SHA1":"632A2F…","SHA256":"9E63E3…","Size":122887866}]}
```

Static JSON, SHA1 and SHA256 per asset, no server component. A check is a GET for a file,
which is what lets PRIVACY.md say an update check carries no identifier.

## 5. Install, upgrade, uninstall **[MEAS]**

**Fresh install** — `Setup.exe --silent`, exit 0. Lands in `%LOCALAPPDATA%\Hangly` with
`current`, `packages`, a `Hangly.exe` shim and `Update.exe`. 409.8 MB.

**Upgrade 0.9.0 → 0.9.1** — exit 0, version on disk went 0.9.0 → 0.9.1. Everything below
was written before the upgrade and read back after it:

| | Before | After |
|---|---|---|
| Rope, including a custom charm | `custom:fed3932b…, hamsa` | **identical** |
| Display name | Upgrade Tester | **kept** |
| Cord style / charm size | GoldChain / 1.25 | **kept** |
| Favourites | custom + daruma | **kept** |
| Recents | custom + nazar | **kept** |
| Milestones | 4210 swings, 3 secrets | **kept** |
| Imported charms | 1, drawing on disk | **kept** |
| Per-place size | 1.35 | **kept** |
| Packages retained | — | 2 |

**Uninstall** — `Update.exe --uninstall --silent`, exit 0. Install directory gone, Start
Menu shortcut gone. **User data in `%APPDATA%\Hangly` is deliberately kept**: that is the
same decision that makes settings survive an update, and it means reinstalling restores
your rope. Anyone who wants it gone deletes that folder. Recorded as a decision, not a
defect — but it is the kind of thing a reviewer will ask about.

## 6. What was built during this audit

| File | What |
|---|---|
| `src/Hangly.App/Services/Updater.cs` | **new** — check, download, apply; channel per architecture; every failure quiet |
| `src/Hangly.App/Services/AppInfo.cs` | `UpdateFeedUrl` |
| `src/Hangly.App/Customize/CustomizeWindow.xaml` | Updates section on About |
| `src/Hangly.App/Customize/CustomizeWindow.xaml.cs` | check and install handlers |
| `.github/workflows/release.yml` | **new** — tag-driven, both architectures, channel asserted |

## 7. Risks

1. **Nothing is signed.** SmartScreen will warn on first run, and SignPath will not sign a
   project that has not released. Unavoidable for the first release; see §3 of
   DISTRIBUTION.md.
2. **The update path has not been exercised against a published feed.** Packaging,
   install, upgrade-by-installer and uninstall are all verified. `UpdateManager` against a
   real GitHub release is not, because no release exists yet. **This is the one thing that
   must be tested before v1.0, and it can only be tested after v0.9.0 is published.**
3. **`FileVersion` is hard-coded to 2.0.0.0** in the csproj, so a 0.9.0 package carries a
   2.0.0.0 file version. Velopack uses the package version, so updates work, but the
   executable's properties disagree with the release. Cosmetic, one line.
4. **Rollback is retention, not a command.** Velopack keeps the previous package — two
   were present after the upgrade — so a bad release can be recovered by publishing a
   higher version. There is no in-app "go back".
5. **120 MB per download.** Deltas fix the repeat case, not the first one. Trimming was
   deprioritised earlier and remains so.

## 8. Effort to close

| | |
|---|---|
| Publish v0.9.0 and verify a live check | **half a day**, mostly waiting |
| Silent background check on launch | half a day |
| `FileVersion` from the build | minutes |
| Release notes in the update prompt | half a day |
