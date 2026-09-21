# Release candidate — v0.9.x beta

21 September 2026. What is ready, what is not, and what only Sharan can do.

Labels: **[MEASURED]**, **[VERIFIED]**, **[INFERRED]**, **[UNVERIFIED]**, as in
`RELEASE-HARDENING-AUDIT.md`.

---

## Readiness

**v1.0 completion: 86%.** Up from 83% at the hardening audit. What moved:

| Area | Was | Now | Why |
|---|---|---|---|
| Packaging and update | 85% | **100%** | The update path has now run end to end against a real install: detect, download by delta, apply, restart, version change, every user setting preserved. It was the largest open unknown in the project |
| Release hardening | 70% | 85% | Both blockers closed; ten smaller findings still open |
| Analytics | 95% | 95% | Unchanged — one orphan event |
| Signing | 0% | 0% | Blocked on SignPath, who cannot be asked until there is a release. There now is |
| Manual QA matrix | 0% | 0% | This is what the beta is for |

`Docs/RELEASE-READINESS.md` carries the weighting.

## Remaining blockers

### Blocking v1.0 — not the beta

1. **The manual QA matrix is unrun.** Scaling other than 200%, more than one monitor,
   mixed DPI, sleep and wake, lock and unlock, Windows 10 1809, and x64 actually
   installed. **[UNVERIFIED]**, all of it. The beta exists to close this.
2. **Nothing is signed.** SmartScreen warns with no publisher name.
   `SIGNPATH-SUBMISSION.md` is written and unsent.
3. **The pre-release flag must be revisited.** `GithubSource` includes pre-releases so
   that a beta can see the beta after it. Once people are on a stable release, publishing
   a pre-release would offer it to them. `Docs/DISTRIBUTION.md` §2. **[VERIFIED]**

### Blocking the beta

**None.**

## Remaining risks

| | State | Carried because |
|---|---|---|
| The Library costs ~140 MB it never returns | **[MEASURED]** | Unpleasant, bounded, not harmful |
| A hard throw costs ~25% of a core for half a minute | **[MEASURED]** | Inherent to drawing a swinging rope |
| Settings corruption is replaced silently | **[VERIFIED]** | Rare; the failure is "starts with defaults" |
| Uninstall leaves the run-at-login registry value | **[VERIFIED]** | Untidy, affects only people who both enabled it and uninstalled |
| `app_quit` probably never arrives | **[INFERRED]** | Distorts one metric, no user impact |
| `collection_charm_selected` is never fired | **[VERIFIED]** | No user impact |
| The charm draws over full-screen applications | **[MEASURED]** | A product decision nobody has made yet |
| No rollback | **[VERIFIED]** as absent | A bad beta is replaced by a better one |
| Windows 10 1809 is claimed and never tested | **[UNVERIFIED]** | Beta, or raise the floor |

## Manual actions required from Sharan

Nobody else can do these.

| # | Action | Why it needs you | Effort |
|---|---|---|---|
| 1 | **Send the SignPath application.** Text is ready in `SIGNPATH-SUBMISSION.md` | It is an application from a person, and the answers name you | 20 minutes |
| 2 | **Give the beta link to testers**, with `TESTER-INSTRUCTIONS.md` | They are your friends | — |
| 3 | **Decide about full-screen.** Should the charm hide over a game or a film? | A product decision, not a defect | — |
| 4 | **Run the QA matrix on real hardware**, or get testers to. `FRIEND-TESTING.md` lists what matters | This VM has one display at one scaling | — |
| 5 | **Decide about Windows 10 1809.** Test it or raise the floor | A promise to strangers either way | — |
| 6 | **Check the SignPath answers against the real form** before pasting | I have not seen the form | 10 minutes |

## Go / No-Go

### Friend testing: **GO**

Both hardening blockers are fixed and measured. The update path is verified end to end,
including that the client talks to the live GitHub API and gets a real answer. Install,
upgrade and uninstall are all measured on a real install. The beta cannot lose anybody's
data, cannot be launched twice, and comes back after Explorer restarts.

What it will do is behave in ways nobody has seen on hardware nobody has tried, which is
the point.

### v1.0: **NO-GO**

Unchanged and for the same three reasons: the QA matrix, the signature, and the
pre-release flag. None is close to done and none can be finished from here.
