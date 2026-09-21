//
//  AppEnvironment.cs
//  Hangly
//
//  The composition root: every service is built here, once.
//

using Hangly.App.Overlay;
using Hangly.App.Services;
using Hangly.App.Analytics;
using Hangly.App.Import;
using Hangly.Core.Import;
using Hangly.App.Tray;
using Hangly.Core.Analytics;
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
    private readonly AnalyticsManager analytics;
    private readonly IAnalyticsProvider analyticsProvider;

    private TrayIcon? tray;
    private OverlayWindow? overlay;

    /// <summary>The overlay, once it exists. Null before bootstrap and after quit.</summary>
    public OverlayWindow? Overlay => overlay;
    private CharmArtworkCache? artwork;
    private IReadOnlyList<string> hanging = [];

    /// <summary>The same rope, with each place's own size, which is what rebuilds it.</summary>
    private IReadOnlyList<RopeCharm> hangingPlaces = [];

    /// <summary>The catalogue plus whatever has been imported. Rebuilt when that changes.</summary>
    private CharmIndex index = new();
    private CustomCharmStore? customCharms;

    /// <summary>
    /// What was last reported, so a change is reported once rather than on every save.
    /// </summary>
    /// <remarks>
    /// The store raises on every write, and a write happens for reasons that are not a
    /// user changing anything — counting the launch is one. Comparing against what was
    /// last reported is what keeps one act one event.
    /// </remarks>
    private OverlaySettings reported = new();
    private Customize.CustomizeWindow? customize;

    public AppEnvironment(SettingsStore? store = null, ILaunchAtLogin? launchAtLogin = null)
    {
        this.store = store ?? new SettingsStore(SettingsStore.DefaultPath);
        this.launchAtLogin = launchAtLogin ?? new RegistryLaunchAtLogin();
        // Not loaded here. Opening the imports folder and parsing its manifest costs
        // every launch about thirty milliseconds, and most launches have nothing in it —
        // a new install certainly does not. It is loaded the moment anything actually
        // needs it: a custom charm on the rope, or the Library being opened.
        if (this.store.Settings.Overlay.Stack.Ids.Any(Hangly.Core.Models.CharmId.IsCustom))
        {
            RebuildIndex();
        }

        // PostHog when this build has a key, nothing when it does not. An app built
        // without one runs with no analytics at all, which is a supported state and the
        // default one.
        analyticsProvider = AppInfo.HasAnalyticsDestination
            ? new PostHogProvider(AppInfo.AnalyticsHost, AppInfo.AnalyticsKey)
            : new NoOpAnalyticsProvider();

        analytics = new AnalyticsManager(
            this.store,
            analyticsProvider,
            AppInfo.AnalyticsHost,
            AppInfo.HasAnalyticsDestination,
            AppInfo.Version,
            AppInfo.BuildNumber,
            AppInfo.WindowsVersion);

        OverlaySettings settings = this.store.Settings.Overlay;
        rope = new RopeSimulation(
            style: settings.RopeStyle,
            timeProfile: RopeTimeProfileTable.ForDate(DateTimeOffset.Now));
    }

    /// <summary>Shows the welcome card when onboarding has not been completed.</summary>
    /// <remarks>
    /// After the overlay, not before it: the card describes a charm hanging from the top
    /// of the screen, and it should be describing one that is already there.
    /// </remarks>
    /// <summary>The version found by the quiet check, if it found one.</summary>
    private UpdateCheck? availableUpdate;

    /// <summary>
    /// The one updater, shared by the quiet check and the About page.
    /// </summary>
    /// <remarks>
    /// Shared rather than made where it is needed, because an <see cref="Updater"/>
    /// remembers what its own check found and can only install that. Two instances would
    /// mean the tray offering an update the About page's Install button knew nothing
    /// about.
    /// </remarks>
    public Updater Updates { get; } = new(AppInfo.UpdateFeedUrl);

    /// <summary>
    /// Looks for an update in the background, once, a little after launch.
    /// </summary>
    /// <remarks>
    /// <b>Quiet by design.</b> Nothing pops up, nothing steals focus and nothing blocks:
    /// the result is one line at the top of the tray menu, where someone will find it
    /// when they are already looking at the menu, and the About page says the same thing
    /// in more detail. An ornament that interrupts you to talk about itself has missed
    /// the point of being an ornament.
    ///
    /// <para>Delayed rather than immediate, because launch is the one moment the app is
    /// already doing everything at once, and a check that finds nothing is worth nothing
    /// to hurry. Every failure is silent — <see cref="Updater"/> never throws — so a
    /// machine with no network simply never hears back.</para>
    /// </remarks>
    private void CheckForUpdateQuietly()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(UpdateCheckDelaySeconds)).ConfigureAwait(false);

                UpdateCheck result = await Updates.CheckAsync().ConfigureAwait(false);
                if (!result.HasUpdate)
                {
                    Diagnostics.Log($"update check: {result.Message}");
                    return;
                }

                availableUpdate = result;
                Diagnostics.Log($"update available: {result.Version}");

                // Nothing has to be told. The tray rebuilds its menu from scratch every
                // time it is opened, so the new line is simply there the next time
                // someone looks.
            }
            catch (Exception exception)
            {
                Diagnostics.Log($"quiet update check failed: {exception.GetType().Name}");
            }
        });
    }

    /// <summary>How long after launch the quiet check runs.</summary>
    private const int UpdateCheckDelaySeconds = 20;

    public void ShowWelcomeIfNeeded()
    {
        if (!Onboarding.WelcomeWindow.IsNeeded(store.Settings))
        {
            ShowFollowIfDue();
            return;
        }

        var welcome = new Onboarding.WelcomeWindow(store, OpenCustomize);

        // Hidden rather than closed when it is dismissed, so the app is still running
        // afterwards. See ProcessLifetime.
        Onboarding.ProcessLifetime.KeepAlive(welcome);

        // The follow card waits for onboarding to finish rather than racing it: IsDue
        // refuses while a name is still owed, so asking again once the welcome window
        // closes is what gets the order right on a first run.
        welcome.Closed += (_, _) => ShowFollowIfDue();
        welcome.Activate();
        Diagnostics.Log("welcome card shown");
    }

    /// <summary>Reopens the welcome card on demand, from the About page.</summary>
    public void ShowWelcomeAgain()
    {
        var welcome = new Onboarding.WelcomeWindow(store, OpenCustomize);
        Onboarding.ProcessLifetime.KeepAlive(welcome);
        welcome.SkipToWelcome();
        welcome.Activate();
        Diagnostics.Log("welcome card reopened from About");
    }

    /// <summary>Shows the follow card when it is due.</summary>
    public void ShowFollowIfDue()
    {
        if (!Onboarding.FollowPrompt.IsDue(store.Settings))
        {
            return;
        }

        var prompt = new Onboarding.FollowPrompt(store, analytics);
        Onboarding.ProcessLifetime.KeepAlive(prompt);
        prompt.Activate();
        prompt.Shown();
        Diagnostics.Log("follow card shown");
    }

    public void Bootstrap()
    {
        // Launch at login is reconciled rather than trusted: the user can have removed
        // the entry while Hangly was not running, so the stored flag is corrected from
        // the system before anything reads it.
        Diagnostics.Log($"bootstrap starting; settings at {SettingsStore.DefaultPath}");

        // On a first run, switch it on rather than merely defaulting the flag to true.
        //
        // Reconciliation below reads the registry and corrects the stored flag from it, so
        // a default of true with no registry entry would be turned back to false on the
        // very next line -- the flag follows the system, not the other way round. Somebody
        // who turns it off later has seen the welcome card, so this cannot undo their
        // choice.
        //
        // Default on because Hangly is a desktop ornament: an ornament that has to be
        // started by hand every morning is one that gets started once.
        if (Onboarding.WelcomeWindow.IsNeeded(store.Settings) && !launchAtLogin.IsEnabled)
        {
            launchAtLogin.SetEnabled(true);
            Diagnostics.Log($"first run: launch at login switched on ({launchAtLogin.IsEnabled})");
        }

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

        // After the tray and before the overlay: starting it counts the launch, which
        // the follow card is scheduled off and which has nothing to do with whether
        // anything is sent.
        reported = store.Settings.Overlay;
        analytics.Start();
        Diagnostics.Log(
            $"analytics {(analytics.IsEnabled ? "on" : "off")}; {analytics.Connection.Summary}");

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

        // Last, and on its own thread, so nothing above waits on a network call.
        CheckForUpdateQuietly();
    }

    /// <summary>Reports charms coming and going, and the count changing.</summary>
    private void ReportCharmChange(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        if (before.Count != after.Count)
        {
            analytics.Track(Events.RopeCountChanged(after.Count));
        }

        foreach (string id in after.Except(before, StringComparer.Ordinal))
        {
            analytics.Track(Events.CharmSelected(id));
            analytics.Track(Events.CharmAdded(id));
        }

        foreach (string id in before.Except(after, StringComparer.Ordinal))
        {
            analytics.Track(Events.CharmRemoved(id));
        }
    }

    /// <summary>
    /// Reports which setting moved, and never what it moved to.
    /// </summary>
    /// <remarks>
    /// The name of the setting is the whole payload. A value here would describe the
    /// person's screen — how large their charm is, where it sits — which PRIVACY.md says
    /// is never sent.
    /// </remarks>
    private void ReportAppearanceChanges(OverlaySettings before, OverlaySettings after)
    {
        foreach ((string name, bool changed) in (ReadOnlySpan<(string, bool)>)
        [
            ("charm_size", before.CharmSize != after.CharmSize),
            ("rope_length", before.RopeLength != after.RopeLength),
            ("opacity", before.Opacity != after.Opacity),
            ("anchor", before.Anchor != after.Anchor),
            ("overlay_visible", before.IsEnabled != after.IsEnabled),
        ])
        {
            if (changed)
            {
                analytics.Track(Events.AppearanceChanged(name));
            }
        }
    }

    /// <summary>
    /// Rebuilds the index from what is on disk, dropping imports whose drawing has gone.
    /// </summary>
    /// <summary>The imports, opening the folder the first time anything asks.</summary>
    /// <summary>The imported charms, shared by every surface that can add or remove one.</summary>
    public CustomCharmStore CustomCharmsStore =>
        customCharms ??= new CustomCharmStore(CustomCharmStore.DefaultDirectory);

    /// <summary>Re-reads the imports after something added one.</summary>
    /// <remarks>
    /// The Create page imports directly rather than through <see cref="ImportCharm"/>,
    /// because it supplies its own name; this is the part of that method it still needs.
    /// </remarks>
    public void CharmsChanged() => RebuildIndex();

    private void RebuildIndex()
    {
        CustomCharmStore charms = CustomCharmsStore;
        var projected = new List<Hangly.Core.Models.CharmCatalogEntry>(charms.Entries.Count);
        foreach (CustomCharmEntry entry in charms.Entries)
        {
            // An entry whose manifest does not name a plain file has no drawing as far as
            // this build is concerned. It resolves to the placeholder rather than to
            // whatever the name was pointing at.
            if (charms.PathFor(entry) is string file)
            {
                projected.Add(entry.AsCatalogEntry(file));
            }
            else
            {
                Diagnostics.Log($"import '{entry.Id}' names a file it should not; ignoring it");
            }
        }

        index = new CharmIndex(projected);
    }

    /// <summary>
    /// Imports a drawing, puts it on the cord, and tells the Library to show it.
    /// </summary>
    /// <remarks>
    /// The two events fire in the macOS build's order and for its reasons: charm_imported
    /// once the file has been read and accepted, charm_saved once it is stored. Neither
    /// carries the file, its name or its size.
    /// </remarks>
    /// <summary>A file was dropped on a charm: import it and put it in that place.</summary>
    /// <remarks>
    /// <b>Off the frame loop.</b> This is raised from the thread that owns the overlay
    /// window, and importing rasterises an SVG and writes two files; doing that inline
    /// would stall the rope for as long as it took. The work is handed to the thread pool
    /// and only the settings write comes back, which the store already serialises.
    ///
    /// <para><b>The place, not the charm.</b> Replacing through the stack is what keeps
    /// everything else true: the place keeps its size, the rope keeps its order, and
    /// favourites and recents are untouched except for the recent entry the import earns.
    /// Dropping on the middle of three replaces the middle of three.</para>
    /// </remarks>
    private void OnFileDroppedOnCharm(int slot, string path)
    {
        _ = Task.Run(() =>
        {
            try
            {
                long size = 0;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch (IOException)
                {
                    // The size is a bucket on one event. Not worth failing an import for.
                }

                // The extension and a size bucket, which is all this event has ever
                // carried: never the path, never the name, never the contents.
                analytics.Track(Events.AirdropFileDropped(Path.GetExtension(path), size));

                ImportOutcome outcome = ImportCharm(path);
                if (!outcome.IsAccepted || outcome.Entry is null)
                {
                    Diagnostics.Log($"drop refused: {outcome.Message}");
                    return;
                }

                string id = Hangly.Core.Models.CharmId.ForCustom(outcome.Entry.Id);
                store.Update(settings => settings with
                {
                    Overlay = settings.Overlay.WithStack(settings.Overlay.Stack.WithCharm(slot, id)),
                    Library = settings.Library.WithRecent(id),
                });

                Diagnostics.Log($"dropped charm went into place {slot}");
            }
            catch (Exception exception)
            {
                Diagnostics.Failure("drop import", exception);
            }
        });
    }

    public ImportOutcome ImportCharm(string path)
    {
        ImportOutcome outcome = CharmImporter.Import(path, CustomCharmsStore);
        if (!outcome.IsAccepted || outcome.Entry is null)
        {
            Diagnostics.Log($"import refused: {outcome.Message}");
            return outcome;
        }

        analytics.Track(Events.CharmImported);
        analytics.Track(Events.CharmSaved);
        RebuildIndex();

        Diagnostics.Log($"imported a charm; {CustomCharmsStore.Entries.Count} now");
        return outcome;
    }

    /// <summary>
    /// Deletes an import, and takes it off the rope and out of the Library with it.
    /// </summary>
    /// <remarks>
    /// Wherever it was hanging the bead takes its place — in every place at once if the
    /// same import was on the rope more than once — which is what the macOS build does.
    /// </remarks>
    public void DeleteCharm(Guid id)
    {
        string charmId = Hangly.Core.Models.CharmId.ForCustom(id);
        CustomCharmsStore.Remove(id);
        RebuildIndex();

        store.Update(settings => settings with
        {
            // Every place, hanging or not. A place that is put away still names a
            // charm, and a deleted import that survived there would come back the next
            // time the count grew.
            Overlay = settings.Overlay.WithStack(
                settings.Overlay.Stack.Replacing(charmId, Hangly.Core.Models.CharmCatalog.DefaultId)),
            Library = settings.Library with
            {
                FavouriteCharmIds = [.. settings.Library.FavouriteCharmIds.Where(existing => existing != charmId)],
                RecentCharmIds = [.. settings.Library.RecentCharmIds.Where(existing => existing != charmId)],
            },
        });

        analytics.Track(Events.CharmRemoved(charmId));
    }

    /// <summary>
    /// Everything the app can hang, for the Library to show.
    /// </summary>
    /// <remarks>
    /// Asking for this is what loads the imports, if a charm on the rope has not already.
    /// The Library is the first thing that asks, which is the right moment.
    /// </remarks>
    public CharmIndex Charms
    {
        get
        {
            if (customCharms is null)
            {
                RebuildIndex();
            }

            return index;
        }
    }

    public CustomCharmStore CustomCharms => CustomCharmsStore;

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

        hangingPlaces = [.. store.Settings.Overlay.Stack.Places];
        hanging = [.. hangingPlaces.Select(place => place.Id)];
        overlay = new OverlayWindow(
            device,
            store.Settings.Overlay,
            rope,
            renderer,
            CharmLibrary.Resolve(artwork, index, hangingPlaces));

        overlay.DragEntered += () => analytics.Track(Events.AirdropDragEntered);
        overlay.FileDropped += OnFileDroppedOnCharm;
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

    /// <summary>
    /// Opens the Customize window, or brings the open one forward.
    /// </summary>
    /// <remarks>
    /// Called from the tray menu, which runs on the thread that owns the XAML
    /// application — the same thread a <c>Window</c> has to be created on.
    /// </remarks>
    private void OpenCustomize()
    {
        try
        {
            // Built once and kept. It hides on close rather than closing, so there is
            // nothing to rebuild and the window comes back where it was left.
            if (customize is null)
            {
                customize = new Customize.CustomizeWindow(store, launchAtLogin, analytics, this);
                Diagnostics.Log("customize window created");
            }

            customize.AppWindow.Show();
            customize.Activate();
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("customize window", exception);
        }
    }

    /// <summary>Opens Customize on the About page, showing the update that was found.</summary>
    private void OpenUpdates()
    {
        OpenCustomize();
        if (availableUpdate is { } found)
        {
            customize?.ShowUpdates(found);
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
        if (!settings.Overlay.IsEnabled)
        {
            HideOverlay();
            hanging = [];
            return;
        }

        ShowOverlay();

        // Rebuilding the charms means measuring artwork, so it happens only when the
        // charms actually changed rather than on every slider move.
        // Rebuilt when the places change in any way that changes what is drawn: which
        // charm is in a place, how many places hang, or how large a place is. A size is
        // part of the metrics the solver is given, so it cannot be applied without
        // rebuilding, and it is cheap to notice here rather than measuring artwork again
        // on every slider move.
        IReadOnlyList<RopeCharm> places = settings.Overlay.Stack.Places;
        if (artwork is not null && !hangingPlaces.SequenceEqual(places))
        {
            IReadOnlyList<string> ids = [.. places.Select(place => place.Id)];
            if (!hanging.SequenceEqual(ids, StringComparer.Ordinal))
            {
                ReportCharmChange(hanging, ids);
            }

            hangingPlaces = [.. places];
            hanging = [.. ids];
            overlay?.SetCharms(CharmLibrary.Resolve(artwork, index, hangingPlaces));
        }

        if (reported.RopeStyle != settings.Overlay.RopeStyle)
        {
            analytics.Track(Events.RopeStyleChanged(settings.Overlay.RopeStyle));
        }

        ReportAppearanceChanges(reported, settings.Overlay);
        reported = settings.Overlay;

        overlay?.Apply(settings.Overlay);
    }

    /// <summary>
    /// The tray menu, rebuilt on every click so a checkmark cannot disagree with the
    /// settings.
    /// </summary>
    /// <remarks>
    /// There is no charm picker here any more. An eighty-one item submenu was a stopgap
    /// while the Library did not exist; it does now, and a list of eighty-one things in a
    /// tray menu is a list rather than a way to choose. Rope and Position stay, because
    /// they are short and genuinely quicker than opening a window for.
    /// </remarks>
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

        // An update that has been found gets one line at the top, and only then. A menu
        // item that is always there saying "no updates" is a menu item nobody reads.
        List<MenuEntry> update = availableUpdate is { HasUpdate: true } newer
            ?
            [
                new MenuEntry($"Update to {newer.Version}…", OpenUpdates),
                MenuEntry.Separator,
            ]
            : [];

        return
        [
            .. update,
            new MenuEntry(
                settings.Overlay.IsEnabled ? "Hide Charm" : "Show Charm",
                () => store.UpdateOverlay(overlay => overlay with { IsEnabled = !overlay.IsEnabled })),
            MenuEntry.Separator,
            new MenuEntry("Customize…", OpenCustomize),
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
            new MenuEntry("Quit Hangly", Quit),
        ];
    }

    /// <summary>
    /// Quits for real, which means letting the one window that refuses to close, close.
    /// </summary>
    private void Quit()
    {
        // Said before the window goes, so the goodbye is sent while there is still a
        // process to send it from.
        analytics.Stop();
        customize?.AllowClose();
        customize = null;
        Onboarding.ProcessLifetime.Release();
        Application.Current.Exit();
    }

    public void Dispose()
    {
        store.Changed -= OnSettingsChanged;
        (analyticsProvider as IDisposable)?.Dispose();
        HideOverlay();
        tray?.Dispose();
        tray = null;
        GC.SuppressFinalize(this);
    }
}
