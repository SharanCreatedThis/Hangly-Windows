# SignPath Foundation — the application, field by field

Written against the real form at **signpath.org/apply**, seen 21 September 2026.
Paste-ready answers, with the reasoning next to each one.

**Three fields need you and cannot be answered from the repository:** *First Name*,
*Last Name* and *Primary Discovery Channel*. They are marked **[YOU]** below.

**Read §3 before you submit.** The form asks something the eligibility conditions do not,
and it changes the timing advice I gave earlier.

---

## 1. The fields

### Project Name *
> *A Google search for this name should clearly identify your project.*

```
Hangly for Windows
```

Not just "Hangly": the macOS original carries that name and searching for it finds the
product page rather than this repository. "Hangly for Windows" identifies exactly this
project.

### Repository URL *
```
https://github.com/SharanCreatedThis/Hangly-Windows
```

### Homepage URL *
> *The project's official homepage. This can be a dedicated website or the repository page.*

```
https://github.com/SharanCreatedThis/Hangly-Windows
```

**Use the repository, not the product site.** The form explicitly allows it, and
`sharancreatedthis.in/products/hangly` currently says *"Download for macOS · macOS 14+ ·
Made for Mac"* with no mention of Windows. A reviewer checking whether the homepage
describes *this* project would find a page about a different one. The repository page
describes the Windows build, shows it, and links the downloads.

*(Worth doing later, not before applying: add a Windows section to the product page, then
this field can point there.)*

### Download URL
> *A page where users can download your software. This page must mention that the project
> uses the SignPath Foundation for code signing.*

```
https://github.com/SharanCreatedThis/Hangly-Windows/releases
```

**The requirement is met as of commit `d3d0a48`.** The v0.9.1 release page now carries:

> **Code signing for this project is provided by [SignPath Foundation](https://signpath.org/).**

The release workflow appends the same section to every future release, and the README has
a *Code signing* section saying the same thing, so the requirement cannot quietly lapse.

### Privacy Policy URL
> *Required if the software collects user data.*

```
https://github.com/SharanCreatedThis/Hangly-Windows/blob/main/PRIVACY.md
```

Hangly does collect user data — a display name you type, and a listed set of usage events
— so this field applies and is not optional. The document lists every event, names the one
that is defined and never sent, and says how to switch it all off.

### Wikipedia URL (optional)
```
(leave blank)
```

### Tagline *
> *A short one-sentence summary. This may be displayed on the SignPath Foundation website.*

```
A charm hangs from a simulated rope on your Windows desktop and swings when you push it.
```

### Description *
> *A short paragraph describing your project and its purpose. Avoid listing
> version-specific features or dependencies.*

```
Hangly for Windows is a desktop ornament. A charm hangs from a rope at the top of your
screen and moves the way a real one would, solved with real rope physics rather than
animated, so it has weight and settles rather than looping. You choose from a collection
of charms, hang more than one at a time, pick how the cord looks, and make charms of your
own from your own pictures. The window is transparent and clicks pass through it
everywhere except the charm itself, so it sits over whatever you are doing without getting
in the way. It asks for no permissions, has no accounts, and has no server behind it. It
is a port of a macOS application of the same name, written for Windows 10 and 11 on both
Intel and ARM processors.
```

### Reputation *
> *Provide links or information showing that your project is widely used or trusted.
> Examples include media coverage, blog posts, download statistics, GitHub insights, or
> community discussions.*

```
I would rather be straight with you than dress this up: Hangly for Windows is new. The
repository went public on 19 September 2026 and the first public beta was published on 21
September, so there are no download numbers, no press and no community to point at yet.

What I can point to:

- It is a port of Hangly for macOS, a finished product of mine with its own product page:
  https://www.sharancreatedthis.in/products/hangly

- The Windows port is built the way a maintained project is built, and that is all visible
  in the repository: 759 automated tests that run on every commit, continuous integration
  green on every commit, a published privacy policy, a security policy, a contributing
  guide and an issue template. Every release artefact carries the commit it was built from
  in its version string, so any published binary can be traced back to its source.

- Public beta releases exist for both x64 and ARM64, built entirely by GitHub Actions,
  with an update mechanism verified end to end against the live release feed.

The reason I am applying now rather than later is the warning. Hangly is downloaded
directly rather than through a store, and every unsigned download shows "Windows protected
your PC" with no publisher name. My current install instructions have to tell people to
click through a warning that looks exactly like a malware warning, which is not advice
anyone should be giving, and it is the single biggest obstacle to the project being used
at all.

If the Foundation would rather see real usage before signing a project, I understand
completely, and I will apply again once the beta has been out for a while. I would
appreciate knowing either way.
```

**Do not embellish this field.** An application that claims traction a reviewer can check
in thirty seconds and disprove is worse than one that is candid. The honest version is
also the one that reads like somebody worth helping.

### Maintainer Type
```
Individual
```
*(Choose the closest option; the project has one maintainer and no organisation behind it.)*

### Build System *
```
GitHub Actions
```

### First Name * — **[YOU]**
```
Sharan
```
Correct this if the account should carry your full legal first name. I only know you as
Sharan, and I am not going to guess at the rest.

### Last Name * — **[YOU]**
```
(your surname)
```
I do not know it, and guessing from an email address is how accounts end up with the wrong
name on a certificate.

### Email *
```
swarnsharan@gmail.com
```
This is the address in `CODE_OF_CONDUCT.md` and `SECURITY.md`, so it matches what the
repository says.

### Company Name
```
(leave blank)
```

### Primary Discovery Channel * — **[YOU]**
> *How did you first discover the SignPath Foundation?*

Answer this one truthfully from your own memory — it is the one field where I have no
idea what the true answer is. If it was through looking into free code signing for
open-source Windows applications, the closest options are usually "Google search" or
"GitHub repository".

### Please specify the exact source (optional) — **[YOU]**
Follows from the answer above. If it came out of researching signing options for this
project, say so.

### Checkboxes
- **"I have read and agree to the SignPath Foundation Code of Conduct…"** — **required,
  tick it.** Read it first; it is short.
- **"I agree to receive other communications from SignPath."** — optional, your call.
- **"I agree to allow SignPath to store and process my personal data."** — **required,
  tick it.**

---

## 2. Eligibility, against their conditions

| Condition | Verdict | Evidence |
|---|---|---|
| OSI-approved licence, no commercial dual-licensing | **Pass** | GitHub's licence API reports `spdx_id: MIT` |
| Public repository | **Pass** | `visibility: PUBLIC` |
| Already released in the form to be signed | **Pass**, with the pre-release question below | `v0.9.1`, twelve assets, two architectures |
| Actively maintained | **Pass** | 67 commits, CI green on all of them |
| Download page describing the software, mentioning SignPath Foundation | **Pass** as of `d3d0a48` | The releases page and the README |
| No malware or circumvention tooling | **Pass** | It hangs a charm on a desktop |
| Verifiable automated build from the repository | **Pass** | Every artefact names its commit in `ProductVersion` |
| SignPath GitHub App installed | After acceptance | Cannot be done before |
| Manual approval per release | After acceptance | Cannot be done before |

Detail in `SIGNPATH-READINESS.md`.

## 3. Risk of rejection — and a correction to my earlier advice

**I previously recommended submitting immediately. Having now seen the form, I would put
the odds lower than I implied, and the reason is the Reputation field.**

The eligibility conditions say nothing about how widely used a project is. The form asks
for it directly, as a required field, and gives "download statistics, media coverage,
GitHub insights" as the examples. Hangly for Windows has none of those: **0 stars, 0
forks, and a repository that has been public for two days.** That is the honest position
and a reviewer will see it in seconds.

| Risk | Likelihood | Notes |
|---|---|---|
| **Reputation — no usage to show** | **Moderate to high** | The one real risk. Nothing in the repository fixes it; only time and users do |
| The pre-release question | Moderate | `v0.9.1` is marked pre-release. Their terms are silent on whether that counts as "released". Ask it in the application rather than assume |
| The artwork clause in `NOTICE.md` | Low | `LICENSE` is unmodified MIT, so the automated check passes. Declare it rather than let it be found |
| `reference/swift/` looks like third-party code | Low | It is the same author's macOS source, kept for checking the port. `NOTICE.md` says so |

### Two ways to play it

**Submit now.** The honest Reputation answer is respectable, the engineering evidence is
real, and the lead time is long — weeks of somebody else's calendar. A decline costs time
and tells you what they want to see.

**Or wait four to six weeks.** Run the friend beta, let the download counter move, collect
a few issues and stars, and apply with numbers in the Reputation box instead of an
apology. The application is materially stronger and nothing else about it changes.

**My recommendation: submit now**, but with your eyes open rather than because I told you
it was a formality. The deciding argument is that every week unsigned is a week of people
meeting a malware-looking warning, and that is the thing suppressing the very usage the
Reputation field wants to see. If they decline, ask them what would change the answer and
apply again after the beta.

**What I do not know:** whether SignPath Foundation allows re-application after a decline.
If that matters to you, ask them before submitting — a short email costs nothing and
removes the only argument for waiting.

## 4. Before you paste

- [ ] Fill in **Last Name** and check **First Name**.
- [ ] Answer **Primary Discovery Channel** truthfully.
- [ ] Read the **Code of Conduct** you are agreeing to.
- [ ] Open https://github.com/SharanCreatedThis/Hangly-Windows/releases and confirm the
      SignPath Foundation line is visible on the page. It is what the Download URL field
      requires and it is the one thing a reviewer will check mechanically.
