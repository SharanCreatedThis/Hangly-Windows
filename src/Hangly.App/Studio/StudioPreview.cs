//
//  StudioPreview.cs
//  Hangly
//
//  The charm being made, drawn by the renderer the rope uses.
//

using Hangly.App.Overlay;
using Hangly.Core.Geometry;
using Hangly.Core.Import;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Studio;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Hangly.App.Studio;

/// <summary>The three previews — Cut-out, Charm, On the Rope — on one canvas.</summary>
/// <remarks>
/// <b>The real renderer, not a likeness.</b> The draft is written as the SVG it will be
/// saved as, into a folder of its own, and drawn through <see cref="CharmArtworkCache"/>
/// and <see cref="RopeRenderer"/> with a private <see cref="RopeSimulation"/> — so the
/// preview is the charm as the overlay will draw it: the shadow, the knot, the swing its
/// mass gives it.
///
/// <para><b>Idle.</b> Frames are asked for only while the rope moves: the canvas hooks
/// <c>CompositionTarget.Rendering</c> when it is pushed and unhooks when the rope sleeps,
/// which is the macOS preview's "paused whenever the rope has settled". A still Studio
/// draws nothing. Under reduced motion the rope hangs still and a push is the reduced
/// nudge, as it is on the overlay.</para>
/// </remarks>
/// <summary>The three ways of looking at a charm being made; one at a time, as on macOS.</summary>
internal enum StudioPreviewMode
{
    /// <summary>The subject with its background gone, on a transparency grid.</summary>
    Cutout,

    /// <summary>The finished charm as the rope will draw it, lit and shadowed.</summary>
    Charm,

    /// <summary>The charm hanging, so its weight can be watched rather than read.</summary>
    OnRope,
}

internal sealed class StudioPreview : IDisposable
{
    private readonly CanvasControl canvas = new();
    private readonly string folder = Path.Combine(Path.GetTempPath(), "Hangly", "studio-preview");
    private readonly RopeMotion motion;
    private readonly RopeStyle style;
    private CharmArtworkCache? artwork;
    private RopeRenderer? renderer;
    private RopeSimulation? rope;
    private CharmDescriptor? charm;
    private StudioDraft? shown;
    private string? drawnFile;
    private StudioImage? cutout;
    private CanvasBitmap? cutoutBitmap;
    private StudioPreviewMode mode = StudioPreviewMode.Charm;
    private bool animating;
    private TimeSpan lastFrame;

    public StudioPreview(RopeStyle style, RopeMotion motion)
    {
        this.style = style;
        this.motion = motion;
        canvas.ClearColor = Windows.UI.Color.FromArgb(255, 33, 33, 33);
        canvas.CreateResources += OnCreateResources;
        canvas.Draw += OnDraw;
        canvas.SizeChanged += (_, _) => FitRope();
        canvas.PointerPressed += (_, args) =>
        {
            if (mode == StudioPreviewMode.OnRope && rope is not null)
            {
                double x = args.GetCurrentPoint(canvas).Position.X;
                rope.Push(x < canvas.ActualWidth / 2 ? 1 : -1);
                Animate();
            }
        };
        AutomationName("Charm preview");
    }

    public FrameworkElement View => canvas;

    /// <summary>Which preview is drawn.</summary>
    public StudioPreviewMode Mode
    {
        get => mode;
        set
        {
            mode = value;
            AutomationName(value switch
            {
                StudioPreviewMode.Cutout => "Cut-out preview",
                StudioPreviewMode.OnRope => "Rope preview. Click to push the charm.",
                _ => "Charm preview",
            });
            if (value == StudioPreviewMode.OnRope)
            {
                FitRope();
                if (RopeMotionTable.PhysicsOf(motion).SwingsOnLaunch)
                {
                    rope?.Push();
                    Animate();
                }
            }

            canvas.Invalidate();
        }
    }

    /// <summary>The subject with its background gone, for the Cut-out preview.</summary>
    public void ShowCutout(StudioImage? image)
    {
        if (ReferenceEquals(image, cutout))
        {
            return;
        }

        cutout = image;
        cutoutBitmap?.Dispose();
        cutoutBitmap = null;
        canvas.Invalidate();
    }

    /// <summary>Shows <paramref name="draft"/>, or nothing.</summary>
    public void Show(StudioDraft? draft, string name)
    {
        if (ReferenceEquals(draft, shown))
        {
            return;
        }

        shown = draft;
        charm = null;
        if (draft is not null && artwork is not null)
        {
            charm = Describe(draft, name);
        }

        if (charm is not null && rope is not null && renderer is not null)
        {
            renderer.Charms = [charm];
            rope.SetCharmStack([charm.Metrics]);
            rope.SetBeads([charm.Beads]);
            FitRope();
        }

        canvas.Invalidate();
    }

    /// <summary>Writes the draft where the artwork cache reads it, under a name of its own.</summary>
    /// <remarks>
    /// A new file per draft, because the cache is keyed on the file: reusing one name would
    /// show the previous draft's raster at a size already drawn.
    /// </remarks>
    private CharmDescriptor? Describe(StudioDraft draft, string name)
    {
        try
        {
            Directory.CreateDirectory(folder);
            if (drawnFile is not null)
            {
                TryDelete(drawnFile);
            }

            drawnFile = Path.Combine(folder, $"{Guid.NewGuid():N}.svg");
            File.WriteAllText(drawnFile, StudioRaster.ToSvg(draft.Square));

            var entry = new CustomCharmEntry
            {
                Id = Guid.Empty,
                Name = name,
                CreatedAt = DateTimeOffset.UnixEpoch,
                ImageFileName = Path.GetFileName(drawnFile),
                Metrics = draft.Metrics,
                Palette = draft.Palette,
            };
            CharmCatalogEntry catalogued = entry.AsCatalogEntry(drawnFile);
            return CharmLibrary.Resolve(artwork!, new CharmIndex([catalogued]), [new RopeCharm(catalogued.Id, 1)])[0];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Services.Diagnostics.Log($"studio: preview could not be written: {exception.GetType().Name}");
            return null;
        }
    }

    private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        cutoutBitmap?.Dispose();
        cutoutBitmap = null;
        artwork?.Dispose();
        artwork = new CharmArtworkCache(sender, folder);
        renderer = new RopeRenderer(artwork);
        rope = new RopeSimulation(style: style);
        rope.SetMotion(motion);
        rope.Start();
        StudioDraft? draft = shown;
        shown = null;
        Show(draft, charm?.DisplayName ?? string.Empty);
    }

    private void FitRope()
    {
        if (rope is null || canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0)
        {
            return;
        }

        // Held to the macOS stage's 0.9 aspect, centred: a rope fits the height it is
        // given and swings about as wide as it is long, so a tall narrow stage would lose
        // the charm sideways on the first push.
        double height = Math.Min(canvas.ActualHeight, canvas.ActualWidth / 0.9);
        rope.Fit(new Size(height * 0.9, height), charmSize: 1, ropeLength: 1);
        rope.ResetToHanging();
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (mode == StudioPreviewMode.Cutout)
        {
            DrawCutout(sender, args.DrawingSession);
            return;
        }

        if (charm is null || artwork is null)
        {
            return;
        }

        if (mode == StudioPreviewMode.Charm)
        {
            // Nearly to the edge, as on macOS: it is the only thing in the middle of the
            // window and its whole job is to be looked at closely.
            double side = Math.Min(sender.ActualWidth, sender.ActualHeight);
            var centre = new Vec2(sender.ActualWidth / 2, sender.ActualHeight / 2);
            artwork.Draw(args.DrawingSession, charm, new CharmPlacement(centre, side * 0.45, Math.PI / 2, charm.Metrics.KnotInset, 0, 0));
            artwork.EndFrame();
            return;
        }

        if (rope is null || renderer is null)
        {
            return;
        }

        double height = Math.Min(sender.ActualHeight, sender.ActualWidth / 0.9);
        float offsetX = (float)((sender.ActualWidth - (height * 0.9)) / 2);
        float offsetY = (float)((sender.ActualHeight - height) / 2);
        args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateTranslation(offsetX, offsetY);
        renderer.Draw(args.DrawingSession, rope.Snapshot(), rope.Style);
        artwork.EndFrame();
    }

    /// <summary>The classic transparency grid, then the cut-out fitted inside a 12-point margin.</summary>
    private void DrawCutout(CanvasControl sender, CanvasDrawingSession session)
    {
        const float cell = 8;
        var light = Windows.UI.Color.FromArgb(255, 235, 235, 235);
        var dark = Windows.UI.Color.FromArgb(255, 204, 204, 204);
        session.Clear(light);
        for (int row = 0; row * cell < sender.ActualHeight; row++)
        {
            for (int column = row % 2; column * cell < sender.ActualWidth; column += 2)
            {
                session.FillRectangle(column * cell, row * cell, cell, cell, dark);
            }
        }

        if (cutout is null)
        {
            return;
        }

        cutoutBitmap ??= CanvasBitmap.CreateFromBytes(
            sender,
            cutout.Pixels,
            cutout.Width,
            cutout.Height,
            Windows.Graphics.DirectX.DirectXPixelFormat.R8G8B8A8UIntNormalized,
            96,
            CanvasAlphaMode.Premultiplied);
        double scale = Math.Min((sender.ActualWidth - 24) / cutout.Width, (sender.ActualHeight - 24) / cutout.Height);
        double width = cutout.Width * scale, height = cutout.Height * scale;
        session.DrawImage(
            cutoutBitmap,
            new Windows.Foundation.Rect((sender.ActualWidth - width) / 2, (sender.ActualHeight - height) / 2, width, height),
            new Windows.Foundation.Rect(0, 0, cutout.Width, cutout.Height),
            1f,
            CanvasImageInterpolation.HighQualityCubic);
    }

    private void Animate()
    {
        if (animating)
        {
            return;
        }

        animating = true;
        lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, object args)
    {
        if (rope is null || mode != StudioPreviewMode.OnRope)
        {
            StopAnimating();
            return;
        }

        TimeSpan now = ((RenderingEventArgs)args).RenderingTime;
        double delta = lastFrame == TimeSpan.Zero ? 1.0 / 60 : Math.Min(0.05, (now - lastFrame).TotalSeconds);
        lastFrame = now;
        rope.Step(delta);
        canvas.Invalidate();
        if (rope.IsSleeping)
        {
            StopAnimating();
        }
    }

    private void StopAnimating()
    {
        if (animating)
        {
            CompositionTarget.Rendering -= OnRendering;
            animating = false;
        }
    }

    private void AutomationName(string name) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(canvas, name);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A temp file; the next draft or the next launch tidies it.
        }
    }

    public void Dispose()
    {
        StopAnimating();
        artwork?.Dispose();
        artwork = null;
        cutoutBitmap?.Dispose();
        cutoutBitmap = null;
        if (drawnFile is not null)
        {
            TryDelete(drawnFile);
        }

        canvas.RemoveFromVisualTree();
    }
}
