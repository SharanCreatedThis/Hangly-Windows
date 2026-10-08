# Beta tester audit

21 September 2026, at commit `648efaa` plus the fixes in this pass. Written by installing
Hangly from a packaged installer onto a machine with no trace of it — no install directory,
no settings, no registry entry, no thumbnail cache — and going through it as somebody who
has never seen it.

Labels: **[LIVE]** watched happening, **[MEAS]** a number taken off the machine,
**[INFER]** follows from something verified but was not itself observed, **[NOT VERIFIED]**
where it says so.

---

## The walk, measured

**[MEAS]** From double-clicking the installer to a library of charms on screen:

| | |
|---|---|
| Clicks | **3** — run the installer, *Continue*, *Explore Library* |
| Installer → welcome card, charm already hanging | **6.6 s** |
| *Explore Library* → Library open, cold thumbnail cache | **2.8 s** |
| Total | **18.1 s** |
| Tray icon needed | **No** |

A correction worth recording, because it nearly went into this document as a finding: the
first measurement of the Library opening read **12.2 s**, and it was wrong. The script
slept for twelve seconds and then looked. The app's own log said the window was built in
160 ms. Re-measured by polling instead of sleeping, on a cache cleared first, it is 2.8 s
cold and about 0.2 s warm.

**Onboarding can be completed without finding the tray icon. [LIVE]** That was the point of
the *Explore Library* button and it holds: install, name, welcome, library, charm — nothing
in that path requires the notification area.

---

## What is confusing

### 1. Which file to download **[INFER]**

The releases page offers four files and the guidance is *Settings → System → About → System
type*. That is three clicks into a settings app to answer a question about a desktop
ornament, before anything has been installed.

Most people will guess x64 and be right. An ARM machine that takes the x64 build runs it
under emulation and never says so — it works, slowly, and nothing explains why.

**Would generate support requests:** yes. "It's really slow" from ARM laptop owners.

### 2. SmartScreen **[VERIFIED]**

Still the first thing that happens, still unavoidable until SignPath signs a release.
Documented in `TESTER-INSTRUCTIONS.md`, the README and the release notes, and it will still
stop some people. **This is the largest single drop-off in the funnel** and nothing in this
pass changed it.

### 3. The tray icon is hidden by default **[VERIFIED]**

Windows 11 puts new notification icons in the overflow behind the `˄` chevron. Every route
into the app — Customize, Library, Create, About, Quit — is behind that icon.

**Fixed as far as copy can fix it.** The welcome card now says where Hangly lives, that
Windows hides new icons, how to get it out, and what right-clicking it does. It used to say
*"right-click the Hangly icon near the clock"*, which described something the person could
not see.

**Still true:** somebody who chooses *Start Using Hangly* and later wants to change their
charm has to find the chevron. The copy tells them; nothing shows them.

### 4. Collections and charms look alike **[INFER]**

A collection card and a charm tile are both rectangles with a picture and a name, and
clicking them does different things — one filters the view, one hangs a charm on your rope.
Nothing distinguishes them until you click one.

---

## What is hidden

| | |
|---|---|
| The tray icon | Behind the chevron, by Windows' own default. Copy now explains it |
| The Ropes half of the Library | Behind a switch that is easy to miss beside the title. **[LIVE]** it works; whether anybody finds it is **[NOT VERIFIED]** |
| The Create tab | In the navigation pane, which is only reachable once the Library window is open |
| *Show welcome again* | On the About page, under **Help**. Added in this pass |
| Everything about privacy | `PRIVACY.md` is thorough and lives in the repository. The About page has the switch but does not link to the document **[VERIFIED]** |

---

## What causes abandonment

In order of how many people it will lose, worst first. All **[INFER]** — none of this has
been watched happening to a real tester, because the beta has not been handed out yet.

1. **SmartScreen.** A warning that looks like a malware warning, on a file from somebody
   they know, before anything has happened.
2. **Picking the wrong architecture and getting a slow app**, then blaming the app.
3. **Dismissing the welcome and not finding the tray icon.** The charm hangs there and
   does nothing anybody asked it to; there is no visible way in.
4. **Expecting more than an ornament.** Nothing in the copy promises more, but a desktop
   utility that does one thing needs that thing to be charming within about a minute.

---

## What would generate support requests

| Likely question | Answer that exists | Where |
|---|---|---|
| "Windows says it's dangerous" | Yes, it is unsigned; here is why | `TESTER-INSTRUCTIONS.md`, release notes |
| "Which file do I download?" | A table and a settings path | Release notes, README |
| "Where did it go?" | Behind the chevron | Welcome card |
| "How do I get rid of it?" | Settings → Apps → Installed apps | `TESTER-INSTRUCTIONS.md` |
| "Is it watching me?" | A full list of every event | `PRIVACY.md` — **not linked from inside the app** |
| "It's slow" | Probably the x64 build on an ARM machine | **Nowhere.** No document says this |

**Two gaps worth closing before a wider beta**, neither done in this pass: link
`PRIVACY.md` from the About page, and say somewhere that the wrong architecture will run
slowly rather than fail.

---

## Fixed in this pass

Everything below was a usability defect rather than a preference, and each was verified
after the change.

| | Was | Now |
|---|---|---|
| Copy assumed a visible tray icon | *"Right-click the Hangly icon near the clock"* | Says where it lives, that Windows hides it, and how to get it out **[LIVE]** |
| No way back to the welcome | Gone forever once a name was stored | **About → Help → Show welcome again**, opening on the second step because the name is already known **[LIVE]** |
| Charm's collection was invisible | Region and tags only | Collection shown under the region in the sidebar |
| Collection cards were wide | 268 points, two per row | 222 points — still two per row at the default width, because the navigation pane takes 200 points that were not in the arithmetic. **The row count did not change. [MEAS]** |
| Sidebar | 296 points | 278 points |

**The collection card change did not achieve what it was for, and saying so is the point.**
Narrowing the card from 268 to 222 was meant to fit three per row; measured afterwards, the
browse column is about 620 points at the default window size, and three cards need 696. It
is a slightly tighter layout that scrolls the same amount.

---

## Not verified

Stated so it is not mistaken for tested.

- **Charm-grid density**, as opposed to collection density. The automation harness could
  not reliably drive the search box to produce a full grid, and the number was not taken
  another way. Tiles are smaller than they were — 84 × 102 rather than 96 × 116 — which is
  arithmetic, not a measurement of what somebody sees.
- **Scroll distance to the last charm.** Same reason.
- **Whether anybody finds the Ropes switch**, the Create tab, or the chevron. These need
  testers, which is what the beta is for.
- **Everything on any machine but this one.** One display, 200% scaling, ARM64 under
  Parallels.
