# Hangly beta — how to install it

Thank you for trying this. It takes about two minutes.

## 1. Pick the right file

Go to the [latest release](https://github.com/SharanCreatedThis/Hangly-Windows/releases/latest)
and download **one** file:

| Your PC | Download |
|---|---|
| Most PCs — Intel or AMD | `Hangly-win-x64-Setup.exe` |
| Snapdragon, Surface Pro X, other ARM PCs | `Hangly-win-arm64-Setup.exe` |

**Not sure which?** Press `Windows` + `Pause`, or go to **Settings → System → About**, and
look at **System type**. If it mentions "ARM-based processor", take the ARM64 file.
Otherwise take x64.

Taking the wrong one is not dangerous. An ARM PC will run the x64 build slowly under
emulation; an Intel PC will refuse the ARM64 build outright.

## 2. Windows will warn you. This is expected.

You will see a blue box: **"Windows protected your PC"**.

1. Click **More info**.
2. Click **Run anyway**.

**This is not a virus warning.** It is what Windows shows for any program that has not
been code-signed, and Hangly is not signed yet — signing is being arranged through
[SignPath Foundation](https://signpath.org/), who do it free for open-source projects. Until
that is in place, every download will show this.

If you would rather check before running it, the file came from this repository's releases
page and the source that built it is public.

## 3. That is it

No administrator password, no options to choose. It installs just for you, puts a shortcut
on your Desktop and in the Start menu, and a charm appears hanging from the top of your
screen.

## Using it

- **Drag the charm.** Grab it and throw it. Everywhere except the charm itself, clicks go
  through to whatever is behind.
- **Right-click the tray icon** — the little Hangly icon near the clock, possibly behind
  the `^` arrow — for the menu: Customize, the rope style, where it hangs, and Quit.
- **Customize → Library** to choose charms. Up to three at once, each at its own size.
- **Customize → Create** to make one from a picture of your own.
- **Customize → About** for statistics, and the switch that turns analytics off.

## Updating

Hangly checks for updates about twenty seconds after it starts, quietly. If there is one,
a line appears at the top of the tray menu saying so. Nothing downloads until you click
it.

## Removing it

**Settings → Apps → Installed apps → Hangly → Uninstall.**

Your settings and any charms you made stay behind in `%APPDATA%\Hangly` in case you
reinstall. Delete that folder if you want them gone too.

## Something went wrong?

See [FRIEND-TESTING.md](FRIEND-TESTING.md) for what is most useful to report and how.
[KNOWN-ISSUES.md](KNOWN-ISSUES.md) lists what is already known, so you can check before
spending time writing it up.
