//
//  HookConnectorRenderer.cs
//  Hangly
//
//  The metal that joins the rope to a charm's hook: an end cap and a jump ring.
//

using Hangly.App.Services;
using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Microsoft.Graphics.Canvas;
using SkiaSharp;

namespace Hangly.App.Overlay;

/// <summary>
/// Joins the rope to a charm with a hook (<see cref="CharmHooks"/>) the way a jeweller does: a jump ring through the
/// charm's own ring, the rope tied on through it — or, on a heavy charm, ending in a metal end cap whose eyelet holds the
/// jump ring. Small, and smaller than what it holds (<see cref="HookConnector"/>): the charm is the hero.
/// macOS's <c>HookConnectorRenderer</c>.
/// </summary>
/// <remarks>
/// <b>Pictures, placed by measurement.</b> The cap and the ring are photographic renders (Assets/Connectors, one pair per
/// finish), made to match the charms; drawn in code the metal looked flat beside them. Where each picture's parts are was
/// measured once (<see cref="HookConnector"/>), so each is sized and placed from the charm's own hook.
///
/// <para><b>Linked through, not laid over.</b> The jump ring's far half is drawn before the charm and its near half
/// after, so the charm's own bar covers the one and is covered by the other — wherever the bar really is. The finish
/// follows the rope, and the rope ends inside the cap, so its cut end never shows.</para>
/// </remarks>
internal sealed class HookConnectorRenderer
{
    /// <summary>The metal a rope's connector is made of: the name of its pictures.</summary>
    public static string FinishOf(RopeStyle style) => style switch
    {
        RopeStyle.Thread or RopeStyle.GoldChain or RopeStyle.TempleThread => "gold",
        RopeStyle.Leather => "brass",
        RopeStyle.MidnightCord => "gunmetal",
        RopeStyle.Neon => "neon",
        _ => "silver",
    };

    /// <summary>The connector on one charm, and the transform from the charm's unit square to the canvas.</summary>
    public readonly record struct Placed(HookConnector Connector, System.Numerics.Matrix3x2 Transform, Vec2 Centre, bool Chained = false)
    {
        /// <summary>
        /// Which way the rope runs as it reaches the hardware: down the charm's hanging line, from its ring through its
        /// centre. Not the artwork's own up and down: a charm turned to put an off-centre ring above its centre hangs
        /// along that line, and arriving along the artwork's axis put an S in a rope that should fall straight.
        /// </summary>
        public Vec2 RopeDirection => Centre - RopeEnd;

        public Vec2 RopeEnd
        {
            get
            {
                // On a chain, the eye, where the chain's last link rests; on a cord, the jump ring's top wire or the cap.
                Vec2 local = Connector.EndFor(Chained);
                System.Numerics.Vector2 end = System.Numerics.Vector2.Transform(
                    new System.Numerics.Vector2((float)local.X, (float)local.Y), Transform);
                return new Vec2(end.X, end.Y);
            }
        }
    }

    /// <inheritdoc cref="HookConnector.BlendLength"/>
    public static double BlendLength(double radius, double cordWidth) => HookConnector.BlendLength(radius, cordWidth);

    /// <summary>The rope eased from the cord into the hardware, onto <paramref name="run"/> (<see cref="HookConnector.Blend"/>).</summary>
    public static void AddBlend(List<Vec2> run, Vec2 leaving, Vec2 end, Vec2 arriving) =>
        run.AddRange(HookConnector.Blend(run[^1], leaving, end, arriving));

    /// <summary>The connector on a charm drawn as <paramref name="hang"/>, or null for a charm without a hook.</summary>
    public static Placed? Place(CharmDescriptor? charm, CharmHang hang, bool chained = false)
    {
        if (charm is not { Connector: HookConnector connector, HangsByOwnCord: false } || hang.Radius <= 1)
        {
            return null;
        }

        Rect body = charm.Body;
        double longest = Math.Max(body.Width, body.Height);
        if (longest <= 0)
        {
            return null;
        }

        float scale = (float)(hang.Radius * 2 / longest);
        System.Numerics.Matrix3x2 transform =
            System.Numerics.Matrix3x2.CreateTranslation(-(float)body.MidX, -(float)body.MidY)
            * System.Numerics.Matrix3x2.CreateScale(scale)
            * System.Numerics.Matrix3x2.CreateRotation((float)hang.Rotation)
            * System.Numerics.Matrix3x2.CreateTranslation((float)hang.Center.X, (float)hang.Center.Y);
        return new Placed(connector, transform, hang.Center, chained);
    }

    private readonly Dictionary<string, CanvasBitmap?> bitmaps = [];
    private CanvasDevice? device;

    /// <summary>The far half of the jump ring, drawn before the charm, so the charm's own bar covers it.</summary>
    public void DrawBack(CanvasDrawingSession session, Placed placed, string finish)
    {
        if (Bitmap(session, $"ring-{finish}") is not CanvasBitmap ring)
        {
            return;
        }

        Rect rect = placed.Connector.RingRect;
        Draw(session, placed, ring, rect, new Rect(rect.MidX, rect.Top - rect.Height, rect.Width, rect.Height * 3));
    }

    /// <summary>The cap, then the near half of the jump ring over the charm's bar and the cap's eyelet.</summary>
    public void DrawFront(CanvasDrawingSession session, Placed placed, string finish)
    {
        if (Bitmap(session, $"ring-{finish}") is not CanvasBitmap ring)
        {
            return;
        }

        Rect ringRect = placed.Connector.RingRect;
        if (placed.Connector.CapRect is Rect capRect && Bitmap(session, $"cap-{finish}") is CanvasBitmap cap)
        {
            Draw(session, placed, cap, capRect, null);
        }

        Draw(session, placed, ring, ringRect, new Rect(ringRect.Left - ringRect.Width, ringRect.Top - ringRect.Height, ringRect.Width * 1.5, ringRect.Height * 3));
    }

    private static void Draw(CanvasDrawingSession session, Placed placed, CanvasBitmap bitmap, Rect destination, Rect? clip)
    {
        System.Numerics.Matrix3x2 previous = session.Transform;
        session.Transform = placed.Transform * previous;
        var target = new Windows.Foundation.Rect(destination.Left, destination.Top, destination.Width, destination.Height);
        var source = new Windows.Foundation.Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height);
        if (clip is Rect area)
        {
            using (session.CreateLayer(1f, new Windows.Foundation.Rect(area.Left, area.Top, area.Width, area.Height)))
            {
                session.DrawImage(bitmap, target, source, 1f, CanvasImageInterpolation.HighQualityCubic);
            }
        }
        else
        {
            session.DrawImage(bitmap, target, source, 1f, CanvasImageInterpolation.HighQualityCubic);
        }

        session.Transform = previous;
    }

    /// <summary>A connector picture from Assets/Connectors, decoded once per graphics device.</summary>
    private CanvasBitmap? Bitmap(CanvasDrawingSession session, string name)
    {
        if (!ReferenceEquals(device, session.Device))
        {
            foreach (CanvasBitmap? stale in bitmaps.Values)
            {
                stale?.Dispose();
            }

            bitmaps.Clear();
            device = session.Device;
        }

        if (bitmaps.TryGetValue(name, out CanvasBitmap? cached))
        {
            return cached;
        }

        CanvasBitmap? bitmap = null;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Connectors", $"{name}.png");
            using SKBitmap? decoded = SKBitmap.Decode(path);
            if (decoded is not null)
            {
                using SKBitmap converted = decoded.Copy(SKColorType.Bgra8888)
                    ?? throw new InvalidOperationException("could not convert");
                byte[] pixels = new byte[converted.ByteCount];
                System.Runtime.InteropServices.Marshal.Copy(converted.GetPixels(), pixels, 0, pixels.Length);
                bitmap = CanvasBitmap.CreateFromBytes(
                    session, pixels, converted.Width, converted.Height,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 96,
                    CanvasAlphaMode.Premultiplied);
            }
        }
        catch (Exception exception)
        {
            Diagnostics.Log($"connector '{name}' could not be loaded: {exception.Message}");
        }

        bitmaps[name] = bitmap;
        return bitmap;
    }
}
