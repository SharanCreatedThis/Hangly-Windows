# Update infrastructure

Everything that has to stay the same for an installed Hangly to recognise a later Hangly
as itself. Written after verifying the update path end to end on 21 September 2026.

Labels: **[MEASURED]** taken off a real install or artefact, **[VERIFIED]** read from the
code or the packaging, **[DOC]** stated by the vendor, **[UNVERIFIED]** not established.

---

## 1. Application identity

**Every identifier below must be identical across v0.9.0, v0.9.1 and v1.0.** Changing any
one of them produces a *second* application rather than an update: a second install
directory, a second entry in Apps & Features, and an installed copy that never hears about
the new one.

| # | Identifier | Value | Set by | Evidence |
|---|---|---|---|---|
| 1 | Package ID | `Hangly` | `vpk pack --packId` | nuspec `<id>Hangly</id>` **[MEASURED]** |
| 2 | Feed package id | `Hangly` | derived from 1 | `"PackageId": "Hangly"` in both feeds **[MEASURED]** |
| 3 | Main executable | `Hangly.exe` | `--mainExe` | nuspec `<mainExe>` **[MEASURED]** |
| 4 | Assembly name | `Hangly` | `<AssemblyName>` in the app project | **[VERIFIED]** |
| 5 | Package title | `Hangly` | `--packTitle` | nuspec `<title>` **[MEASURED]** |
| 6 | Authors / publisher | `sharancreatedthis` | `--packAuthors` | nuspec `<authors>`; uninstall key `Publisher` **[MEASURED]** |
| 7 | Shortcut AUMID | `velopack.Hangly` | Velopack, from 1 | nuspec `<shortcutAumid>` **[MEASURED]** |
| 8 | Install directory | `%LOCALAPPDATA%\Hangly` | Velopack, from 1 | uninstall key `InstallLocation` **[MEASURED]** |
| 9 | Uninstall registry key | `HKCU\…\Uninstall\Hangly` | Velopack, from 1 | **[MEASURED]** |
| 10 | Data directory | `%APPDATA%\Hangly` | `SettingsStore.DefaultPath` | **[VERIFIED]** |
| 11 | Run-at-login value name | `Hangly` | `RegistryLaunchAtLogin.ValueName` | **[VERIFIED]** |
| 12 | Single-instance mutex | `Local\Hangly.SingleInstance` | `SingleInstance.Name` | **[VERIFIED]** |
| 13 | Tray window class | `HanglyTrayWindow` | `TrayIcon` | **[VERIFIED]** |
| 14 | Analytics install id | a UUID in `settings.json` | written once, per machine | survived the update **[MEASURED]** |

**None of these is derived from the version number**, which is what makes an update an
update. Verified rather than assumed: after updating 0.9.0 → 0.9.1 the uninstall key was
still `Hangly`, with `InstallLocation` unchanged and only `DisplayVersion` moving from
`0.9.0` to `0.9.1`. **[MEASURED]**

### What *is* allowed to change

`Version` / `packVersion`, and the file names derived from it
(`Hangly-<version>-<rid>-full.nupkg`). Nothing else.

### The one thing that would break identity

Changing `--packId`. It is the root of items 2, 7, 8 and 9. There is no reason to and no
plan to; it is written down here so that it is a decision rather than a typo.

---

## 2. Update feeds

### Where they are

| Channel | Feed asset | Who reads it |
|---|---|---|
| `win-arm64` | `releases.win-arm64.json` | ARM64 builds |
| `win-x64` | `releases.win-x64.json` | x64 builds |

Both are published as assets on **every** GitHub release of this repository, at:

```
https://github.com/SharanCreatedThis/Hangly-Windows/releases/download/<tag>/releases.<channel>.json
```

The application does not hard-code that URL. `AppInfo.UpdateFeedUrl` is the repository URL
— `https://github.com/SharanCreatedThis/Hangly-Windows` — and `GithubSource` asks GitHub's
releases API which releases exist and where their assets are. **The feed location is
therefore a consequence of the repository, not a separate thing that can drift.**
**[VERIFIED]**

### Channel separation

The channel is chosen by the build, not by the server: `Updater.Channel` returns
`win-arm64` or `win-x64` from `RuntimeInformation.ProcessArchitecture`, and is passed as
`UpdateOptions.ExplicitChannel`. **[VERIFIED]**

`vpk` defaults to a channel called `win`, which would put both architectures on one line
of releases and offer an ARM64 machine an x64 package. The release workflow passes
`--channel` explicitly and then **fails the build** if the expected feed file is missing.
**[VERIFIED]**, and `tools/validate-release.ps1` asserts `releases.win.json` does **not**
exist. **[MEASURED]** on v0.9.0.

### Stability between beta and v1.0

| | |
|---|---|
| Feed file names | Unchanged — derived from the channel, which is derived from the architecture |
| Feed location | Unchanged — derived from the repository URL |
| Channel names | Unchanged — `win-arm64`, `win-x64` |
| Does any workflow step regenerate a feed in a way that breaks installs? | **No.** Each release produces its own feed listing its own package; older releases keep theirs. Velopack reads the newest it can see |

### Pre-releases — the one thing that is not stable yet

`GithubSource` is constructed with **`prerelease: true`**, and this must be revisited
before v1.0. With it false, Velopack asks for `/releases/latest`, which **excludes
pre-releases and returns 404 when every release is one** — measured the minute v0.9.0 went
public as a pre-release:

```
17:12:16  update check failed: HttpRequestException
17:12:16  update check: Couldn't check for updates just now.
```

**[MEASURED]**. With it true the source enumerates `/releases` and picks the highest
version, so a stable v1.0 still wins over any 0.9.x beta. The cost arrives after v1.0:
publishing a pre-release would then offer it to people running stable.
`Docs/DISTRIBUTION.md` §2 carries the decision and the two ways out.

---

## 3. The update path, verified end to end

Performed on 21 September 2026, Windows 11 ARM64, on a real install.

| Step | Result | Evidence |
|---|---|---|
| Install v0.9.0 | Installed; `FileVersion 0.9.0.0`, `ProductVersion 0.9.0+416b85d8…` | **[MEASURED]** |
| The installed copy asks the live GitHub API | `update check: Hangly is up to date.` — a real answer from `api.github.com`, not an error | **[MEASURED]** |
| Detection | `CheckForUpdates : 0.9.1 available`, `IsInstalled: True`, `CurrentVersion: 0.9.0` | **[MEASURED]** |
| The release's notes reach the client | 144 characters of `NotesMarkdown` | **[MEASURED]** |
| Delta rather than a full download | `deltas to target: 1`, `base release: 0.9.0` — **only because both versions were packed into one directory by hand.** Releases built by CI carry no delta; see below | **[MEASURED]** |
| Download | complete in 11.1 s | **[MEASURED]** |
| Apply and restart | Velopack handed over to `Update.exe`; the app came back on its own | **[MEASURED]** |
| Version afterwards | `0.9.1.0`, and the uninstall key's `DisplayVersion` moved with it | **[MEASURED]** |
| Downgrade protection | `isDowngrade: False` reported by the client before applying | **[MEASURED]** |

### User data across the update

Seeded with distinctive values, then compared after. **Everything below was identical.**

| | Before | After |
|---|---|---|
| Display name | `Release Engineering` | `Release Engineering` |
| Rope style / charm size / rope length / offset | GoldChain / 1.45 / 0.85 / 37 | identical |
| Charms on the cord | `custom:fed3932b…, daruma, nazar` | identical |
| Per-charm sizes | 1.55 / 0.75 / 1.15 | identical |
| Favourites | `custom:fed3932b…, thorHammer` | identical |
| Recents | 4 ids, in order | identical |
| Statistics | charmsHung 42, secrets 4, swings 12345 | identical |
| Analytics setting | disabled | disabled |
| Analytics install id | `b21d9729-6ec2-…` | identical |
| Custom charm manifest | 1 entry | 1 entry |
| Imported file on disk | 502,532 bytes, `sha256 477BA584…` | identical |

The launch counter moved 102 → 103, which is the app starting, not data being lost.
**[MEASURED]**

**One thing to know about favourites.** A favourite naming a charm that no longer exists
in the catalogue is dropped when settings are read. Eleven seasonal charms were removed
before this release, so a favourite of one of those disappears. That is the settings clamp
working as designed, not the update losing data. **[MEASURED]**

### The same thing again, against the live GitHub source

Repeated on 21 September 2026 with both releases published, using the same `GithubSource`
Hangly itself builds:

```
Source             : GithubSource (prerelease: true)
Feed               : https://github.com/SharanCreatedThis/Hangly-Windows
IsInstalled        : True
CurrentVersion     : 0.9.0
CheckForUpdates    : 0.9.1 available
  package          : Hangly-0.9.1-win-arm64-full.nupkg
  size             : 122864582 bytes
  deltas to target : 0
DownloadUpdates    : complete in 67951 ms
ApplyUpdatesAndRestart: handing over to Update.exe
```

Afterwards: version `0.9.1.0`, uninstall key `DisplayVersion 0.9.1`, every user setting
identical, and the new build's own check reporting `Hangly is up to date.` — the failure
that v0.9.0 shipped with, gone. **[MEASURED]**

**`deltas to target: 0` is the finding.** The release workflow packs in a clean checkout,
so `vpk` has no previous package to diff against and every release is a full download.
`Docs/DISTRIBUTION.md` §2 has the detail and the fix.

### Uninstall

| | After `Update.exe --uninstall --silent` |
|---|---|
| `%LOCALAPPDATA%\Hangly` | removed |
| Uninstall registry key | removed |
| Desktop shortcut | removed |
| Start menu shortcut | removed |
| Process | gone |
| `%APPDATA%\Hangly` | **kept, by design** — so a reinstall remembers your charms |

**[MEASURED]**. One known gap: the run-at-login registry value is not removed. It was not
set during this run, so the gap is **[VERIFIED]** from the code rather than reproduced.

---

## 4. Unsigned now, signed at v1.0 — what that transition can break

**Nothing in the update path.** The evidence, rather than the reassurance:

| Question | Answer | Evidence |
|---|---|---|
| Does Velopack verify a signature before applying an update? | **No.** It verifies the package's **SHA** | `ChecksumFailedException`, `UpdateManager.VerifyPackageChecksumAsync`, `VelopackAsset.SHA1` / `.SHA256` in the API **[DOC]** |
| Is there any signature-checking code at all? | **No.** Searching `Velopack.dll` for `signature`, `Authenticode` and `verifySignature` returns **zero** occurrences; `Checksum` returns 12, `SHA256` 9 | **[MEASURED]** |
| Does the feed carry a signature field? | **No.** The asset record is `PackageId, Version, Type, FileName, SHA1, SHA256, Size, NotesMarkdown, NotesHTML` | **[MEASURED]** |
| Does signing change package identity? | **No.** `--signTemplate` runs a command per file; it does not touch the nuspec, the packId, the channel or the rid | **[VERIFIED]** |
| Does it change the installed location? | **No.** That comes from the packId | **[VERIFIED]** |
| Does it change update detection? | **No.** That is the channel and the feed name | **[VERIFIED]** |
| Does it change how an update is applied? | **No.** The *old* version's `Update.exe` applies the new package, and it checks the checksum the feed published for it | **[VERIFIED]** |

So an unsigned 0.9.x can update itself to a signed 1.0.0. The checksum in the feed is
computed at pack time from whatever the files are, signed or not, so signing and
verification never disagree.

### What signing *does* change

- **SmartScreen.** A signed download from a publisher with no history still warns, but
  with the verified publisher name shown. Reputation then accrues against that identity
  across versions. An unsigned download builds no reputation at all, so every release
  starts from zero — which is the actual cost of staying unsigned. **[DOC]**
- **Delta size, once.** Signing rewrites every file it touches, so the delta from the last
  unsigned release to the first signed one will be close to a full download. One release,
  and then deltas are small again. **[INFERRED]** from how deltas are computed.
- **The release notes and the download page**, which currently tell people to expect the
  warning and click through it. Both need editing when it stops being true —
  `SIGNPATH-CHECKLIST.md` §5.

### What must not happen at v1.0

**Do not change the publisher name** (`--packAuthors sharancreatedthis`) to match the
certificate's subject if they differ. It is identifier #6 and it appears in the uninstall
entry. A certificate issued to a different legal name does not require the package's
author field to change, and changing it needlessly is a gratuitous identity change.
