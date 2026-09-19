# Hangly — Architecture

This document explains how Hangly is put together, why each boundary sits where it
does, and where Phase 2 plugs in.

---

## 1. Principles

Four rules shape every decision below.

1. **Value types hold state; reference types hold behaviour.** Settings are plain
   `Codable` structs. Nothing observable, nothing AppKit, nothing that can be mutated
   from two places at once.
2. **One funnel per side effect.** Every settings write goes through one property
   setter. Every window mutation goes through one controller. When there is exactly
   one path, it can be reasoned about and tested.
3. **No singletons.** Services are constructed once in a composition root and injected
   downward. This is what makes the store testable against a throwaway
   `UserDefaults` suite and the login-item logic testable without touching the real
   system daemon.
4. **AppKit is an implementation detail of the window layer.** It does not leak into
   models, view models or the SwiftUI tree.

---

## 2. Layer map

```
                          ┌──────────────────────────────┐
                          │            App               │
                          │  HanglyApp   (SwiftUI scenes)│
                          │  AppDelegate (lifecycle)     │
                          │  AppEnvironment (DI root)    │
                          └──────────────┬───────────────┘
                                         │ constructs, injects
            ┌────────────────────────────┼────────────────────────────┐
            ▼                            ▼                            ▼
   ┌─────────────────┐          ┌─────────────────┐         ┌──────────────────┐
   │    Services     │          │   View models   │         │     Physics      │
   │ SettingsStore   │◀────────▶│ MenuBarVM       │◀───────▶│ RopeSimulation   │
   │ OverlayWindowC. │  read /  │ SettingsVM      │  steps  │ RopeConfiguration│
   │ ScreenObserver  │  write   │ OverlayVM       │         │ SimulationClock  │
   │ CharmManager    │          │ CharmLibraryVM  │         │                  │
   │ CustomCharmStore│          │                 │         │                  │
   │ CharmImageProc. │          │                 │         │                  │
   │ CharmImportCoord│          │                 │         │                  │
   │ CharmLibrary    │          │                 │         │                  │
   │ AudioService    │          │ CharmStudioVM   │         │                  │
   │ Accessibility   │          │                 │         │                  │
   │ CharmStudioPipe.│          │                 │         │                  │
   │ CharmStudioWin. │          │                 │         │                  │
   │ LaunchAtLogin   │          └────────┬────────┘         └──────────────────┘
   │ SettingsPresent.│                   │ drives
   └────────┬────────┘                   ▼
            │                   ┌─────────────────┐
            │ owns              │      Views      │
            ▼                   │ MenuBarView     │
   ┌─────────────────┐          │ SettingsView    │
   │  OverlayPanel   │◀─ hosts ─│ OverlayRootView │
   │   (NSPanel)     │          └─────────────────┘
   └─────────────────┘
            │
            ▼
   ┌──────────────────────────────────────────────────────┐
   │ Models — OverlaySettings, AppSettings, CharmStack,    │
   │          RopeStyle, WeatherMood, ScreenPlacement      │
   │          (pure values, no imports beyond Foundation)  │
   └──────────────────────────────────────────────────────┘
```

Dependencies point inward. Models know nothing about anything else.

---

## 3. How a change flows

Dragging the opacity slider in Settings:

```
  Slider
    └─▶ SettingsViewModel.opacity (set)
          └─▶ SettingsStore.update { $0.overlay.opacity = … }
                └─▶ SettingsStore.settings (set)
                      ├─▶ persist to UserDefaults          (side effect, once)
                      └─▶ Observation registrar fires
                            ├─▶ SwiftUI re-renders Settings and the menu bar icon
                            └─▶ ObservationStream yields OverlaySettings
                                  └─▶ OverlayWindowController.apply(_:)
                                        └─▶ OverlayPanel reconfigured / repositioned
```

The important property: the view model never talks to the window controller, and the
window controller never talks to a view. They share one observable value and react
independently. Adding a third consumer later costs nothing.

---

## 4. MVVM in practice

| Layer | Holds | Examples |
|---|---|---|
| **Model** | Pure data and pure functions | `OverlaySettings`, `ScreenPlacement` |
| **View** | Layout and presentation only | `SettingsView`, `OverlayRootView`, `MenuBarView` |
| **View model** | Presentation state and commands | `SettingsViewModel`, `MenuBarViewModel` |
| **Service** | Side effects and system APIs | `SettingsStore`, `OverlayWindowController` |

**View models expose flat, typed properties, not the settings document.** The Settings
view binds to `viewModel.opacity`, never to `store.settings.overlay.opacity`. Reads
pass straight through to the store so Observation still tracks them; writes route
through `update`, which persists atomically. The view has no idea `AppSettings`
exists.

**Views own their view models** via `@State`, constructed from the injected
environment:

```swift
struct SettingsView: View {
    @State private var viewModel: SettingsViewModel

    init(environment: AppEnvironment) {
        _viewModel = State(initialValue: SettingsViewModel(environment: environment))
    }
}
```

`@State` gives the view model a lifetime tied to the view identity rather than to the
body evaluation, so SwiftUI re-evaluating a scene does not discard user state.

Two surfaces are owned differently, and for the same reason — they outlive any view:

- `OverlayRootView` takes a plain `let`, because the window controller (not SwiftUI)
  owns the panel and therefore the view model.
- `MenuBarView` and `MenuBarIcon` share one `MenuBarViewModel` owned by
  `AppEnvironment`. The status item exists for the whole life of the app, and sharing
  one view model is what guarantees the icon and the menu checkmark can never disagree.

**View models take explicit dependencies, never the container.** `SettingsViewModel`
asks for a `SettingsStore` and a `LaunchAtLoginManaging`, not an `AppEnvironment`, so
a test constructs it from two fakes and nothing else.

**Where are the view models on disk?** Beside their views (`Views/Settings/`,
`Views/Overlay/`, `MenuBar/`) rather than in a separate `ViewModels` tree. The brief
fixed the top-level folders, and feature-grouping reads better anyway: everything
belonging to the Settings window is in one place.

---

## 5. Composition root

`AppEnvironment` builds the entire object graph exactly once and owns every service
lifetime. It is constructed by `AppDelegate`, not by `HanglyApp`, for a specific
reason: the delegate is the only place with a guaranteed *launched* and *about to
terminate* callback. Services therefore start after AppKit is ready, so the overlay
panel is never created before the window server can place it.

```swift
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let environment = AppEnvironment()

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        environment.bootstrap()
    }

    func applicationWillTerminate(_ notification: Notification) {
        environment.shutdown()
    }
}
```

Every initialiser parameter has a production default, so tests substitute only what
they care about:

```swift
let environment = AppEnvironment(
    settingsStore: SettingsStore(defaults: throwawaySuite),
    launchAtLogin: FakeLoginItemManager()
)
```

---

## 6. Concurrency model

The project builds in **Swift 6 language mode with `SWIFT_STRICT_CONCURRENCY = complete`**.

Hangly is a UI app whose entire job is driving windows, so the model is deliberately
simple: **everything is `@MainActor`.** Services, view models and window classes are
all main-actor isolated. There is no background work in Phase 1, therefore no
actor-hopping, no `nonisolated` escape hatches and no `@unchecked Sendable`.

Where concurrency does appear it is structured:

- `OverlayWindowController.start()` spawns two `Task`s, one per input stream, and
  cancels both in `stop()`.
- Notification callbacks capture an `AsyncStream.Continuation` — which is `Sendable` —
  rather than `self`, so no non-`Sendable` state crosses a boundary.

When Phase 2 moves simulation off the main actor, the boundary is already named:
`PhysicsSimulating` is `@MainActor`, and an off-main solver should publish immutable
snapshots across the boundary rather than relaxing that isolation.

---

## 7. Bridging Observation to services

SwiftUI observes `@Observable` types automatically. Plain services get nothing. The
overlay window controller is not a view, but it must react to settings changes.

`ObservationStream` (in `Utilities/`) closes that gap by re-arming
`withObservationTracking` after every change and republishing the value:

```swift
let settingsStream = ObservationStream { settingsStore.settings.overlay }

for await overlay in settingsStream.values {
    apply(overlay)
}
```

Two details matter:

- `onChange` fires *before* the mutation is visible, so the re-arm hops to the next
  main-actor turn to read the settled value.
- The stream buffers the newest value only. Settings are state, not events; replaying
  a backlog of stale frames after a slow consumer would be wrong.

Only properties read inside the closure are tracked, so unrelated changes — for
example toggling launch-at-login — do not wake the window layer.

---

## 8. The overlay window

**Why AppKit.** SwiftUI's `Window` scene cannot express a borderless, non-activating,
click-through window pinned above every other application. `OverlayPanel` is an
`NSPanel`; SwiftUI renders its contents through `NSHostingView`. AppKit owns the
window, SwiftUI owns the pixels.

`NSPanel` rather than `NSWindow` specifically because only a panel supports
`.nonactivatingPanel`.

Each requirement maps to one property:

| Requirement | Implementation |
|---|---|
| Transparent | `isOpaque = false`, `backgroundColor = .clear`, `hasShadow = false` |
| Above all apps | `level = .statusBar` |
| Click-through | `ignoresMouseEvents = true` |
| Never steals focus | `.nonactivatingPanel`, `canBecomeKey = false`, `orderFrontRegardless()` |
| All Spaces / full screen | `.canJoinAllSpaces`, `.stationary`, `.fullScreenAuxiliary` |
| Pinned to a screen corner | `ScreenPlacement.frame(for:anchor:in:offset:edgeInset:)` |

**Window level.** `.statusBar` (25) sits above normal and floating windows of every
application, and above the menu bar, while staying below system menus and alerts —
which is where an overlay belongs. `.screenSaver` would cover system UI the user needs.

**Placement is pure.** `ScreenPlacement` takes `CGRect`s and returns a `CGRect`. It
imports only CoreGraphics, so anchoring rules are unit-tested exactly, with no display
attached. `NSScreen+Hangly` is the thin adapter that supplies the bounds.

The geometry works in AppKit's y-up global coordinate space, which means it handles a
secondary display positioned left of the primary — a negative global origin — without
a special case. There is a test for exactly that.

**Screen choice.** `NSScreen.screens.first` (the primary display), deliberately not
`NSScreen.main`. `main` follows keyboard focus, so the overlay would hop between
displays as the user switched apps. Per-display selection is Phase 2.

**Lifetime.** The panel is created lazily and torn down completely when the overlay is
disabled, so a hidden overlay costs nothing rather than lingering as an invisible
window.

---

## 9. Menu bar and no Dock icon

Two mechanisms, belt and braces:

- `LSUIElement` is `true` in `Info.plist`, which is what actually makes the app an
  accessory at launch.
- `NSApp.setActivationPolicy(.accessory)` runs in `applicationDidFinishLaunching`, so
  the behaviour holds even if the app is launched in a way that bypasses the plist.

`HanglyApp` declares one scene: `MenuBarExtra`. There is no `WindowGroup` and no
`Window`, which is why no window appears at launch.

`MenuBarExtra` uses `.menu` style, so SwiftUI renders the content as a real `NSMenu`.
That buys native appearance, keyboard navigation and VoiceOver support at no cost —
and is why the menu holds only commands, with the richer controls in Customize.

**Neither window is a scene.** The overlay is an `NSPanel` because a borderless,
non-activating, click-through window pinned above every other app cannot be
expressed as a `Window`. Customize is an `NSWindow` around an `NSHostingController`
for a different reason: a `Window` scene does not close. It hides, and keeps its
view tree, its hosting view and its backing store for the next time it is asked
for — which the RC1 audit measured as about 45 MB held behind a closed window in an
app that idles at 23.

`CustomizeWindowController` therefore drops its content view controller in
`windowWillClose`, exactly as `CharmStudioWindowController` already did, and asks
`ArtworkMemory` to release the bitmaps the window's pages rasterised. Opening
Customize again rebuilds all of it, which is what the first open did anyway.

A side effect worth having: the controller owns the page selection
(`CustomizeNavigation`), so the menu can point the window at a page before there is
a window, without the SwiftUI `OpenWindowAction` the old presenter had to borrow
from the one view guaranteed to exist.

---

## 10. Persistence

Settings are stored as a single JSON document in `UserDefaults` under a versioned key.

**One write path.** The `settings` setter is a manual computed property over
`@ObservationIgnored` storage, using the registrar hooks the `@Observable` macro
generates:

```swift
var settings: AppSettings {
    get {
        access(keyPath: \.settings)
        return storage
    }
    set {
        guard newValue != storage else { return }
        withMutation(keyPath: \.settings) { storage = newValue }
        persist(newValue)
    }
}
```

This is why there is no `save()` to forget: SwiftUI bindings, the menu bar toggle and
programmatic updates all persist through the same setter. The equality guard means a
no-op assignment writes nothing.

**Decoding is tolerant, by design.** A settings file outlives the build that wrote it.
Synthesised `Codable` throws on a missing key, which would mean a new field in a future
release discards every existing user preference. Instead both settings types implement
`init(from:)` with `decodeIfPresent` and per-field fallbacks, and clamp numeric values
into their supported ranges. Unknown keys from a newer build are ignored. A document
that is corrupt beyond that is logged and replaced with defaults rather than blocking
launch. `AppSettings.schemaVersion` is written on every save so a future migration can
be deliberate.

**Launch at login is reconciled, not trusted.** The user can remove the login item in
System Settings while Hangly is not running. `AppEnvironment.bootstrap()` reads
`SMAppService` and corrects the stored flag, so the Settings toggle always reflects
reality.

---

## 11. Testing strategy

The tests target the layers that carry real logic and no system dependencies:

- **`ScreenPlacementTests`** — anchoring, offsets, clamping, negative screen origins,
  oversized overlays. Exact assertions, because the geometry is pure.
- **`SettingsCodingTests`** — empty, partial, unknown-key and out-of-range documents;
  lossless round-trip.
- **`SettingsStoreTests`** — the real store against a throwaway `UserDefaults` suite:
  persistence, the redundant-write guard, reset, corrupt-document recovery.
- **`RopeSimulationTests`** — the solver, which is the most valuable of the four
  because "swings naturally" is otherwise only checkable by eye: segment count and
  mass distribution, the pinned anchor, stretch bounds under free swinging and under
  a hard flick, gravity, damping, refresh-rate equivalence, long-run stability,
  oversized-frame clamping, grab targeting, exact cursor tracking, momentum on
  release, and sleeping and waking.

- **`CharmImageProcessorTests`** — every link of the pipeline on synthetic images
  with known answers: PNG, JPEG and WebP loading, refusal of other formats,
  downsampling, flood fill, trusting existing alpha, fitting, empty-image rejection,
  the physics derivation, and two end-to-end runs, one of which exercises Vision.
- **`CustomCharmStoreTests`** and **`CharmImportTests`** — persistence, reload,
  removal, pruning of missing bitmaps, corrupt-manifest tolerance, forty imports,
  and import and delete through the manager including a clean failure.
- **`CharmStudioPipelineTests`**, **`UndoStackTests`** and **`CharmStudioViewModelTests`**
  — see §17.
- **`SoundSynthesizerTests`** and **`CharmSoundTests`** — every material is finite,
  bounded, non-silent, deterministic, decays, and ends near silence; materials are
  assigned as specified; a disabled or zero-volume service never starts the engine.
- **`CharmLibraryDocumentTests`** and **`CharmLibraryViewModelTests`** — the JSON
  document covers every kind and agrees with the code on names, every entry is
  complete with a real category and an existing preview asset, and the browser's
  search, category partition, selection, favourites persistence and handling of
  imports all behave.
- **`CharmTests`** and **`CharmSelectionTests`** — the registry covers every kind,
  metrics are sane and genuinely distinct, every charm's artwork is non-empty and
  stays inside its unit square, interpolation hits both ends and the middle, and the
  selection persists and degrades gracefully when a document names a charm this build
  does not know.

This is only possible because those layers hold no AppKit types and because the store
takes its defaults suite by injection. The solver in particular is pure arithmetic
over `CGPoint`, which is why claims like "never stretches unrealistically" are
numbers in a test rather than an opinion about a screenshot.

Three real bugs were found this way rather than by watching the overlay: dragging past
the rope's reach tore the links apart and snapped back on release; a single downstream
projection sweep oscillated instead of converging; and a `for ... where` clause written
as an early exit silently ran the full relaxation budget on every step, because `where`
filters iterations rather than ending the loop.

The window and render layers are intentionally not unit-tested: they are thin,
declarative mappings onto `NSPanel` properties and drawing commands, and verifying
them meaningfully needs a real window server.

---

## Deep dives

Three parts of the system are documented on their own, because each is large
enough to read in one sitting and is the thing a contributor is most likely to
have come for:

- **[The physics engine](Physics.md)** — the Verlet solver, its constraints, the
  fixed timestep, and why the rope cannot be stretched.
- **[The charm system](Charm-System.md)** — the charm abstraction, the shipped
  collection, the Library, and the Studio.
- **[SVG import](SVG-Import.md)** — the artwork pipeline, vector rendering, and
  the measurement that separates a charm from its beads.

## 12. Product quality

### Rendering without filters

The cord is four strokes — two offset low-alpha passes for a soft shadow, the cord,
and a thin highlight along the lit edge — and the ambient glow is a radial gradient.
None of it uses a `GraphicsContext` filter. A blur or shadow filter rasterises an
offscreen layer on every frame, and a first attempt with three of them measured at
over 20% of a core and a hundred megabytes of resident memory while swinging.
Strokes and gradients cost only their geometry. The one filter that remains is the
charm's own drop shadow, a single layer that was already in the budget.

The other per-frame cost removed was artwork construction: `CharmLayer` now carries a
charm's `CharmArtwork`, built once when the layer is created, instead of rebuilding
dozens of `CGPath`s 120 times a second.

The cord is thin, dark and twisted, and all of it is strokes: two offset low-alpha
passes for its shadow, the cord itself under a gradient, two dashed passes for the
twist, and a hairline highlight along its lit edge. Dashes are measured along the
path, so the twist bands follow every bend and stay evenly spaced as the rope swings.

A charm's drop shadow is the one place a filter survived, because a shadow has to
follow the artwork's alpha rather than a path. At three times the size it now hangs
at, resolving that offscreen pass every frame is the kind of cost this renderer
exists to avoid, so the shadow is made once instead: `RGBABitmap.shadow` blurs the
artwork's alpha on the CPU with three box passes, `VectorImage` caches the result
per size beside the artwork itself, and every frame afterwards draws an ordinary
image. Rasterisation sizes are rounded up to a fixed step for the same reason — a
charm growing through its settle-in would otherwise rasterise afresh on every frame
of it.

### Sound

`SoundSynthesizer` renders each `CharmSound` as a sum of exponentially decaying sine
partials, plus filtered noise for the percussive materials, with a short attack and a
release fade so nothing ends on a click. It is deterministic — the noise generator has
a fixed seed — so the output is asserted in tests. `AudioService` caches the buffers,
starts the engine on the first sound and stops it after a few seconds of quiet: a
running `AVAudioEngine` keeps a render thread awake, and that alone would break the
idle budget. If the engine refuses to start it marks itself unavailable rather than
retrying on every swing.

Sounds are triggered in two places only, both direct user actions: releasing a swing
above a speed threshold, and switching charms. Nothing plays spontaneously, which is
what makes defaulting the setting on reasonable.

### Motion and accessibility

`AccessibilityPreferences` mirrors Reduce Motion for the AppKit side and observes
changes; SwiftUI views read the environment. Under Reduce Motion the rope is built at
rest via `resetToHanging()`, charm changes complete in one frame, the overlay fades
are skipped, and card hover has no scale. Library cards are `Button`s, which is what
makes them focusable and activatable from the keyboard and gives VoiceOver one
control with a label, a value and a hint.

### Recovery

`CustomCharmStore.loadManifest` is three passes: decode, prune, recover. A manifest
that will not decode is renamed with a timestamp, never overwritten. Entries whose
bitmap is missing are dropped and their ids reported. Any PNG in the folder with no
entry — from either of the above, or a hand copy — is analysed for mass and colour
and re-registered, reusing the file's UUID so identity survives a rebuild. At launch,
`CharmManager.reconcile()` moves a selection or favourite that names a missing import
so the settings document stops pointing at a ghost. `SettingsStore` keeps an
unreadable document under a backup key before falling back.

### Measured

| State | CPU | Physical footprint |
|---|---|---|
| Swinging into place | ~14% of one core | 42 MB peak |
| Settled | 0.6% of one core | 23 MB |

The moving figure is dominated by redrawing a transparent window at the display's
rate rather than by what is drawn in it: an empty canvas over the same simulation
still costs about two thirds of it, and the larger charm with its beads measured
0.6 points above the same build with the previous charm size and no beads.

`footprint` rather than `rss`: resident set size counts pages of shared frameworks
and, before the filters were removed, the compositor's layer pool. Physical footprint
is the number Activity Monitor shows and the one that matters for a background app.

## 13. Extension points

The seams are already cut. Phase 2 should not require reshaping any existing type.

**A new charm.** Add a `CharmKind` case and a type conforming to `Charm`. Describe the
shapes and pick a mass and a size. No rendering, menu or physics code changes.

**A fourth charm on the rope.** `CharmStack.maximumCount`, plus a row in
`RopeConfiguration.Layout.attachments(forCharmCount:segmentCount:)` and
`charmScale(forCharmCount:)`. The solver, the renderer, the menu and Settings are all
driven from the stack's length. Whether the rope can carry a fourth without crowding
is a question for `CharmStackTests`, which will answer it exhaustively.

**A fourth time of day.** A `RopeTimeProfile` case, its three scales, and an hour
range in `forHour(_:)`. Everything else — the menu, persistence, the solver — is
driven from `allCases`. See [the physics notes](Physics.md#the-time-of-day).

**A sixth kind of weather.** A `WeatherCondition` case, its codes in
`fromWMOCode(_:)`, and a `WeatherMood` describing what it does to a colour. The
renderer is handed a mood and never learns what the weather is, which is what keeps
every effect expressible as an adjustment to the artwork rather than a layer over
it. See [the weather notes](Weather.md).

**A new rope style.** Add a `RopeStyle` case and its two rows — a `RopePhysicsProfile`
the solver reads and a `RopeAppearance` the renderer reads. The solver, the renderer
and the menu are all driven from `allCases`, so a sixth cord is a table entry. Only a
genuinely new *kind* of texture needs code: a `RopeTexture` case and a method on
`RopeStyleRenderer` to draw it. See [the physics notes](Physics.md#rope-styles).

**Another import source.** Anything that produces a file URL — the clipboard, a
share extension, a URL scheme — calls `CharmImportCoordinator.importImage(at:)` and
gets the same processing, storage and feedback as a drop.

**Renaming or reordering imports.** Both are edits to `CustomCharmEntry` in the
manifest; the menu follows the store's `entries` automatically.

**Multiple displays.** `ScreenPlacement` is display-agnostic and handles negative
origins. Add a display identifier to `OverlaySettings` and resolve it in
`NSScreen+Hangly`.

**Multiple overlays.** `OverlayWindowController` owns one panel. Making it own a
keyed collection is a contained change, because nothing outside it holds a panel
reference.

---

## 14. Decisions and trade-offs

| Decision | Alternative | Why |
|---|---|---|
| Verlet integration | Spring-mass with explicit forces | Momentum survives a drag release for free; positional constraints cannot go unstable the way stiff springs do |
| Fixed 240 Hz timestep | Step by the frame delta | Makes behaviour identical across refresh rates, and provable in a test |
| `Canvas` for the rope | Twenty-one shape views | Rebuilding and diffing twenty-one view identities every frame would dominate a 120 Hz budget |
| Sleep when settled | Simulate forever | A menu bar ornament cannot burn 14% of a core permanently |
| Debug mode via `UserDefaults` | A menu item or settings toggle | It is a developer switch, and Phase 2 was explicitly not to touch menus or settings |
| Charms described as geometry | Each charm drawing itself | One lighting model means the set looks coherent, and a new charm is shapes only |
| `CGPath` in the model layer | SwiftUI `Path` | Keeps models free of SwiftUI and lets charm bounds be unit-tested |
| Charm metrics on the simulation | Inside `RopeConfiguration` | `fitted(to:)` rebuilds the configuration on resize and would wipe them |
| Polling the selection per frame | Observing the manager | Puts the change on the same clock as the animation, removing an ordering question |
| `CharmID` stored as one string | Synthesised enum Codable | A built-in is written exactly as before Phase 4, so old settings documents still decode |
| Vision, then flood fill | Flood fill only | Subject lifting handles photographs; the fill is the dependable fallback for flat art |
| Trust existing alpha | Always remove background | A hand-cut sticker must not be cut again |
| Mass from coverage | A fixed mass for imports | A solid shape and a wispy one should not swing the same |
| Lazy bitmap loading | Load all at launch | Unlimited imports must not mean unbounded memory |
| Drop onto the charm | A drop zone window | The charm disc is already the overlay's only interactive region; nothing else changes |
| Collection artwork as vectors | Hand-drawn PNGs | Resolution independent, bounds-tested, lit by the shared model, and previews regenerate from source |
| Metadata in JSON, geometry in Swift | All in code or all in data | Words are edited and reviewed; shapes are typed and tested; a test keeps them in agreement |
| Generated preview assets | Committed art | Previews cannot drift from what hangs on the rope |
| Card click switches the rope | A separate Apply button | The overlay is the preview; one click was the requirement |
| SVGs in the asset catalog with vector data preserved | Loose files in the bundle | Vector scaling and dark-appearance variants come from the catalog for free |
| Rasterise per size and cache | Draw the vector every frame | One rasterisation per size keeps the 120 Hz path a bitmap draw |
| Keep the artwork's cord and beads | Crop to the charm body | The boundary is not tagged in the files, and cropping discards part of the design |
| Rotate vector artwork with the rope | Screen-fixed like geometry | Artwork that draws its own cord must hang in line with the physics cord |
| Strokes and gradients, no filters | Blur and shadow filters | Filters rasterise an offscreen layer per frame; measured at 20% CPU and 100 MB |
| Synthesized sound | Bundled recordings | First-party, tiny, deterministic and testable; one material per charm family |
| Engine stops when quiet | Engine always running | A render thread awake at idle would cost more than the whole rope |
| Sound on by default | Off by default | It plays only on the user's own action, never spontaneously |
| Move a corrupt manifest aside | Overwrite it | Nothing the user made is destroyed by a recovery |
| Every interactive import opens the Studio | Keep the silent one-shot import | Nothing reaches the library unseen; the unattended path remains for code |
| Studio hosted in an `NSWindow` | SwiftUI `Window` scene | Any service can open it with a file, and a closed window releases its view tree |
| Undo over a value type | `NSUndoManager` registrations | One struct copy per step, testable without a window, one step per slider drag |
| Generation-checked reprocessing | Serial queue | A superseded result is discarded rather than waited for |
| AppKit `NSPanel` for the overlay | SwiftUI `Window` scene | SwiftUI cannot express borderless + non-activating + click-through + always-on-top |
| `@Observable` | `ObservableObject` | Finer-grained tracking, no `@Published` boilerplate; `ObservationStream` covers the non-SwiftUI gap |
| Manual `access`/`withMutation` | `didSet` on a stored property | `@Observable` rewrites stored properties; a manual computed property is the supported hook for a side effect |
| Everything `@MainActor` | Actors per service | Phase 1 has no background work; blanket main-actor isolation removes an entire class of bug for zero cost |
| `.menu` MenuBarExtra style | `.window` style | Real `NSMenu`: native look, keyboard navigation and VoiceOver for free |
| Tolerant `Codable` | Synthesised `Codable` | A synthesised decoder throws on a missing key, discarding every preference when a field is added |
| View models beside views | Separate `ViewModels/` folder | The brief fixed the top-level folders; feature-grouping keeps a feature readable as a unit |
| Universal binary (`$(ARCHS_STANDARD)`) | Single-arch binary | Ships as a fat binary (`arm64` + `x86_64`) supporting Apple Silicon (M1–M4) and Intel natively |
| Ad-hoc code signing | Automatic with a team | A clean checkout builds and runs with no developer account |
