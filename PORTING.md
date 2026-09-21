# Porting notes

What was carried across unchanged, what had to be rewritten because Windows is not
macOS, and what has not been done yet.

Read this before trusting anything in `Hangly.App`.

---

## 1. Where the port stands

### Done and verified

`Hangly.Core` compiles clean and **67 tests pass**. It contains:

- the Verlet solver, its constraint passes, the drag surface, the cord curve, the bead
  pass, the charm-stack layout and the mass distribution;
- the nine rope styles and the three time-of-day profiles, as tables;
- the settings document, its tolerant decoding and its one write path;
- the placement geometry.

The tests are the Swift suite's assertions at the same tolerances, including the two that
matter most: two 120 Hz frames match one 60 Hz frame to within 1e-9, and three charms
thrown in circles for 900 frames overlap by less than 1e-6 points.

### Compiled, and now run

Everything in `Hangly.App` was written on a Mac, where WinUI 3 cannot be built at all. It
has since been built and run on Windows 11 ARM64, and five separate faults came out of
doing so — not one of which a compiler or a green CI run could have caught. Four were
bugs and are recorded in the git log. The fifth was not a bug: it was the wrong window
layer, and it is the subject of §3.

### Not started

Roughly two thirds of the macOS app, by line count:

- **The charm catalogue.** The 82 SVGs are copied in, and `CharmArtworkCache` can
  rasterise them, but the catalogue itself — each charm's mass, radius ratio, knot inset,
  palette, bead description and sound — is a table of about 1,700 lines in
  `Models/Charms/` that has not been transcribed. Until it is, `RopeRenderer.Charms` is
  empty and the rope hangs with nothing on it.
- **The Customize window** — the whole settings UI, about 8,700 lines of SwiftUI across
  Customize, Library and Studio.
- **The Charm Studio**, its pipeline and its undo stack.
- **Custom charm import**, the image processor and the custom charm store.
- **Weather and seasons**, and the Open-Meteo client behind them.
- **Sound** — the synthesiser and the per-material charm sounds.
- **Analytics, updates, the welcome flow, AirDrop.**

None of it is blocked; all of it is work.

> **This list is where the port stood when it was written, and is kept for that.** Every
> item on it except the Studio and sound has since been built. The roadmap settled on
> 21 September 2026 is: **weather and seasons are removed permanently**, **sound is
> v1.1**, and **Creator Studio is v1.1** — the **Create** tab is v1.0's answer to making
> your own charm. `STATUS.md` and `Docs/RELEASE-READINESS.md` are the current picture.

---

## 2. What was carried across unchanged

The solver is a transcription, line for line, including its comments — because the
comments are where the reasoning lives and the reasoning is the valuable part. Every
tuning constant is the same number. Thread is still exactly the default configuration,
and there is a test that says so.

The charm artwork is the **same 82 SVG files**, copied rather than converted. Both builds
rasterise vectors at the exact size each frame needs rather than shipping baked bitmaps,
for the reason the macOS build measured: an asset catalog's bitmaps came to 56 MB against
18 MB of vectors, and none of them were ever drawn.

---

## 3. What had to be rewritten, and why

### The overlay window

macOS gives one object — `NSPanel` — that is transparent, above every app, click-through,
non-activating and present on all Spaces. Windows gives none of that, so the five
properties are assembled from extended window styles. The mapping is tabulated in
`Interop/NativeMethods.cs`.

Two consequences worth knowing:

- **Transparency is not something WinUI can give, and the fallback was taken.** Read this
  before putting the overlay back inside a `Window`. A WinUI 3 desktop window's HWND is
  created without `WS_EX_NOREDIRECTIONBITMAP`, and that style cannot be added afterwards:
  the opaque redirection surface is allocated at `CreateWindowEx` time. A null
  `Background`, a null `SystemBackdrop`, a Win2D control clearing to transparent and
  `DwmExtendFrameIntoClientArea` all paint *onto* that surface rather than replacing it,
  so every combination of them still composites as a white rectangle with a perfectly
  correct rope inside it. That was watched happening, not reasoned about. The Windows App
  SDK pinned here has no `TransparentBackdrop` to ask for instead — checked against the
  shipped metadata rather than assumed.

  So the overlay is a plain Win32 layered window that paints itself.
  `Overlay/LayeredOverlaySurface.cs` creates it, renders each frame into a Win2D
  `CanvasRenderTarget` in premultiplied BGRA, and hands the pixels to
  `UpdateLayeredWindow`. Nothing below it changed: `RopeRenderer` takes a
  `CanvasDrawingSession` and never knew where the session came from, and `Hangly.Core`
  never knew there was a window at all. That separation is the reason this was a contained
  rewrite of one file instead of a rebuild, and it is the reason to keep it.

  The alternative was a composition swapchain under DirectComposition, which keeps the
  pixels on the GPU and is the faster of the two. It was not taken: it costs several COM
  vtables that have to be declared in exactly the right order to work at all, weighed
  against one read-back of a window this size that only happens while the rope is awake.
  If a sustained drag ever shows up in a profile, that is the thing to write.
- **There is no "all Spaces".** Windows has no public per-window API to show a window on
  every virtual desktop. `IVirtualDesktopManager` can tell you which desktop a window is
  on and move it, but pinning is undocumented COM that changes between builds. The
  overlay currently lives on whichever desktop it was created on. This is a real feature
  gap against the macOS build, not an oversight.

### Coordinates

AppKit's global space is y-up with the origin at the bottom left; Win32's is y-down from
the top left. `ScreenPlacement` is therefore **restated** in the platform's convention
rather than transcribed with a flip at the boundary, because a placement bug hiding
inside a coordinate inversion is a bug nobody can see. It carries the original's full
test list, including the negative-origin cases.

The canvas the solver works in was already y-down in both builds, so no axis is flipped
inside the physics.

### The clock

`CADisplayLink` becomes `DwmFlush`, called once per turn of the overlay's own frame loop.
It was `CompositionTarget.Rendering`, which is the closer analogue and was the right
answer while the overlay was still a XAML window; that event fires only while there is a
XAML tree being composed, and there is no longer one. Both block until the desktop
compositor has finished a frame, so the pacing is unchanged and so is the reason for not
using a timer.

Windows has no equivalent of `preferredFrameRateRange`, so the idle rate is implemented
by delivering one tick in four rather than by asking the system for fewer frames. Skipped
intervals are **accumulated, not dropped**, so throttling changes how often the solver is
asked to advance and never how far it advances.

The loop runs on a thread of its own, which is also the thread that creates the window and
pumps its messages. That is not a performance choice: a window whose thread does not pump
is declared unresponsive and replaced by a ghost, so the window has to live wherever the
loop lives. Settings arriving from the tray are handed across as one volatile reference
and picked up at the top of a frame, which is the entire cross-thread surface.

### Input

A click-through window receives no mouse messages, so the cursor is polled with
`GetCursorPos` and `GetAsyncKeyState` on the same tick that steps the physics. This
matches the macOS build, which polls `NSEvent.mouseLocation` rather than installing an
event tap. `WS_EX_TRANSPARENT` is toggled as the cursor enters the charm's grab radius,
and the write is guarded on change for the reason the macOS build recorded: setting it
unconditionally at 120 Hz keeps a settled overlay measurably busy.

### The menu bar

`MenuBarExtra` becomes `Shell_NotifyIcon` plus `TrackPopupMenu` — about 300 lines where
the original had a scene. It buys back the same three things: native appearance, keyboard
navigation and screen-reader support, because it is the system's own menu.

### Persistence

`UserDefaults` becomes one JSON file under `%LOCALAPPDATA%`, written through a temporary
file and moved over the real one so a crash mid-write cannot leave a half-document. The
tolerant decoding is kept exactly: a missing key falls back per field, an unknown key
from a newer build is ignored, an out-of-range value is clamped, and a corrupt document
is replaced rather than blocking launch.

Enums are written **by name**. An ordinal would tie the file to the declaration order of
`RopeStyle`, and inserting a cord in the middle would silently re-point every existing
user's rope at a different one.

### One .NET trap worth naming

Swift's `Double.ulpOfOne` is machine epsilon, about 2.2e-16. .NET's `double.Epsilon` is
the smallest denormal, about 5e-324 — a different number by three hundred orders of
magnitude, and the solver compares against it on nearly every line. It is defined once in
`Geometry/Precision.cs` and `double.Epsilon` is used nowhere.

---

## 4. Running it, from a Mac

This port is developed on a Mac against a Windows 11 ARM64 guest in Parallels Desktop.
Everything below was learned by getting it wrong first, and none of it is discoverable
from the code.

**The one that costs an afternoon:** `prlctl exec` without `--current-user` runs as
**SYSTEM, in session 0**. Commands succeed, exit codes are real, logs get written — and a
GUI process started that way is launched into a desktop nobody can see. Hangly has no
taskbar button by design, so this is indistinguishable from the app failing to start.

```sh
prlctl list -a                                    # find the VM name
prlctl exec "Windows 11" --current-user <command> # session 1, the desktop you can see
```

Check it is doing what you think: `--current-user` reports `USERNAME=<you>` and
`SessionId=1`; without it, the machine account and session 0.

### The guest's layout

Nothing here is installed by an installer, so it can be rebuilt on a fresh VM in minutes.

| | |
|---|---|
| .NET SDK | `C:\dotnet` — from `https://dot.net/v1/dotnet-install.ps1`, `-Channel 9.0 -Architecture arm64`. No Visual Studio: WinUI's XAML compiler and the Windows App SDK build targets all arrive through NuGet. |
| Source | `C:\src\Hangly` — mirrored from the Mac with `robocopy /MIR`, excluding `.git`, `bin`, `obj` |
| Published app | `C:\hangly\app` |
| Startup log | `%LOCALAPPDATA%\Hangly\hangly.log` |
| Settings | `%LOCALAPPDATA%\Hangly\settings.json` |

**Build and run from `C:\`, never from the share.** The Parallels share mounts at
`C:\Mac\Home` (and `Z:`), and it exposes only Desktop, Documents and Downloads — a file
written anywhere else on the Mac is simply not there. More importantly it is a UNC path,
and WinUI's resource loading is unreliable from one. Mirror to `C:\src` and publish to
`C:\hangly\app`.

**Kill the app before publishing.** A running Hangly holds `Hangly.dll` open and the
publish fails ten retries later with MSB3027, which reads like a build error and is not.

The loop is about a minute:

```powershell
Stop-Process -Name Hangly -Force -ErrorAction SilentlyContinue
robocopy \\Mac\Home\Documents\Hangly-Windows C:\src\Hangly /MIR /XD .git bin obj
C:\dotnet\dotnet.exe publish C:\src\Hangly\src\Hangly.App\Hangly.App.csproj `
  -c Release -r win-arm64 -p:Platform=ARM64 -o C:\hangly\app
Start-Process C:\hangly\app\Hangly.exe
```

### Seeing it

`screencapture` on the Mac captures the Mac, and needs Screen Recording permission that a
terminal may not have. Capture **inside the guest** instead, with
`System.Drawing.Graphics.CopyFromScreen` over `SystemInformation.VirtualScreen`, and write
the PNG into `C:\Mac\Home\Documents\...` to read it from the Mac.

Call `SetProcessDPIAware()` first. PowerShell is not DPI-aware, so at 200% the virtual
screen comes back in logical pixels and the shot lands cropped to a quarter of the desk.

**Transparency and smoothness cannot be judged from a log.** Four fatal bugs and one wrong
window layer all passed a green build. Screenshot it.

### Driving it

The overlay polls the cursor rather than handling mouse messages — a click-through window
receives none — so nothing short of moving the real pointer exercises the path a user
takes. `SetCursorPos` plus `mouse_event` is the whole of it.

The tray menu is a `TrackPopupMenu` popup. It does **not** publish its items to UI
Automation the way a XAML menu does, so drive it with the keyboard: find the tray icon
through UI Automation (match the name exactly and only in the bottom strip of the screen,
or File Explorer's refresh button will match "Hangly" for a folder of that name), open the
overflow chevron only if the icon is not already showing, right-click, then arrow keys and
Enter.

### Measuring it

`dotnet-counters` needs `DOTNET_ROOT=C:\dotnet` and that directory on `PATH`, because it
is a framework-dependent tool and the runtime is not where it expects. Without them it
reports "You must install .NET to run this application" while `dotnet --info` works fine.

Use `--duration` rather than killing the collector: a collector killed mid-write leaves a
truncated CSV.

### Custom charm import

macOS imports **photographs** — PNG, JPEG, WebP, HEIC — and the hard part of
`CharmImageProcessor` is deciding which pixels are the subject, using Vision's subject
lifting with a flood fill behind it. Windows has no equivalent of that, and this build
imports **SVG** instead, where the question does not arise: a vector drawing already says
which pixels are ink.

So the two platforms accept different files. That is the deviation, and it is not a small
one — a macOS user imports a photo of their cat, and a Windows user cannot yet.

Everything after the input is reproduced rather than reinvented, because those parts are
arithmetic rather than platform:

| | |
|---|---|
| Mass | `2.0 + 3.2 × density`, clamped to 2.0–4.5, with the same framing correction so re-framing a drawing does not change what it weighs |
| Palette | derived from the average colour by the same four proportions |
| Naming | the file stem, underscores and hyphens to spaces, falling back to "Custom Charm" |
| Manifest | the same fields in the same order — id, name, createdAt, imageFileName, metrics, palette |
| Repairs at load | an entry whose drawing is gone is dropped; a drawing with no entry is re-registered |
| Deleting | the bead takes its place wherever it hung, and it leaves favourites |
| Analytics | `charm_imported` then `charm_saved`, in that order, for the same reasons |

Two things the Windows build does that macOS does not, and one it does not do:

- **Beads come from the splitter.** A shipped charm's artwork is a cord, some beads and a
  charm, and the catalogue says how many parts are beads. An import is a subject on its
  own, so it declares zero beads — which is what stops the splitter reading the top of
  somebody's drawing as a bead and hanging the rest underneath.
- **The file is sanitised.** An SVG is a document, not a picture: it can carry script,
  fetch remote resources and declare entities that expand until the machine gives up.
  `SvgSanitizer` rewrites it into the subset that draws. macOS has no equivalent because a
  PNG cannot ask for anything.
- **There is no Studio.** macOS routes every interactive import through it so nothing
  reaches the library unseen. Here an import goes straight in, and the Library is where
  you look at it.

### Two Windows things that had to be worked out rather than ported

**`FileOpenPicker` does not work in this app.** WinUI's picker was tried first, with
`InitializeWithWindow` as the documentation requires for an app with no package identity.
It logged that it was opening, showed no window, and never returned — no dialog, no
exception. `GetOpenFileNameW` has no such opinion and is what the app uses.

**An id had to become a shape.** Until imports, every charm id was a catalogue id, and the
settings document leaned on that: an id it did not recognise was replaced with the bead,
and an unrecognised favourite was dropped. An imported charm is not in the catalogue and
never will be. macOS does not have this problem because its identifier is an enum of two
cases; `CharmId` is the same idea in the id itself — `custom:` and a UUID — which the
settings layer can recognise without knowing which imports exist.

### Two rendering defects that were shipped, and what they were

Both were reported as "Windows looks worse than macOS" and both turned out to be
correctness bugs rather than taste. They are recorded here because both were invisible
to the test suite and to CI, and one of them had been in the port since the first commit.

**Charm artwork was rasterised in points, not pixels.** `CharmArtworkCache` sized its
raster from the charm's radius, which the solver states in points, and handed the result
to a drawing session measured in DIPs over a surface at the display's DPI. On a 100%
display those are the same number and the artwork was correct. On anything above it the
bitmap was stretched on the way to the screen — at 200%, one source pixel per four device
pixels, which is what made the metal on Captain America's shield read as mush. The cord
and the beads were never affected because they are strokes, resolved by Direct2D at the
target's own resolution; only the artwork went through a fixed-size raster, which is why
the defect looked like an artwork problem rather than a scaling one.

The fix is one line — the raster is sized in device pixels, `radius × 2 × Dpi / 96` — and
the destination rectangle stays in points, so Direct2D composes the bitmap's scale with
the target's DPI transform and samples one source pixel per device pixel. The cache key
is the pixel size, so it already tells one display's rasters from another's.

Measured on the same charm at the same on-screen size (110px across the red ring, 192 dpi):
mean absolute gradient across the shield rose from 24.3 to 36.6 per pixel, and the peak
from 121 to 397. The artwork itself was never the limit — the SVGs carry raster payloads
around 492×556, far more than the ~200px the charm is drawn at.

**The rope swung out of its own window.** Two separate causes, which is why it looked
intermittent:

1. *The canvas was too narrow for the swing.* `Layout.canvasScale` grows the canvas width
   with the charm size alone — the room a *hanging* charm needs. A swinging one sweeps
   `totalLength × sin(initialAngle)` either side of the anchor, which at the shipped
   values is 92 points against 110 points of half-canvas before the charm's own radius is
   counted at all. `OverlayMetrics.CanvasSize` now takes the wider of the two. Every term
   is derived from the solver's constants, so there is nothing to keep in step by hand.

   *Sizing for the release angle was not enough, and that was a second mistake.* The
   first fix used `totalLength × sin(initialAngle)` — the arc a *released* rope swings
   through. But the charm is draggable, and `ReachableTarget` clamps the held node to
   `MaximumReachRatio` (0.98) of the cord above it, which is very nearly a full circle
   around the anchor. A charm cut in half at the end of a drag is exactly as wrong as one
   cut in half mid-swing, so the half-width is the drag reach, not the swing reach:
   307 points at the shipped settings against the 156 the release angle asks for.
   `EnvelopeTests` now walks the whole drag circle in two-degree steps as well as running
   the release and settle.

   A window this wide does not need capping to the display. `ScreenPlacement.ClampAnchorPoint`
   already keeps the *anchor* on screen rather than the whole window — it says so, and for
   this reason — and the charm follows the cursor, which cannot leave the display. A canvas
   wider than the screen therefore always covers wherever the charm can be taken.

2. *The rope was flung every time it was re-fitted.* `Resize` moved the anchor and left
   the rope where it was, so the next step pinned node zero to the new place and the
   constraint solver whipped that displacement down the chain. At launch the anchor moves
   from `(0, 0)` to the middle of the canvas, and the charm was thrown 236 points sideways
   on a canvas 220 wide — it left the window before it ever settled. The same thing
   happened, smaller, on every turn of the size slider. `Resize` now translates the rope
   and its Verlet history with the anchor, which preserves velocity exactly, and rebuilds
   outright when the rope has not started, because then there is no motion to preserve.

   A third contributor sat behind the same call: the overlay resized first and set the two
   sliders afterwards, so a rope at length 1.5 was briefly fitted at length 1. `Fit` does
   all three together and the overlay uses it.

`EnvelopeTests` is the guard: every rope style, one to three charms, and both sliders at
0.5, 1.0 and 2.0 — 270 combinations, each stepped through twelve seconds of release and
settle, asserting no charm is ever drawn outside the canvas. Before the fix the worst case
needed 1.70× the half-width it had; after, the worst needs 0.86× of it.

**The cost, stated plainly.** The overlay is much wider, because it now holds the drag
envelope rather than the swing envelope. Measured on the VM at 200%, Leather, charm size
1.4, rope length 1 — the same settings on either side of the change:

| | window | per present | idle | dragging |
|---|---|---|---|---|
| Swing-sized | 724 × 806 | 2.23 MB | 0.5% of one core | 9.4% of one core |
| Drag-sized | 1329 × 806 | 4.09 MB | 0.3% of one core | 19.8% of one core |

At both sliders maxed the surface is 2456 × 1433 (13.4 MB) and a sustained drag costs
27.9% of one core. Startup is unchanged at 271 ms median.

The shape of that is the important part: **idle is flat**. A settled rope presents nothing
at all, so the larger surface costs nothing until someone actually grabs the charm, and the
cost while they are holding it scales with the surface as you would expect. The read-back
per drawn frame is the term that grows, which is the known price of the layered-window
approach over a composition swapchain — see the note on `LayeredOverlaySurface`. If the
drag cost ever becomes the thing that matters, that is the trade to revisit, not the
canvas size.

**One thing that is still an assumption.** `OverlayMetrics.BaseWidth` and `BaseHeight`
(220 × 360 points) entered the port in its first commit with no recorded source, and
`reference/swift/` carries the physics and the models but not the view layer, so there is
nothing in the repository to check them against. The height is demonstrably right — the
layout fractions divide 360 exactly, and `TailFraction` is precisely the room the lowest
charm and its halo need. The width is now derived rather than trusted, so it no longer
matters what the original number was; but if the macOS overlay turns out to be a different
width, that is worth reconciling, and the deviation is deliberate and documented here.

## 5. Suggested order of work

1. ~~**Get it to compile**, on Windows.~~ Done.
2. ~~**Get one charm on screen**, and confirm the window is genuinely transparent and
   genuinely click-through before anything else.~~ Done, and it cost the window layer.
   This was the right thing to do second: every line of settings UI written before it
   would have been written on top of a window nobody could see through.
3. **Transcribe the catalogue.** It is a table; it is mechanical; it unblocks everything
   visual. Consider generating it from the Swift source rather than typing it.
4. **The Customize window.** The largest remaining piece, and the one with the most room
   to be a Windows app rather than a translated Mac one.
5. ~~Weather, seasons, sound, Studio — in whatever order matters to you.~~ Superseded.
   Weather and seasons were removed from the roadmap permanently; sound and Creator
   Studio are v1.1. What follows step 4 is release hardening, not more features.
