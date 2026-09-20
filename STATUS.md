# Status

As of the first run on real Windows hardware. Build health and feature completeness are
reported separately here, because they are separate questions and conflating them is how
a project convinces itself it is nearly finished.

- **Build:** green. All three CI jobs pass on `windows-latest`.
- **Runs:** yes — Windows 11 ARM64 at 200%. The rope hangs on the desktop, is transparent,
  and can be thrown.
- **Features:** roughly 40% ported. All eighty-one charms are in and drawing.

---

## 1. Build health

| Job | Result |
|---|---|
| Solver and models (tests) | ✅ 92 / 92 passing on Windows |
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

Nothing is failing. The three risks this section used to list have all been settled on a
real machine — Windows 11 ARM64 at 200% scaling:

- **Transparency.** Was wrong, and could not be fixed where it was written. A WinUI 3
  window owns an opaque redirection surface created with its HWND, and no XAML property
  replaces it, so the overlay drew a correct rope inside a white rectangle. The window
  layer is now a plain Win32 layered window; see PORTING.md §3.
- **The P/Invoke surface.** Exercised. Two real defects came out of it — the wrong export
  name for `Shell_NotifyIconW`, and a `NOTIFYICONDATA` declared short enough that the
  shell refused it silently — and both are fixed.
- **SkiaSharp's native binary on ARM64.** Loads, and rasterises the charm.

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
- The **overlay**, confirmed by watching it rather than by inferring it: a transparent,
  click-through, always-on-top window; the charm grabbed, dragged, thrown and left to
  settle; the tray icon, its menu, and the menu's keyboard navigation.
- The **charm catalogue — all eighty-one**, generated from the Swift by
  `tools/generate-catalogue.py` rather than transcribed. Every one of them opens,
  rasterises and measures: `Hangly.exe --check-artwork` reports *81 measured, 0 missing,
  0 unmeasurable*, on both architectures.
- The **artwork splitter**, which is what makes a charm more than a picture: it finds the
  beads in the artwork's own silhouette and measures where the knot sits, so a rope at
  rest is laid out as the designer drew it. Charms are drawn cropped to their measured
  body, and their beads ride the cord as the solver's own particles.
- **One, two or three charms** chosen from the tray, grouped by their packs.

## 3. What does not work

- **The window layer is new, and has one machine's worth of evidence behind it.** Launch,
  transparency, click-through, dragging and the tray menu have all been watched working on
  Windows 11 ARM64 at 200%. None of it has been seen at 100%, on x64, on a second display,
  or across a DPI change — and the overlay recomputes its scale only when it repositions.
- **Nothing is packaged or signed yet.** Velopack produces an installer that installs and
  runs — measured, not assumed — but no release has been cut, no feed is hosted, and
  SignPath has not been applied to. `Docs/DISTRIBUTION.md` has the decisions and the
  numbers.
- **A presented frame costs a read-back**, and that is now the honest cost rather than a
  suspected one. `UpdateLayeredWindow` wants the pixels in system memory, so each drawn
  frame is two 1.27 MB copies out of the render target. It allocates nothing: profiled
  over a thirty-second drag, allocation fell from 154 MB/s to 0.7 MB/s and gen-2
  collections from 1,040 to 7. What remains is memory bandwidth, and it is only spent
  while the rope is awake.
- **No settings UI at all.** Everything is changed through the tray menu or by editing
  `%LOCALAPPDATA%\Hangly\settings.json` by hand.

## 4. What remains to be ported

Ordered by what unblocks the most.

| Subsystem | Swift lines | Notes |
|---|---:|---|
| **Customize window** | ~4,500 | The whole settings UI. The largest single piece, and the one with the most room to be a Windows app rather than a translated Mac one. |
| **Charm Library** | ~1,800 | Browser, search, categories, favourites. |
| **Charm Studio** | ~2,400 | Editor, pipeline, undo stack. **Deferred: explicitly out of scope for v1.** |
| **Custom charm import** | ~1,200 | Image processor, store, dialogs. |
| **Weather** | ~700 | Open-Meteo client, moods, effects. |
| **Seasons** | ~600 | Seasonal packs and the coordinator. |
| **Sound** | ~500 | Synthesiser and per-material charm sounds. |
| **Menu bar artwork** | ~400 | The animated tray icon. Currently a static icon. |
| **Welcome, updates, analytics** | ~900 | |

## 5. Completion

**Roughly 40%** by weighted line count of the macOS source.

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
| Models | ~60% |
| Views | ~8% |

## 6. Distribution

Decided and documented in `Docs/DISTRIBUTION.md`; measured on win-arm64 at 0.9.0.

| | |
|---|---|
| Velopack packages self-contained win-arm64 WinUI | ✅ installs, runs, renders |
| Installer download | 120.8 MB (from a 400.1 MB payload) |
| Delta to the next version | 0.2 MB |
| Settings survive installing over an existing copy | ✅ once moved out of the install directory |
| Signing | not started — SignPath needs a released artifact first |
| Trimming | not started |
| x64 package | never built |

## 7. Verified, and not

Build health and feature completeness are separate questions, and so are "it compiles"
and "somebody watched it work". This is the second list.

### Watched working

| | |
|---|---|
| Windows 11 ARM64 at 200% | ✅ the configuration everything below was seen on |
| Windows 11 ARM64 at 250% | ✅ incidentally, during a display-scaling incident |
| **x64, under ARM64 emulation** | ✅ PE machine AMD64, launches, renders, 81 charms measured |
| Transparency, click-through, drag, throw, settle | ✅ |
| Tray icon, menu, keyboard navigation | ✅ |
| All 81 charms load, rasterise and measure | ✅ |
| Charms drawn cropped to their measured body, beads on the cord | ✅ one, two and three at a time |
| Velopack install, run, and settings surviving an install-over | ✅ |

### Not verified, and not claimed

Deferred to a manual pass before a release, because automating display changes inside the
guest cost an incident once already and is not worth a second:

| | |
|---|---|
| 100% and 150% scaling | ❌ never seen |
| **Native x64 hardware** | ❌ emulation exercises the binary, not the silicon |
| Two monitors | ❌ the host has one display; not testable here |
| Monitor hot-unplug | ❌ the fallback exists in `DisplayObserver.DisplayAt` and is untested end to end |
| Wake from sleep | ❌ never tried |
| **Windows 10 1809**, the floor the manifest declares | ❌ never tried. Either test it or raise the floor; claiming it is the one option that is not available |
| A DPI change *while running* | ❌ and expected to be wrong — nothing handles `WM_DPICHANGED`, so `scale` is stale until something repositions the window |

## 8. Next milestone

The Customize window. The catalogue is what unblocked it: there are eighty-one charms to
offer now, and a tray submenu is not where somebody chooses one.

**Charm Studio is deferred — explicitly out of scope for v1.**
