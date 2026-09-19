//
//  RopeConfiguration.cs
//  Hangly
//
//  Tunable constants for the rope solver.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;

namespace Hangly.Core.Physics;

/// <summary>Every number the rope solver depends on, in one value type.</summary>
/// <remarks>
/// Separating the tuning from the solver keeps <see cref="RopeSimulation"/> free of
/// magic numbers and lets tests drive the solver into deliberately hostile
/// configurations without touching shipped behaviour.
///
/// <para>Units are points and seconds throughout, matching the canvas the rope is
/// drawn into. The canvas has its origin at the top left with <c>y</c> increasing
/// downward, so gravity is a <b>positive</b> <c>y</c> acceleration. That is the same
/// convention Win2D and the original's SwiftUI canvas both use, so no axis is flipped
/// anywhere in the port.</para>
/// </remarks>
public readonly record struct RopeConfiguration
{
    /// <summary>Number of links. Twenty is the shipped value.</summary>
    public int SegmentCount { get; init; }

    /// <summary>Rest length of one link, in points.</summary>
    public double SegmentLength { get; init; }

    /// <summary>Downward acceleration in points per second squared.</summary>
    public double Gravity { get; init; }

    /// <summary>
    /// Fraction of velocity carried into the next step. Below 1 this is air drag: the
    /// rope loses energy and eventually settles instead of swinging forever.
    /// </summary>
    public double Damping { get; init; }

    /// <summary>Gauss-Seidel relaxation passes per step. More passes means a stiffer rope.</summary>
    /// <remarks>
    /// Corrections propagate roughly one link per pass, so this must exceed
    /// <see cref="SegmentCount"/> for a disturbance at the charm to reach the anchor
    /// within a single step. Below that, yanking one end leaves the far end unaware and
    /// the links in between absorb the difference by stretching.
    /// </remarks>
    public int ConstraintIterations { get; init; }

    /// <summary>
    /// Inequality-projection sweeps applied after relaxation, enforcing the stretch
    /// ceiling on any link relaxation left over its limit.
    /// </summary>
    public int StretchPasses { get; init; }

    /// <summary>
    /// Relaxation stops early once no link moved further than this in a whole pass.
    /// Both solver loops are capped rather than fixed: a settled rope converges in a
    /// pass or two and exits, so the large caps cost nothing except in the rare frame
    /// that actually needs them.
    /// </summary>
    public double ConvergenceTolerance { get; init; }

    /// <summary>
    /// Hard ceiling on how far a link may exceed its rest length, as a ratio. Applied
    /// after relaxation, so the rope cannot visibly stretch even if the solver has not
    /// fully converged.
    /// </summary>
    public double MaxStretchRatio { get; init; }

    /// <summary>
    /// Physics advances in fixed slices of this length regardless of the display's
    /// refresh rate. This is what makes behaviour identical at 60, 120 and 165 Hz, and
    /// what keeps Verlet stable.
    /// </summary>
    public double FixedTimeStep { get; init; }

    /// <summary>
    /// Upper bound on the real time consumed by one frame. Prevents a stall or a wake
    /// from sleep from triggering hundreds of catch-up steps at once.
    /// </summary>
    public double MaxFrameDuration { get; init; }

    /// <summary>
    /// Speed ceiling in points per second, applied per node. A safety rail: no
    /// legitimate interaction reaches it, but it makes divergence impossible.
    /// </summary>
    public double MaximumSpeed { get; init; }

    /// <summary>
    /// How far the charm may be dragged from the anchor, as a fraction of the rope's
    /// total length. Below one, so a fully extended rope keeps a little slack for the
    /// solver to work with and never reads as a rigid bar.
    /// </summary>
    public double MaximumReachRatio { get; init; }

    /// <summary>Node speed, in points per second, below which the rope counts as still.</summary>
    public double RestSpeed { get; init; }

    /// <summary>
    /// Consecutive still frames before the solver stops working. The rope is a
    /// background ornament, so once it has settled it must stop costing anything;
    /// without this the overlay would redraw at the display rate forever.
    /// </summary>
    public int FramesBeforeSleep { get; init; }

    /// <summary>
    /// Angle from vertical the rope is released at on first appearance, in radians. A
    /// small offset means the rope visibly swings into place instead of being motionless
    /// until touched.
    /// </summary>
    public double InitialAngle { get; init; }

    /// <summary>Points of cord that charm radii are measured against.</summary>
    /// <remarks>
    /// Deliberately <em>not</em> <see cref="TotalLength"/>. Charm sizes are written as
    /// fractions of a rope, and for as long as there was one slider called "Size" that
    /// was the same thing. It is not any more: lengthening the rope must not fatten the
    /// charm, and enlarging the charm must not lower it. This is the fixed ruler both
    /// are measured with, set once from the canvas and left alone by both controls.
    ///
    /// <para>Zero means "never fitted to a canvas", in which case the rope's own length
    /// is the ruler, exactly as it was before the two could move apart.</para>
    /// </remarks>
    public double CharmUnit { get; init; }

    /// <summary>How large the charm is drawn, as the person asked for it.</summary>
    public double CharmSizeScale { get; init; }

    /// <summary>
    /// Points between the end of the rope and the bottom of the canvas. What stops the
    /// lowest charm being clipped by the edge of the window. Zero carries the same
    /// meaning as it does for <see cref="CharmUnit"/>.
    /// </summary>
    public double SlackBelow { get; init; }

    /// <summary>Where the rope is pinned, in points below the top of the canvas.</summary>
    /// <remarks>
    /// Held in points rather than taken as a fraction of the canvas, because the canvas
    /// grows to make room for a long rope or a large charm and the charm must go on
    /// hanging from the same place on the display when it does.
    /// </remarks>
    public double AnchorHeight { get; init; }

    /// <summary>Number of nodes, which is one more than the number of links.</summary>
    public int PointCount => SegmentCount + 1;

    /// <summary>Total rest length of the rope.</summary>
    public double TotalLength => SegmentCount * SegmentLength;

    /// <summary>The ruler charm radii are measured with, after the person's charm size.</summary>
    public double CharmReference => (CharmUnit > 0 ? CharmUnit : TotalLength) * CharmSizeScale;

    /// <summary>Room below the rope for the lowest charm and its halo, in points.</summary>
    public double CharmHeadroom =>
        SlackBelow > 0 ? SlackBelow : TotalLength * Layout.TailFraction / Layout.LengthFraction;

    /// <summary>Where the rope is pinned in a canvas of this size.</summary>
    public Vec2 Anchor(Size size) =>
        AnchorHeight > 0 ? new Vec2(size.Width / 2, AnchorHeight) : Layout.AnchorIn(size);

    /// <summary>The rope exactly as it shipped: thread, at three in the afternoon.</summary>
    public static RopeConfiguration Default { get; } = new()
    {
        SegmentCount = 20,
        SegmentLength = 11,
        Gravity = 2000,
        Damping = 0.999,
        ConstraintIterations = 256,
        StretchPasses = 256,
        ConvergenceTolerance = 0.05,
        MaxStretchRatio = 1.02,
        FixedTimeStep = 1.0 / 240.0,
        MaxFrameDuration = 0.1,
        MaximumSpeed = 6000,
        MaximumReachRatio = 0.98,
        RestSpeed = 4.0,
        FramesBeforeSleep = 60,
        InitialAngle = 0.38,
        CharmUnit = 0,
        CharmSizeScale = 1,
        SlackBelow = 0,
        AnchorHeight = 0,
    };

    /// <summary>Fits the rope to a canvas, keeping the shipped proportions at any scale.</summary>
    /// <remarks>
    /// The canvas is not the ruler. It used to be: every length here was a fraction of
    /// the window's height, so growing the window to make room for a bigger charm also
    /// lengthened the rope, and lengthening the rope also fattened the charm. What the
    /// canvas gives is a <em>unit</em> — the height the shipped window would have had
    /// for this rope — and both controls are then measured against that instead of
    /// against each other.
    /// </remarks>
    /// <param name="size">The overlay canvas size in points.</param>
    /// <param name="style">
    /// The cord the charm hangs on. Taken here rather than applied afterwards because
    /// the overlay re-fits on every resize, and a style left out of the fit would be
    /// silently reset to thread the first time the window changed size.
    /// </param>
    /// <param name="profile">
    /// The time of day the rope is moving in, applied for the same reason and with the
    /// same consequence if it were left out.
    /// </param>
    /// <param name="charmSize">How large the charm is drawn. Changes nothing about the rope.</param>
    /// <param name="ropeLength">
    /// How far the charm hangs, as a multiple of the shipped rope. Changes nothing about
    /// the charm.
    /// </param>
    public static RopeConfiguration Fitted(
        Size size,
        RopeStyle style = RopeStyleTable.Default,
        RopeTimeProfile profile = RopeTimeProfileTable.Baseline,
        double charmSize = 1,
        double ropeLength = 1)
    {
        RopeConfiguration configuration = Default;
        Size room = Layout.CanvasScale(charmSize, ropeLength);

        // The height the shipped canvas would have had. Every proportion below is taken
        // against this rather than against the canvas actually handed over, which is
        // what keeps the two controls from reading each other.
        double unit = Math.Max(40, size.Height / room.Height);

        double charmUnit = unit * Layout.LengthFraction;
        configuration = configuration with
        {
            CharmUnit = charmUnit,
            CharmSizeScale = charmSize,
            SegmentLength = charmUnit * ropeLength / configuration.SegmentCount,
            AnchorHeight = unit * Layout.AnchorFraction,
        };

        configuration = configuration with
        {
            SlackBelow = Math.Max(0, size.Height - configuration.AnchorHeight - configuration.TotalLength),
        };

        return configuration.Applying(style, profile);
    }

    /// <summary>Returns this configuration with a style's solver values applied.</summary>
    /// <remarks>
    /// Every styled value is written from <see cref="Default"/> rather than from the
    /// receiver, so this is idempotent and order-independent: applying leather and then
    /// thread gives thread, not a rope that remembers being leather. Geometry — segment
    /// count, length, timestep, sleep — is a property of the canvas and is left exactly
    /// as it was.
    /// </remarks>
    public RopeConfiguration Applying(RopeStyle style, RopeTimeProfile profile = RopeTimeProfileTable.Baseline)
    {
        RopePhysicsProfile physics = RopeStyleTable.PhysicsOf(style);
        RopeConfiguration defaults = Default;

        RopeConfiguration configuration = this with
        {
            Gravity = defaults.Gravity * physics.GravityScale,
            Damping = physics.Damping,
            MaxStretchRatio = physics.MaxStretchRatio,
            ConstraintIterations = physics.ConstraintIterations,
            StretchPasses = physics.StretchPasses,
        };

        // The time of day goes on last and scales what the style decided, so a leather
        // rope stays the quick one and night is the quicker version of whichever rope
        // you are on. Written from the defaults for the same reason the style is: so
        // that applying morning and then afternoon gives the afternoon, not a rope that
        // remembers the morning.
        RopeTimePhysics time = RopeTimeProfileTable.PhysicsOf(profile);
        return configuration with
        {
            Damping = 1 - ((1 - configuration.Damping) * time.EnergyLossScale),
            InitialAngle = defaults.InitialAngle * time.ReleaseAngleScale,
            RestSpeed = defaults.RestSpeed * time.RestSpeedScale,
        };
    }

    /// <summary>Proportions shared by the solver and the renderer.</summary>
    public static class Layout
    {
        /// <summary>Rope length as a fraction of canvas height.</summary>
        public const double LengthFraction = 0.69;

        /// <summary>Anchor height as a fraction of canvas height.</summary>
        /// <remarks>
        /// Small rather than zero: the cord's own drawing starts exactly here, and a
        /// sliver keeps antialiasing at the top of a top-inset-zero window from reading
        /// as a hard clip.
        /// </remarks>
        public const double AnchorFraction = 0.01;

        /// <summary>Extra radius around the charm that still accepts a grab.</summary>
        public const double GrabPadding = 10.0;

        /// <summary>
        /// The largest a charm may be as a fraction of the rope between it and its
        /// nearest neighbour.
        /// </summary>
        /// <remarks>
        /// Below a half, so two adjacent charms can never sum past ninety per cent of
        /// the rope between them and a tenth of it is always left showing as cord. This
        /// is the guarantee rather than the tuning: <see cref="CharmScale"/> keeps the
        /// shipped charms clear of it, and this catches anything a future asset asks for
        /// that would not be.
        /// </remarks>
        public const double CharmClearance = 0.45;

        /// <summary>How far a charm's ambient halo reaches past its own radius.</summary>
        public const double CharmHaloExtent = 1.7;

        /// <summary>
        /// What is left of the shipped canvas below the end of the rope: the room the
        /// lowest charm and its halo hang in.
        /// </summary>
        public static double TailFraction => 1 - AnchorFraction - LengthFraction;

        /// <summary>
        /// How much larger than the shipped canvas the overlay has to be to hold a rope
        /// this long with charms this big.
        /// </summary>
        /// <remarks>
        /// The canvas used to be scaled by the one "Size" slider, which is why that
        /// slider moved everything at once. Now it grows by exactly what has been asked
        /// for and no more: a longer rope needs more room below the anchor, a larger
        /// charm needs more room below the rope and more to either side, and neither is
        /// any reason to change the other. At the shipped values this returns exactly
        /// 1×1, so the overlay is the window it always was.
        /// </remarks>
        public static Size CanvasScale(double charmSize, double ropeLength)
        {
            double height = AnchorFraction + (LengthFraction * ropeLength) + (TailFraction * charmSize);
            return new Size(Math.Max(1, charmSize), Math.Max(1, height));
        }

        /// <summary>Anchor point for a canvas of the given size.</summary>
        public static Vec2 AnchorIn(Size size) => new(size.Width / 2, size.Height * AnchorFraction);

        /// <summary>Which nodes the charms hang from, from the anchor down.</summary>
        /// <remarks>
        /// The last charm always takes the end node, so a single charm is attached
        /// exactly where it has always been and nothing about the one-charm rope
        /// changes. The others divide the rope as evenly as whole nodes allow: two
        /// charms take nodes 10 and 20, three take 6, 13 and 20. Rounding down rather
        /// than to nearest is what puts the spare node at the <em>top</em>, which is the
        /// gap a short cord is least noticeable in.
        /// </remarks>
        public static int[] Attachments(int charmCount, int segmentCount)
        {
            int charms = Math.Max(1, Math.Min(charmCount, CharmStack.MaximumCount));
            var nodes = new int[charms];
            for (int index = 1; index <= charms; index++)
            {
                if (index == charms)
                {
                    nodes[index - 1] = segmentCount;
                    continue;
                }

                double node = Math.Floor(segmentCount * (double)index / charms);

                // At least one node clear of its neighbours, however the arithmetic falls.
                nodes[index - 1] = Math.Max(index, Math.Min(segmentCount - (charms - index), (int)node));
            }

            return nodes;
        }

        /// <summary>How much a charm shrinks when it has company.</summary>
        /// <remarks>
        /// Three charms at full size would crowd the rope, and a string of charms reads
        /// better with smaller ones anyway — a single charm is a statement, three are a
        /// string. One charm is untouched, so this changes nothing about the rope as it
        /// shipped.
        /// </remarks>
        public static double CharmScale(int charmCount) =>
            Math.Max(1, Math.Min(charmCount, CharmStack.MaximumCount)) switch
            {
                1 => 1.0,
                2 => 0.92,
                _ => 0.82,
            };
    }
}
