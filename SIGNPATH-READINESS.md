# SignPath Foundation readiness

21 September 2026. An audit of this repository against what SignPath Foundation asks for,
with evidence rather than assurances.

**`SIGNPATH-CHECKLIST.md`** is the procedure for the whole signing track. This document is
narrower: is the repository ready to be *looked at*, and what would a reviewer object to.

---

## Readiness score: 90%

The 10% is one open question that only SignPath can answer, and it is in §3.

| | |
|---|---|
| Eligibility conditions met | **8 of 9**, with the ninth ambiguous rather than failed |
| Repository quality | **Ready** |
| Blocking work remaining | **None in this repository** |
| Recommended action | **Ask them the pre-release question, then submit** — §6 |

---

## 1. Their conditions, with evidence

SignPath Foundation's published conditions, each with what this repository actually shows.

| # | Condition | State | Evidence |
|---|---|---|---|
| 1 | An OSI-approved licence, with no commercial dual-licensing | **Met** | `gh api repos/SharanCreatedThis/Hangly-Windows/license` returns `spdx_id = MIT`, `name = MIT License`, `key = mit`. **[MEASURED]** — and it did not, until today: see §2 |
| 2 | A public repository | **Met** | `gh repo view` reports `visibility=PUBLIC`. **[MEASURED]** |
| 3 | **Already released in the form that should be signed** | **Met, with a question** | `v0.9.1` is published with twelve assets for two architectures, and `v0.9.0` before it. Both are marked **pre-release**, and whether that satisfies "released" is §3. **[MEASURED]** |
| 4 | Actively maintained | **Met** | Continuous commit history, CI green on every commit, an issue template and a security policy. **[MEASURED]** |
| 5 | Functionality described on a download page | **Met** | The README opens with what Hangly is, shows two screenshots, and the release page carries the changelog for that version. **[MEASURED]** |
| 6 | No malware or security-circumvention tooling | **Met** | It hangs a charm on a desktop. `SECURITY.md` describes the actual attack surface — imported SVG, the charm store, the updater — and a 42-file hostile corpus is in the test suite. **[VERIFIED]** |
| 7 | A verifiable automated build from the source in that repository | **Met** | `.github/workflows/release.yml` builds and packages both architectures; every asset on `v0.9.0` came out of run `35589550778`. The packaged executable's `ProductVersion` reads `0.9.0+416b85d8be2c4dc25f211d357ee3f3359f5deb4f`, so the artefact names the commit that produced it. **[MEASURED]** |
| 8 | The SignPath GitHub App installed, signing inside the workflow run | **Not yet** | Cannot be done before the application is accepted. `SIGNPATH-CHECKLIST.md` §4 |
| 9 | Every release manually approved before signing | **Not yet** | Same — configured after acceptance |

Conditions 8 and 9 are *post-acceptance* configuration, not application prerequisites.
Nothing in this repository blocks them: `vpk pack` already takes `--signTemplate`, so
signing drops into the existing packaging step rather than restructuring it.

## 2. What was wrong, and is now fixed

Two things would have been visible to a reviewer this morning.

**GitHub reported the licence as "Other".** `spdx_id` was `NOASSERTION`. The text was
plainly MIT, but GitHub matches `LICENSE` against the standard templates and two
deviations broke the match: `Copyright ©` where the template says `(c)`, and a section of
artwork and branding terms appended below the licence. For a project whose first
eligibility condition is *an OSI-approved licence*, a sidebar reading **Other** is the
worst possible first impression. `LICENSE` is now exactly MIT; the artwork terms moved to
`NOTICE.md` word for word. **[MEASURED]** before and after.

**There were no screenshots.** For an application whose entire subject is something you
look at, the repository contained one image, and it was a payment QR code. There are two
now, in the README.

Also added, because their absence reads as an unfinished project rather than a small one:
`CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `NOTICE.md`, an issue template
with a security contact link, repository topics and a homepage URL.

## 3. The one open question

**Does a GitHub pre-release satisfy "already released"?**

`v0.9.0` is published and downloadable, and it is marked as a pre-release because it is a
beta. SignPath's terms and their GitHub integration documentation are both silent on
pre-releases.

This is not something to assume in either direction. It is one line in the application,
and the answer decides whether the first signed build is v0.9.x or v1.0.

**Suggested wording, to include in the application rather than ask separately:**

> Hangly for Windows has a public release, currently marked as a pre-release while it is
> in beta with a small group of testers. If SignPath Foundation requires a stable,
> non-pre-release version before signing, please say so and I will apply again once v1.0
> ships — I would rather ask than assume.

Asking inside the application costs nothing and removes the only reason this could be
declined on a technicality.

## 4. Rejection risks, in order of likelihood

| Risk | Likelihood | What reduces it |
|---|---|---|
| **The pre-release question** (§3) | Moderate | Ask it in the application, in the applicant's own words |
| **The artwork clause reads as "not fully open source"** | Low–moderate | `LICENSE` is unmodified MIT, so the automated check passes. `NOTICE.md` is separate and says plainly that the code is MIT "full stop". This is the same separation many projects make between code and brand assets. Worth one sentence in the application so a reviewer meets it from the applicant rather than discovering it |
| **`reference/swift/` looks like third-party code** | Low | It is a read-only copy of the *same author's* macOS application, kept so the port can be checked against the original. `NOTICE.md` says so explicitly. A reviewer skimming the file tree sees Swift in a C# repository and may wonder; the answer is one click away |
| **Project maturity** — no stars, one maintainer, a young repository | Low | Not a stated condition. SignPath Foundation exists for exactly this kind of project. The counterweight is visible seriousness: 759 tests, CI on every commit, a documented privacy policy, a security policy, and release artefacts that name their own commit |
| **Unsigned artefacts already published** | None | This is the situation the programme is for |

## 5. What a first-time reviewer sees

Walked through in the order somebody actually looks.

1. **The repository header** — a description, a homepage, eight topics, and **MIT License**
   in the sidebar.
2. **The README** — what Hangly is in one sentence, a screenshot of it running, what it
   does, how to install it, which file to pick, an honest note that Windows will warn
   because it is not signed yet, how to build it, how to report a problem, the privacy
   summary and the roadmap.
3. **The Releases page** — `v0.9.0`, twelve assets, two architectures, release notes that
   describe the release rather than listing commits.
4. **The file tree** — `src`, `tests`, `tools`, `Docs`, and the community files where they
   are expected.
5. **The documents, if they read further** — `PRIVACY.md` lists every analytics event and
   what is never sent; `SECURITY.md` names the real attack surface; `Docs/DISTRIBUTION.md`
   explains the signing plan and states that SignPath Foundation is the intended route.

There is nothing here that needs explaining away.

## 6. Recommendation

**Submit now.** `v0.9.1` is published — the build with a working update check, verified
updating itself from GitHub — and nothing else in this repository needs doing first.

Do it with the pre-release question written into the application (§3). Everything else is
in place and nothing further in this repository needs doing first.

**Do not wait for v1.0.** The application takes weeks of somebody else's calendar, it is
the longest lead time in the project, and the only thing waiting achieves is delaying an
answer to a question that can be asked now.
