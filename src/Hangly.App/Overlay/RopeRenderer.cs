//
//  RopeRenderer.cs
//  Hangly
//
//  Drawing the cord, the beads and the charms with Win2D.
//

using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.UI;

namespace Hangly.App.Overlay;

/// <summary>Draws one frame of the rope.</summary>
/// <remarks>
/// A transcription of the macOS <c>RopeStyleRenderer</c>, and deliberately so: every
/// style is drawn with ordinary strokes of the path the cord already has — a dashed pass
/// is a twist, a wider flatter pair is a braid, a heavy notch with a lit rim is a chain,
/// and three progressively wider, fainter strokes are neon's halo.
///
/// <para><b>Nothing uses a filter.</b> Win2D has a Gaussian blur effect and it would be
/// the obvious way to draw a glow. It is not used, for the same reason the original does
/// not: a blur is an off-screen pass per frame, and at 120 Hz over a window this size it
/// costs more than the entire solver. Three strokes read the same and cost nothing.</para>
///
/// <para>The drawing session is handed in rather than owned, so this class holds no
/// device resources and survives a device-lost without a rebuild.</para>
/// </remarks>
public sealed class RopeRenderer
{
    private readonly CharmArtworkCache artwork;

    public RopeRenderer(CharmArtworkCache artwork)
    {
        this.artwork = artwork;
        LiveObjects.Track("RopeRenderer", this);
    }

    /// <summary>The end caps and jump rings joining the rope to charms with a hook.</summary>
    private readonly HookConnectorRenderer connectors = new();

    /// <summary>What the charms hanging on the rope are, from the anchor down.</summary>
    /// <remarks>
    /// Set from the frame loop. Setting it lets go of the artwork of every charm that is no
    /// longer hanging; see <see cref="CharmArtworkCache.Retain"/>.
    /// </remarks>
    public IReadOnlyList<CharmDescriptor> Charms
    {
        get => charms;
        set
        {
            charms = value;
            artwork.Retain(value.Select(charm => charm.FileName));
        }
    }

    private IReadOnlyList<CharmDescriptor> charms = [];

    /// <summary>Appearance → Glow. Set from the frame loop.</summary>
    public GlowLevel Glow
    {
        get => glow;
        set => glow = value;
    }

    private GlowLevel glow = GlowLevel.Soft;

    /// <summary>
    /// Whether this frame's rope is a chain, which hangs a charm with a hook by its own last link through the charm's
    /// ring rather than by a jump ring: set as each frame starts, and read by <see cref="HangFor"/>.
    /// </summary>
    private bool chained;

    /// <summary>
    /// How far past where it meets a charm without a hook the cord goes on behind it, against the charm's radius: far
    /// enough that its end is never seen at the artwork's edge. macOS's <c>RopeCanvasView.ropeMeetsTuck</c>.
    /// </summary>
    private const double RopeMeetsTuck = 0.12;

    /// <summary>
    /// The canvas's height in points, which a charm that hangs by its own drawn rope fills
    /// down from its top. Zero — the default, for the Library's and the Studio's small
    /// canvases — hangs it to the rope's end and its own radius.
    /// </summary>
    public double CanvasHeight { get; set; }

    /// <summary>How much of the canvas below its top such a charm fills, leaving room for its shadow.</summary>
    private const double OwnCordFill = 0.96;

    public void Draw(CanvasDrawingSession session, RopeSnapshot snapshot, RopeStyle style)
    {
        if (snapshot.Points.Count < 2)
        {
            return;
        }

        session.Antialiasing = CanvasAntialiasing.Antialiased;
        chained = style.IsChain();
        RenderTimes.Mark(-1);

        // The Spider-Man entrance's web, behind the rope it hangs from.
        if (snapshot.Bloom is WebBloom bloom)
        {
            DrawWebBloom(session, bloom);
        }

        // No DPI transform here, and that is the correction to an earlier mistake worth
        // recording: a CanvasControl's drawing session is already in DIPs, so scaling it
        // again by the window's DPI drew everything twice its size. On a 200% display the
        // rope then overshot its canvas and the charm hung below the bottom edge, which
        // looked like a missing charm rather than an oversized rope.
        //
        // The window is sized in physical pixels as points × scale, so the control's DIP
        // space and the solver's point space are the same space. Nothing to convert.
        RopeAppearance appearance = RopeStyleAppearanceTable.AppearanceOf(style);
        double charmRadius = snapshot.Charms.Count > 0 ? snapshot.Charms[^1].Radius : 10;
        double width = RopeStyleAppearanceTable.WidthFor(style, charmRadius);

        // One stretch of cord per charm, from where it leaves the charm above (or the anchor) to where it reaches this one,
        // along the cord the solver swings, eased into the charm's ring or top (CordStretches).
        var placed = new HookConnectorRenderer.Placed?[snapshot.Charms.Count];
        for (int index = 0; index < snapshot.Charms.Count; index++)
        {
            placed[index] = HookConnectorRenderer.Place(index < Charms.Count ? Charms[index] : null, HangFor(snapshot, index), chained);
        }

        List<CordStretch> stretches = CordStretches(snapshot, placed, width);
        var runs = new List<List<Vec2>>(stretches.Count);
        var runStarts = new List<double>(stretches.Count);
        foreach (CordStretch stretch in stretches)
        {
            runs.Add(stretch.Points);
            runStarts.Add(stretch.Start);

            // Audit only: how far the cord below a charm starts from that charm's exit. Zero, always, by construction.
            if (PresentTimes.On && stretch.Slot > 0 && stretch.Slot - 1 < Charms.Count && Charms[stretch.Slot - 1] is { Figure: null, HangsByOwnCord: false } upper)
            {
                RenderTimes.NoteJunction((stretch.Points[0] - ExitOf(snapshot, stretch.Slot - 1, upper).Point).Magnitude);
            }
        }

        string finish = HookConnectorRenderer.FinishOf(style);
        RenderTimes.Mark(0);
        // Where a chain ends hooked through a charm's ring, so its last link is laid to rest there.
        var hookEnds = new List<Vec2>();
        if (chained)
        {
            foreach (HookConnectorRenderer.Placed? connector in placed)
            {
                if (connector is HookConnectorRenderer.Placed hooked)
                {
                    hookEnds.Add(hooked.RopeEnd);
                }
            }
        }

        DrawRopeLayer(session, snapshot, runs, runStarts, hookEnds, style, appearance, width, charmRadius);
        DrawPictureRopes(session, snapshot);
        RenderTimes.Mark(1);
        DrawBeads(session, BeadsOnCord(snapshot, stretches), appearance);
        RenderTimes.Mark(2);
        DrawCharms(session, snapshot, placed, finish, style, width);
        RenderTimes.Mark(3);
        artwork.EndFrame();
    }

    /// <summary>A straight length of cord in <paramref name="style"/>, for the Library's rope cards.</summary>
    /// <remarks>
    /// The macOS <c>RopeSwatch</c>: drawn by the rope renderer itself rather than pictured,
    /// so a card can never show a cord the overlay does not draw. Sized against a charm
    /// larger than anything that hangs — at true proportions every style is a hairline, and
    /// on a card the texture is the whole thing being chosen between.
    /// </remarks>
    public static void DrawSwatch(CanvasDrawingSession session, RopeStyle style, Vec2 top, Vec2 bottom, double charmRadius = 86)
    {
        session.Antialiasing = CanvasAntialiasing.Antialiased;
        RopeAppearance appearance = RopeStyleAppearanceTable.AppearanceOf(style);
        DrawCord(session, [top, bottom], style, appearance, RopeStyleAppearanceTable.WidthFor(style, charmRadius), charmRadius, head: false, shadow: false);
    }

    /// <summary>
    /// The cord, in the gaps the charms leave. A charm hides the cord it is drawn over,
    /// so a string of three needs four visible pieces of cord rather than one line with
    /// three discs on top of it.
    /// </summary>
    private static void DrawCord(
        CanvasDrawingSession session,
        IReadOnlyList<Vec2> run,
        RopeStyle style,
        RopeAppearance appearance,
        double width,
        double charmRadius,
        bool head,
        bool shadow = true,
        bool hooked = false,
        double alongCord = 0)
    {
        using CanvasPathBuilder builder = BuildSpline(session, run, head);
        using var path = CanvasGeometry.CreatePath(builder);

        // The glow first and underneath: three progressively wider, fainter strokes. No
        // blur, for the reason on the type.
        if (appearance.GlowStrength > 0)
        {
            for (int halo = 3; halo >= 1; halo--)
            {
                float haloWidth = (float)(width * (1 + (halo * 1.1 * appearance.GlowStrength)));
                double alpha = appearance.GlowStrength * 0.16 / halo;
                session.DrawGeometry(path, ToColor(appearance.Palette.Light, alpha), haloWidth);
            }
        }

        // A shadow under the cord, falling the same way and the same distance as the
        // charm's. A cord with no shadow over a charm that has one reads as two objects
        // lit by different suns.
        // A shadow gives the cord depth over a desktop; on a card it reads as a smudge.
        if (shadow)
        {
            DrawCordShadow(session, path, width, charmRadius);
        }

        // The rope's own picture laid along it (RopeSurface); a style without one is drawn as a lit rod.
        if (RopeSurface.Has(session, style))
        {
            RopeSurface.Draw(session, path, style, width, hooked, alongCord);
            return;
        }

        DrawCylinder(session, path, appearance, width);

        DrawTexture(session, path, appearance, width);
    }

    /// <summary>The cord as a lit rod rather than a filled line.</summary>
    /// <remarks>
    /// <b>Painted across the cord, not out from its middle.</b> Concentric strokes were
    /// tried first and were wrong in a way that is obvious once measured: they give a
    /// dark edge on <em>both</em> sides with a light middle, which is a tube lit from the
    /// front. The shipping macOS cord, read across its width on a white ground, runs
    /// 216, 166, 113, 109, 74 — light on the left edge, dark on the right, a ramp with no
    /// bright middle at all. That is a rod lit from one side, and the side is up-and-left,
    /// which is where the beads are lit from too.
    ///
    /// <para>So each pass is offset across the full width rather than nested inside the
    /// last, and the colour runs Light → Primary → Deep as it crosses. The passes are
    /// much wider than their spacing so they overlap and antialias into a ramp instead of
    /// banding — which they did at nine narrow steps on an eight-pixel cord, where each
    /// step's contribution was less than a pixel.</para>
    ///
    /// <para>Offsetting the whole path approximates offsetting along the cord's normal.
    /// It is a good approximation because the cord hangs within a few degrees of vertical
    /// almost all the time, and the error at full swing is a fraction of a point.</para>
    ///
    /// <para>Nine strokes of a path that is already built costs nothing worth measuring,
    /// and a settled rope draws none of them.</para>
    /// </remarks>
    private static void DrawCylinder(
        CanvasDrawingSession session,
        CanvasGeometry path,
        RopeAppearance appearance,
        double width)
    {
        // The silhouette first, so the ramp above it never leaves a gap at the edges.
        // Not the deep ink at full strength: measured against macOS, the darkest point
        // across its cord is 74 of 255, and Deep alone came out at 39.
        CharmColor rim = CharmColor.Interpolate(appearance.Palette.Deep, appearance.Palette.Primary, 0.3);
        session.DrawGeometry(path, ToColor(rim, 1), (float)(width * 1.1));

        // Then the cross-section, painted across the cord rather than out from its middle.
        for (int step = 0; step < CylinderSteps; step++)
        {
            double t = step / (double)(CylinderSteps - 1);

            CharmColor ink = t < 0.5
                ? CharmColor.Interpolate(appearance.Palette.Light, appearance.Palette.Primary, t * 2)
                : CharmColor.Interpolate(appearance.Palette.Primary, appearance.Palette.Deep, (t - 0.5) * 2);

            DrawOffset(
                session,
                path,
                ToColor(ink, 1),
                (float)(width * CylinderStrokeWidth),
                (float)((t - 0.5) * width * CylinderSpread),
                0);
        }
    }

    /// <summary>How many passes the cord's cross-section is walked in.</summary>
    private const int CylinderSteps = 12;

    /// <summary>Each pass's stroke width, as a fraction of the cord's. Wide, so they overlap.</summary>
    private const double CylinderStrokeWidth = 0.45;

    /// <summary>How far the passes spread across the cord, as a fraction of its width.</summary>
    private const double CylinderSpread = 0.62;

    /// <summary>The cord's own drop shadow.</summary>
    /// <remarks>
    /// <b>The same light as the charm's.</b> This used to fall down and to the right by a
    /// fraction of the cord's own width, which is two mistakes: the charm's shadow falls
    /// straight down, and a couple of points of cord cast a shadow a couple of points
    /// wide, which never cleared its own edge. It was a dark line along the cord rather
    /// than a shadow on the desktop, and the report was simply that the rope had none.
    /// Both now take their fall from the charm's radius through the one ratio, so the
    /// distance is the same for the cord and for the thing hanging on it.
    ///
    /// <para><b>Two passes, not one.</b> The wider, fainter one first and the tighter one
    /// over it, which is how macOS softens it — "two offset low-alpha passes" — and is
    /// what antialiasing can give without a blur. A blur is an off-screen pass per frame,
    /// which this renderer exists to avoid.</para>
    /// </remarks>
    private static void DrawCordShadow(
        CanvasDrawingSession session,
        CanvasGeometry path,
        double width,
        double charmRadius)
    {
        // Down and to the right, because the light is up and to the left — the same light
        // the cord's own shading is painted for, and the beads with it.
        //
        // Straight down was tried and is wrong here for a reason worth keeping: the cord
        // hangs vertical almost all the time, so a shadow directly below it lands exactly
        // on the cord and is never seen. The charm gets away with falling straight down
        // because it is a wide disc; a line cannot.
        //
        // The distance is the charm's, so one light casts both, and never less than the
        // cord is wide — below that the shadow is still hidden under its own caster.
        double distance = Math.Max(charmRadius * CharmShadowOffsetRatio, width * 1.6);
        var fall = (float)(distance * ShadowDiagonal);

        Color near = Color.FromArgb((byte)Math.Round(255 * CordShadowOpacity), 0, 0, 0);
        Color far = Color.FromArgb((byte)Math.Round(255 * CordShadowOpacity * 0.55), 0, 0, 0);

        // The wider, fainter pass first and the tighter one over it: two offset low-alpha
        // strokes are how macOS softens this, and it is what antialiasing can give
        // without a blur.
        DrawOffset(session, path, far, (float)(width * 2.1), fall * 1.5f, fall * 1.5f);
        DrawOffset(session, path, near, (float)(width * 1.35), fall, fall);
    }

    /// <summary>Strokes the path shifted, without disturbing the caller's transform.</summary>
    private static void DrawOffset(
        CanvasDrawingSession session,
        CanvasGeometry path,
        Color color,
        float strokeWidth,
        float dx,
        float dy)
    {
        System.Numerics.Matrix3x2 previous = session.Transform;
        session.Transform = System.Numerics.Matrix3x2.CreateTranslation(dx, dy) * previous;
        session.DrawGeometry(path, color, strokeWidth);
        session.Transform = previous;
    }

    /// <summary>
    /// How dark the cord's shadow is.
    /// </summary>
    /// <remarks>
    /// Lighter than the charm's 0.326. The charm's shadow falls on the desktop well clear
    /// of the artwork; the cord is a couple of points across, so its shadow lands against
    /// its own edge, and at the charm's opacity it read as a black outline rather than as
    /// depth — measured at 36 against macOS's darkest cord reading of 85.
    /// </remarks>
    private const double CordShadowOpacity = 0.16;

    /// <summary>
    /// How far a shadow falls below what casts it, as a fraction of the charm's radius.
    /// </summary>
    /// <remarks>
    /// The charm's number, stated once and used by both. `CharmArtworkCache` owns the
    /// charm's own shadow and carries the same ratio; if that one moves this must move
    /// with it, because the whole point is that one light casts both.
    /// </remarks>
    private const double CharmShadowOffsetRatio = 0.041;

    /// <summary>One axis of a 45° fall, so a diagonal offset travels the stated distance.</summary>
    private const double ShadowDiagonal = 0.7071067811865476;

    /// <summary>The pattern worked along the cord.</summary>
    /// <remarks>
    /// Skipped entirely below 1.4 points of width, where any pattern turns to mud and the
    /// only thing a second stroke adds is a slightly dirtier line. That floor is why the
    /// styles carry a <see cref="RopeAppearance.MinimumWidth"/> at all.
    /// </remarks>
    private static void DrawTexture(
        CanvasDrawingSession session,
        CanvasGeometry path,
        RopeAppearance appearance,
        double width)
    {
        if (width < 1.4 || appearance.Texture.Kind == RopeTextureKind.Smooth)
        {
            return;
        }

        RopeTexture texture = appearance.Texture;

        switch (texture.Kind)
        {
            case RopeTextureKind.Twist:
            case RopeTextureKind.Web:
            {
                // A dashed pass across the cord reads as the lit side of a twist. The web
                // is the same idea at a finer pitch and a lighter ink.
                // CustomDashStyle alone is what makes the dashes custom. CanvasDashStyle
                // has no Custom member to pair it with — setting the array is the switch.
                CanvasStrokeStyle style = Dashes((float)texture.Pitch, (float)texture.Pitch, 0, CanvasCapStyle.Flat);
                double inset = width * (1 - texture.Offset);
                session.DrawGeometry(path, ToColor(appearance.Palette.Light, 0.55), (float)inset, style);
                break;
            }

            case RopeTextureKind.Braid:
            {
                // Two offset dashed passes, wider and flatter than a twist: the over and
                // under of a flat braid.
                CanvasStrokeStyle style = Dashes((float)texture.Pitch, (float)(texture.Pitch * 0.9), 0, CanvasCapStyle.Flat);
                CanvasStrokeStyle offsetStyle = Dashes((float)texture.Pitch, (float)(texture.Pitch * 0.9), (float)texture.Pitch, CanvasCapStyle.Flat);
                double inset = width * (1 - texture.Offset);
                session.DrawGeometry(path, ToColor(appearance.Palette.Light, 0.40), (float)inset, style);
                session.DrawGeometry(path, ToColor(appearance.Palette.Secondary, 0.55), (float)inset, offsetStyle);
                break;
            }

            case RopeTextureKind.Links:
            {
                // A heavy notch with a lit rim: the gap between two links, and the light
                // catching the near edge of each.
                CanvasStrokeStyle notch = Dashes((float)(texture.Pitch * texture.Thickness), (float)texture.Pitch, 0, CanvasCapStyle.Round);
                session.DrawGeometry(path, ToColor(appearance.Palette.Deep, 0.85), (float)(width * 1.05), notch);
                session.DrawGeometry(path, ToColor(appearance.Palette.Light, 0.60), (float)(width * 0.45), notch);
                break;
            }

            case RopeTextureKind.Smooth:
            default:
                break;
        }
    }

    /// <summary>
    /// The quadratic spline through the node midpoints, with each node as a control
    /// point — the same curve the solver threads its beads on.
    /// </summary>
    /// <param name="head">
    /// Whether to carry the cord up to the top of the canvas before the first point.
    /// </param>
    /// <summary>A dash pattern, made once and kept: the cord's texture asks for the same few on every frame.</summary>
    /// <remarks>
    /// They were made new on every frame and never disposed — a native Direct2D object each, three a frame on a braided
    /// cord, left for the finaliser while the rope swung.
    /// </remarks>
    private static CanvasStrokeStyle Dashes(float dash, float gap, float offset, CanvasCapStyle cap)
    {
        var key = (dash, gap, offset, cap);
        if (!strokeStyles.TryGetValue(key, out CanvasStrokeStyle? style))
        {
            style = new CanvasStrokeStyle { CustomDashStyle = [dash, gap], DashOffset = offset, DashCap = cap };
            strokeStyles[key] = style;
        }

        return style;
    }

    // Stroke styles belong to no device, so one set serves every overlay the app builds; only its loop thread draws.
    private static readonly Dictionary<(float Dash, float Gap, float Offset, CanvasCapStyle Cap), CanvasStrokeStyle> strokeStyles = [];

    private static CanvasPathBuilder BuildSpline(
        CanvasDrawingSession session,
        IReadOnlyList<Vec2> points,
        bool head)
    {
        var builder = new CanvasPathBuilder(session);

        // The cord starts at the top of the canvas rather than at the anchor. The anchor
        // sits a hundredth of the canvas down — `RopeConfiguration.Layout.AnchorFraction`
        // — which on macOS is hidden behind the menu bar the overlay hangs under. Windows
        // has nothing up there, so that hundredth read as a rope beginning in mid-air a
        // few pixels below the edge of the screen.
        //
        // Drawn rather than simulated: the anchor is a fixed point and the cord above it
        // cannot move, so this is a straight line up to the edge and no physics changes.
        //
        // Only for the piece that starts at the anchor. Every other piece starts under a
        // charm somewhere down the rope, and giving those a head drew a cord from the top
        // of the screen down to each of them — one rope per charm, which is exactly what
        // it looked like.
        // Prepended rather than drawn as its own line, so the anchor becomes a control
        // point of the spline and the cord curves into it instead of meeting it at a
        // corner. The point itself is directly above the anchor and never moves.
        IReadOnlyList<Vec2> path = head && points[0].Y > 0
            ? Headed(points)
            : points;

        builder.BeginFigure(ToVector(path[0]));

        for (int index = 1; index < path.Count - 1; index++)
        {
            Vec2 control = path[index];
            Vec2 finish = (path[index] + path[index + 1]) * 0.5;
            builder.AddQuadraticBezier(ToVector(control), ToVector(finish));
        }

        builder.AddLine(ToVector(path[^1]));
        builder.EndFigure(CanvasFigureLoop.Open);
        return builder;
    }

    /// <summary>One charm's stretch of drawn cord: from <see cref="Start"/> along the cord, above the charm in place
    /// <see cref="Slot"/>; beads are threaded on its first <see cref="Threaded"/> points, never on along the cord behind
    /// the charm.</summary>
    private readonly record struct CordStretch(int Slot, double Start, List<Vec2> Points, int Threaded);

    private readonly RopeCurve cordCurve = new();

    /// <summary>
    /// Each charm's stretch of cord as drawn, one per charm, built along the cord the solver swings: from where the cord
    /// comes out of the charm above (or the anchor) to where it reaches this one, then eased into its ring — or into its
    /// top on its centre line and on behind it. macOS's <c>RopeCanvasView.cordStretches</c>.
    /// </summary>
    /// <remarks>
    /// <b>Why per charm, and not cut out of one line.</b> The cord used to be the solver's polyline, cut wherever it
    /// passed through any charm's covered circle, and the pieces were then matched back to charms by position: a figure's
    /// piece dropped by its index, a ring's piece found as "the run ending nearest it". With two or three charms those
    /// circles — a ring's reaches a blend's length, 1.4 radii, above it — swallowed the whole cord between two charms, so
    /// the lower one hung with no cord at all; and the matching then picked the wrong pieces, so a cord could run past a
    /// charm to the one below while another was eased into its ring: two cords (Sharan, 6 Oct). Built per charm from the
    /// solver's own spans (<see cref="CharmPlacement.CordEntry"/>, <see cref="CharmPlacement.CordExit"/>), every charm has
    /// exactly one stretch, and nothing has to be matched.
    /// </remarks>
    private List<CordStretch> CordStretches(RopeSnapshot snapshot, IReadOnlyList<HookConnectorRenderer.Placed?> placed, double width)
    {
        var stretches = new List<CordStretch>(snapshot.Charms.Count + 1);
        if (snapshot.Points.Count < 2)
        {
            return stretches;
        }

        if (snapshot.Charms.Count == 0)
        {
            // Nothing hangs: the whole cord.
            cordCurve.Rebuild(snapshot.Points, snapshot.Points[^1]);
            List<Vec2> whole = cordCurve.Polyline(0, cordCurve.Length);
            stretches.Add(new CordStretch(-1, 0, whole, whole.Count));
            return stretches;
        }

        cordCurve.Rebuild(snapshot.Points, snapshot.Charms[^1].Center);
        double start = 0;
        Exit? above = null;
        for (int slot = 0; slot < snapshot.Charms.Count; slot++)
        {
            CharmPlacement charm = snapshot.Charms[slot];
            CharmDescriptor? descriptor = slot < Charms.Count ? Charms[slot] : null;
            double entry = Math.Clamp(charm.CordEntry, 0, cordCurve.Length);

            // A charm that hangs by the rope in its own artwork has no cord of ours, and neither does a figure from such
            // a picture: its own rope is drawn along this stretch instead (DrawPictureRopes).
            bool ownCord = descriptor is { HangsByOwnCord: true } || descriptor?.Figure is not null;
            if ((entry > start || above is not null) && !ownCord)
            {
                entry = Math.Max(start, entry);
                List<Vec2> points;
                int threaded;
                if (placed[slot] is HookConnectorRenderer.Placed connector)
                {
                    // On a charm with a hook the rope ends inside the connector's cap, above the ring.
                    points = Eased(start, above, entry, charm, connector.RopeEnd, connector.RopeDirection, width);
                    threaded = points.Count;
                }
                else if (descriptor is not null)
                {
                    // On one without, it ends at the top of the charm on its own centre line, and goes on behind it from
                    // there. Stopped where it first touched the charm's outline, it ended at the charm's side whenever the
                    // charm did not hang along it — the nazar's, held up beside the anchor (Sharan, 5 Oct).
                    var axis = new Vec2(Math.Cos(charm.Angle), Math.Sin(charm.Angle));
                    Vec2 top = charm.Center - (axis * (charm.Radius * AttachmentInset(descriptor, charm)));
                    points = Eased(start, above, entry, charm, top, axis, width);
                    threaded = points.Count;
                    if (CordReach(charm, slot) is Vec2 reach)
                    {
                        points.Add(reach);
                    }
                }
                else
                {
                    points = cordCurve.Polyline(start, entry);
                    threaded = points.Count;
                }

                if (points.Count >= 2)
                {
                    stretches.Add(new CordStretch(slot, start, points, threaded));
                }
            }

            start = Math.Max(start, charm.CordExit);
            above = ownCord || descriptor is null ? null : ExitOf(snapshot, slot, descriptor);
        }

        CheckOwnership(snapshot, stretches);
        return stretches;
    }

    /// <summary>
    /// Every charm our cord hangs has exactly one stretch, and no stretch belongs to two charms or to none. Asserted in
    /// debug builds, and counted for the frame audit (HANGLY_AUDIT_FRAMES) in any build.
    /// </summary>
    private void CheckOwnership(RopeSnapshot snapshot, List<CordStretch> stretches)
    {
        if (!PresentTimes.On && !System.Diagnostics.Debugger.IsAttached)
        {
            return;
        }

        int expected = 0, missing = 0, duplicates = 0, orphans = 0;
        for (int slot = 0; slot < snapshot.Charms.Count; slot++)
        {
            bool ownCord = slot < Charms.Count && (Charms[slot].HangsByOwnCord || Charms[slot].Figure is not null);
            int owned = 0;
            foreach (CordStretch stretch in stretches)
            {
                owned += stretch.Slot == slot ? 1 : 0;
            }

            if (ownCord)
            {
                orphans += owned;
                continue;
            }

            expected++;
            missing += owned == 0 ? 1 : 0;
            duplicates += owned > 1 ? owned - 1 : 0;
        }

        foreach (CordStretch stretch in stretches)
        {
            orphans += stretch.Slot < 0 || stretch.Slot >= snapshot.Charms.Count ? 1 : 0;
        }

        System.Diagnostics.Debug.Assert(missing == 0 && duplicates == 0 && orphans == 0,
            $"rope ownership: {expected} charms, {stretches.Count} ropes, {missing} without one, {duplicates} duplicated, {orphans} orphaned");
        RenderTimes.NoteOwnership(expected, stretches.Count, missing, duplicates, orphans);
    }

    /// <summary>Where the cord comes out from behind a charm to the charm below it: on the charm's own centre line as it is
    /// drawn, tucked just inside its ink there (<see cref="CharmArtworkRegions.RopeLeavesOffset"/>), leaving down that
    /// line. macOS's <c>RopeCanvasView.Exit</c>.</summary>
    private readonly record struct Exit(Vec2 Point, Vec2 Leaving, Vec2 Center, double Radius);

    /// <remarks>
    /// Worked out from the very placement the charm is drawn with (<see cref="HangFor"/>). The cord below a charm used to
    /// start where the solver's cord crossed the charm's bounding circle — round, where the artwork is not, and swinging
    /// with the solver's cord while the charm is drawn turned to hang from its ring — so on a swinging rope of two or
    /// three it began in mid-air beside the charm: the bell's cord left of the bell, the nimbu-mirchi's below nothing
    /// (Sharan, 6 Oct).
    /// </remarks>
    private Exit ExitOf(RopeSnapshot snapshot, int slot, CharmDescriptor descriptor)
    {
        CharmHang hang = HangFor(snapshot, slot);
        double down = hang.Rotation + (Math.PI / 2);
        var direction = new Vec2(Math.Cos(down), Math.Sin(down));
        // Turned with the charm as it is drawn: across along its width, down along its axis.
        var across = new Vec2(Math.Cos(hang.Rotation), Math.Sin(hang.Rotation));
        Vec2 offset = descriptor.RopeLeavesOffset ?? new Vec2(0, 0);
        Vec2 point = hang.Center + (across * (hang.Radius * offset.X)) + (direction * (hang.Radius * offset.Y));
        return new Exit(point, direction, hang.Center, hang.Radius);
    }

    /// <summary>
    /// The cord from <paramref name="start"/> to where it leaves for the charm — a blend's length above
    /// <paramref name="end"/>, where it is first seen to reach that far (not a curl of it hidden behind the charm) — and
    /// eased from there into <paramref name="end"/>, arriving along <paramref name="arriving"/>. macOS's <c>eased</c>.
    /// </summary>
    /// <remarks>Below another charm it starts instead at that charm's <see cref="Exit"/>: out along the charm's own line
    /// and onto the cord a blend's length from it, or — two charms too close for that — straight on into this one.</remarks>
    private List<Vec2> Eased(double start, Exit? above, double entry, CharmPlacement charm, Vec2 end, Vec2 arriving, double width)
    {
        double reach = (end - charm.Center).Magnitude + HookConnectorRenderer.BlendLength(charm.Radius, width);
        double leave = cordCurve.ArcFirstEnteringCircle(charm.Center, reach, start) ?? cordCurve.ArcEnteringCircle(charm.Center, reach);

        // Always from the cord's own point: held up at the anchor, the cord above the charm is all hidden behind it, and
        // there is no stretch left to draw before it.
        double leaveArc = Math.Max(start, Math.Min(entry, leave));
        List<Vec2> points;
        if (above is Exit exit)
        {
            // Onto the cord where it is first clear of the charm above by a blend's length.
            double clear = (exit.Point - exit.Center).Magnitude + HookConnectorRenderer.BlendLength(exit.Radius, width);
            if (cordCurve.ArcFirstLeavingCircle(exit.Center, clear, start) is not double join || join >= leaveArc)
            {
                points = [exit.Point];
                HookConnectorRenderer.AddBlend(points, exit.Leaving, end, arriving);
                return points;
            }

            double joinAngle = cordCurve.AngleAtArc(join);
            points = [exit.Point];
            HookConnectorRenderer.AddBlend(points, exit.Leaving, cordCurve.PointAtArc(join), new Vec2(Math.Cos(joinAngle), Math.Sin(joinAngle)));
            List<Vec2> along = cordCurve.Polyline(join, leaveArc);
            for (int index = 1; index < along.Count; index++)
            {
                points.Add(along[index]);
            }
        }
        else
        {
            points = cordCurve.Polyline(start, leaveArc);
        }

        Vec2 from = cordCurve.PointAtArc(leaveArc);
        if (points.Count == 0 || (points[^1] - from).Magnitude > 0.01)
        {
            points.Add(from);
        }

        double angle = cordCurve.AngleAtArc(Math.Max(start, leaveArc - 0.5));
        HookConnectorRenderer.AddBlend(points, new Vec2(Math.Cos(angle), Math.Sin(angle)), end, arriving);
        return points;
    }

    /// <summary>
    /// The beads, each laid on its charm's stretch of cord as drawn, at its own distance along the cord. The solver
    /// threads them on the cord it swings, which runs to the charm's centre; the cord drawn leaves that line a blend's
    /// length above the charm to ease into its ring or top, so a bead near the charm was left on a stretch nobody could
    /// see — in mid-air beside the bell, the Vel, Karuppu (Sharan, 5 Oct). macOS's <c>RopeCanvasView.beadPlacements</c>.
    /// </summary>
    private static List<BeadPlacement> BeadsOnCord(RopeSnapshot snapshot, List<CordStretch> stretches)
    {
        var beads = new List<BeadPlacement>(snapshot.Beads.Count);
        foreach (BeadPlacement bead in snapshot.Beads)
        {
            CordStretch? owned = null;
            foreach (CordStretch stretch in stretches)
            {
                if (stretch.Slot == bead.Owner)
                {
                    owned = stretch;
                    break;
                }
            }

            if (bead.Arc is not double arc || owned is not CordStretch on || on.Threaded < 2)
            {
                beads.Add(bead);
                continue;
            }

            (Vec2 position, double angle) = Along(on.Points, 0, on.Threaded - 1, Math.Max(0, arc - on.Start), bead.Size.Height / 2);
            beads.Add(bead with { Position = position, Angle = angle });
        }

        return beads;
    }

    /// <summary>The point <paramref name="distance"/> along a run from its point <paramref name="from"/>, never closer than
    /// <paramref name="margin"/> to its point <paramref name="to"/>, and the run's direction there.</summary>
    private static (Vec2 Position, double Angle) Along(List<Vec2> run, int from, int to, double distance, double margin)
    {
        double total = 0;
        for (int index = from + 1; index <= to; index++)
        {
            total += (run[index] - run[index - 1]).Magnitude;
        }

        double left = Math.Max(0, Math.Min(distance, total - margin));
        for (int index = from + 1; index <= to; index++)
        {
            Vec2 delta = run[index] - run[index - 1];
            double span = delta.Magnitude;
            if (span <= Precision.UlpOfOne)
            {
                continue;
            }

            if (left <= span || index == to)
            {
                return (run[index - 1] + (delta * Math.Min(1, left / span)), Math.Atan2(delta.Y, delta.X));
            }

            left -= span;
        }

        return (run[to], Math.PI / 2);
    }

    /// <summary>
    /// The cord on past the knot, for a charm whose cord meets its artwork lower than it
    /// can be hung from — straight down the charm's axis and behind it, so it shows only
    /// through what the artwork leaves open: between a Snitch's wings, to the ball. macOS's
    /// <c>RopeCanvasView.cordReach</c>.
    /// </summary>
    /// <remarks>
    /// On every charm without a hook, down its centre line to where that line first meets the artwork and a little past,
    /// out of sight (<see cref="RopeMeetsTuck"/>) — or further, where the cord meets the artwork past that, between a
    /// Snitch's wings to the ball. Stopped where the knot is measured, it ended in mid-air above any charm whose top is
    /// off its centre line — over Ronaldo's back, the lingam's cobra (Sharan, 4 Oct); carried on to the centre, it showed
    /// down the gap between Stormbreaker's blades. macOS's <c>cordReach</c>.
    /// </remarks>
    /// <summary>
    /// Where the cord meets a charm with no hook, as a fraction of the radius back from its centre: at its knot — unless
    /// the first ink on its centre line is a sliver that ends above the knot, the tip of an ear the line just clips, when
    /// it is in that ink. Carried on to the knot, the cord's end hung in the clear beside Mewtwo's ear (Sharan, 5 Oct).
    /// macOS's <c>RopeCanvasView.attachmentInset</c>.
    /// </summary>
    private static double AttachmentInset(CharmDescriptor charm, CharmPlacement placement) =>
        charm.RopeMeetsInset is double meets && charm.RopeMeetsDepth is double depth && meets - depth > placement.KnotInset
            ? meets - (depth / 2)
            : placement.KnotInset;

    private Vec2? CordReach(CharmPlacement placement, int slot)
    {
        // A charm with a hook is tied to it; one hung by its own drawn rope, or a figure of a picture, has none of ours.
        if (slot >= charms.Count || charms[slot] is not { Connector: null, HangsByOwnCord: false, Figure: null } charm)
        {
            return null;
        }

        double own = charm.CordInset ?? placement.KnotInset;
        // Tucked no deeper than the ink there goes: through a thin ear or a halo's ring, the full tuck left the cord's end
        // in the clear past it (Mewtwo, the praying angel; Sharan, 5 Oct).
        double tuck = Math.Min(RopeMeetsTuck, (charm.RopeMeetsDepth ?? RopeMeetsTuck) * 0.5);
        double inset = charm.RopeMeetsInset is double meets ? Math.Min(own, meets - tuck) : own;
        if (inset >= placement.KnotInset)
        {
            return null;
        }

        var direction = new Vec2(Math.Cos(placement.Angle), Math.Sin(placement.Angle));
        return placement.Center - (direction * (placement.Radius * inset));
    }

    /// <summary>
    /// The cord in a layer of its own, with every charm's silhouette taken out of it before
    /// it is laid down: not one rope pixel shows through a charm or rings its edge, and it
    /// is seen only where a charm is open — through a hook, between a Snitch's wings.
    /// </summary>
    /// <remarks>
    /// The layer covers the cord and nothing more, so it costs what the cord does rather
    /// than a full-canvas pass.
    /// </remarks>
    private void DrawRopeLayer(
        CanvasDrawingSession session,
        RopeSnapshot snapshot,
        List<List<Vec2>> runs,
        List<double> runStarts,
        List<Vec2> hookEnds,
        RopeStyle style,
        RopeAppearance appearance,
        double width,
        double charmRadius)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (List<Vec2> run in runs)
        {
            foreach (Vec2 point in run)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        if (minX > maxX)
        {
            return;
        }

        // Room for the cord's glow and shadow either side of its line.
        double margin = (width * 6) + 4;
        var origin = new Vec2(Math.Floor(minX - margin), Math.Floor(minY - margin));
        float layerWidth = (float)Math.Ceiling(maxX + margin - origin.X);
        float layerHeight = (float)Math.Ceiling(maxY + margin - origin.Y);

        // A Direct2D layer of the frame's own session, not a render target of its own. The cord was drawn into a separate
        // GPU surface and that surface's session closed mid-frame, which made the CPU wait for the GPU to finish it before
        // the frame could go on: on the VM those waits were the frame audit's spikes, up to 138 ms in "close" (Sharan,
        // 6 Oct). A layer is composited by Direct2D within the one session, and its content stays its own, so the charms'
        // silhouettes taken out of it with DestinationOut still take out only the cord.
        var bounds = new Windows.Foundation.Rect(origin.X, origin.Y, layerWidth, layerHeight);
        RenderTimes.Part(0);
        using (session.CreateLayer(1f, bounds))
        {
            for (int index = 0; index < runs.Count; index++)
            {
                Vec2 last = runs[index][^1];
                bool hooked = hookEnds.Exists(end => (end - last).Magnitude < 0.5);
                DrawCord(session, runs[index], style, appearance, width, charmRadius, head: index == 0, hooked: hooked, alongCord: runStarts[index]);
            }

            RenderTimes.Part(1);

            for (int index = 0; index < snapshot.Charms.Count && index < Charms.Count; index++)
            {
                artwork.EraseBehind(session, Charms[index], HangFor(snapshot, index));
            }

            RenderTimes.Part(2);
        }

        RenderTimes.Part(3);
        RenderTimes.Part(4);
    }


    // A picture's own rope

    /// <summary>
    /// Each figure's own rope, from where the cord above it starts down to where the figure takes hold of it: the stretch
    /// of the artwork cut into bands a few pixels tall, each centred on the rope's line within it and laid on the cord at
    /// its place, turned to the cord there. Short bands follow any bend. macOS's <c>PictureRopeRenderer</c>.
    /// </summary>
    /// <remarks>
    /// <b>Always at the figures' own scale, never squeezed or stretched</b>, and always reaching all the way: laid from the
    /// figure upward, so its end — the knots at Gwen, the strand into Spider-Man's hand — is where the artwork has it.
    /// When the cord is shorter than the picture's rope (a large charm size) the top of it does not show; when it is
    /// longer (a small size, or Elastic pulling) the plain stretch of the same rope repeats above it.
    /// </remarks>
    private void DrawPictureRopes(CanvasDrawingSession session, RopeSnapshot snapshot)
    {
        if (snapshot.Charms.Count == 0 || !Charms.Any(charm => charm.Figure is not null))
        {
            return;
        }

        pictureCurve.Rebuild(snapshot.Points, snapshot.Charms[^1].Center);
        System.Numerics.Matrix3x2 previous = session.Transform;
        for (int slot = 0; slot < snapshot.Charms.Count && slot < Charms.Count; slot++)
        {
            if (Charms[slot].Figure is not PictureFigure figure || figure.HalfPixels <= 0 || Strip(Charms[slot], figure) is not { } strip)
            {
                continue;
            }

            CharmPlacement placement = snapshot.Charms[slot];
            double cordStart = slot == 0 ? 0 : snapshot.Charms[slot - 1].CordExit;
            double cordEnd = Math.Clamp(placement.CordEntry, 0, pictureCurve.Length);
            if (cordEnd <= cordStart + 0.5)
            {
                continue;
            }

            // Tied at both ends to the figures as drawn: from the hand of the figure above (or the anchor) to this one's
            // hand or knot, on each figure's own axis. Ending where the cord crosses the figure's outline left a gap
            // under Spider-Man's lower hand whenever the rope bent at him.
            List<Vec2> points = pictureCurve.Polyline(cordStart, cordEnd);
            points.Add(Attachment(placement, top: true));
            if (slot > 0 && Charms[slot - 1].Figure is not null && points.Count > 0)
            {
                points[0] = Attachment(snapshot.Charms[slot - 1], top: false);
            }

            picturePath.Rebuild(points);
            double start = 0, end = picturePath.Length;
            double scale = placement.Radius / figure.HalfPixels;

            // The rope as drawn, its bottom at the figure, at the figure's scale.
            foreach (PictureBand band in strip.Bands)
            {
                double middle = end - ((strip.Length - (band.Top + (band.Height / 2))) * scale);
                if (middle + (band.Height * scale / 2) > start)
                {
                    PlaceBand(session, strip, band, middle, scale, previous);
                }
            }

            // Plain rope above it, as far up as the cord goes.
            double repeat = (strip.PlainBottom - strip.PlainTop) * scale;
            if (repeat <= 1)
            {
                continue;
            }

            for (double bottom = end - (strip.Length * scale); bottom > start; bottom -= repeat)
            {
                foreach (PictureBand band in strip.Bands)
                {
                    if (band.Top < strip.PlainTop || band.Top > strip.PlainBottom)
                    {
                        continue;
                    }

                    double middle = bottom - ((strip.PlainBottom - (band.Top + (band.Height / 2))) * scale);
                    if (middle + (band.Height * scale / 2) > start)
                    {
                        PlaceBand(session, strip, band, middle, scale, previous);
                    }
                }
            }
        }

        session.Transform = previous;
    }

    /// <summary>Where the rope meets a figure as drawn: on its axis, its knot inset from its centre, up or down.</summary>
    private static Vec2 Attachment(CharmPlacement placement, bool top)
    {
        var axis = new Vec2(Math.Cos(placement.Angle), Math.Sin(placement.Angle));
        double reach = placement.Radius * placement.KnotInset;
        return placement.Center + (axis * (top ? -reach : reach));
    }

    private readonly PolylinePath picturePath = new();

    /// <summary>A path the rope is laid along: the cord's line, ending exactly at the hands it is tied to.</summary>
    private sealed class PolylinePath
    {
        private readonly List<Vec2> points = [];
        private readonly List<double> cumulative = [];

        public double Length => cumulative.Count > 0 ? cumulative[^1] : 0;

        public void Rebuild(IReadOnlyList<Vec2> source)
        {
            points.Clear();
            cumulative.Clear();
            foreach (Vec2 point in source)
            {
                if (points.Count > 0 && (point - points[^1]).Magnitude <= 0.01)
                {
                    continue;
                }

                cumulative.Add(points.Count == 0 ? 0 : cumulative[^1] + (point - points[^1]).Magnitude);
                points.Add(point);
            }
        }

        private int Segment(double arc)
        {
            int low = 0, high = points.Count - 1;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (cumulative[mid] <= arc)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        public Vec2 PointAtArc(double arc)
        {
            if (points.Count < 2)
            {
                return points.Count == 1 ? points[0] : default;
            }

            arc = Math.Clamp(arc, 0, Length);
            int index = Segment(arc);
            double span = cumulative[index + 1] - cumulative[index];
            double t = span > 0 ? (arc - cumulative[index]) / span : 0;
            return points[index] + ((points[index + 1] - points[index]) * t);
        }

        public double AngleAtArc(double arc)
        {
            if (points.Count < 2)
            {
                return Math.PI / 2;
            }

            int index = Segment(Math.Clamp(arc, 0, Length));
            Vec2 delta = points[index + 1] - points[index];
            return Math.Atan2(delta.Y, delta.X);
        }
    }

    private void PlaceBand(
        CanvasDrawingSession session, PictureStrip strip, PictureBand band, double middle, double scale, System.Numerics.Matrix3x2 previous)
    {
        Vec2 point = picturePath.PointAtArc(middle);
        double angle = picturePath.AngleAtArc(middle) - (Math.PI / 2);
        double height = (band.Height * scale) + 0.6;
        session.Transform = System.Numerics.Matrix3x2.CreateRotation((float)angle)
            * System.Numerics.Matrix3x2.CreateTranslation((float)point.X, (float)point.Y) * previous;
        session.DrawImage(
            strip.Bitmap,
            new Windows.Foundation.Rect(-band.Centre * scale, -height / 2, strip.Width * scale, height),
            band.Source);
    }

    private readonly RopeCurve pictureCurve = new();
    private readonly Dictionary<(string File, Rect Rope), PictureStrip?> pictureStrips = [];

    private sealed record PictureBand(Windows.Foundation.Rect Source, double Top, double Height, double Centre);

    /// <param name="Length">The stretch's height, and <paramref name="Width"/> its width, in artwork pixels.</param>
    /// <param name="PlainTop">The plain stretch's first band, and <paramref name="PlainBottom"/> its last, in rows of the strip.</param>
    private sealed record PictureStrip(
        CanvasBitmap Bitmap, IReadOnlyList<PictureBand> Bands, double Length, double Width, double PlainTop, double PlainBottom);

    private const double PictureBandHeight = 6;
    private const double PicturePixelsPerArtwork = 2;

    /// <summary>The figure's stretch of rope cut into bands, made once and kept.</summary>
    private PictureStrip? Strip(CharmDescriptor charm, PictureFigure figure)
    {
        var key = (charm.FileName, figure.RopeAbove);
        if (pictureStrips.TryGetValue(key, out PictureStrip? kept))
        {
            return kept;
        }

        double unit = figure.LongestPixels * PicturePixelsPerArtwork;
        CanvasBitmap? bitmap = artwork.RasterColumn(charm.FileName, unit, figure.RopeAbove);
        PictureStrip? strip = null;
        if (bitmap is not null)
        {
            int width = (int)bitmap.SizeInPixels.Width, height = (int)bitmap.SizeInPixels.Height;
            byte[] pixels = bitmap.GetPixelBytes();
            var bands = new List<PictureBand>();
            var centroids = new List<(double Middle, double Centre)>();
            double length = height / PicturePixelsPerArtwork;
            for (double top = 0; top < length; top += PictureBandHeight)
            {
                double bandHeight = Math.Min(PictureBandHeight, length - top);
                int first = (int)Math.Round(top * PicturePixelsPerArtwork);
                int last = Math.Min(height, (int)Math.Round((top + bandHeight) * PicturePixelsPerArtwork));

                // The rope's middle in this band, alpha-weighted, for fitting the line the whole stretch runs down.
                double sum = 0, weight = 0;
                for (int row = first; row < last; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        byte alpha = pixels[(((row * width) + column) * 4) + 3];
                        if (alpha > 30)
                        {
                            sum += column * (double)alpha;
                            weight += alpha;
                        }
                    }
                }

                if (weight > 0)
                {
                    centroids.Add((top + (bandHeight / 2), sum / weight / PicturePixelsPerArtwork));
                }

                bands.Add(new PictureBand(
                    new Windows.Foundation.Rect(0, first, width, Math.Max(1, last - first)), top, bandHeight, width / 2.0 / PicturePixelsPerArtwork));
            }

            // Every band against the one line the rope runs down, so its folds and knots stand out as the artwork has
            // them (RopeLine).
            var line = new RopeLine(centroids);
            for (int index = 0; index < bands.Count; index++)
            {
                PictureBand band = bands[index];
                bands[index] = band with { Centre = line.CentreAt(band.Top + (band.Height / 2)) ?? band.Centre };
            }

            // A copy of its own: the artwork cache lets go of rasters it has not been asked for in a while.
            CanvasBitmap own = CanvasBitmap.CreateFromBytes(
                bitmap.Device, pixels, width, height, bitmap.Format, bitmap.Dpi, bitmap.AlphaMode);
            double plainTop = (figure.PlainAbove.Top - figure.RopeAbove.Top) * figure.LongestPixels;
            double plainBottom = Math.Min(length, plainTop + (figure.PlainAbove.Height * figure.LongestPixels)) - PictureBandHeight;
            strip = new PictureStrip(own, bands, length, width / PicturePixelsPerArtwork, plainTop, Math.Max(plainTop, plainBottom));
        }

        pictureStrips[key] = strip;
        return strip;
    }


    /// <summary>
    /// Where a charm is drawn: at its place on the rope, turned with the cord that meets it
    /// — or, for a charm that hangs by its own drawn rope, from where our cord would have
    /// started, down past its place by its radius, as macOS's <c>RopeCanvasView.hang</c>.
    /// </summary>
    /// <summary>The turn that puts a charm's ring on its own hanging line, so the cord runs straight into it. macOS's
    /// <c>pivoted</c>.</summary>
    /// <remarks>A fixed turn, measured from the artwork — the ring's offset from the charm's centre line, no more. It once
    /// turned the charm to face wherever the cord was, and a cord looping back in a fast drag swung it upside down.</remarks>
    private double Pivoted(RopeSnapshot snapshot, CharmPlacement placement, CharmHang hang, HookConnector connector, Rect body)
    {
        // Turned about where the charm bears on what holds it: a chain's last link on the eye, or the jump ring's lower
        // wire in it. Turned about the ring's top instead, a ring off the eye's line left the charm 2–3 points off the
        // cord (the camera; Sharan, 5 Oct).
        Vec2 end = connector.ContactFor(chained);
        double x = end.X - body.MidX, y = end.Y - body.MidY;
        // Turned back against the ring's offset: added, the turn doubled it, and at rest the rope veered to reach it.
        return y < 0 ? hang.Rotation - Math.Atan2(x, -y) : hang.Rotation;
    }

    private CharmHang HangFor(RopeSnapshot snapshot, int slot)
    {
        CharmPlacement placement = snapshot.Charms[slot];
        var resting = new CharmHang(placement.Center, placement.Radius, placement.Angle - (Math.PI / 2));

        // A charm hung by a ring turns on it until the ring points straight up the cord: its ring is not always on the
        // charm's centre line, and drawn square to the cord's last link the cord folded at its end to reach the ring.
        if (slot < Charms.Count && Charms[slot] is { Connector: HookConnector connector, HangsByOwnCord: false } hooked)
        {
            resting = resting with { Rotation = Pivoted(snapshot, placement, resting, connector, hooked.Body) };
        }

        if (slot >= Charms.Count || !Charms[slot].HangsByOwnCord || Charms[slot].Body.Height <= 0)
        {
            return resting;
        }

        Rect body = Charms[slot].Body;
        Vec2 top = slot == 0 ? snapshot.Points[0] : snapshot.Charms[slot - 1].Center;
        Vec2 reach = placement.Center - top;
        double distance = reach.Magnitude;
        if (distance <= 1)
        {
            return resting;
        }

        Vec2 direction = reach / distance;

        // Down as far as the canvas allows: it is one tall picture, five times taller than
        // wide, so stopping at the rope's end left both figures a few dozen points across.
        double height = Math.Max(distance + placement.Radius, (CanvasHeight - top.Y) * OwnCordFill);
        double longest = Math.Max(body.Width, body.Height);
        return new CharmHang(
            top + (direction * (height / 2)),
            height / 2 * longest / body.Height,
            Math.Atan2(direction.Y, direction.X) - (Math.PI / 2));
    }

    /// <summary>The cord's nodes with the point it leaves the screen by in front.</summary>
    /// <remarks>
    /// <b>Directly above the anchor, and fixed there.</b> The anchor sits a hundredth of
    /// the canvas down — <c>RopeConfiguration.Layout.AnchorFraction</c> — which on macOS
    /// is hidden behind the menu bar the overlay hangs under. Windows has nothing up
    /// there, so that hundredth read as a rope beginning in mid-air a few pixels below the
    /// edge of the screen. This closes it.
    ///
    /// <para><b>Why it does not follow the rope.</b> It did, briefly: the head was
    /// extended along the first segment's own direction so the join would be collinear and
    /// show no fold. But the first segment swings, and a head that follows it slides along
    /// the top edge every frame — the cord's end wandering across the screen instead of
    /// staying where it is pinned. Worse near horizontal, where the distance to the edge
    /// divided by a vanishing vertical component sends it a long way sideways and then
    /// snaps it back when the cap catches it.</para>
    ///
    /// <para>The anchor is a fixed point, so what is above it is a fixed point too. The
    /// fold that motivated following the rope is gone anyway: this is prepended to the
    /// spline rather than drawn as a straight line to the anchor, which makes the anchor a
    /// control point and turns the corner into a curve.</para>
    /// </remarks>
    private static IReadOnlyList<Vec2> Headed(IReadOnlyList<Vec2> points)
    {
        var path = new List<Vec2>(points.Count + 1) { new(points[0].X, 0) };
        path.AddRange(points);
        return path;
    }

    /// <summary>The beads on the cord, drawn from the artwork they were measured in.</summary>
    /// <remarks>
    /// Each bead is a region of its charm's own SVG. The splitter already measured those
    /// rectangles — it is how the solver knows a bead's size, place and weight — and this
    /// used to throw them away and fill an ellipse instead, which is why a Nazar's three
    /// gold beads arrived as one flat oval: they touch, so they are one measured run, and
    /// only the artwork knows there are three of them in it.
    ///
    /// <para>The ellipse survives as the fallback for a charm whose artwork could not be
    /// split — an import with no measured beads, or an asset that does not match what the
    /// catalogue claims. Those have no bead artwork to draw, and a drawn bead is better
    /// than a gap in the cord.</para>
    /// </remarks>
    private void DrawBeads(CanvasDrawingSession session, IReadOnlyList<BeadPlacement> beads, RopeAppearance appearance)
    {
        // Beads arrive grouped by the charm that threads them, in the order that charm's
        // regions were measured, so the run of each owner counts its own way through.
        int ordinal = 0;
        int owner = -1;

        foreach (BeadPlacement bead in beads)
        {
            if (bead.Owner != owner)
            {
                owner = bead.Owner;
                ordinal = 0;
            }

            CharmDescriptor? charm = bead.Owner >= 0 && bead.Owner < Charms.Count
                ? Charms[bead.Owner]
                : null;

            if (charm is not null && ordinal < charm.BeadRegions.Count)
            {
                artwork.DrawBead(session, charm, bead, charm.BeadRegions[ordinal]);
            }
            else
            {
                DrawPlainBead(session, appearance, bead);
            }

            ordinal++;
        }
    }

    /// <summary>A bead for a charm whose artwork could not be measured.</summary>
    private static void DrawPlainBead(
        CanvasDrawingSession session,
        RopeAppearance appearance,
        BeadPlacement bead)
    {
        CharmPalette palette = appearance.BeadTint ?? appearance.Palette;
        var center = ToVector(bead.Position);

        session.FillEllipse(
            center,
            (float)(bead.Size.Width / 2),
            (float)(bead.Size.Height / 2),
            ToColor(palette.Primary, 1));

        // One highlight up and left of centre, which is where the light is in every
        // piece of this artwork.
        session.FillEllipse(
            new System.Numerics.Vector2(
                center.X - (float)(bead.Size.Width * 0.16),
                center.Y - (float)(bead.Size.Height * 0.18)),
            (float)(bead.Size.Width * 0.18),
            (float)(bead.Size.Height * 0.16),
            ToColor(palette.Light, 0.75));
    }

    /// <summary>Strokes the entrance's web, from the top edge to the rope: silk-white threads over a faint dark underline, so it reads on a white window as well as a dark wallpaper.</summary>
    /// <remarks>
    /// Drawn from the web's fixed pattern each frame that is drawn at all — a settled rope draws
    /// none — as short straight lines, so nothing is allocated per frame and nothing is kept. The same strokes, widths and
    /// alphas as macOS's <c>WebBloomRenderer</c>.
    /// </remarks>
    private static void DrawWebBloom(CanvasDrawingSession session, WebBloom bloom)
    {
        if (bloom.Growth <= 0.001)
        {
            return;
        }

        using var round = new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
        Color shadow = Color.FromArgb((byte)Math.Round(255 * 0.22), 0, 0, 0);
        Color silk = Color.FromArgb((byte)Math.Round(255 * 0.92), 255, 255, 255);
        IReadOnlyList<WebBloom.Thread> threads = bloom.Threads;

        // Every underline first, then every thread, so no shadow falls across silk.
        foreach (bool isShadow in new[] { true, false })
        {
            foreach (WebBloom.Thread thread in threads)
            {
                // Four straight pieces along the curve, joined by round caps: indistinguishable
                // from the curve at these sizes, and no geometry object to build per frame.
                float width = (float)(isShadow ? thread.Width + 1.1 : thread.Width);
                Vec2 previous = thread.Start;
                for (int piece = 1; piece <= 4; piece++)
                {
                    Vec2 next = thread.At(piece / 4.0);
                    session.DrawLine(ToVector(previous), ToVector(next), isShadow ? shadow : silk, width, round);
                    previous = next;
                }
            }
        }
    }

    private void DrawCharms(
        CanvasDrawingSession session, RopeSnapshot snapshot, IReadOnlyList<HookConnectorRenderer.Placed?> placed, string finish,
        RopeStyle style, double ropeWidth)
    {
        // Every glow first, then every charm. Interleaved, the second charm's glow would
        // be painted over the first charm's artwork — a glow reaches 1.7 radii and three
        // charms on one cord are closer together than that — and a charm seen through its
        // neighbour's colour is not what any of this is for.
        for (int index = 0; index < snapshot.Charms.Count; index++)
        {
            DrawAmbientGlow(session, snapshot, index);
        }

        for (int index = 0; index < snapshot.Charms.Count; index++)
        {
            CharmPlacement placement = snapshot.Charms[index];
            CharmDescriptor? descriptor = index < Charms.Count ? Charms[index] : null;

            // There used to be an ambient halo here: a filled disc of 1.7 radii at 6%
            // alpha in the charm's own light colour. It is gone, and nothing replaces it
            // in that role, because the original has no such thing. What it actually did
            // was put a hard-edged circle behind every charm — visible in every screenshot
            // of this build and in none of macOS. The depth it was reaching for is the
            // drop shadow, which CharmArtworkCache now casts from the artwork's alpha.
            //
            // CharmHaloExtent still sizes the canvas. That is a separate job: it is the
            // headroom the layout reserves below the lowest charm, and the shadow and the
            // swing both need it.
            // The far half of the jump ring on a charm with a hook, under the charm's bar.
            if (!chained && index < placed.Count && placed[index] is HookConnectorRenderer.Placed back)
            {
                connectors.DrawBack(session, back, finish);
            }

            artwork.Draw(session, descriptor, HangFor(snapshot, index));
        }

        // The cap and the near half of the jump ring, over the charm — or, on a chain, the chain's own last link, hooked
        // through the charm's ring.
        foreach (HookConnectorRenderer.Placed? connector in placed)
        {
            if (connector is not HookConnectorRenderer.Placed front)
            {
                continue;
            }

            if (chained)
            {
                Vec2 direction = front.RopeDirection;
                RopeSurface.DrawEndLink(session, style, front.RopeEnd, Math.Atan2(direction.Y, direction.X), ropeWidth);
            }
            else
            {
                connectors.DrawFront(session, front, finish);
            }
        }
    }

    /// <summary>The charm's glow, in its own shape: see <see cref="CharmArtworkCache.DrawGlow"/>.</summary>
    /// <remarks>
    /// Drawn where the charm itself is drawn, turned with it, and under the charm and its shadow, so neither is
    /// tinted by it. It replaces a radial gradient round every charm, which was a disc whatever the charm's shape.
    /// </remarks>
    private void DrawAmbientGlow(CanvasDrawingSession session, RopeSnapshot snapshot, int index)
    {
        if (index >= Charms.Count || GlowTable.StrengthOf(glow) is not GlowStrength strength)
        {
            return;
        }

        artwork.DrawGlow(session, Charms[index], HangFor(snapshot, index), strength);
    }

    private static System.Numerics.Vector2 ToVector(Vec2 point) => new((float)point.X, (float)point.Y);

    private static Color ToColor(CharmColor color, double alpha) => Color.FromArgb(
        (byte)Math.Clamp(color.Alpha * alpha * 255, 0, 255),
        (byte)Math.Clamp(color.Red * 255, 0, 255),
        (byte)Math.Clamp(color.Green * 255, 0, 255),
        (byte)Math.Clamp(color.Blue * 255, 0, 255));
}

/// <summary>For the smoothness checks only: the worst of each part of a frame's drawing, and rasterisations.</summary>
internal static class RenderTimes
{
    private static readonly double[] Worst = new double[4];
    private static long last;
    public static int Rasters;

    /// <summary>How many times the rope's offscreen layer was made, and the worst of each part of drawing into it: making
    /// it, the cords, the charms cut out of it, closing it, and putting it on screen.</summary>
    public static int LayersMade;

    /// <summary>The worst gap between a charm's exit and the start of the cord below it, in points.</summary>
    private static double junctionWorst;
    private static int junctions;

    private static int ownershipFrames, ownershipMissing, ownershipDuplicates, ownershipOrphans, ownershipMismatch;

    /// <summary>One frame's rope ownership: charms our cord hangs, ropes drawn, and any that broke the rule.</summary>
    public static void NoteOwnership(int charms, int ropes, int missing, int duplicates, int orphans)
    {
        ownershipFrames++;
        ownershipMissing += missing;
        ownershipDuplicates += duplicates;
        ownershipOrphans += orphans;
        ownershipMismatch += charms != ropes ? 1 : 0;
    }

    public static void NoteJunction(double gap)
    {
        junctionWorst = Math.Max(junctionWorst, gap);
        junctions++;
    }
    private static readonly double[] Parts = new double[5];
    private static long partAt;

    public static void Part(int index)
    {
        if (!PresentTimes.On)
        {
            return;
        }

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(index > 0 ? partAt : last, now).TotalMilliseconds;
        Parts[index] = Math.Max(Parts[index], elapsed);
        FrameParts[index] = elapsed;
        partAt = now;
    }

    /// <summary>This frame's own times, overwritten every frame, for the spike report (FrameAudit): layout (placing the
    /// charms and building the cord), rope, beads, charms; and the rope's parts as <see cref="Part"/> names them.</summary>
    public static readonly double[] Frame = new double[4];
    public static readonly double[] FrameParts = new double[5];

    /// <summary>Bitmaps made this frame — artwork rasterised, a rope or connector picture decoded — each an upload to the
    /// graphics card.</summary>
    public static int FrameUploads;

    public static void ResetFrame()
    {
        Array.Clear(Frame);
        Array.Clear(FrameParts);
        FrameUploads = 0;
    }

    /// <summary>Index -1 opens a frame's drawing; 0 closes its layout; 1–3 close the rope, beads and charms.</summary>
    public static void Mark(int index)
    {
        if (!PresentTimes.On)
        {
            return;
        }

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(last, now).TotalMilliseconds;
        if (index > 0)
        {
            Worst[index] = Math.Max(Worst[index], elapsed);
        }

        if (index >= 0)
        {
            Frame[index] = elapsed;
        }

        last = now;
    }

    public static string Take()
    {
        string text = $"rope {Worst[1]:0.0} (to layer {Parts[0]:0.0}, cords {Parts[1]:0.0}, cut-outs {Parts[2]:0.0}, close {Parts[3]:0.0}, " +
            $"composite {Parts[4]:0.0}, layers made {LayersMade}), beads {Worst[2]:0.0}, charms {Worst[3]:0.0}, raster lookups {Rasters}; " +
            $"junctions {junctions}, worst gap {junctionWorst:0.000} pt; " +
            $"rope ownership: {ownershipFrames} frames, ropes != charms {ownershipMismatch}, missing {ownershipMissing}, duplicates {ownershipDuplicates}, orphans {ownershipOrphans}";
        Array.Clear(Worst);
        Array.Clear(Parts);
        Rasters = 0;
        LayersMade = 0;
        junctionWorst = 0;
        junctions = 0;
        ownershipFrames = ownershipMissing = ownershipDuplicates = ownershipOrphans = ownershipMismatch = 0;
        return text;
    }
}
