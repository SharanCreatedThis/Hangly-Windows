# Friend testing

What would actually help, and how to tell me about it.

## What I need most

Hangly has been built and tested on **one** machine: a Windows 11 ARM64 virtual machine,
one display, at 200% scaling. Everything outside that is untested — not "probably fine",
untested.

So the most useful things you can do are the ordinary ones, on your own machine:

| If you have | Please check |
|---|---|
| **An Intel or AMD PC** | That it installs and runs at all. The x64 build has been packaged but never installed by anybody |
| **Any scaling except 200%** | Right-click desktop → Display settings → Scale. Is the charm sharp? Does it get cut off at the edges when you throw it? |
| **Two or more monitors** | Does it appear on the right one? Customize → Appearance → Position. What happens if you unplug one? |
| **Two monitors at different scalings** | Drag the charm's display between them — does it resize correctly? |
| **Windows 10** | It claims to support 1809 and later. Nobody has ever run it on Windows 10 |
| **A laptop** | Close the lid, open it again. Does the charm come back? Does the battery drain oddly? |
| **A tidy taskbar** | Turn on auto-hide. Does the charm move to somewhere odd? |

## Things worth trying for five minutes

1. Hang three charms, give them different sizes, drag them into a different order.
2. Make a charm from a photo on your phone — drop it straight onto the hanging charm.
3. Pick a different rope style and drag the charm hard.
4. Quit from the tray menu, start it again. Is everything as you left it?
5. Leave it running for a few hours and see whether anything degrades.

## How to report something

[Open an issue](https://github.com/SharanCreatedThis/Hangly-Windows/issues/new/choose), or
just message me — whichever is easier. There is no wrong way.

**What makes a report easy to act on:**

- What you did and what happened.
- Your Windows version, your scaling, and how many monitors.
- **The log**: `%APPDATA%\Hangly\hangly.log`. Paste the path into Explorer's address bar.
  It covers the last run only and has no personal information in it —
  [PRIVACY.md](PRIVACY.md) lists exactly what it can contain.
- A screenshot, if it is something you can see.

A report with none of that is still worth sending. "The charm went weird when I plugged in
my monitor" is a perfectly good bug report.

## What I already know is unfinished

[KNOWN-ISSUES.md](KNOWN-ISSUES.md). Please skim it — it will save you writing up something
already on the list.

## What Hangly collects while you test

The same as always, and no more because you are testing: a small, listed set of events,
plus the display name you type. Not your files, not your file names, not your Windows
account. Switch it off in **Customize → About** and nothing is sent at all.
[PRIVACY.md](PRIVACY.md) is the full account.

## What happens to your feedback

It goes into the issue list, and the ones that block v1.0 get fixed before it. If you
report something and it goes quiet, chase me.
