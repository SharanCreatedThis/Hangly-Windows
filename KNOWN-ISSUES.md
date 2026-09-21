# Known issues

As of the v0.9.0 beta, 21 September 2026. Everything here is known and written down, so
please do not spend time writing up something already on the list — although if you hit
one and it is worse than described, that is worth saying.

Labels are about **how each is known**: **[MEASURED]** on the author's machine,
**[VERIFIED]** by reading the code, **[UNVERIFIED]** means nobody has tested it either way.

---

## Expected, and not bugs

**Windows warns you when you run the installer.** *"Windows protected your PC."* The build
is not code-signed. Click More info → Run anyway. Signing is being arranged through
SignPath Foundation. **[VERIFIED]**

**The download is about 120 MB**, and **so is each update.** It contains its own copy of
.NET, so there is nothing else to install and nothing else to keep up to date. Velopack can
ship small differences between versions instead, and the release pipeline is not set up to
produce them yet — so updating from one beta to the next downloads the whole thing again.
Measured: the 0.9.0 → 0.9.1 update was 122.9 MB and took 68 seconds. **[MEASURED]**

**The charm draws over full-screen windows**, including games and video. There is no
"hide when something is full-screen" behaviour yet. **[MEASURED]**

**Uninstalling leaves your settings behind** in `%APPDATA%\Hangly`, on purpose, so a
reinstall remembers your charms. Delete that folder to be rid of them. **[VERIFIED]**

---

## Fixed since the first beta

These were the first round of tester reports, and they are done. If you are updating from
0.9.1 rather than installing fresh, note that **the new defaults and the welcome flow only
appear on a new profile** — you keep the charm and rope you already chose, which is the
point.

- The app no longer shuts down after you type your name. It used to, and you had to start
  it a second time.
- There is a welcome step after the name, so it is clear setup finished.
- Hangly starts with Windows by default, and appears in Startup Apps. Turn it off in
  **Customize → Appearance**.
- The Customize window opens in the middle of the screen instead of wherever Windows felt
  like putting it.
- The Library gives browsing most of the window, has a **Charms / Ropes** switch, and shows
  three or four times as many collections at once.
- A new install hangs a nazar on a gold chain at the top right.

## Known problems

**Opening the Library costs about 140 MB of memory and does not give it back** when you
close the window. Hangly sits around 150 MB before you open it and about 290 MB
afterwards, for the rest of the session. It is not a leak that grows — it stops there —
but it is more than it should be. **[MEASURED]**

**A hard throw keeps the CPU busy for a while.** Dragging costs about a quarter of one
core, and a rope thrown hard keeps swinging — and drawing — for the best part of half a
minute afterwards. At rest it is about 1%. **[MEASURED]**

**Uninstalling does not remove the "run at login" entry** if you turned that on. Windows
will try to start a program that is no longer there at each login, and silently fail. It
is untidy rather than harmful. **[VERIFIED]**

**A corrupted settings file is replaced silently.** If `settings.json` is damaged — a
power cut mid-write, say — Hangly starts with defaults and does not tell you your old
settings are gone. **[VERIFIED]**

**A favourite of a charm that no longer exists disappears.** Eleven seasonal charms were
removed from the catalogue before this release; if an old settings file referenced one, it
is dropped on load rather than shown as missing. Only affects people who ran a
pre-release build. **[MEASURED]**

---

## Not tested by anybody, on any machine

This is the honest list, and it is the main reason for the beta.

| | |
|---|---|
| **x64 machines** | The x64 build is packaged and published. It has never been installed or run. **[UNVERIFIED]** |
| **Any scaling except 200%** | 100%, 125%, 150%, 175% are all untested. **[UNVERIFIED]** |
| **More than one monitor** | Including which display it picks and what happens when you unplug one. **[UNVERIFIED]** |
| **Two monitors at different scalings** | **[UNVERIFIED]** |
| **Sleep and wake** | **[UNVERIFIED]** |
| **Lock and unlock** | **[UNVERIFIED]** |
| **Windows 10** | The installer claims to support 1809 and later. Nobody has run it on Windows 10. **[UNVERIFIED]** |
| **Taskbar auto-hide** | Turning it on changes the usable screen area, and Hangly may not notice until something else moves it. **[VERIFIED]** in the code, consequence **[UNVERIFIED]** |
| **Dragging a file from Explorer onto the charm** | The code is there and the drop path works from the file picker; the Explorer drag itself could not be tested automatically. **[UNVERIFIED]** |

---

## Not in this version, on purpose

- **Sound.** Removed rather than left as a switch that does nothing. Coming in v1.1.
- **Creator Studio** — the full editor macOS has. The **Create** tab is this version's
  answer, and it works. Studio is v1.1.
- **Photo import with subject cut-out.** You can import a photo; Hangly will not cut the
  subject out of it for you. v1.1.
- **Weather charms and seasonal charms.** These exist on macOS and are **not coming to
  Windows**. Removed from the roadmap permanently.

---

## Something not on this list?

[Open an issue](https://github.com/SharanCreatedThis/Hangly-Windows/issues/new/choose) —
[FRIEND-TESTING.md](FRIEND-TESTING.md) says what makes a report easy to act on.
