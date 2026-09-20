//
//  RopeSimulation.cs
//  Hangly
//
//  Verlet rope with distance-constraint relaxation.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>A hanging rope simulated with Verlet integration and position-based constraints.</summary>
/// <remarks>
/// The step order is deliberate and is what makes the rope both stable and stiff:
/// <list type="number">
/// <item><b>Pin the anchor.</b> Done first so a moved anchor drags the rope this step
/// rather than next, which is what makes a window resize look physical.</item>
/// <item><b>Integrate.</b> Each free node moves by its own damped displacement plus
/// gravity. No forces, no velocity array: Verlet infers velocity from history.</item>
/// <item><b>Drive the held node.</b> A dragged node is written directly, with its
/// history set so that its implied velocity matches the cursor's.</item>
/// <item><b>Relax constraints.</b> Gauss-Seidel passes pull each link back to its rest
/// length. Later passes see the corrections of earlier ones, so convergence is fast.</item>
/// <item><b>Clamp stretch.</b> A final hard pass guarantees no link exceeds its limit
/// even if relaxation has not fully converged.</item>
/// </list>
///
/// <para>Time advances in fixed slices. The display's refresh rate only decides how
/// often <see cref="Step"/> is called, never how far the physics moves per slice, so the
/// rope behaves identically at 60 Hz and 120 Hz.</para>
///
/// <para>Not thread-safe, and deliberately so: it is owned by the UI thread that drives
/// it, which is the port of the original's <c>@MainActor</c> isolation. Moving the
/// solver off that thread means publishing immutable snapshots across the boundary, not
/// locking this.</para>
/// </remarks>
public sealed partial class RopeSimulation
{
    /// <summary>
    /// The rope's nodes, anchor first. An array rather than a list so the constraint
    /// passes can write through an index into the struct in place.
    /// </summary>
    internal RopePoint[] Points = [];

    private double accumulator;
    private int stillFrames;

    public RopeSimulation(
        RopeConfiguration? configuration = null,
        Vec2 anchor = default,
        CharmMetrics? charmMetrics = null,
        IReadOnlyList<CharmMetrics>? charmStack = null,
        RopeStyle style = RopeStyleTable.Default,
        RopeTimeProfile timeProfile = RopeTimeProfileTable.Baseline)
    {
        // The style is applied to the configuration here, so Configuration and Style can
        // never disagree. That invariant is the whole defence against the obvious bug in
        // this feature: the overlay re-fits the rope on every resize, and anything that
        // rebuilds the configuration without the style silently puts the charm back on
        // thread.
        RopeConfiguration resolved = (configuration ?? RopeConfiguration.Default).Applying(style, timeProfile);
        IReadOnlyList<CharmMetrics> stack = charmStack is { Count: > 0 }
            ? [.. charmStack]
            : [charmMetrics ?? CharmMetrics.Default];

        Configuration = resolved;
        Anchor = anchor;
        CharmStackMetrics = stack;
        CharmLayout = CharmStackLayout.Resolve(stack, resolved);
        Style = style;
        TimeProfile = timeProfile;
        Reset();
    }

    /// <summary>Every number the solver reads.</summary>
    public RopeConfiguration Configuration { get; internal set; }

    /// <summary>Where the rope is pinned.</summary>
    public Vec2 Anchor { get; private set; }

    /// <summary>The cord the charm hangs on.</summary>
    /// <remarks>
    /// Held here as well as in the configuration because a resize re-fits the
    /// configuration from scratch, and the style has to survive that.
    /// </remarks>
    public RopeStyle Style { get; internal set; }

    /// <summary>The time of day the rope is moving in, held for the same reason.</summary>
    public RopeTimeProfile TimeProfile { get; internal set; }

    /// <summary>
    /// How far the charm hangs, as a multiple of the shipped rope. Held here for the same
    /// reason as the other two: a resize re-fits from scratch.
    /// </summary>
    public double RopeLength { get; internal set; } = 1;

    /// <summary>
    /// How large the charm is drawn, held for the same reason. Separate from
    /// <see cref="RopeLength"/> in every sense: neither reads the other.
    /// </summary>
    public double CharmSize { get; internal set; } = 1;

    /// <summary>Mass and size of every charm on the rope, from the anchor down.</summary>
    public IReadOnlyList<CharmMetrics> CharmStackMetrics { get; internal set; }

    /// <summary>Where those charms hang and how large they are allowed to be.</summary>
    /// <remarks>
    /// Held rather than recomputed because the renderer, the bead pass and the mass pass
    /// all read it several times a step.
    /// </remarks>
    public CharmStackLayout CharmLayout { get; internal set; }

    /// <summary>
    /// The charm on the end of the rope. Everything that predates stacks means this one,
    /// and for a single charm it is the whole stack.
    /// </summary>
    public CharmMetrics CharmMetricsValue =>
        CharmStackMetrics.Count > 0 ? CharmStackMetrics[^1] : CharmMetrics.Default;

    /// <summary>The beads threaded on the cord above the charm, nearest the anchor first.</summary>
    internal RopeBead[] Beads = [];

    /// <summary>Where the drawn cord stops, which is where the charm's artwork takes over.</summary>
    public Vec2 CordEnd { get; internal set; }

    /// <summary>Length of the drawn cord, in points. Beads are threaded along it.</summary>
    public double CordLength { get; internal set; }

    /// <summary>Direction from the knot to each charm's centre, which is how each hangs.</summary>
    internal readonly List<double> CharmOrientations = [];

    /// <summary>
    /// The stretch of cord each charm covers, from where the cord meets it to where the
    /// cord comes back out below it.
    /// </summary>
    internal readonly List<ArcSpan> CharmSpans = [];

    /// <summary>How the charm on the end of the rope hangs.</summary>
    public double CharmOrientation =>
        CharmOrientations.Count > 0 ? CharmOrientations[^1] : Math.PI / 2;

    public bool IsRunning { get; private set; }

    /// <summary>Fixed steps consumed by the most recent frame. Surfaced in debug mode.</summary>
    public int LastStepCount { get; private set; }

    /// <summary>Whether the rope has settled and stopped simulating. Cleared by <see cref="Wake"/>.</summary>
    public bool IsSleeping { get; private set; }

    /// <summary>What each charm on the rope says its beads are, in proportions.</summary>
    internal IReadOnlyList<IReadOnlyList<CharmBead>> BeadDescriptions { get; set; } = [];

    /// <summary>The drawn cord, rebuilt each step and reused rather than reallocated.</summary>
    internal readonly RopeCurve Curve = new();

    // Written by the drag surface, read by the solver.
    internal int? DragIndex;
    internal Vec2 DragTarget;
    internal Vec2 DragVelocity;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        if (Points.Length == 0)
        {
            Reset();
        }

        accumulator = 0;
        IsRunning = true;
        Wake();
    }

    public void Stop()
    {
        IsRunning = false;
        accumulator = 0;
    }

    /// <summary>Advances the simulation.</summary>
    /// <param name="deltaTime">Elapsed time in seconds since the previous step.</param>
    public void Step(double deltaTime)
    {
        if (!IsRunning || deltaTime <= 0 || IsSleeping)
        {
            LastStepCount = 0;
            return;
        }

        // Clamping the accumulator stops a stall or a wake from sleep turning into a
        // burst of catch-up steps, which would look like the rope teleporting.
        accumulator = Math.Min(accumulator + deltaTime, Configuration.MaxFrameDuration);

        double timeStep = Configuration.FixedTimeStep;
        int taken = 0;
        while (accumulator >= timeStep)
        {
            Advance(timeStep);
            accumulator -= timeStep;
            taken += 1;
        }

        LastStepCount = taken;
        UpdateSleepState();
    }

    /// <summary>Wakes the rope so the next <see cref="Step"/> does work again.</summary>
    /// <summary>
    /// How far the lowest charm hangs to one side of the anchor, in points.
    /// </summary>
    /// <remarks>
    /// Here so a caller that only wants to know which side the charm is on does not have
    /// to take a <see cref="Snapshot"/> to find out. A snapshot allocates the points, the
    /// charms and the beads; anything asking this question on every frame of a 120 Hz
    /// loop cannot afford that, and counting swings is exactly such a caller.
    /// </remarks>
    public double CharmOffsetFromAnchor =>
        Points.Length == 0 ? 0 : Points[^1].Position.X - Anchor.X;

    /// <summary>Gives the charm a sideways shove, as though someone had flicked it.</summary>
    /// <remarks>
    /// The speed is not invented. It is the speed the charm would already be carrying at
    /// the bottom of a swing released from <see cref="RopeConfiguration.InitialAngle"/> —
    /// the same release the rope performs when it starts — so a push looks like the
    /// launch rather than like a new number somebody chose. Written into the node's
    /// history, because history is what Verlet reads as velocity.
    /// </remarks>
    /// <param name="direction">Positive pushes right, negative left.</param>
    public void Push(double direction = 1)
    {
        if (Points.Length < 2 || direction == 0)
        {
            return;
        }

        double length = Configuration.TotalLength;
        double speed = Math.Sqrt(
            2 * Configuration.Gravity * length * (1 - Math.Cos(Configuration.InitialAngle)));

        ref RopePoint charm = ref Points[^1];
        charm.PreviousPosition = charm.Position
            - new Vec2(Math.Sign(direction) * speed * Configuration.FixedTimeStep, 0);

        Wake();
    }

    public void Wake()
    {
        IsSleeping = false;
        stillFrames = 0;
    }

    /// <summary>
    /// Applies a one-shot velocity impulse to the node at <paramref name="index"/>, as if
    /// something hit it from outside. The rope carries the momentum from there.
    /// </summary>
    /// <remarks>
    /// Uses the same Verlet trick as a drag release: the gap between position and
    /// previous position <em>is</em> the velocity, so writing <c>PreviousPosition</c>
    /// backward by the desired displacement gives the node exactly that much speed on the
    /// next step.
    /// </remarks>
    public void ApplyImpulse(Vec2 velocity, int index)
    {
        if (index < 0 || index >= Points.Length || Points[index].InverseMass <= 0)
        {
            return;
        }

        double timeStep = Configuration.FixedTimeStep;
        Points[index].PreviousPosition = Points[index].Position - (velocity * timeStep);
        Wake();
    }

    /// <summary>A settled rope is indistinguishable from a still image, so stop drawing one.</summary>
    private void UpdateSleepState()
    {
        if (DragIndex is not null)
        {
            stillFrames = 0;
            return;
        }

        double speedLimit = Configuration.RestSpeed * Configuration.FixedTimeStep;

        bool moving = false;
        for (int index = 0; index < Points.Length && !moving; index++)
        {
            moving = Points[index].Displacement.Magnitude > speedLimit;
        }

        for (int index = 0; index < Beads.Length && !moving; index++)
        {
            moving = Beads[index].Displacement.Magnitude > speedLimit;
        }

        if (moving)
        {
            stillFrames = 0;
            return;
        }

        stillFrames += 1;
        if (stillFrames >= Configuration.FramesBeforeSleep)
        {
            IsSleeping = true;
        }
    }

    public void Reset() => Reset(Configuration.InitialAngle);

    /// <summary>
    /// Rebuilds the rope hanging straight down with no motion, for users who have asked
    /// for reduced motion: it then moves only when they move it.
    /// </summary>
    public void ResetToHanging() => Reset(0);

    private void Reset(double angle)
    {
        Points = RopePoint.Chain(Configuration, Anchor, CharmMetricsValue, angle);
        accumulator = 0;
        DragIndex = null;
        DragVelocity = Vec2.Zero;
        LastStepCount = 0;
        RebuildBeads(preservingMotion: false);
        Wake();
    }

    /// <summary>
    /// Fits the rope to a canvas and to the person's two sliders in one step.
    /// </summary>
    /// <remarks>
    /// The three belong together. <see cref="Resize"/> re-fits from
    /// <see cref="CharmSize"/> and <see cref="RopeLength"/> as they stand, so resizing
    /// first and setting the sliders afterwards fits the rope twice — once to numbers
    /// nobody asked for — and the charm visibly moves through the wrong position on the
    /// way to the right one.
    /// </remarks>
    public void Fit(Size canvasSize, double charmSize, double ropeLength)
    {
        CharmSize = charmSize;
        RopeLength = ropeLength;
        Resize(canvasSize);
    }

    /// <summary>
    /// Re-fits the rope to a new canvas without discarding its motion, so changing the
    /// overlay scale makes the rope swing rather than snap.
    /// </summary>
    public void Resize(Size canvasSize)
    {
        RopeConfiguration fitted = RopeConfiguration.Fitted(
            canvasSize,
            Style,
            TimeProfile,
            CharmSize,
            RopeLength);

        // A rope that has not started has no motion to preserve, so it is laid out on the
        // new canvas rather than moved onto it. That is the difference between the overlay
        // fitting its rope for the first time and the person dragging a slider: the first
        // is a construction, the second is a change to something already swinging.
        bool needsRebuild = Points.Length != fitted.PointCount || !IsRunning;

        Vec2 previousAnchor = Anchor;
        Configuration = fitted;
        Anchor = fitted.Anchor(canvasSize);
        RefreshLayout();

        if (needsRebuild)
        {
            Reset();
        }
        else
        {
            // The whole rope travels with the anchor, history included.
            //
            // Without this the anchor teleports and the rope does not: the next step pins
            // node zero to the new place and the constraint solver whips that displacement
            // down the chain, which reads as the charm being flung. It was worth 236 points
            // of excursion on a canvas 220 wide — the rope left the window on launch, when
            // the overlay is first fitted to its canvas, and again on every turn of the
            // size slider, which moves the anchor by half of what the canvas grew.
            //
            // Translating by the delta is what keeps this a *move* rather than a yank:
            // every node keeps its offset from the anchor and its offset from its own
            // previous position, so the velocity Verlet reads out of that history is
            // exactly what it was and a rope that was mid-swing stays mid-swing.
            Vec2 shift = Anchor - previousAnchor;
            if (shift != Vec2.Zero)
            {
                for (int index = 0; index < Points.Length; index++)
                {
                    Points[index].Position += shift;
                    Points[index].PreviousPosition += shift;
                }
            }

            RebuildBeads(preservingMotion: true);
            Wake();
        }
    }

    private void Advance(double timeStep)
    {
        EnforceAnchor();
        Integrate(timeStep);
        DriveDraggedPoint(timeStep);

        // Relax until converged, or until the pass budget runs out. Written as a `while`
        // because the exit condition is the point: in the original a `for ... where`
        // filtered iterations rather than ending the loop, and quietly ran the full
        // budget on every step.
        int relaxations = 0;
        double residual = double.PositiveInfinity;
        while (relaxations < Configuration.ConstraintIterations && residual >= Configuration.ConvergenceTolerance)
        {
            residual = Math.Max(SolveDistanceConstraints(), SeparateCharms());
            relaxations += 1;
        }

        EnforceMaximumStretch();
        RefreshCord();
        AdvanceBeads(timeStep);
    }

    private void EnforceAnchor()
    {
        if (Points.Length == 0)
        {
            return;
        }

        Points[0].Position = Anchor;
        Points[0].PreviousPosition = Anchor;
    }

    private void Integrate(double timeStep)
    {
        var gravityStep = new Vec2(0, Configuration.Gravity * timeStep * timeStep);
        double damping = Configuration.Damping;
        double displacementLimit = Configuration.MaximumSpeed * timeStep;

        for (int index = 0; index < Points.Length; index++)
        {
            if (index == DragIndex || Points[index].InverseMass <= 0)
            {
                continue;
            }

            Vec2 carried = (Points[index].Displacement * damping).Limited(displacementLimit);
            Points[index].PreviousPosition = Points[index].Position;
            Points[index].Position += carried + gravityStep;
        }
    }

    /// <summary>Moves the held node toward the cursor, at a finite rate.</summary>
    /// <remarks>
    /// The rate limit matters more than it looks. A held node that teleports leaves the
    /// rest of the chain an unreachable configuration to solve in one step, and relaxation
    /// cannot redistribute that far in the passes available, so links visibly stretch for
    /// a frame. A real cursor cannot teleport, so following at a bounded speed is the
    /// faithful model as well as the stable one. At human pointer speeds the limit is
    /// never reached and tracking is exact.
    /// </remarks>
    private void DriveDraggedPoint(double timeStep)
    {
        if (DragIndex is not int index)
        {
            return;
        }

        Vec2 current = Points[index].Position;
        double travelLimit = Configuration.MaximumSpeed * timeStep;
        Points[index].Position = current + (DragTarget - current).Limited(travelLimit);
        Points[index].SetVelocity(DragVelocity, timeStep);
    }

    /// <summary>Sets one node's inverse mass.</summary>
    /// <remarks>
    /// The only way anything outside the solver may touch a node, and it exists so that
    /// bead loading can live beside the beads rather than here.
    /// </remarks>
    internal void SetInverseMass(double value, int index)
    {
        if (index < 0 || index >= Points.Length)
        {
            return;
        }

        Points[index].InverseMass = value;
    }
}
