# Hangly for Windows

A port of [Hangly](https://github.com/sharancreatedthis/Hangly) — a charm that hangs
from a simulated rope on your desktop — to Windows 10 and 11, on x64 and ARM64.

C#, WinUI 3, .NET 9, Win2D.

---

## Status

[![Build](https://github.com/SharanCreatedThis/Hangly-Windows/actions/workflows/build.yml/badge.svg)](https://github.com/SharanCreatedThis/Hangly-Windows/actions/workflows/build.yml)

**This builds, and it has never been run.** Those are both true and neither implies the
other. [STATUS.md](STATUS.md) is the detailed report; [PORTING.md](PORTING.md) explains
what was rewritten rather than transcribed, and why.

| | |
|---|---|
| Solver, models, settings, placement | Ported — **67 / 67 tests passing on Windows CI** |
| Overlay window, tray, renderer, artwork | **Compiles and publishes** on x64 and ARM64; never launched |
| Charms on the rope | **One** — the plain bead, as an explicit bootstrap |
| Studio and photo import | **Not started** |

Roughly **25%** ported by weighted line count. The hardest quarter, and the one where
being approximately right is the same as being wrong, is the part that is done.

The next thing that matters is not more code — it is running the thing on real Windows
hardware and finding out whether the overlay is genuinely transparent. See
[STATUS.md §6](STATUS.md#6-next-milestone).

---

## Requirements

- Windows 10 version 1809 (build 17763) or later, or Windows 11
- x64 or ARM64
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 17.10+ with the **Windows App SDK C# templates** workload, or the
  `Microsoft.WindowsAppSDK` NuGet package restored on the command line

## Building

The solver and everything under `Hangly.Core` build and test on **any** platform —
macOS, Linux, Windows — because nothing in them touches Windows:

```
dotnet test tests/Hangly.Core.Tests/Hangly.Core.Tests.csproj
```

The app itself needs Windows:

```
dotnet build Hangly.sln -c Release -p:Platform=x64
dotnet publish src/Hangly.App/Hangly.App.csproj -c Release -r win-arm64 --self-contained
```

## How it is put together

```
Hangly.Core          no Windows types anywhere — runs and is tested on any machine
  Geometry/          Vec2, Size, Rect, and the one epsilon the solver compares against
  Physics/           the Verlet solver, the cord curve, the bead pass, the charm layout
  Models/            rope styles, time-of-day profiles, palettes, charm metrics
  Settings/          the settings document and its one write path

Hangly.App           the Windows head
  Overlay/           the transparent click-through window, the clock, the Win2D renderer
  Tray/              the notification-area icon, which is this port's menu bar
  Interop/           the Win32 surface the overlay needs, and nothing else
  Services/          displays, launch-at-login
  Assets/Charms/     82 SVGs, copied unchanged from the macOS build
```

Dependencies point inward. `Hangly.Core` knows nothing about WinUI, Win2D or Win32, which
is what lets "the rope never stretches beyond 1.02× its rest length" be a number in a
test rather than an opinion about a screenshot.

## The rope

Twenty segments, twenty-one nodes, solved with Verlet integration and position-based
constraints at a fixed 240 Hz regardless of what the display is doing. Nine cords, each a
different set of solver values rather than a different texture. One, two or three charms
on one cord. Beads that ride the cord as particles in their own right.

All of that is described in the original's
[physics documentation](https://github.com/sharancreatedthis/Hangly/blob/main/Docs/Physics.md),
which this port follows to the number — and the test suite here is the Swift suite's
assertions with the same tolerances, so any drift shows up as a failure rather than as a
rope that feels slightly wrong.

## Licence

MIT, as the original.
