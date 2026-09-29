//
//  WelcomeHero.cs
//  Hangly
//
//  The charm, hanging, at the top of the welcome card.
//

using Hangly.App.Overlay;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Hangly.App.Onboarding;

/// <summary>A real rope with the real charm on it, swinging: macOS's <c>AboutHero</c> on the welcome card.</summary>
/// <remarks>
/// <b>The app, not a picture of it.</b> The charm that is hanging — Spider-Man on a new
/// install — on the cord that is hanging, Spider Thread, drawn by the overlay's own
/// <see cref="RopeRenderer"/> with a private <see cref="RopeSimulation"/>, at macOS's size:
/// the full width of the card, 250 points tall from the window's top edge, the charm at
/// 2.5 times.
///
/// <para><b>The entrance, silently.</b> With Spider-Man hanging it opens the way the
/// overlay does — the rope reeled out while the web spreads along the top edge — and
/// nothing is played: the entrance's sound belongs to the overlay's audio service, which
/// this never touches.</para>
///
/// <para><b>Idle when still.</b> Frames are asked for only while the rope moves, as in
/// the Studio preview: <c>CompositionTarget.Rendering</c> is hooked on a push and unhooked
/// when the rope sleeps. It swings once as the card opens, unless motion is reduced, and a
/// click pushes it again.</para>
/// </remarks>
internal sealed class WelcomeHero : IDisposable
{
    public const double Height = 250;

    private const double CharmSize = 2.5;

    private readonly CanvasControl canvas = new()
    {
        Height = Height,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        ClearColor = Microsoft.UI.Colors.Transparent,
    };

    private readonly OverlaySettings settings;
    private readonly CharmIndex index;
    private CharmArtworkCache? artwork;
    private RopeRenderer? renderer;
    private RopeSimulation? rope;
    private bool animating;
    private bool disposed;
    private TimeSpan lastFrame;

    public WelcomeHero(OverlaySettings settings, CharmIndex index)
    {
        this.settings = settings;
        this.index = index;
        canvas.CreateResources += OnCreateResources;
        canvas.Draw += OnDraw;
        canvas.SizeChanged += (_, _) => Fit();
        canvas.PointerPressed += (_, args) =>
        {
            if (rope is null)
            {
                return;
            }

            rope.Push(args.GetCurrentPoint(canvas).Position.X < canvas.ActualWidth / 2 ? 1 : -1);
            Animate();
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(canvas, "WelcomeHero");
    }

    public FrameworkElement View => canvas;

    private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        artwork?.Dispose();
        artwork = new CharmArtworkCache(sender, CharmArtworkCache.DefaultDirectory);
        renderer = new RopeRenderer(artwork) { Glow = settings.Glow };

        string id = settings.Stack.Places.LastOrDefault().Id ?? CharmCatalog.FirstRunId;
        CharmDescriptor charm = CharmLibrary.Resolve(artwork, index, [new RopeCharm(id, 1)])[0];
        renderer.Charms = [charm];

        RopeMotion motion = Services.SystemMotion.Resolve(settings.Motion);
        rope = new RopeSimulation(style: settings.RopeStyle);
        rope.SetMotion(motion);
        rope.SetCharmStack([charm.Metrics]);
        rope.SetBeads([charm.Beads]);
        rope.Start();
        Fit();
        if (motion != RopeMotion.Reduced && IntroTable.IsSpiderMan(id))
        {
            rope.BeginIntro();
            Animate();
        }
        else if (RopeMotionTable.PhysicsOf(motion).SwingsOnLaunch)
        {
            rope.Push();
            Animate();
        }
    }

    /// <summary>Fits the rope to the canvas's width, anchored at its top edge.</summary>
    private void Fit()
    {
        if (rope is null || canvas.ActualWidth <= 0)
        {
            return;
        }

        rope.Fit(new Hangly.Core.Geometry.Size(canvas.ActualWidth, Height), charmSize: CharmSize, ropeLength: 1);
        if (rope.IntroElapsed is null)
        {
            rope.ResetToHanging();
        }

        canvas.Invalidate();
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (rope is null || renderer is null || artwork is null)
        {
            return;
        }

        renderer.Draw(args.DrawingSession, rope.Snapshot(), rope.Style);
        artwork.EndFrame();
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
        if (rope is null)
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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        StopAnimating();
        artwork?.Dispose();
        artwork = null;
        canvas.RemoveFromVisualTree();
    }
}
