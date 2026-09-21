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

    public RopeRenderer(CharmArtworkCache artwork) => this.artwork = artwork;

    /// <summary>What the charms hanging on the rope are, from the anchor down.</summary>
    public IReadOnlyList<CharmDescriptor> Charms { get; set; } = [];

    public void Draw(CanvasDrawingSession session, RopeSnapshot snapshot, RopeStyle style)
    {
        if (snapshot.Points.Count < 2)
        {
            return;
        }

        session.Antialiasing = CanvasAntialiasing.Antialiased;

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

        DrawCord(session, snapshot, appearance, width, charmRadius);
        DrawBeads(session, snapshot, appearance);
        DrawCharms(session, snapshot);
    }

    /// <summary>
    /// The cord, in the gaps the charms leave. A charm hides the cord it is drawn over,
    /// so a string of three needs four visible pieces of cord rather than one line with
    /// three discs on top of it.
    /// </summary>
    private static void DrawCord(
        CanvasDrawingSession session,
        RopeSnapshot snapshot,
        RopeAppearance appearance,
        double width,
        double charmRadius)
    {
        using CanvasPathBuilder builder = BuildSpline(session, snapshot.Points);
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
        DrawCordShadow(session, path, width, charmRadius);

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
                var style = new CanvasStrokeStyle
                {
                    CustomDashStyle = [(float)texture.Pitch, (float)texture.Pitch],
                    DashCap = CanvasCapStyle.Flat,
                };
                double inset = width * (1 - texture.Offset);
                session.DrawGeometry(path, ToColor(appearance.Palette.Light, 0.55), (float)inset, style);
                break;
            }

            case RopeTextureKind.Braid:
            {
                // Two offset dashed passes, wider and flatter than a twist: the over and
                // under of a flat braid.
                var style = new CanvasStrokeStyle
                {
                    CustomDashStyle = [(float)texture.Pitch, (float)(texture.Pitch * 0.9)],
                    DashCap = CanvasCapStyle.Flat,
                };
                var offsetStyle = new CanvasStrokeStyle
                {
                    CustomDashStyle = [(float)texture.Pitch, (float)(texture.Pitch * 0.9)],
                    DashOffset = (float)texture.Pitch,
                    DashCap = CanvasCapStyle.Flat,
                };
                double inset = width * (1 - texture.Offset);
                session.DrawGeometry(path, ToColor(appearance.Palette.Light, 0.40), (float)inset, style);
                session.DrawGeometry(path, ToColor(appearance.Palette.Secondary, 0.55), (float)inset, offsetStyle);
                break;
            }

            case RopeTextureKind.Links:
            {
                // A heavy notch with a lit rim: the gap between two links, and the light
                // catching the near edge of each.
                var notch = new CanvasStrokeStyle
                {
                    CustomDashStyle = [(float)(texture.Pitch * texture.Thickness), (float)texture.Pitch],
                    DashCap = CanvasCapStyle.Round,
                };
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
    private static CanvasPathBuilder BuildSpline(CanvasDrawingSession session, IReadOnlyList<Vec2> points)
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
        if (points[0].Y > 0)
        {
            builder.BeginFigure(new System.Numerics.Vector2((float)points[0].X, 0));
            builder.AddLine(ToVector(points[0]));
        }
        else
        {
            builder.BeginFigure(ToVector(points[0]));
        }

        for (int index = 1; index < points.Count - 1; index++)
        {
            Vec2 control = points[index];
            Vec2 finish = (points[index] + points[index + 1]) * 0.5;
            builder.AddQuadraticBezier(ToVector(control), ToVector(finish));
        }

        builder.AddLine(ToVector(points[^1]));
        builder.EndFigure(CanvasFigureLoop.Open);
        return builder;
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
    private void DrawBeads(CanvasDrawingSession session, RopeSnapshot snapshot, RopeAppearance appearance)
    {
        // Beads arrive grouped by the charm that threads them, in the order that charm's
        // regions were measured, so the run of each owner counts its own way through.
        int ordinal = 0;
        int owner = -1;

        foreach (BeadPlacement bead in snapshot.Beads)
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

    private void DrawCharms(CanvasDrawingSession session, RopeSnapshot snapshot)
    {
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
            artwork.Draw(session, descriptor, placement);
        }
    }

    private static System.Numerics.Vector2 ToVector(Vec2 point) => new((float)point.X, (float)point.Y);

    private static Color ToColor(CharmColor color, double alpha) => Color.FromArgb(
        (byte)Math.Clamp(color.Alpha * alpha * 255, 0, 255),
        (byte)Math.Clamp(color.Red * 255, 0, 255),
        (byte)Math.Clamp(color.Green * 255, 0, 255),
        (byte)Math.Clamp(color.Blue * 255, 0, 255));
}
