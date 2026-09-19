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

    public void Draw(CanvasDrawingSession session, RopeSnapshot snapshot, RopeStyle style, double scale)
    {
        if (snapshot.Points.Count < 2)
        {
            return;
        }

        session.Antialiasing = CanvasAntialiasing.Antialiased;

        // The solver works in points and the swapchain in pixels; one transform here is
        // the whole of the conversion, so nothing below has to know about DPI.
        session.Transform = System.Numerics.Matrix3x2.CreateScale((float)scale);

        RopeAppearance appearance = RopeStyleAppearanceTable.AppearanceOf(style);
        double charmRadius = snapshot.Charms.Count > 0 ? snapshot.Charms[^1].Radius : 10;
        double width = RopeStyleAppearanceTable.WidthFor(style, charmRadius);

        DrawCord(session, snapshot, appearance, width);
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
        double width)
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

        // The body: deep underneath for the shadowed side, primary over it.
        session.DrawGeometry(path, ToColor(appearance.Palette.Deep, 1), (float)(width * 1.25));
        session.DrawGeometry(path, ToColor(appearance.Palette.Primary, 1), (float)width);

        DrawTexture(session, path, appearance, width);
    }

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
        builder.BeginFigure(ToVector(points[0]));

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

    private void DrawBeads(CanvasDrawingSession session, RopeSnapshot snapshot, RopeAppearance appearance)
    {
        foreach (BeadPlacement bead in snapshot.Beads)
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
    }

    private void DrawCharms(CanvasDrawingSession session, RopeSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.Charms.Count; index++)
        {
            CharmPlacement placement = snapshot.Charms[index];
            CharmDescriptor? descriptor = index < Charms.Count ? Charms[index] : null;

            // The ambient halo, which is what CharmHaloExtent sizes the canvas for.
            if (descriptor is not null)
            {
                session.FillEllipse(
                    ToVector(placement.Center),
                    (float)(placement.Radius * RopeConfiguration.Layout.CharmHaloExtent),
                    (float)(placement.Radius * RopeConfiguration.Layout.CharmHaloExtent),
                    ToColor(descriptor.Palette.Light, 0.06));
            }

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
