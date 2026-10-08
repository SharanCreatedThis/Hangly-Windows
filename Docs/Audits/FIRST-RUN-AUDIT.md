# First five minutes

21 September 2026, at commit `648efaa`. What somebody meets between downloading Hangly and
putting it down, walked through on a clean profile rather than imagined.

Labels: **[MEAS]** taken off this machine, **[LIVE]** watched happening, **[DOC]** stated
by the code or the vendor, **[INFER]** follows but was not observed.

The six issues testers reported are fixed and verified — §1 records what they were, because
a fix with no account of the fault is a claim. §2 is what is still wrong, found by doing
the same walk again afterwards.

---

## 1. What testers hit, and what it was

| # | Reported | Root cause | State |
|---|---|---|---|
| 1 | "It closes after you type your name and you have to open it again" | WinUI ends the process when its last window closes. On a first run the welcome card is the only XAML window Hangly has. **[LIVE]** — log says `welcome card completed`, then no process | **Fixed.** The onboarding windows hide instead of closing **[LIVE]** |
| 2 | "I thought that was it — it just asked my name" | There was no welcome, only a name prompt | **Fixed.** A second step: what Hangly is, and *Explore Library* / *Start Using Hangly* **[LIVE]** |
| 3 | "It doesn't come back when I restart" | Not broken — disabled, and re-disabled. The flag defaults to false and bootstrap reconciles it *from the registry*, so a changed default is overwritten on the next line **[DOC]** | **Fixed.** First run writes the Run entry **[MEAS]** |
| 4 | "The settings window opens in a weird place" | Nothing positioned it. It was resized and never moved, so the shell cascaded it **[DOC]** | **Fixed.** Centred on the work area; left gap 608, right gap 608 **[MEAS]** |
| 5 | "You can't really browse the charms" | The detail panel held a fixed column whether or not anything was selected, and ~380 points of cord controls sat above the first charm in an 800-point window | **Fixed.** Cord controls moved to the sidebar; nine collection cards visible where two or three were **[MEAS]** |
| 6 | "Why is it a grey bead" | The first-run charm was the *fallback* charm | **Fixed.** Nazar, gold chain, top right, 85% rope, 145% charm **[MEAS]**, pinned in a test |

## 2. Still rough, found on the walk afterwards

Nothing here blocks the beta. They are listed in the order somebody meets them.

### 2.1 The download page asks a question most people cannot answer **[INFER]**

The releases page offers `Hangly-win-x64-Setup.exe` and `Hangly-win-arm64-Setup.exe`, and
the guidance is *Settings → System → About → System type*. That is three clicks into a
settings app to answer a question about a charm. Most people will guess, and most guesses
will be right, but the x64 build on an ARM machine runs under emulation and says nothing
about it.

**Worth doing before a wider beta:** one combined installer that picks, or plainer wording
— "almost certainly this one" next to x64.

### 2.2 SmartScreen is still the first thing that happens **[VERIFIED]**

Unavoidable until SignPath signs a release, and documented in three places. It remains the
single largest drop-off in the funnel and nothing in this pass changed it.

### 2.3 The welcome card is taller than its contents **[MEAS]**

560 × 480 points for both steps. The first step fills it; the second leaves roughly a third
of the card empty below the buttons. It reads as something failing to load.

**Fix:** size the window to its content, or shrink it to ~430 for the second step. Not
done in this pass because resizing a window mid-flow draws the eye to the wrong thing, and
choosing between the two wants somebody looking at it rather than measuring it.

### 2.4 Nothing points at the tray icon except a sentence **[INFER]**

The welcome card says *"Right-click the Hangly icon near the clock"*. On Windows 11 that
icon is behind the overflow chevron by default, so the sentence is describing something
the person cannot see. Every route into the app — Customize, Library, Create, About, Quit —
is behind it.

**Worth considering:** opening the Library once on first run, so the window has been seen
at least once and the person knows it exists. *Explore Library* offers exactly that, and a
tester who picks *Start Using Hangly* never sees it.

### 2.5 The charm can be lost behind the taskbar's clock area **[INFER]**

The default anchor is now top right. On a display with a top-docked taskbar the overlay
uses the work area, so this is handled; the charm sits under whatever is in the top right
corner of *other windows*, which is where the notification area, window controls and many
apps' toolbars are. The Library's Charms/Ropes switch was moved off that corner for exactly
this reason during this pass.

**Not a defect.** It is the cost of a top-right default, and top right was asked for.
Worth watching in tester feedback.

### 2.6 There is no way back to the welcome card **[VERIFIED]**

`WelcomeWindow.IsNeeded` is false forever once a name is stored. Somebody who dismisses it
in three seconds has no way to see what Hangly does, short of editing `settings.json`.

**Fix:** a "Show welcome again" item on the About page. Small, and not in this pass.

### 2.7 The cord controls are now above the charm they describe **[MEAS]**

In the redesigned sidebar the size slider says *"Size of Nazar boncuğu — 100% of its own"*
and sits **above** the picture of the charm it is talking about. It reads slightly
backwards.

**Fix:** put the detail above the cord controls, or move the slider under the detail. Left
alone deliberately: the cord block has to stay near the top, because it is what somebody
opening the Library came to change.

### 2.8 Collections are cards, charms are tiles, and the difference is not obvious **[INFER]**

Clicking a collection card and clicking a charm tile do different things — one filters, one
hangs a charm — and they are the same shape at a glance. Nothing says which is which until
you click one.

## 3. Measurements after the change

Normal launch, one charm, same machine as every other measurement in this repository:

| | Before this pass | After |
|---|---|---|
| Process start to `overlay window shown` | 317 ms | **253 ms** |
| Working set | 142.1 MB | **135.8 MB** |
| Handles | 539 | **539** |
| Idle CPU, 60 s | 1.15% | **0.83%** |

**Neither number should be read as an improvement.** The profile differs — the earlier
measurements carried three charms and these carry the new default of one — and the honest
claim is that none of these changes costs anything measurable. The handle count is the one
to watch, because the first version of the first-run fix put it at **736**: an empty window
held open for the life of the process costs about twelve megabytes and two hundred handles,
and measuring it is what replaced it with something free.

## 4. Updater and data implications

**None.** Checked rather than assumed:

- **No identifier moved.** Package id, channel, feed names, install directory, uninstall
  key, Run value name, mutex name and AUMID are all untouched. `Docs/UPDATE-INFRASTRUCTURE.md`
  §1 is the list, and every entry on it is still what it was.
- **No settings schema change.** The new defaults are *initialiser* values, which apply
  only to keys absent from a document. An existing `settings.json` writes every key, so an
  updating user keeps their charm, their rope, their position and their sizes.
  **[VERIFIED]** by how `AppSettings.FromJson` binds, and by the existing test that a
  partial document keeps its own values.
- **Launch at login does not change for existing users.** It is switched on only when
  `WelcomeWindow.IsNeeded` is true, which is false for anybody who has completed onboarding.
- **No analytics event was added, removed or renamed.**
- **One new type**, `RopeChoiceItem`, used only in the Library's rope list.

The one consequence worth stating: **an existing user updating to 0.9.2 will not see the
new defaults, the welcome card, or launch-at-login switched on.** That is correct — they
chose what they have — but it means the fixes above can only be confirmed by a tester on a
clean profile, or by deleting `%APPDATA%\Hangly` first.
