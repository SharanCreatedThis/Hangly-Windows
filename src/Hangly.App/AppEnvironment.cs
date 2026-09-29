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
    private readonly IUserAnalyticsService analytics;

    private TrayIcon? tray;
    private OverlayWindow? overlay;

    /// <summary>The overlay, once it exists. Null before bootstrap and after quit.</summary>
    public OverlayWindow? Overlay => overlay;
    private CharmArtworkCache? artwork;

    /// <summary>Sound effects. Holds nothing open between sounds; see <see cref="Audio.AudioService"/>.</summary>
    private Audio.AudioService? audio;
    private IReadOnlyList<string> hanging = [];

    /// <summary>The same rope, with each place's own size, which is what rebuilds it.</summary>
    private IReadOnlyList<RopeCharm> hangingPlaces = [];

    /// <summary>The catalogue plus whatever has been imported. Rebuilt when that changes.</summary>
    private CharmIndex index = new();
    private CustomCharmStore? customCharms;

    private Customize.CustomizeWindow? customize;

    private readonly Hangly.Core.Registry.RegistrySync registry;

    /// <summary>The XAML thread's queue, for things the overlay's thread asks the windows to do.</summary>
    private Microsoft.UI.Dispatching.DispatcherQueue? xamlQueue;

    /// <summary>This launch is Velopack starting the new version after an update: it opens nothing but the release notes and is not counted.</summary>
    private readonly bool updatedLaunch = Environment.GetCommandLineArgs()
        .Skip(1)
        .Contains(Hangly.Core.Lifecycle.LaunchIntent.UpdatedArgument, StringComparer.OrdinalIgnoreCase);

    /// <summary>When this process started, for <see cref="Hangly.Core.Lifecycle.UpdateTiming.SettleAfterLaunch"/>.</summary>
    private readonly long startedAt = Environment.TickCount64;

    /// <summary>This launch is the first of a new feature version after an update: it opens the release notes.</summary>
    private bool releaseNotesDue;

    /// <summary>Looks, once a minute, for the moment to install a downloaded update.</summary>
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? updateWait;

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

        // Google Analytics when this build has its stream, nothing when it does not. A build
        // without it runs with no analytics at all, which is a supported state and the
        // default one.
        analytics = (IUserAnalyticsService?)Ga4UserAnalytics.For(AppInfo.Ga4MeasurementId, AppInfo.Ga4ApiSecret)
            ?? new NoOpUserAnalytics();
        HanglyAnalytics.Use(analytics);

        // A first identify made offline goes as soon as the network comes back, not on
        // the next launch. The manager ignores this when nothing is pending, which is
        // nearly always, and marshals it onto this thread itself.
        // The installation registry: this installation's one document, kept up to date. No
        // client when the build has no registry URL, which is every build but a release.
        registry = new Hangly.Core.Registry.RegistrySync(
            new Hangly.Core.Registry.InstallationStore(),
            Installation.HttpRegistryClient.For(AppInfo.RegistryUrl),
            this.store,
            new Hangly.Core.Registry.PlatformFacts(AppInfo.WindowsVersion, AppInfo.Version, AppInfo.Architecture),
            analytics: analytics,
            crashes: CrashReporter.Pending,
            installEvidence: () => Hangly.Core.Registry.InstallEvidence.Earliest(Hangly.Core.Registry.InstallEvidence.DefaultFolders));

        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

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
    /// Keeps Hangly up to date without a word: checks a little after launch and once a
    /// day after that, downloads whatever it finds, and installs it at the first moment
    /// nobody is looking (<see cref="WaitForQuietMoment"/>) — or at the next Quit or start.
    /// </summary>
    /// <remarks>
    /// <b>Silent by design.</b> Nothing pops up, nothing steals focus and nothing blocks.
    /// Hangly starts at sign-in and is rarely quit, so a check made only at launch could
    /// leave somebody a release behind for weeks; hence the daily repeat. Once a package is
    /// downloaded the tray gains one line, "Restart to update", for anybody who would
    /// rather not wait — and otherwise the next restart, or the next Quit, applies it.
    ///
    /// <para>Every failure is silent — <see cref="Updater"/> never throws — so a machine
    /// with no network simply tries again tomorrow.</para>
    /// </remarks>
    private void CheckForUpdateQuietly()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(UpdateCheckDelaySeconds)).ConfigureAwait(false);
            while (true)
            {
                try
                {
                    UpdateCheck result = await Updates.CheckAsync().ConfigureAwait(false);
                    Diagnostics.Log(result.HasUpdate ? $"update available: {result.Version}" : $"update check: {result.Message}");
                    if (result.HasUpdate)
                    {
                        availableUpdate = result;
                        if (await Updates.DownloadAsync().ConfigureAwait(false))
                        {
                            // Nothing more to check for until this one is applied.
                            xamlQueue?.TryEnqueue(WaitForQuietMoment);
                            return;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Diagnostics.Log($"quiet update check failed: {exception.GetType().Name}");
                }

                await Task.Delay(UpdateCheckInterval).ConfigureAwait(false);
            }
        });
    }

    /// <summary>Installs the downloaded update once <see cref="Hangly.Core.Lifecycle.UpdateTiming"/> says nobody would see it.</summary>
    /// <remarks>
    /// Hangly starts at sign-in and is rarely quit, so waiting for a Quit could leave somebody on an old version for
    /// weeks. Instead the update goes in while they are away — locked, or a screen saver up — and never with a Hangly
    /// window open. The restart opens only the release notes, waiting for them in the middle of the screen, and no
    /// card; the entrance plays as on any launch when Spider-Man is on the rope. Runs on the XAML thread, which owns
    /// the windows it asks about.
    /// </remarks>
    private void WaitForQuietMoment()
    {
        if (updateWait is not null || xamlQueue is null)
        {
            return;
        }

        updateWait = xamlQueue.CreateTimer();
        updateWait.Interval = Hangly.Core.Lifecycle.UpdateTiming.CheckEvery;
        updateWait.Tick += (timer, _) =>
        {
            bool windowOpen = customize?.AppWindow.IsVisible == true || Onboarding.ProcessLifetime.AnyVisible;
            Hangly.Core.Lifecycle.UpdateMoment moment = UpdateMomentReader.Read(
                TimeSpan.FromMilliseconds(Environment.TickCount64 - startedAt), windowOpen);
            if (!Hangly.Core.Lifecycle.UpdateTiming.ShouldInstall(moment))
            {
                return;
            }

            timer.Stop();
            Diagnostics.Log($"away and idle {moment.Idle.TotalMinutes:F0} min; installing the update");
            if (Updates.ApplyQuietlyAndRestart())
            {
                Exit();
            }
        };
        updateWait.Start();
        Diagnostics.Log("update downloaded; waiting for a quiet moment to install it");
    }

    /// <summary>How long after launch the first check runs.</summary>
    private const int UpdateCheckDelaySeconds = 20;

    /// <summary>How often a running Hangly checks again.</summary>
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Hangly was opened again while running: the Library, in front — or the welcome card,
    /// while it is still up, because it may be asking for a name. The macOS rule.
    /// </summary>
    /// <remarks>
    /// Listened for from launch; the signal arrives on the thread pool and is carried to
    /// the XAML thread, which owns every window here. Opening the Library reuses the one
    /// Customize window, so relaunching repeatedly never makes a second.
    /// </remarks>
    public void ListenForRelaunch()
    {
        Microsoft.UI.Dispatching.DispatcherQueue queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        xamlQueue ??= queue;
        Services.SingleInstance.ListenForRelaunch(() => queue.TryEnqueue(OnRelaunched));
    }

    private void OnRelaunched()
    {
        if (activeWelcome is { } welcome && welcome.AppWindow.IsVisible)
        {
            Diagnostics.Log("relaunched while the welcome card is up; bringing it forward");
            Interop.WindowPlacement.BringToFront(welcome);
            return;
        }

        Diagnostics.Log("relaunched; opening the Library");
        OpenLibrary();
    }

    /// <summary>Whether this launch came from a sign-in entry written before <c>--login</c>.</summary>
    private bool legacyLoginEntry;

    /// <summary>This launch is the first of a new version (<see cref="NoteVersion"/>).</summary>
    private bool firstLaunchOfUpdate;

    /// <summary>Where this launch lands, once onboarding and the tray are settled. See LaunchIntent.</summary>
    /// <remarks>
    /// Called after <see cref="ShowWelcomeIfNeeded"/>: a first launch has already put the
    /// welcome card up and this does nothing more. A returning person who opened Hangly
    /// gets the Library; a sign-in or update start gets nothing but the charm.
    /// </remarks>
    public void OpenForLaunch(IReadOnlyCollection<string> arguments)
    {
        Hangly.Core.Lifecycle.LaunchDestination destination = Hangly.Core.Lifecycle.LaunchIntent.Decide(
            arguments,
            Onboarding.WelcomeWindow.IsNeeded(store.Settings),
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            legacyLoginEntry);
        Diagnostics.Log($"launch: {destination}{(arguments.Count > 0 ? " (" + string.Join(' ', arguments) + ")" : string.Empty)}");
        // An update launch is the first launch of a new version, whoever started it: 0.9.x's updater restarts the app
        // without the argument 2.1's passes.
        analytics.Log(AnalyticsEvent.AppLaunch(
            atLogin: arguments.Contains("--login", StringComparer.Ordinal) || legacyLoginEntry,
            afterUpdate: updatedLaunch || firstLaunchOfUpdate));
        // The release notes stand in for the Library on the launch they appear: one window, not two.
        if (destination == Hangly.Core.Lifecycle.LaunchDestination.Library && !releaseNotesDue)
        {
            OpenLibrary();
        }
    }

    /// <summary>The welcome card most recently shown, which hides rather than closes.</summary>
    private Onboarding.WelcomeWindow? activeWelcome;

    public void ShowWelcomeIfNeeded()
    {
        // After an update to a new feature version, the release notes and nothing else: not the Enjoying Hangly card
        // as well. The restart after an update never asks for a name either; a later launch does.
        if (releaseNotesDue && (updatedLaunch || !Onboarding.WelcomeWindow.IsNeeded(store.Settings)))
        {
            var notes = new Onboarding.ReleaseNotesWindow();
            Onboarding.ProcessLifetime.KeepAlive(notes);
            notes.Activate();
            Diagnostics.Log($"release notes shown for {AppInfo.Version}");
            return;
        }

        if (updatedLaunch)
        {
            Diagnostics.Log("restarted by an update: no welcome or card this launch");
            return;
        }

        if (!Onboarding.WelcomeWindow.IsNeeded(store.Settings))
        {
            ShowFollowIfDue();
            return;
        }

        // Quit is passed only on this route. The About page's "Show welcome again" opens
        // the same card for somebody who already has a name, and closing that one must
        // not take the app with it.
        var welcome = new Onboarding.WelcomeWindow(store, OpenLibrary, Quit, index);
        activeWelcome = welcome;

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
        var welcome = new Onboarding.WelcomeWindow(store, OpenLibrary);
        activeWelcome = welcome;
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

        var prompt = new Onboarding.FollowPrompt(store);
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

        // Before the overlay reads the rope, and before the launch is counted: a launch
        // count of zero is how a new install is told from somebody updating. Marked done
        // at the end of bootstrap, once the overlay is up.
        bool introduced = false;
        bool gaveBack = false;
        store.Update(settings =>
        {
            (AppSettings advanced, gaveBack) = EntranceIntroduction.Advance(settings);
            (AppSettings next, introduced) = EntranceIntroduction.Apply(advanced);
            return next;
        });
        if (gaveBack)
        {
            // Before the overlay reads it. The rope is not part of it, so the cord built above stands.
            Diagnostics.Log("the look lent for the Spider-Man introduction is given back");
        }

        if (introduced)
        {
            // The rope was built in the constructor, before this ran, on the old cord.
            rope.SetStyle(store.Settings.Overlay.RopeStyle);
            Diagnostics.Log("Spider-Man lent for two launches, on Spider Thread, to introduce the entrance");
        }

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
        // Read before the repair below rewrites it: an old-style entry is the one case where a
        // sign-in launch cannot say so, and LaunchIntent needs to know it was there.
        legacyLoginEntry = launchAtLogin.HasLegacyEntry;

        if (Onboarding.WelcomeWindow.IsNeeded(store.Settings) && !launchAtLogin.IsEnabled)
        {
            launchAtLogin.SetEnabled(true);
            Diagnostics.Log($"first run: launch at login switched on ({launchAtLogin.IsEnabled})");
        }

        // An entry naming another copy is this person's choice to start Hangly, pointing at
        // the wrong file. Honour the choice and fix the file, rather than read it as off.
        if (launchAtLogin.IsStale && store.Settings.LaunchAtLogin)
        {
            launchAtLogin.SetEnabled(true);
            Diagnostics.Log($"launch at login pointed at another copy; repointed ({launchAtLogin.IsEnabled})");
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

        // After the tray and before the overlay. The support card is scheduled off this count, which the quiet
        // restart after an update is not: nobody started Hangly.
        if (!updatedLaunch)
        {
            Hangly.Core.Lifecycle.LaunchCounter.Count(store);
        }
        NoteVersion();
        NoteChoices(store.Settings.Overlay);

        // Creates the installation's permanent ID on its first launch; sends the record once
        // there is a nickname to send, and any crash reports waiting from the last run. Not
        // awaited — the registry is never on the path to a charm.
        _ = registry.Start();
        Diagnostics.Log($"registry {(registry.IsConfigured ? "configured" : "not configured in this build")}");

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

        // The launch got this far, so the Spider-Man introduction has happened: never again.
        if (!store.Settings.Milestones.SpiderManIntroCompleted)
        {
            store.Update(EntranceIntroduction.Complete);
        }

        // Last, and on its own thread, so nothing above waits on a network call.
        CheckForUpdateQuietly();
        StartAuditCycle();
        OpenAuditPage();
    }

    /// <summary>
    /// For screenshots and memory audits only: with <c>HANGLY_AUDIT_OPEN</c> set to a
    /// Customize section — charms, create, appearance, about — opens it at launch, so a
    /// page can be captured without anybody driving the tray. Inert when unset. The macOS
    /// build's equivalent is its <c>com.hangly.audit.*</c> notifications.
    /// </summary>
    private void OpenAuditPage()
    {
        if (Environment.GetEnvironmentVariable("HANGLY_AUDIT_OPEN") is not { Length: > 0 } section)
        {
            return;
        }

        OpenCustomize();
        customize?.ShowSectionNamed(section);
        Diagnostics.Log($"audit: opened {section}");

        // With HANGLY_AUDIT_STUDIO naming a picture, it is opened in the Studio as a drop would.
        if (Environment.GetEnvironmentVariable("HANGLY_AUDIT_STUDIO") is { Length: > 0 } picture)
        {
            customize?.OpenInStudio(picture, null);
            Diagnostics.Log("audit: opened a picture in the Studio");
        }
    }

    /// <summary>
    /// For the memory audit only: with <c>HANGLY_AUDIT_CYCLE</c> set to a number of seconds,
    /// hangs a different set of one to three charms that often, forty times, walking the
    /// whole catalogue, then stops — so memory can be read across dozens of charm changes
    /// without anybody driving the tray. Inert when the variable is not set, which is always
    /// outside a measurement. The macOS build's equivalent is its audit notifications.
    /// </summary>
    private void StartAuditCycle()
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("HANGLY_AUDIT_CYCLE"), out int seconds) || seconds <= 0)
        {
            return;
        }

        IReadOnlyList<CharmCatalogEntry> all = CharmCatalog.All;
        Microsoft.UI.Dispatching.DispatcherQueueTimer timer =
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(seconds);
        int step = 0;
        timer.Tick += (_, _) =>
        {
            int count = (step % 3) + 1;
            List<string> ids = [.. Enumerable.Range(0, count).Select(offset => all[((step * 3) + offset) % all.Count].Id)];
            store.UpdateOverlay(overlay => overlay.WithStack(CharmStackState.Of(ids)));
            Diagnostics.Log($"audit cycle {step + 1}/40: {string.Join(", ", ids)}");
            if (++step >= 40)
            {
                timer.Stop();
            }
        };
        timer.Start();
        Diagnostics.Log($"audit cycle: every {seconds} s");
    }

    /// <summary>
    /// Puts the rope on the time of day it is now, and sets one timer for the next change.
    /// </summary>
    /// <remarks>
    /// The time of day used to be read once, at launch, so a PC left on overnight kept the
    /// morning's rope all day. Now a single one-shot timer is armed for the next boundary —
    /// 05:00, 12:00 or 18:00, three wakeups a day — and re-armed each time it fires, and
    /// again when the clock jumps (changed, or woken from sleep), because a timer set for
    /// noon is not to be trusted across either. Nothing polls. The same rule as the macOS
    /// <c>AmbientCoordinator</c>, which reads the clock on its frame loop instead.
    /// </remarks>
    public void FollowTheClock()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        Hangly.Core.Models.RopeTimeProfile profile = Hangly.Core.Models.RopeTimeProfileTable.ForDate(now);
        if (overlay is { } running)
        {
            running.SetTimeProfile(profile);
        }
        else
        {
            // No frame loop to hand it to; the rope is not being stepped, so it is safe here.
            rope.SetTimeProfile(profile);
        }

        timeOfDayTimer ??= Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        timeOfDayTimer.Stop();
        timeOfDayTimer.IsRepeating = false;
        TimeSpan wait = Hangly.Core.Models.RopeTimeProfileTable.NextBoundary(now) - now;
        timeOfDayTimer.Interval = wait + TimeSpan.FromSeconds(1);
        timeOfDayTimer.Tick -= OnTimeOfDayTick;
        timeOfDayTimer.Tick += OnTimeOfDayTick;
        timeOfDayTimer.Start();
        Diagnostics.Log($"time of day: {profile}; next change in {wait:hh\\:mm\\:ss}");
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? timeOfDayTimer;

    private void OnTimeOfDayTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args) => FollowTheClock();

    /// <summary>Saves the swings the overlay has counted into "Swings survived".</summary>
    /// <remarks>
    /// The overlay counts on its frame loop and hands the count over here: when About is
    /// read, every five minutes at most while the charm is swinging, when the charm is
    /// hidden, and at quit. It used to hand over only when About was opened, so the swings
    /// of any session that ended without a look at About were lost. Taking the count zeroes
    /// the overlay's, so nothing is saved twice.
    /// </remarks>
    public void BankSwings()
    {
        long swung = overlay?.TakeSwings() ?? 0;
        if (swung > 0)
        {
            store.Update(settings => settings with
            {
                Milestones = settings.Milestones with { SwingsSurvived = settings.Milestones.SwingsSurvived + swung },
            });
        }
    }

    /// <summary>Hangs one charm, alone, from the tray's favourites list.</summary>
    /// <remarks>
    /// Replaces the rope rather than adding to it. The menu has no way to say which place
    /// on the cord somebody meant, and a favourite picked from a menu is somebody saying
    /// "that one" rather than "that one as well".
    /// </remarks>
    private void HangOnly(string id)
    {
        // Counted and remembered like any other hang; it used to be neither.
        store.Update(settings => Hanging.HangAlone(settings, id));
        Diagnostics.Log($"tray: hung '{id}' from favourites");
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
    /// <summary>A picture was dropped on a charm: open it in Creator Studio, for that place.</summary>
    /// <remarks>
    /// <b>The Studio, not a silent import</b> (decision D7): dropping is the primary way in,
    /// so it opens the Studio with the picture in it — background removed, subject found —
    /// and Save with "Use on rope" puts the charm in the place it was dropped on. Dropping
    /// on the middle of three still replaces the middle of three; it just shows you the
    /// charm first. macOS does the same.
    ///
    /// <para>Raised on the overlay's own thread; the window is XAML and is opened on the
    /// thread that owns XAML.</para>
    /// </remarks>
    private void OnFileDroppedOnCharm(int slot, string path)
    {
        Diagnostics.Log($"picture dropped on place {slot}; opening the Studio");
        if (xamlQueue?.TryEnqueue(() => OpenStudio(path, slot)) != true)
        {
            Diagnostics.Log("drop could not reach the UI thread");
        }
    }

    /// <summary>Opens Customize on Create with a picture in the Studio.</summary>
    public void OpenStudio(string path, int? slot)
    {
        OpenCustomize();
        customize?.OpenInStudio(path, slot);
    }

    public ImportOutcome ImportCharm(string path)
    {
        ImportOutcome outcome = CharmImporter.Import(path, CustomCharmsStore);
        if (!outcome.IsAccepted || outcome.Entry is null)
        {
            Diagnostics.Log($"import refused: {outcome.Message}");
            return outcome;
        }

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
        audio ??= new Audio.AudioService(store);
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
            CharmLibrary.Resolve(artwork, index, hangingPlaces),
            audio);

        xamlQueue ??= Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        overlay.FileDropped += OnFileDroppedOnCharm;
        overlay.SwingsToBank += () => xamlQueue?.TryEnqueue(BankSwings);
        overlay.ClockChanged += () => xamlQueue?.TryEnqueue(FollowTheClock);
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
        // Hangly is not usable until it has a name: while the welcome card is waiting for
        // one, the Library, Create and the rest bring the card forward instead. As on macOS.
        if (activeWelcome is { } waiting && waiting.AppWindow.IsVisible && Onboarding.WelcomeWindow.IsNeeded(store.Settings))
        {
            Interop.WindowPlacement.BringToFront(waiting);
            return;
        }

        try
        {
            // Built once and kept. It hides on close rather than closing, so there is
            // nothing to rebuild.
            bool created = customize is null;
            customize ??= new Customize.CustomizeWindow(store, launchAtLogin, registry, this);
            if (created)
            {
                Diagnostics.Log("customize window created");
            }

            // Every Hangly window opens in the middle of the display the pointer is on,
            // every time, as on macOS. One already open, or minimised, is being brought
            // back rather than opened, and stays where it is.
            IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(customize);
            if (!created && !customize.AppWindow.IsVisible && !Interop.NativeMethods.IsIconic(handle))
            {
                customize.CentreForOpening();
            }

            customize.AppWindow.Show();

            // Show does not lift a minimised window out of the taskbar — it stays
            // minimised and Activate raises nothing, so picking Library off the tray menu
            // appeared to do nothing at all once the window had been minimised once.
            // BringToFront restores, activates, and takes the foreground when this was
            // asked for from outside — Hangly opened again.
            Interop.WindowPlacement.BringToFront(customize);
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("customize window", exception);
        }
    }

    /// <summary>Opens the window on the Library, whatever page it was left on.</summary>
    /// <remarks>
    /// Every route that names the Library goes through here — the tray menu entry and the
    /// welcome card's Explore Library button — because the window is kept alive between
    /// openings and otherwise comes back on whatever page was last read.
    /// </remarks>
    private void OpenLibrary()
    {
        OpenCustomize();
        customize?.ShowLibrary();
        Diagnostics.Log("opened on the library");
    }

    /// <summary>Opens Creator Studio, empty, ready for a picture.</summary>
    private void OpenCreate()
    {
        OpenCustomize();
        customize?.ShowSectionNamed("create");
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
        BankSwings();
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
        NoteChoices(settings.Overlay);
        if (artwork is not null && !hangingPlaces.SequenceEqual(places))
        {
            IReadOnlyList<string> ids = [.. places.Select(place => place.Id)];

            hangingPlaces = [.. places];
            hanging = [.. ids];
            overlay?.SetCharms(CharmLibrary.Resolve(artwork, index, hangingPlaces));
        }


        overlay?.Apply(settings.Overlay);
    }

    /// <summary>What was hanging, and on which cord, when choices were last noted.</summary>
    private IReadOnlyList<string>? notedCharms;
    private RopeStyle? notedRope;

    /// <summary><c>charm_selected</c> for each charm newly on the rope and <c>rope_selected</c> for a new cord.</summary>
    /// <remarks>
    /// Noticed here, where every change to the settings arrives, rather than at each of the places a charm or a rope
    /// can be chosen — the Library, the tray, the Studio — so none of them can forget to report it. The first call
    /// only records what is there: a launch is not a choice.
    /// </remarks>
    private void NoteChoices(OverlaySettings overlay)
    {
        IReadOnlyList<string> ids = overlay.Stack.Ids;
        if (notedCharms is not null)
        {
            foreach (string id in ids.Except(notedCharms, StringComparer.Ordinal))
            {
                analytics.Log(AnalyticsEvent.CharmSelected(id));
            }

            if (notedRope != overlay.RopeStyle)
            {
                analytics.Log(AnalyticsEvent.RopeSelected(overlay.RopeStyle));
            }
        }

        notedCharms = ids;
        notedRope = overlay.RopeStyle;
    }

    /// <summary>Notes this launch's version, and reports an update when it changed.</summary>
    private void NoteVersion()
    {
        string? previous = store.Settings.Milestones.LastLaunchedVersion;
        if (previous == AppInfo.Version)
        {
            return;
        }

        // 0.9.x did not record its version, so an install that has run before is an update too.
        releaseNotesDue = Hangly.Core.Text.ReleaseHighlights.ShouldShow(
            previous, AppInfo.Version, updatedBefore: updatedLaunch || store.Settings.Milestones.LaunchCount > 1);

        // An install that has launched before but never recorded a version is an update from 0.9.x: every Windows
        // release before 2.1.0 was a 0.9 release, and none of them kept its version.
        if (previous is not null || store.Settings.Milestones.LaunchCount > 1)
        {
            string from = previous ?? "0.9.x";
            firstLaunchOfUpdate = true;
            analytics.Log(AnalyticsEvent.AppUpdate(from));
            analytics.Log(AnalyticsEvent.UpdateInstalled(from, AppInfo.Version));
        }

        store.Update(settings => settings with
        {
            Milestones = settings.Milestones with { LastLaunchedVersion = AppInfo.Version },
        });
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

        // Favourites, as the quick way back to a charm somebody uses often. Empty is a
        // sentence rather than an empty submenu, because a submenu that opens onto nothing
        // reads as broken.
        List<MenuEntry> charms = settings.Library.FavouriteCharmIds
            .Select(id => (Id: id, Entry: index.Find(id)))

            // Find falls back to the plain bead for an id it does not know, so a
            // favourite of a charm that has since been deleted would appear in this menu
            // wearing the bead's name. Comparing the ids back is what catches that.
            .Where(found => string.Equals(found.Entry.Id, found.Id, StringComparison.Ordinal))
            .Select(found => new MenuEntry(
                found.Entry.DisplayName,
                () => HangOnly(found.Id),
                IsChecked: settings.Overlay.Stack.Ids.Contains(found.Id, StringComparer.Ordinal)))
            .ToList();

        if (charms.Count == 0)
        {
            charms.Add(new MenuEntry("Add charms to favourites to access them quickly.", null));
        }

        // An update that has been found gets one line at the top, and only then. A menu
        // item that is always there saying "no updates" is a menu item nobody reads.
        List<MenuEntry> update = Updates.ReadyVersion is { } ready
            ?
            [
                new MenuEntry($"Restart to update to {ready}", RestartToUpdate),
                MenuEntry.Separator,
            ]
            : availableUpdate is { HasUpdate: true } newer
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
            new MenuEntry("Library", OpenLibrary),
            // The two behaviour switches people flip day to day, one click away — the same
            // words as the macOS menu, directly below Library, and the same settings as
            // Appearance → Behaviour — the menu is rebuilt from them each time it opens.
            new MenuEntry(
                "Always on Top",
                () => store.UpdateOverlay(overlay => overlay with
                {
                    WindowMode = WindowModeTable.FromAlwaysOnTop(!WindowModeTable.IsAlwaysOnTop(overlay.WindowMode)),
                }),
                IsChecked: WindowModeTable.IsAlwaysOnTop(settings.Overlay.WindowMode)),
            new MenuEntry(
                "Auto-hide during full-screen video",
                () => store.UpdateOverlay(overlay => overlay with { HidesDuringFullscreenVideo = !overlay.HidesDuringFullscreenVideo }),
                IsChecked: settings.Overlay.HidesDuringFullscreenVideo),
            new MenuEntry("Create…", OpenCreate),
            MenuEntry.Separator,
            new MenuEntry("Charms", Children: charms),
            new MenuEntry("Rope", Children: ropes),
            .. DisplayMenu(settings.Overlay),
            MenuEntry.Separator,
            new MenuEntry("Quit Hangly", Quit),
        ];
    }

    /// <summary>Which display the rope hangs on, when there is a choice to make.</summary>
    /// <remarks>
    /// Absent on a single display, where it could only ever say one thing — unless a
    /// display was chosen and is now unplugged, when it stays so the choice can be seen and
    /// undone. The chosen display is remembered by its stable id, so it survives a reboot,
    /// a dock and a rearrangement, and the rope goes back to it by itself when it returns.
    /// </remarks>
    private List<MenuEntry> DisplayMenu(OverlaySettings overlay)
    {
        IReadOnlyList<DisplayInfo> displays = DisplayObserver.Displays();
        List<Hangly.Core.Geometry.DisplayIdentity> identities = [.. displays.Select(display => display.Identity)];
        bool isMissing = Hangly.Core.Geometry.DisplayChoice.IsMissing(identities, overlay.DisplayId);
        if (displays.Count < 2 && !isMissing)
        {
            return [];
        }

        int main = Hangly.Core.Geometry.DisplayChoice.Main(identities);
        int resolved = Hangly.Core.Geometry.DisplayChoice.Resolve(identities, overlay.DisplayId, overlay.DisplayIndex);
        bool followsMain = overlay.DisplayId is null && resolved == main;
        IReadOnlyList<string> labels = Hangly.Core.Geometry.DisplayChoice.Labels(identities);

        var entries = new List<MenuEntry>
        {
            new(
                "Main display",
                () => store.UpdateOverlay(settings => settings with { DisplayId = null, DisplayName = null, DisplayIndex = 0 }),
                IsChecked: followsMain),
            MenuEntry.Separator,
        };

        for (int index = 0; index < displays.Count; index++)
        {
            DisplayInfo display = displays[index];
            string label = labels[index];
            entries.Add(new MenuEntry(
                index == main ? $"{label} (main)" : label,
                () => store.UpdateOverlay(settings => settings with { DisplayId = display.Id, DisplayName = label, DisplayIndex = 0 }),
                IsChecked: !followsMain && !isMissing && index == resolved));
        }

        if (isMissing)
        {
            // Checked and inert: this is still the choice, and the rope is on the main
            // display only until it comes back.
            entries.Add(new MenuEntry($"{overlay.DisplayName ?? "Chosen display"} (not connected)", null, IsChecked: true));
        }

        return [new MenuEntry("Display", Children: entries)];
    }

    /// <summary>Applies the downloaded update now, and comes back on the new version.</summary>
    private async void RestartToUpdate()
    {
        Diagnostics.Log($"restart to update: {await Updates.DownloadAndApplyAsync().ConfigureAwait(true)}");
    }

    /// <summary>
    /// Quits for real, which means letting the one window that refuses to close, close.
    /// </summary>
    private void Quit()
    {
        Updates.ApplyOnExit();
        Exit();
    }

    /// <summary>Ends the process: the swings saved, and every held window released so WinUI can exit.</summary>
    private void Exit()
    {
        updateWait?.Stop();
        BankSwings();
        customize?.AllowClose();
        customize = null;
        Onboarding.ProcessLifetime.Release();
        Application.Current.Exit();
    }

    private void OnNetworkAvailabilityChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs args)
    {
        if (args.IsAvailable)
        {
            registry.NetworkBecameAvailable();
        }
    }

    public void Dispose()
    {
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        store.Changed -= OnSettingsChanged;
        audio?.Dispose();
        HideOverlay();
        tray?.Dispose();
        tray = null;
        GC.SuppressFinalize(this);
    }
}
