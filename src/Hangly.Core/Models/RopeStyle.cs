//
//  RopeStyle.cs
//  Hangly
//
//  The nine cords a charm can hang on.
//

namespace Hangly.Core.Models;

/// <summary>What the rope is made of.</summary>
/// <remarks>
/// A style is the whole answer to "what is this charm hanging on": how the cord is
/// drawn, how thick it is, what colour it is, and — the part that makes it more than
/// a skin — how it swings. Choosing one is choosing a feel, not a texture.
///
/// Pure data on purpose. The solver reads <see cref="RopeStyleTable.Physics"/> and
/// the renderer reads the appearance, so a new style is a table row rather than a
/// change to either.
/// </remarks>
public enum RopeStyle
{
    /// <summary>The twisted gold thread Hangly has always hung its charms on.</summary>
    Thread,

    /// <summary>A flat braided cord: stiffer than thread, and quicker to settle.</summary>
    Leather,

    /// <summary>Linked gold. The heaviest, and the slowest to swing.</summary>
    GoldChain,

    /// <summary>Linked silver: a chain's weight with a little more life in it.</summary>
    SilverChain,

    /// <summary>A lit filament. The lightest and the most responsive.</summary>
    Neon,

    /// <summary>White silk. The thinnest cord, and the one that barely gives at all.</summary>
    SpiderThread,

    /// <summary>A dark cord that swallows the light around it.</summary>
    MidnightCord,

    /// <summary>Hand-twisted cotton in saffron and vermilion, the way a temple thread is tied.</summary>
    TempleThread,

    /// <summary>Polished silver satin. A cord where <see cref="SilverChain"/> is links.</summary>
    SilverCord,
}

/// <summary>How a style swings.</summary>
/// <remarks>
/// Every field is a multiplier on, or a replacement for, a value the solver already
/// had. None of them is a new force, a spring or a second integrator: the rope stays
/// the same twenty-segment Verlet chain, solved the same way, so a style cannot make
/// it unstable and cannot make it non-deterministic.
///
/// <para><b>Why there is no mass here.</b> The obvious way to make a gold chain feel
/// heavy is to give the rope's nodes more mass, and in this solver that does close to
/// nothing. Gravity is applied as a positional delta of <c>gravity × dt²</c>, which is
/// mass-independent; damping scales a displacement, which is mass-independent; and a
/// distance constraint splits its correction by the <em>ratio</em> of the two nodes'
/// inverse masses, which is unchanged when every node is scaled together. Multiplying
/// every rope node's mass by four is very nearly the identity function.</para>
///
/// <para>So weight is expressed the way it is actually perceived instead: a heavy
/// thing accelerates slowly, swings with a long period and keeps going. That is
/// <see cref="GravityScale"/> and <see cref="Damping"/>. Stiffness is
/// <see cref="MaxStretchRatio"/> and the pass budgets. The one place a literal mass
/// still changes the picture is the single link between the last rope node and the
/// charm, because there the ratio genuinely differs: that is
/// <see cref="CharmMassScale"/>.</para>
/// </remarks>
/// <param name="GravityScale">
/// Multiplier on <see cref="Physics.RopeConfiguration.Gravity"/>. The only knob in a
/// pendulum that changes its period, which goes as <c>1 / sqrt(gravityScale)</c>. It
/// does not change where the rope hangs at rest — straight down is straight down at
/// any gravity — so a style change never moves the charm, only how it travels.
/// </param>
/// <param name="Damping">
/// Replaces <see cref="Physics.RopeConfiguration.Damping"/>. Applied per fixed step at
/// 240 Hz, so small differences compound quickly: 0.999 keeps 79% of a swing's motion
/// over a second where 0.9955 keeps 34%.
/// </param>
/// <param name="MaxStretchRatio">
/// Replaces <see cref="Physics.RopeConfiguration.MaxStretchRatio"/>. A chain is
/// inextensible and a thread is not.
/// </param>
/// <param name="ConstraintIterations">
/// Replaces <see cref="Physics.RopeConfiguration.ConstraintIterations"/>. A cap rather
/// than a fixed cost — relaxation exits as soon as it converges — so a stiffer style
/// pays for its extra passes only on the frames that need them.
/// </param>
/// <param name="StretchPasses">
/// Replaces <see cref="Physics.RopeConfiguration.StretchPasses"/>. Tracks the
/// iteration budget: a tighter ceiling takes more sweeps to satisfy.
/// </param>
/// <param name="CharmMassScale">
/// Multiplier on the charm's mass, which sets the inverse-mass ratio of the one link
/// that has a ratio worth changing. Above one the charm holds its line and the rope
/// bends around it; below one the charm is flicked about by the cord.
/// </param>
public readonly record struct RopePhysicsProfile(
    double GravityScale,
    double Damping,
    double MaxStretchRatio,
    int ConstraintIterations,
    int StretchPasses,
    double CharmMassScale);

/// <summary>The two records per style: what the solver reads, and what a person reads.</summary>
public static class RopeStyleTable
{
    /// <summary>
    /// The shipped default, and the behaviour every build before rope styles had.
    /// Thread is exactly <see cref="Physics.RopeConfiguration.Default"/>, which is why
    /// every physics comparison is made against it.
    /// </summary>
    public const RopeStyle Default = RopeStyle.Thread;

    /// <summary>
    /// What Hangly ships hanging. Its own name because "the rope a new install gets"
    /// and "the rope the physics is measured against" are two questions that no longer
    /// share an answer.
    /// </summary>
    public const RopeStyle Shipped = RopeStyle.SpiderThread;

    /// <summary>The rope a brand-new install hangs its first charm on.</summary>
    /// <remarks>
    /// Gold chain rather than <see cref="Shipped"/>. The two are different questions:
    /// <see cref="Shipped"/> is what the macOS build has always drawn and what the
    /// rendering tests measure against, and moving it would move those. This only decides
    /// what somebody meets on a first launch.
    /// </remarks>
    public const RopeStyle FirstRun = RopeStyle.GoldChain;

    /// <summary>Every style, in menu order.</summary>
    public static IReadOnlyList<RopeStyle> All { get; } = Enum.GetValues<RopeStyle>();

    /// <summary>The solver values for a style.</summary>
    public static RopePhysicsProfile PhysicsOf(RopeStyle style) => style switch
    {
        // Exactly `RopeConfiguration.Default`. Thread is not a style so much as the
        // name for what the rope already did, and RopeStyleTests asserts these numbers
        // against the defaults so the two cannot drift apart.
        RopeStyle.Thread => new RopePhysicsProfile(1.0, 0.999, 1.02, 256, 256, 1.0),

        // Stiffer and quicker to settle: a braided cord has real internal friction,
        // and barely stretches at all.
        RopeStyle.Leather => new RopePhysicsProfile(1.08, 0.9955, 1.006, 288, 288, 1.05),

        // The heavy one. Slow to start, slow to turn round, slow to give up.
        RopeStyle.GoldChain => new RopePhysicsProfile(0.72, 0.9992, 1.004, 320, 320, 1.40),

        // A chain, but a lighter gauge: the same character with more life in it.
        RopeStyle.SilverChain => new RopePhysicsProfile(0.84, 0.9988, 1.005, 320, 320, 1.25),

        // Barely there. Quickest to react, quickest to swing, and the only style with
        // any real give in it.
        RopeStyle.Neon => new RopePhysicsProfile(1.22, 0.9997, 1.03, 224, 224, 0.78),

        // Silk: a fraction of the weight of anything else here and stronger than all of
        // it. Quick like neon, but where neon is elastic this barely gives at all.
        //
        // 1.004 and not 1.003: at 1.003 a hard throw measured 1.00313, because the
        // clamp is one-sided and adaptive and cannot land exactly on a ceiling that
        // tight within any pass budget worth paying for. Gold chain sits at the same
        // number for the same reason.
        RopeStyle.SpiderThread => new RopePhysicsProfile(1.16, 0.9996, 1.004, 336, 336, 0.80),

        // Between the two chains in weight, and the most damped thing here after
        // leather: a heavy braid absorbs a swing rather than carrying it.
        RopeStyle.MidnightCord => new RopePhysicsProfile(0.78, 0.9986, 1.007, 304, 304, 1.32),

        // Cotton, hand-twisted and a little loose. The softest cord here: it has the
        // most give of the nine and it settles in its own time.
        RopeStyle.TempleThread => new RopePhysicsProfile(1.04, 0.9982, 1.026, 232, 232, 0.92),

        // A woven cord rather than a linked chain, and it behaves like one:
        // SilverChain's weight less a third of it, and quicker on the turn.
        RopeStyle.SilverCord => new RopePhysicsProfile(0.94, 0.9990, 1.009, 272, 272, 1.08),

        _ => PhysicsOf(RopeStyle.Thread),
    };

    /// <summary>Shown in the tray menu.</summary>
    public static string DisplayNameOf(RopeStyle style) => style switch
    {
        RopeStyle.Thread => "Thread",
        RopeStyle.Leather => "Leather",
        RopeStyle.GoldChain => "Gold Chain",
        RopeStyle.SilverChain => "Silver Chain",
        RopeStyle.Neon => "Neon",
        RopeStyle.SpiderThread => "Spider Thread",
        RopeStyle.MidnightCord => "Midnight Cord",
        RopeStyle.TempleThread => "Temple Thread",
        RopeStyle.SilverCord => "Silver Cord",
        _ => "Thread",
    };

    /// <summary>What it is and how it behaves, for the Library.</summary>
    /// <remarks>
    /// Both halves matter: a style is a look <em>and</em> a feel, and someone choosing
    /// one from a picture of a cord has been told only half of it.
    /// </remarks>
    public static string SummaryOf(RopeStyle style) => style switch
    {
        RopeStyle.Thread => "Twisted gold. The cord Hangly has always used — light, quick, and quiet.",
        RopeStyle.Leather => "A flat braid. Stiffer than thread, and settles a little sooner.",
        RopeStyle.GoldChain => "Linked gold. The heaviest of the five, and the slowest to swing.",
        RopeStyle.SilverChain => "Linked silver. A chain's weight with a little more life in it.",
        RopeStyle.Neon => "A lit filament. The lightest cord and the most responsive.",
        RopeStyle.SpiderThread => "Spun silk. The thinnest cord of all, and stronger than anything its weight.",
        RopeStyle.MidnightCord => "A dark braid. Heavy, quiet, and slow to give a swing up.",
        RopeStyle.TempleThread => "Twisted cotton in saffron and vermilion. Soft, and it has some give.",
        RopeStyle.SilverCord => "Woven silver. A chain's shine with a cord's quickness.",
        _ => string.Empty,
    };
}
