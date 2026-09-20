//
//  AppEnvironment.cs
//  Hangly
//
//  The composition root: every service is built here, once.
//

using Hangly.App.Overlay;
using Hangly.App.Services;
using Hangly.App.Tray;
using Hangly.Core.Models;
using Hangly.Core.Physics;
using Hangly.Core.Settings;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;

namespace Hangly.App;

/// <summary>Builds the entire object graph exactly once and owns every service lifetime.</summary>
/// <remarks>
/// No singletons. Services are constructed here and injected downward, which is what
/// makes the store testable against a throwaway directory and the login-item logic
/// testable without touching the registry.
///
/// <para>The one flow worth reading: a settings change goes through
/// <see cref="SettingsStore.Update"/>, which persists it and raises
/// <see cref="SettingsStore.Changed"/>; the tray menu and the overlay window both listen
/// and react independently. Neither talks to the other. That is the port of the
/// original's Observation stream, with an event standing in for
/// <c>withObservationTracking</c>.</para>
/// </remarks>
public sealed class AppEnvironment : IDisposable
{
    private readonly SettingsStore store;
    private readonly ILaunchAtLogin launchAtLogin;
    private readonly RopeSimulation rope;

    private TrayIcon? tray;
    private OverlayWindow? overlay;
    private CharmArtworkCache? artwork;

    public AppEnvironment(SettingsStore? store = null, ILaunchAtLogin? launchAtLogin = null)
    {
        this.store = store ?? new SettingsStore(SettingsStore.DefaultPath);
        this.launchAtLogin = launchAtLogin ?? new RegistryLaunchAtLogin();

        OverlaySettings settings = this.store.Settings.Overlay;
        rope = new RopeSimulation(
            style: settings.RopeStyle,
            timeProfile: RopeTimeProfileTable.ForDate(DateTimeOffset.Now));
    }

    public void Bootstrap()
    {
        // Launch at login is reconciled rather than trusted: the user can have removed
        // the entry while Hangly was not running, so the stored flag is corrected from
        // the system before anything reads it.
        Diagnostics.Log($"bootstrap starting; settings at {SettingsStore.DefaultPath}");

        bool actuallyEnabled = launchAtLogin.IsEnabled;
        if (actuallyEnabled != store.Settings.LaunchAtLogin)
        {
            store.Update(settings => settings with { LaunchAtLogin = actuallyEnabled });
        }

        // The tray and the overlay are independent surfaces, and a failure in one is not a
        // reason to lose the other. This was learned the direct way: a mistyped P/Invoke in
        // the tray took down the whole app during bootstrap, so the rope never appeared —
        // and the rope is the app. The menu is how you quit and change settings, which
        // matters, but it is recoverable by editing the settings file where a charm that
        // never draws is not recoverable at all.
        try
        {
            tray = new TrayIcon("Hangly")
            {
                MenuBuilder = BuildMenu,
            };
            Diagnostics.Log("tray icon registered");
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("tray icon", exception);
            Diagnostics.Log("carrying on without a tray icon; the overlay still runs");
        }

        store.Changed += OnSettingsChanged;

        if (store.Settings.Overlay.IsEnabled)
        {
            // Not wrapped. If the overlay cannot be created there is nothing left worth
            // running, and the exception carries the reason up to the log.
            ShowOverlay();
        }
        else
        {
            Diagnostics.Log("overlay disabled in settings; tray only");
        }
    }

    private void ShowOverlay()
    {
        if (overlay is not null)
        {
            return;
        }

        CanvasDevice device = CreateDevice();
        Diagnostics.Log("canvas device created");

        artwork = new CharmArtworkCache(device, CharmArtworkCache.DefaultDirectory);
        var renderer = new RopeRenderer(artwork);

        overlay = new OverlayWindow(device, store.Settings.Overlay, rope, renderer, BuiltInCharms.Default);
        Diagnostics.Log("overlay window constructed");

        // Returns as soon as the frame loop is running. The window itself is created on
        // that loop's thread, so "shown" is logged from there rather than here.
        overlay.Begin();
    }

    /// <summary>A Win2D device, in software if the hardware will not give one.</summary>
    /// <remarks>
    /// Win2D wants a Direct3D 11 device, and there are real machines that cannot provide
    /// one: a virtual GPU under a hypervisor, a remote desktop session, a driver that has
    /// just been reset. On those, asking for the shared hardware device throws and takes
    /// the whole app down before anything has been drawn — which looks, to the person who
    /// double-clicked it, exactly like nothing happening.
    ///
    /// <para>The software renderer is slower and entirely adequate for a rope: this is a
    /// few hundred stroked segments, not a game. Falling back is strictly better than
    /// refusing to start, so the failure is logged and the app carries on.</para>
    /// </remarks>
    private static CanvasDevice CreateDevice()
    {
        try
        {
            return CanvasDevice.GetSharedDevice();
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("hardware canvas device", exception);
            Diagnostics.Log("falling back to the software renderer");
            return new CanvasDevice(forceSoftwareRenderer: true);
        }
    }

    private void HideOverlay()
    {
        // Torn down completely rather than hidden, so a disabled overlay costs nothing
        // instead of lingering as an invisible window holding a swapchain.
        overlay?.Close();
        overlay = null;
        artwork?.Dispose();
        artwork = null;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        if (settings.Overlay.IsEnabled)
        {
            ShowOverlay();
            overlay?.Apply(settings.Overlay);
        }
        else
        {
            HideOverlay();
        }
    }

    /// <summary>
    /// The tray menu, rebuilt on every click so a checkmark cannot disagree with the
    /// settings.
    /// </summary>
    private IReadOnlyList<MenuEntry> BuildMenu()
    {
        AppSettings settings = store.Settings;

        var ropes = RopeStyleTable.All
            .Select(style => new MenuEntry(
                RopeStyleTable.DisplayNameOf(style),
                () => store.UpdateOverlay(overlay => overlay with { RopeStyle = style }),
                IsChecked: settings.Overlay.RopeStyle == style))
            .ToList();

        var anchors = Enum.GetValues<OverlayAnchor>()
            .Select(anchor => new MenuEntry(
                OverlayAnchorTable.DisplayNameOf(anchor),
                () => store.UpdateOverlay(overlay => overlay with { Anchor = anchor }),
                IsChecked: settings.Overlay.Anchor == anchor))
            .ToList();

        return
        [
            new MenuEntry(
                settings.Overlay.IsEnabled ? "Hide Charm" : "Show Charm",
                () => store.UpdateOverlay(overlay => overlay with { IsEnabled = !overlay.IsEnabled })),
            MenuEntry.Separator,
            new MenuEntry("Rope", Children: ropes),
            new MenuEntry("Position", Children: anchors),
            MenuEntry.Separator,
            new MenuEntry(
                "Launch at Login",
                () =>
                {
                    bool enabled = !store.Settings.LaunchAtLogin;
                    launchAtLogin.SetEnabled(enabled);
                    store.Update(current => current with { LaunchAtLogin = enabled });
                },
                IsChecked: settings.LaunchAtLogin),
            MenuEntry.Separator,
            new MenuEntry("Quit Hangly", () => Application.Current.Exit()),
        ];
    }

    public void Dispose()
    {
        store.Changed -= OnSettingsChanged;
        HideOverlay();
        tray?.Dispose();
        tray = null;
        GC.SuppressFinalize(this);
    }
}
