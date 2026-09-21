# Notice — artwork and branding

The [MIT licence](LICENSE) covers **the source code**. This file covers everything else,
and nothing here takes anything away from that licence: the code is MIT, full stop.

These terms used to sit at the bottom of `LICENSE` itself. They were moved here for a
mechanical reason worth stating plainly: GitHub reads `LICENSE` with a parser that matches
it against the standard licence texts, and any addition to the file makes it report the
project's licence as **"Other"** rather than **"MIT"**. Nothing about the terms changed —
only which file they live in.

## What the MIT licence covers

All source code in this repository: `src/`, `tests/`, `tools/`, the build files and the
documentation.

## What it does not cover

- The charm artwork in `src/Hangly.App/Assets/Charms/`
- The branding sources in `src/Hangly.App/Assets/Branding/`
- The application icon
- The **Hangly** name and wordmark

These are **© 2026 sharancreatedthis, all rights reserved.**

## What you may do

Build it, fork it, modify it, and redistribute the software.

## What to please not do

- Redistribute the artwork on its own.
- Present the artwork as your own work.
- Ship a competing build carrying the Hangly name or icon.

If you want to do something this does not obviously cover, ask — the answer is usually
yes.

## Third-party code

Hangly depends on open-source packages, each under its own licence: Windows App SDK,
Win2D, SkiaSharp, Svg.Skia and Velopack. Their licences travel with them and are not
affected by anything here.

## `reference/swift/`

This directory holds a read-only copy of parts of the macOS Hangly source, kept so the
Windows port can be checked against the original. It is the same author's code under the
same ownership and is not maintained here.
