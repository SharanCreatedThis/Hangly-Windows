# The physics engine

How Hangly's rope is simulated: a Verlet solver with position-based constraints,
running at a fixed rate independent of the display.

Part of the [architecture documentation](Architecture.md).

---

## The rope

### Why Verlet

Verlet stores no velocity. A node's velocity is implied by the gap between where it
is and where it was. Three consequences shape the whole design:

- **Momentum after release is free.** Letting go of the charm simply stops writing
  its position; the gap the drag left behind *is* its velocity.
- **Constraints are positional.** Satisfying a link means moving nodes, with no force
  to integrate and no stiffness term to tune into instability.
- **It is stable at large constraint counts**, which an explicit spring solver is not.

### Step order

```
enforce anchor  →  integrate  →  drive held node  →  relax  →  clamp stretch
```

The anchor is pinned first so a moved anchor drags the rope this step rather than
next, which is what makes resizing the overlay look physical.

### Fixed timestep

Physics advances in fixed 240 Hz slices; the display only decides how often
`step(deltaTime:)` is called. At 120 Hz that is two slices per frame, at 60 Hz four.
Behaviour is therefore *identical* across refresh rates, which is asserted directly:
two 120 Hz frames match one 60 Hz frame to within 1e-9.

The accumulator is clamped, so a stall or a wake from sleep cannot trigger a burst of
catch-up steps that would look like the rope teleporting.

### Inextensibility

Three mechanisms, in increasing order of severity:

1. **Relaxation** pulls each link toward its rest length. It runs adaptively: up to a
   large pass budget, exiting as soon as no node moved more than a tolerance. A
   settled rope exits in a pass or two, so the large budget costs nothing except in
   the rare frame that needs it. The budget must exceed the segment count, because
   corrections propagate roughly one link per pass — below that, yanking one end
   leaves the far end unaware and the links between absorb the difference by
   stretching.
2. **A one-sided projection** then forces any remaining over-long link back to the
   ceiling, sharing the correction between its ends. An earlier version snapped the
   offending node straight onto the limit; that oscillated rather than converged,
   because with the chain pinned at both ends each sweep undid the last one's work.
3. **The drag target is clamped** onto the circle the rope can actually reach, and the
   held node follows it at a bounded speed. Without the first, pulling past the rope's
   length holds both ends further apart than the rope can span and the links have
   nowhere to go but stretch. Without the second, a teleporting node leaves the chain
   an unsolvable configuration for one frame.

Measured worst-case link stretch:

| Input | Worst stretch |
|---|---|
| Free swinging | 1.0009 |
| Hard flick, 3000 pt/s, reversing, yanked past reach | 1.0115 |
| Synthetic torture, ~7 revolutions per second | 1.027 |

The first two are within the 1.02 ceiling and are asserted as such. The third exceeds
it briefly and is asserted only to stay bounded and recover, because no pointer can
produce it and relaxation cannot fully converge inside one frame at that rate.

### Sleeping

A settled rope is indistinguishable from a still image, so the solver stops. After
half a second with every node below the rest speed, `step` becomes a no-op, the view
model stops publishing snapshots — so Observation never fires and SwiftUI never
redraws — and the display link drops from 120 to 30 per second. It still ticks,
because the same tick polls the cursor for a grab.

Measured on a 120 Hz display:

| State | CPU |
|---|---|
| Swinging | ~14% of one core |
| Settled | ~0.6% of one core |

Grabbing the charm, moving the anchor or resizing wakes it.

### Interaction and click-through

AppKit cannot pass a click through part of a window and keep the rest, so
`ignoresMouseEvents` is toggled on the whole panel once per frame based on whether the
cursor is within the charm's grab radius. `NSEvent.mouseLocation` is polled rather
than monitored: it needs no event tap and therefore no Accessibility permission. The
assignment is guarded on change, because writing it unconditionally every frame talks
to the window server often enough to keep a settled overlay measurably busy.

SwiftUI's side is narrowed by a `contentShape` of the same disc, so the gesture only
fires on the charm.


---

## The time of day

The rope moves a little differently in the morning than at night. The goal is that
someone thinks *the rope feels a little different today* and never *this is a
different rope*, and almost all of the design follows from taking the second half of
that as seriously as the first.

So **gravity is untouched**. It is the only knob in a pendulum that changes its
period, and a changed period is exactly what would read as a different rope. So are
the length, the segment count, the stiffness and the stretch ceiling. What changes
is how long motion *persists* — felt rather than seen, and the one property of a
swing a person could not name if asked but would notice if it were wrong.

| | Morning | Afternoon | Night |
|---|---|---|---|
| Energy loss, `1 - damping` | ×0.80 | ×1.00 | ×1.30 |
| Release angle | ×1.14 | ×1.00 | ×0.88 |
| Rest threshold | ×0.94 | ×1.00 | ×1.10 |

Afternoon is the identity, asserted as such: the rope at three in the afternoon is
the rope that shipped, to the last bit.

### Why scales and not numbers

Every field is a multiplier, and that is the whole reason the feature composes. The
five rope styles already have damping four times apart; a profile that wrote a fixed
damping number would flatten them into each other, and a leather rope at night would
settle at the same rate as a neon one. Scaling what each style already does keeps
leather the quick one and makes night the quicker version of whatever the rope
happens to be made of.

Scaling *energy loss* rather than damping is the same argument one level down.
Damping is a number just under one and the interesting part is the sliver below it:
halving that halves the rate a swing dies away at, where halving the damping itself
would stop the rope dead.

### Measured

Time for the rope to settle from its own release, in simulated seconds:

| | Morning | Afternoon | Night |
|---|---|---|---|
| Thread | 44.4 | 37.1 | 28.8 |
| Leather | 11.9 | 9.9 | 7.9 |
| Gold Chain | 52.9 | 42.8 | 31.5 |
| Neon | 96.8 | 85.7 | 71.7 |

About a fifth either side of the afternoon, for every style, with each style's own
character intact — leather still settles in a tenth of neon's time.

### Switching

Morning from five, afternoon from noon, night from six, read in the machine's own
time zone. The profile is checked **once a minute**, not once a frame: reading a
`Date()` a hundred and twenty times a second to learn something that changes twice a
day is the definition of idle work.

Applying one is not a wake. A style change is something a person just asked for and
should see at once; noon arriving is not, so `setTimeProfile(_:)` rewrites the
numbers the next step will read and does nothing else. Waking a settled rope to tell
it the afternoon has started would cost a second of simulation, twice a day, to show
nobody anything.

---

## More than one charm

A rope can carry one, two or three charms. It stays one rope: twenty segments,
twenty-one nodes, one anchor, no branches. A charm is centred on a node — which was
already true of the single charm, whose centre is the last node — and the others take
nodes further up.

### Where they attach

`RopeConfiguration.Layout.attachments(forCharmCount:segmentCount:)` divides the rope
as evenly as whole nodes allow, and the last charm always takes the end node:

| Charms | Nodes |
|---|---|
| 1 | 20 |
| 2 | 10, 20 |
| 3 | 6, 13, 20 |

Because the bottom charm keeps the end node, a rope carrying one charm is attached,
weighted and sized exactly as it was before stacks existed — asserted by
`CharmStackTests`, which checks every charm in the catalogue against the old formula.

### How large they may be

`CharmStackLayout` resolves the stack against the rope, walking from the anchor down
and giving each charm what the rope has left. Three limits apply, and the smallest
wins:

- **Its beads.** What a charm occupies on the cord is not its own circle but its
  circle *and the beads threaded above it* — a daruma's beads reach a full radius past
  its knot. Sizing on radius alone left the bottom charm's beads needing thirty-seven
  points of cord and holding twenty-five, and they ended up inside the charm above.
- **Its neighbours.** No charm may take more than 45% of the rope between it and the
  charm either side, so two neighbours can never sum past 90% of the space between
  them and a tenth of it is always left showing as cord. Above the first charm the
  neighbour is the anchor, which keeps it out of the menu bar.
- **The canvas.** The bottom charm's halo has to stay inside it.

A per-count scale — 1.0, 0.92, 0.82 — does the aesthetic work on top, so the limits
are a guarantee rather than the everyday path. The guarantee is what protects against
artwork nobody has drawn yet: `CharmStackTests` resolves every charm in every place at
every count and asserts nothing overlaps and nothing is clipped.

### Weight

Each charm puts its mass on its own node. The end node *becomes* its charm, because
there is no rope below it to weigh anything; an interior node is a piece of rope as
well, so it keeps its unit mass and the charm is added to it. Bead load stacks on top
exactly as before. That is the whole of "the physics account for the combined weight":
three charms put three masses on one chain, and the solver that already balanced one
against the cord balances three the same way, with no new forces and nothing random.

### Charms cannot pass through each other

Spacing the attachments is not enough on its own. A hard throw folds the rope, and a
fold brings two attachment nodes far closer together than the cord between them —
measured at up to **eighty points of overlap** before anything was done about it.

So `separateCharms()` adds a minimum-distance constraint between every pair of
charm-carrying nodes, solved in the same relaxation as the links, splitting its
correction by inverse mass exactly as the link solver does. One-sided, like the
stretch ceiling: charms already far enough apart are untouched, so it costs nothing on
a hanging rope. With it, the same throws leave a worst-case overlap of 5·10⁻¹⁴ points.

### Beads belong to a charm

Every bead carries the index of the charm it hangs above. Its rest place is measured
from *that* charm's knot, and it is held inside the stretch of cord
`CharmStackLayout` allotted that charm — not between the live undersides of the
charms either side of it. The distinction matters more than it sounds: a cord bending
around a charm has more of itself inside that charm's circle than a straight one, so
the gap between two charms *appears* to shrink as the rope swings although the cord
cannot shrink at all. Taking the bound from the live gap hands the beads that artifact
as a squeeze, and a hard throw measured eleven points of overlap on beads nine points
across. Taking it from the charm's own allotment gives zero, whatever the rope is
doing.

Where the artwork's own spacing will not fit the cord a crowded charm has, the group
is compressed toward its charm by a few per cent — which is invisible, where rest
positions that do not fit are not: every bead then fights its tether against the
separation pass for ever.

---

## Rope styles

A charm can hang on one of five cords — Thread, Leather, Gold Chain, Silver Chain
and Neon — and the choice is a physical one, not a skin. `RopeStyle` is a table of
two records per style: a `RopePhysicsProfile` the solver reads and a
`RopeAppearance` the renderer reads.

### Why a style has no mass

The obvious way to make a gold chain feel heavy is to give the rope's nodes more
mass. In this solver that does almost nothing, for three reasons that all follow
from position-based dynamics:

- **Gravity is applied as a positional delta** of `gravity × dt²`. There is no force
  and no division by mass, so every node falls at the same rate whatever it weighs —
  which is also what really happens.
- **Damping scales a displacement**, not a momentum, so it too is mass-independent.
- **A distance constraint splits its correction by the *ratio* of the two nodes'
  inverse masses.** Scale every node together and the ratio is unchanged. Multiplying
  the whole rope's mass by four is very nearly the identity function.

So weight is expressed the way it is actually perceived instead. A heavy thing
accelerates slowly, swings with a long period and keeps going; a light one answers
at once and settles quickly. Those are `gravityScale` and `damping`. Stiffness — how
much the cord gives when the charm is thrown — is `maxStretchRatio` and the two pass
budgets. And the one place a literal mass still changes the picture is the single
link between the last rope node and the charm, because there the ratio genuinely
differs: that is `charmMassScale`.

`gravityScale` is the only knob in a pendulum that changes its period, which goes as
`1 / sqrt(gravityScale)`. It does not move the rope at rest — straight down is
straight down at any gravity — so changing style never moves the charm, only how it
travels.

| | Gravity | Damping | Stretch ceiling | Charm mass |
|---|---|---|---|---|
| Thread | ×1.00 | 0.9990 | 1.020 | ×1.00 |
| Leather | ×1.08 | 0.9955 | 1.006 | ×1.05 |
| Gold Chain | ×0.72 | 0.9992 | 1.004 | ×1.40 |
| Silver Chain | ×0.84 | 0.9988 | 1.005 | ×1.25 |
| Neon | ×1.22 | 0.9997 | 1.030 | ×0.78 |

Damping is applied per fixed step at 240 Hz, so the small differences compound: over
one second Thread keeps 79% of a swing's motion where Leather keeps 34%.

Thread's row is `RopeConfiguration.default`, asserted as such by `RopeStyleTests`, so
the rope Hangly shipped with cannot drift as styles are added. `RopeStyleBehaviourTests`
swings all five and measures them, because the real risk in this feature is not that
it crashes but that it ships as five names for one rope.

### Applying a style

`RopeConfiguration.applying(_:)` writes every styled value from
`RopeConfiguration.default` rather than from the receiver, which makes it idempotent
and order-independent: applying Leather and then Thread gives Thread, not a rope that
remembers being Leather. Geometry — segment count and length, timestep, sleep — is a
property of the canvas and is left alone.

`RopeConfiguration.fitted(to:style:)` takes the style rather than having it applied
afterwards. The overlay re-fits the rope whenever its window changes size, and a fit
that forgot the style would silently put the charm back on Thread the first time the
user rescaled it.

### Changing style mid-swing

The solver is told the instant the user picks a style. `RopeSimulation.setStyle(_:)`
replaces the configuration and the charm's mass and wakes the rope; it does not
reset, pause, or re-fit anything. Every node keeps its position **and its history**,
so the velocity Verlet infers from that history is untouched and a charm that was
mid-swing carries that swing into the new cord.

The *look* is what eases, over 250 ms, in `RopeStyleTransition`. Colour, thickness
and glow are numbers and are interpolated; a dash pattern is not, so the outgoing and
incoming textures are cross-faded over a single blended cord body instead. Someone
dragging down the Rope menu is comparing how the styles feel, and a cord that waited
a quarter of a second — or worse, waited for the rope to settle — would be answering
a question they had stopped asking.

### Drawing a cord

`RopeStyleRenderer` draws every style with ordinary strokes of the path the cord
already has: a dashed pass is a twist, a wider flatter pair is a braid, a heavy notch
with a lit rim is a chain, and three progressively wider, fainter strokes are neon's
halo. Nothing uses a filter, for the reasons in
[the architecture notes](Architecture.md#rendering-without-filters).

The beads riding the cord follow it too, recoloured through a luminance ramp that
keeps the artwork's own shading and exchanges only its hue, cached per size per style
by `VectorImage`. Thread and Gold Chain leave the artwork alone, because its beads are
already that gold and a recolour of a colour it already is can only lose detail.

---

## Beads

Charms hang on a cord with beads threaded above them, and those beads are
simulated too. The rules they follow, and why the bead pass cannot disturb the
rope, are described in [the charm system](Charm-System.md#beads-on-the-cord).
