# Changelog

The section for a version is what the release page says and what the app shows on its
About page when it finds that update. One text, three places: `tools/release-notes.ps1`
pulls the section out and the release workflow hands it to both.

Headings are `## <version> — <date>`. Nothing else is a version heading.

## Unreleased

**Changed**

- Analytics now records who uses Hangly, not what they do with it. One message is sent the
  first time you start Hangly, again if you change your name, and again when a new major
  version arrives — your name, that this is Windows, the processor, and the versions of
  Windows and Hangly. Nothing about launches, charms, ropes or anything else you do in the
  app is sent any more. PostHog is also asked not to work out where you are.
- You can change your name in **Customize → Appearance**.
- Once a day that Hangly runs, it says it is still running and on which version — nothing
  else — so the number of people using it and the versions they are on can be counted.
- If you uninstall Hangly, it says so once, and removes itself from your Windows startup
  list. It used to leave that entry behind, pointing at a program that was gone.
- If you install Hangly with no internet connection, your profile is sent by itself when
  the connection comes back.

**Fixed**

- The About page no longer says analytics never sends your name. It did, and now it says so.
- The analytics details no longer draw your name on top of another row.

## 0.9.4 — 2026-09-22

**Changed**

- Hangly asks for your name before it starts, and now means it. The welcome card's
  Continue button was already disabled until you typed one, but closing the card with the
  X went around that and left Hangly running without a name. Closing it without answering
  now closes Hangly, and the card comes back next time you open it.

**Fixed**

- Nothing is sent to the usage-data project until you have given a name. The first launch
  used to report itself before the welcome card had appeared, so those reports carried no
  name at all. They wait for it now — the launch is still counted, just with you on it.
- Anonymous analytics remains entirely optional and can be switched off on the About page,
  exactly as before.

## 0.9.3 — 2026-09-22

**Fixed**

- Updating from 0.9.1 or earlier no longer throws the charm half off the side of the
  screen. A charm that hung in a corner before the position slider existed now lands just
  inside that edge instead of exactly on it. If you have already moved the slider
  yourself, nothing changes — your position is kept.

## 0.9.2 — 2026-09-22

Hangly now says who made it, quietly, in the places you would look.

**New**

- The creator's name and handle appear in the sidebar on every page — Library, Create,
  Appearance and About — and the handle opens Instagram.
- A creator card on the About page, with the website, Instagram, a charm suggestion and
  Buy Creator a Coffee in one place.
- The same credit on the welcome card and on the "Enjoying Hangly?" card, each with the
  website and coffee links beside it.
- The UPI address in the coffee sheet copies when you click it.

**Improved**

- Small charms are much sharper. The artwork is reduced in steps now rather than sampled
  down in one go, so a charm at 50% keeps its detail instead of breaking up into speckle.
- The About page fits without scrolling, and its two columns balance.
- The sidebar shows the charm larger, and nothing in it scrolls.
- Hangly wears its own icon in the title bar and the task switcher.
- New installs hang a Spider-Man on a spider thread, and Restore defaults puts the whole
  rope back rather than only the cord.

**Fixed**

- Library on the tray menu opens the Library, not whichever page you last read, and it
  works when the window is minimised.
- The rope stays pinned where it meets the top of the screen instead of sliding along the
  edge while the charm swings.
- The cord ends at the charm's clamp rather than running on through it.
- Collection previews are no longer cropped.
- Windows now reaches the analytics project at all. Every 0.9.1 build shipped without its
  key, so nothing was ever sent; names and platform now arrive with it.

## 0.9.1 — 2026-09-21

- Fixes the update check, which failed on every launch of 0.9.0. GitHub's "latest
  release" only counts full releases, and answers with an error when every release is a
  beta — so a beta could never see the beta that followed it. It asks a different way now.
- If you are on 0.9.0, please install this one over the top; 0.9.0 cannot fetch it itself.

## 0.9.0 — 2026-09-21

The first Hangly for Windows.

- A charm hangs from a rope on your desktop, swings when you push it, and settles the way
  a real one would.
- Seventy charms, in collections, with favourites and a record of what you hung recently.
- Up to three charms on one rope, each at its own size.
- Nine rope styles, and a charm you can drag, flick and drop wherever you like.
- Create your own charm from a picture or an SVG.
- Runs on ARM64 and x64, and updates itself.
