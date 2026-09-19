# Status

As of the first green CI run. Build health and feature completeness are reported
separately here, because they are separate questions and conflating them is how a project
convinces itself it is nearly finished.

- **Build:** green. All three CI jobs pass on `windows-latest`.
- **Features:** roughly a quarter ported.

---

## 1. Build health

| Job | Result |
|---|---|
| Solver and models (tests) | ✅ 67 / 67 passing on Windows |
| App — `win-x64` Release | ✅ builds and publishes |
| App — `win-arm64` Release | ✅ builds and publishes |

### Build problems that were found and fixed

Every one of these came from code written on a Mac, where none of it could be compiled.

| Problem | Cause | Fix |
|---|---|---|
| Wrong package versions across the board | Guessed from memory. Windows App SDK is at **2.5.1**, not 1.6.x; Win2D 1.4.0; Svg.Skia 5.2.3; SkiaSharp 4.152.1 | Verified each against the NuGet API and pinned |
| `CanvasDashStyle.Custom` does not exist | Assumed an enum member by analogy | Setting `CustomDashStyle` is itself the switch; the `DashStyle` assignment was removed |
| `Microsoft.Graphics.Canvas.DirectX` not found | `DirectXPixelFormat` is a WinRT type | Corrected to `Windows.Graphics.DirectX.DirectXPixelFormat` |
| `WMC9999: Object reference not set` | **Not a XAML bug.** `WMC1509` showed MarkupCompilePass2 had no local assembly, which is what it reports when the C# compile it depends on already failed | Disappeared once the C# errors were fixed |
| `PRI175` / `PRI277` duplicate resource | `hangly.ico` was both `ApplicationIcon` (embedded) and `Content` (a file) | Ships once, embedded; the tray reads it back out of the executable |
| Warnings-as-errors broke the XAML compiler's own output | Generated code cannot satisfy the analyzers this repo turns on, and cannot be edited | Rule switched off for `Hangly.App` only; still enforced in `Hangly.Core` |
| Missing `using`, `using var` on a non-disposable | Ordinary mistakes no compiler had yet seen | Fixed |

### Build problems still possible

Nothing is failing. These are risks that only a real Windows machine can retire — CI
proves the app *builds*, never that it *runs*:

- **Transparency.** The overlay depends on a transparent XAML root, a null `SystemBackdrop`
  and a Win2D control clearing to transparent. If any of the three misbehaves, the window
  composites as a grey rectangle. This is the single most likely thing to be wrong.
- **The P/Invoke surface.** `Interop/NativeMethods.cs` and `Tray/TrayIcon.cs` compile,
  which says nothing about whether the signatures marshal correctly at runtime.
- **SkiaSharp's native binary on ARM64.** Restores and publishes; has never been loaded.

---

## 2. What works

- The **rope solver**, complete and verified: Verlet integration, relaxation to
  convergence, the one-sided stretch ceiling, charm separation, the bead pass, the cord
  curve, sleeping and waking, the fixed 240 Hz timestep.
- **Nine rope styles** and **three time-of-day profiles**, as data tables.
- **One, two or three charms** on one cord, with the layout guarantees intact.
- The **settings document**: tolerant decoding, clamping, atomic writes, one write path.
- **Placement geometry**, restated in Win32's coordinate convention and tested in it.
- Both architectures **publish as self-contained applications**.

## 3. What does not work

- **Nothing has ever been run.** Launch, transparency, click-through, dragging the charm,
  the tray menu — all unverified. CI cannot see a screen.
- **One charm exists**, the plain bead, wired by `BuiltInCharms` as an explicit bootstrap.
- **No beads are drawn on any charm**, because every charm in the real catalogue derives
  its beads by splitting its own artwork into regions, and that splitter is not ported.
- **No settings UI at all.** Everything is changed through the tray menu or by editing
  `%LOCALAPPDATA%\Hangly\settings.json` by hand.

## 4. What remains to be ported

Ordered by what unblocks the most.

| Subsystem | Swift lines | Notes |
|---|---:|---|
| **Charm catalogue** | ~1,900 | 82 charms: mass, radius, knot inset, palette, beads, sound. Mechanical. Unblocks everything visual. |
| **Artwork splitter** | ~400 | Derives each charm's beads from its SVG regions. Required before any charm but the bead has its beads. |
| **Customize window** | ~4,500 | The whole settings UI. The largest single piece, and the one with the most room to be a Windows app rather than a translated Mac one. |
| **Charm Library** | ~1,800 | Browser, search, categories, favourites. |
| **Charm Studio** | ~2,400 | Editor, pipeline, undo stack. |
| **Custom charm import** | ~1,200 | Image processor, store, dialogs. |
| **Weather** | ~700 | Open-Meteo client, moods, effects. |
| **Seasons** | ~600 | Seasonal packs and the coordinator. |
| **Sound** | ~500 | Synthesiser and per-material charm sounds. |
| **Menu bar artwork** | ~400 | The animated tray icon. Currently a static icon. |
| **Welcome, updates, analytics** | ~900 | |

## 5. Completion

**Roughly 25%** by weighted line count of the macOS source.

That number understates progress in one way and overstates it in another, and both are
worth saying:

- It **understates** it because the hardest and least forgiving part is finished. The
  solver is the piece where being approximately right is the same as being wrong, and it
  is done and asserted to 1e-9 against the original.
- It **overstates** it because the remaining 75% is mostly UI, and UI is where a port
  stops being a transcription and starts being design work. Those lines will not come as
  fast as the solver's did.

| | Ported |
|---|---|
| Physics | ~100% |
| App shell and services | ~40% |
| Models | ~25% |
| Views | ~8% |

## 6. Next milestone

**Run it on real Windows hardware and confirm the overlay is genuinely transparent and
genuinely click-through.**

Everything else is queued behind that one observation. If transparency works, the
remaining work is a long, well-understood transcription. If it does not, the window layer
needs rethinking — and that is a decision worth making before another line of UI is
written on top of it.

Concretely:

1. Download the `hangly-win-x64` artifact from the green CI run, or clone and
   `dotnet run --project src/Hangly.App`.
2. Confirm: the window appears; the desktop is visible through it; a blue bead hangs from
   a cord and swings; clicks pass through everywhere except the bead; the bead can be
   grabbed, thrown, and carries its momentum; the tray icon opens a working menu.
3. Report what actually happened — including which of those failed.
