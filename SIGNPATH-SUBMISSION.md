# SignPath Foundation — the application, filled in

Everything to paste into the form, plus the reasoning behind each answer.

> **One caveat, stated up front.** I have not seen the current application form. The field
> names below are the ones SignPath Foundation's application is known to use; if a field
> on the real form is named differently or asks something not covered here, the substance
> is in §1 and §2 and can be re-cut to fit. **Check each answer against the form before
> pasting.**

`SIGNPATH-READINESS.md` has the eligibility assessment and the evidence. This document is
the text.

---

## 1. The fields

### Project name
```
Hangly for Windows
```

### Project description — one line
```
A desktop ornament for Windows: a charm hangs from a simulated rope on your desktop and
swings when you push it.
```

### Project description — longer
```
Hangly for Windows is a desktop ornament. A charm hangs from a rope at the top of your
screen, and it moves the way a real one would -- twenty rope segments solved with Verlet
integration at a fixed 240 Hz, so it has weight and it settles rather than animating.

There are seventy charms in collections, up to three can hang on one cord at their own
sizes, and you can make your own from a picture or an SVG. The window is transparent and
click-through everywhere except the charm itself, so it sits over whatever you are
working on without getting in the way. It asks for no permissions, has no accounts and
has no server.

It is a port of Hangly for macOS, written in C# on WinUI 3, .NET 9 and Win2D, and it runs
on Windows 10 1809 and later on both x64 and ARM64.
```

### Repository URL
```
https://github.com/SharanCreatedThis/Hangly-Windows
```

### Project website
```
https://www.sharancreatedthis.in/products/hangly
```

### Licence
```
MIT
```

> GitHub's licence API reports `spdx_id: MIT` for this repository, so the automated check
> and the human check agree.

### Applicant / maintainer
```
Sharan (sharancreatedthis)
swarnsharan@gmail.com
Sole maintainer and copyright holder.
```

### Is the project open source?
```
Yes. All source code is MIT, in the repository above.

One thing worth declaring rather than leaving to be discovered: the charm artwork, the
application icon and the Hangly name are the author's own and are not MIT -- they are
covered separately in NOTICE.md. The LICENSE file itself is unmodified MIT and covers all
code. This is the usual separation between an open-source codebase and the brand and
artwork shipped with it; there is no dual licensing, no commercial edition, and no paid
tier.
```

### What will be signed
```
Windows application binaries and installers produced by Velopack:

  Hangly-win-x64-Setup.exe
  Hangly-win-arm64-Setup.exe
  Hangly.exe and the executables inside each package

Two architectures per release, x64 and ARM64.
```

### Build system / CI
```
GitHub Actions. The release workflow is at .github/workflows/release.yml and is triggered
by a version tag. It builds each architecture, packages with Velopack, and uploads the
artefacts to a GitHub release; nothing is built or uploaded from a developer machine.

Each packaged executable carries the commit it was built from in its ProductVersion
string -- for example 0.9.0+416b85d8be2c4dc25f211d357ee3f3359f5deb4f -- so any published
artefact can be traced to its source.

The packaging tool already accepts a signing command (vpk pack --signTemplate), so
signing drops into the existing step inside the workflow run that produced the artefact.
```

### Release frequency
```
Expected to be low: a handful of releases a year. Currently in beta with a small group of
testers ahead of v1.0.
```

### Why the project needs code signing
```
Hangly is downloaded directly rather than through a store, and every unsigned download
shows "Windows protected your PC" with no publisher name. For a small desktop ornament
that is a hard first impression to recover from -- people reasonably read that warning as
a virus warning, and the current advice in the install instructions is to click through
it, which is not advice anybody should be giving.

Signing also means the identity accrues reputation across versions rather than every
release starting from nothing.

A paid certificate is not something this project can justify, which is why SignPath
Foundation is the route rather than a fallback.
```

### Anything else the reviewer should know
```
Two things I would rather state than have found.

The repository contains a directory called reference/swift/. It is a read-only copy of
parts of the macOS version of Hangly -- the same author's code -- kept so the Windows port
can be checked against the original behaviour. It is not third-party code and it is not
built or shipped.

The current public release, v0.9.1, is marked as a pre-release because it is in beta with
a small group of testers. If SignPath Foundation requires a stable, non-pre-release
version before signing, please say so and I will apply again once v1.0 ships -- I would
rather ask than assume.
```

### Terms
```
Agreed: OSI-approved licence, public repository, verifiable automated build, and manual
approval of every signing request.
```

---

## 2. If the form asks for a single paragraph

Some application forms have one free-text box. This is the whole case in one:

```
Hangly for Windows is an MIT-licensed desktop ornament -- a charm that hangs from a
simulated rope on your desktop and swings with real physics. It is a C# / WinUI 3 port of
a macOS application by the same author, it runs on Windows 10 and 11 on x64 and ARM64, and
it is built and packaged entirely by GitHub Actions, with each artefact carrying the
commit it came from. It is distributed as a direct download rather than through a store,
so every release currently shows an unsigned-publisher warning that people reasonably
mistake for a virus warning. The project asks for no permissions, has no server and no
accounts, and publishes a full privacy policy and security policy in the repository. A
paid certificate is not something a project this size can justify, which is why I am
applying to the Foundation. The current release is marked as a pre-release while it is in
beta; if a stable release is required first, please tell me and I will re-apply at v1.0.
```

---

## 3. Eligibility assessment

**Eligible, on eight of nine conditions, with the ninth ambiguous rather than failed.**

| Condition | Verdict |
|---|---|
| OSI-approved licence, no commercial dual-licensing | **Pass** — MIT, confirmed by GitHub's own API |
| Public repository | **Pass** |
| Already released in the form to be signed | **Pass, pending their reading of "pre-release"** |
| Actively maintained | **Pass** |
| Functionality described on a download page | **Pass** |
| No malware or circumvention tooling | **Pass** |
| Verifiable automated build from the repository | **Pass** |
| SignPath GitHub App installed | **After acceptance** |
| Manual approval per release | **After acceptance** |

Full evidence for each in `SIGNPATH-READINESS.md` §1.

## 4. Risk of rejection

**Low, with one moderate item.**

1. **The pre-release question — moderate.** Handled by asking it in the application.
2. **The artwork clause — low.** `LICENSE` is unmodified MIT so the automated check
   passes; the declaration in §1 means a reviewer meets it from the applicant rather than
   finding it.
3. **`reference/swift/` — low.** Declared in §1 and explained in `NOTICE.md`.
4. **Project maturity — low.** Not a stated condition, and the programme exists for small
   projects. 759 tests, CI on every commit, and published privacy and security policies
   are the counterweight.

Nothing here is a reason to delay.

## 5. When to submit

**Now — as soon as `v0.9.1` is published.**

The application is answered, the repository is in order, and the only open question is
asked inside the application rather than waiting on an answer before sending it.

This has been the longest-lead item in the project for weeks. Waiting for v1.0 does not
improve the application; it only delays the reply.

**Before pasting:** read §1 against the real form, and check the two places that name a
version — the "anything else" answer and the single paragraph — still say `v0.9.1`.
