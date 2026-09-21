# Changelog

The section for a version is what the release page says and what the app shows on its
About page when it finds that update. One text, three places: `tools/release-notes.ps1`
pulls the section out and the release workflow hands it to both.

Headings are `## <version> — <date>`. Nothing else is a version heading.

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
