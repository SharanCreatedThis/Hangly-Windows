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
5. Weather, seasons, sound, Studio — in whatever order matters to you.
