//
//  RopeSheet.cs
//  Hangly
//
//  Every charm on the rope, drawn offscreen by the real renderer, for checking by eye.
//

using Hangly.App.Overlay;
using Hangly.Core.Geometry;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Microsoft.Graphics.Canvas;

namespace Hangly.App.Services;

/// <summary>
/// A development switch, <c>--rope-sheet &lt;folder&gt;</c>: every charm in the catalogue
/// hanging from a straight cord, through the overlay's own <see cref="RopeRenderer"/>, in
/// sheets of twenty-four — whole, and as a close-up of where the cord meets each one.
/// </summary>
/// <remarks>
/// Drawn on the shared device into offscreen targets, so it opens no window and touches
/// nothing on screen: the way to look at how the rope meets all hundred and sixty charms
/// on this platform without hanging them one at a time. macOS renders the same sheets
/// from <c>RopeCanvasView</c>.
/// </remarks>
internal static class RopeSheet
{
    private const int Columns = 8;
    private const int PerSheet = 24;
    private const float CellWidth = 200;
    private const float Label = 16;

    public static void Write(string folder)
    {
        Directory.CreateDirectory(folder);
        CanvasDevice device = CanvasDevice.GetSharedDevice();
        var artwork = new CharmArtworkCache(device, CharmArtworkCache.DefaultDirectory);
        var renderer = new RopeRenderer(artwork) { Glow = GlowLevel.Off };
        var index = new CharmIndex();
        IReadOnlyList<CharmCatalogEntry> all = CharmCatalog.All;

        for (int start = 0, sheet = 1; start < all.Count; start += PerSheet, sheet++)
        {
            CharmCatalogEntry[] batch = [.. all.Skip(start).Take(PerSheet)];
            Draw(device, renderer, artwork, index, batch, radius: 58, cellHeight: 330, close: false,
                Path.Combine(folder, $"w{sheet}-hang.png"));
            Draw(device, renderer, artwork, index, batch, radius: 150, cellHeight: 140, close: true,
                Path.Combine(folder, $"w{sheet}-joint.png"));
        }
    }

    private static void Draw(
        CanvasDevice device,
        RopeRenderer renderer,
        CharmArtworkCache artwork,
        CharmIndex index,
        CharmCatalogEntry[] batch,
        double radius,
        float cellHeight,
        bool close,
        string path)
    {
        int rows = (batch.Length + Columns - 1) / Columns;
        using var target = new CanvasRenderTarget(device, Columns * CellWidth, rows * (cellHeight + Label), 192);
        using (CanvasDrawingSession session = target.CreateDrawingSession())
        {
            session.Clear(Windows.UI.Color.FromArgb(255, 28, 28, 31));
            for (int slot = 0; slot < batch.Length; slot++)
            {
                CharmCatalogEntry entry = batch[slot];
                CharmDescriptor charm = CharmLibrary.Resolve(artwork, index, [new RopeCharm(entry.Id, 1)])[0];
                renderer.Charms = [charm];

                // A straight cord from above the cell down to the charm's knot.
                double knot = charm.Metrics.KnotInset;
                double length = close ? 120 : 150;
                var anchor = new Vec2(CellWidth / 2, close ? -40 : 6);
                var center = new Vec2(anchor.X, anchor.Y + length + (radius * knot));
                Vec2[] points = [.. Enumerable.Range(0, 21).Select(i => anchor + ((center - anchor) * (i / 20.0)))];
                double entryArc = (center - anchor).Magnitude - (radius * knot);
                var snapshot = new RopeSnapshot(
                    points,
                    [new CharmPlacement(center, radius, Math.PI / 2, knot, entryArc, entryArc)],
                    [],
                    1,
                    false);

                float x = slot % Columns * CellWidth;
                float y = slot / Columns * (cellHeight + Label);
                using (session.CreateLayer(1f, new Windows.Foundation.Rect(x, y, CellWidth, cellHeight)))
                {
                    session.Transform = System.Numerics.Matrix3x2.CreateTranslation(x, y);
                    renderer.Draw(session, snapshot, RopeStyle.GoldChain);
                    session.Transform = System.Numerics.Matrix3x2.Identity;
                }

                session.DrawText(entry.DisplayName, x + 6, y + cellHeight, Microsoft.UI.Colors.White);
            }
        }

        target.SaveAsync(path).AsTask().GetAwaiter().GetResult();
    }
}
