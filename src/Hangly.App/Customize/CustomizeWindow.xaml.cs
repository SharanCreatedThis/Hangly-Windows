//
//  CustomizeWindow.xaml.cs
//  Hangly
//
//  Where everything about Hangly is changed.
//

using System.Globalization;
using Hangly.App.Services;
using Hangly.App.Import;
using Hangly.Core.Analytics;
using Hangly.Core.Import;
using Hangly.Core.Lifecycle;
using Hangly.Core.Models;
using Hangly.Core.Settings;
using Hangly.Core.Studio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace Hangly.App.Customize;

/// <summary>The settings window.</summary>
/// <remarks>
/// <b>It hides rather than closes, and that is not a preference.</b> WinUI ends the
/// process when its last window closes, and Hangly has no other XAML window — the overlay
/// is a plain Win32 layered window and the tray is a message-only one. Closing this the
/// ordinary way took the whole app down with it, charm and tray icon included, which was
/// watched happening before this was written.
///
/// <para>Hiding is also the better behaviour for a tray application: the window keeps its
/// size, its position and whichever page was open.</para>
///
/// <para><b>Every control writes straight through to the store.</b> There is no apply
/// button and no draft copy, because the rope is on screen behind the window and the
/// point of moving a slider is watching it move. The store persists and raises, the
/// overlay listens, and this window listens too so that a change made from the tray shows
/// up here — guarded by <see cref="isLoading"/>, or setting a control from the store
/// would write the value it just read straight back.</para>
/// </remarks>
public sealed partial class CustomizeWindow : Window
{
    private readonly SettingsStore store;
    private readonly ILaunchAtLogin launchAtLogin;
    private readonly Hangly.Core.Registry.RegistrySync registry;
    private readonly AppEnvironment environment;
    private readonly List<CharmTile> tiles = [];
    private readonly Dictionary<string, CharmTile> tilesById = new(StringComparer.Ordinal);
    private readonly List<ToggleButton> chips = [];

    private CharmFilter filter = CharmFilter.All;
    private string query = string.Empty;
    private string? selectedCharmId;

    private bool isClosingForReal;
    private bool isLoading;

    /// <summary>Which charm on the cord a click in the grid replaces.</summary>
    private int selectedSlot;

    /// <summary>The charm the detail panel is describing, if any.</summary>
    private CharmCatalogEntry? detailed;

    /// <summary>The places on the rope, as the reorder strip holds them.</summary>
    private readonly System.Collections.ObjectModel.ObservableCollection<SlotTile> slotTiles = [];

    /// <summary>True while the strip is being rebuilt, so its own events are ignored.</summary>
    private bool isRebuildingSlots;

    /// <summary>True while the size slider is being set from the settings rather than by hand.</summary>
    private bool isLoadingSlotSize;

    /// <summary>The last secret shown, so the next one is a different one.</summary>
    private string? lastSecret;

    public CustomizeWindow(
        SettingsStore store,
        ILaunchAtLogin launchAtLogin,
        Hangly.Core.Registry.RegistrySync registry,
        AppEnvironment environment)
    {
        this.store = store;
        this.launchAtLogin = launchAtLogin;
        this.registry = registry;
        this.environment = environment;

        // Held for the whole of construction, and dropped by Load's finally.
        //
        // Every control here writes straight through to the store, so building them is
        // indistinguishable from a user moving them unless something says otherwise.
        // Setting a slider's Minimum coerces its Value, which raises ValueChanged — so
        // merely opening this window wrote charmSize 0.5, ropeLength 0.5 and opacity 0.2
        // over whatever the user had. That was watched happening to a real settings file.
        isLoading = true;

        InitializeComponent();
        Title = "Hangly";

        // Hangly's indigo rather than the system accent, as on the cards (HanglyButtons).
        Branding.HanglyButtons.Brand(SupportButton);
        SupportButton.CornerRadius = Branding.HanglyButtons.Corner;
        SupportButton.Height = 44;
        SupportButton.Padding = new Thickness(20, 0, 20, 0);
        SupportButton.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        SupportButton.ActualThemeChanged += (_, _) => Branding.HanglyButtons.Brand(SupportButton);
        AppWindow.Closing += OnClosing;

        ResizeToDefault();

        ConfigureSliders();
        BuildCharmGrid();
        BuildRopeChoices();
        BuildAnchorChoices();
        BuildAbout();
        BuildStudio();
        BuildShortcuts();
        Load();

        store.Changed += OnStoreChanged;
        Closed += (_, _) =>
        {
            store.Changed -= OnStoreChanged;
        };
    }

    /// <summary>Opens at a size the charm grid reads well at.</summary>
    /// <remarks>
    /// <c>AppWindow.Resize</c> is in physical pixels, not DIPs, so a fixed number opens a
    /// window half the intended size on a 200% display and a quarter of it at 400%.
    /// WinUI's own default is a fraction of the desktop, which on a large monitor is a
    /// settings window the size of a wall.
    /// </remarks>
    /// <summary>What the detail panel is showing. Bound from the XAML.</summary>
    public CharmDetail Detail { get; } = new();

    private void ResizeToDefault()
    {
        CentreForOpening();
        Interop.WindowIcon.Apply(this);
        FixTheSize();
    }

    /// <summary>Puts the window, at its one size, in the middle of the display the pointer is on.</summary>
    /// <remarks>
    /// Called on every open, not just the first: the window hides rather than closes, and
    /// every Hangly window opens in the middle of the display in use rather than where it
    /// was left (<see cref="Interop.WindowPlacement.SizeAndCentre"/>).
    /// </remarks>
    public void CentreForOpening() => Interop.WindowPlacement.SizeAndCentre(this, 1120, 800);

    /// <summary>Takes away resizing and maximising, and leaves everything else.</summary>
    /// <remarks>
    /// <b>The layout is designed for one size.</b> Everything on every page is arranged to
    /// fit the default window without scrolling, and a window that can be dragged to any
    /// shape is a window where that is true at one shape and false at the rest. Letting
    /// somebody make it four hundred points wide and then meeting a clipped control is
    /// worse than not letting them.
    ///
    /// <para>Moving, minimising and closing all still work — only the two that change the
    /// shape are gone.</para>
    /// </remarks>
    private void FixTheSize()
    {
        if (AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            return;
        }

        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
    }

    /// <summary>Lets the window close for good, on the way out of the application.</summary>
    public void AllowClose()
    {
        isClosingForReal = true;
        Close();
    }

    private OverlaySettings Overlay => store.Settings.Overlay;

    private void OnClosing(
        Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (isClosingForReal)
        {
            return;
        }

        args.Cancel = true;
        sender.Hide();
    }

    /// <summary>
    /// Ranges in code rather than in the markup. Set as XAML attributes these threw
    /// XamlParseException on <c>RangeBase.Minimum</c> — a slider's bounds have to be
    /// consistent at every step of being assigned, and attribute order is the markup
    /// compiler's business rather than ours. Here the order is stated.
    /// </summary>
    private void ConfigureSliders()
    {
        foreach ((Slider slider, double low, double high) in ((Slider, double, double)[])
            [(SizeSlider, 0.5, 2.0), (LengthSlider, 0.5, 2.0), (OpacitySlider, 0.2, 1.0),

            // From the top of the screen down to the macOS maximum. Below zero is macOS
            // tucking the knot under its menu bar, which has no meaning here (PositionPicker).
            (VerticalSlider, 0, Hangly.Core.Models.PositionPicker.MaximumOffsetY)])
        {
            slider.Maximum = high;
            slider.Minimum = low;
            slider.StepFrequency = 0.05;
            slider.SmallChange = 0.05;
            slider.LargeChange = 0.1;
        }
    }

    /// <summary>
    /// Builds one tile per charm, once, and never again.
    /// </summary>
    /// <remarks>
    /// Filtering regroups these same objects rather than making new ones. A tile owns a
    /// decoded <c>BitmapImage</c>, so rebuilding the grid on every keystroke would
    /// re-decode eighty-one PNGs per letter typed — which is the difference between a
    /// search box that keeps up and one that stutters.
    /// </remarks>
    private void BuildCharmGrid()
    {
        RebuildTiles();

        // Anything cached for a charm that is gone -- a deleted import, or a charm an
        // earlier build had -- goes with it.
        CharmThumbnails.Prune([.. environment.Charms.All.Select(entry => entry.Id)]);

        slotTiles.CollectionChanged += OnSlotsReordered;
        BuildCollections();
        BuildFilterChips();
        ShowResults();
    }

    /// <summary>
    /// One tile per charm the app knows about, shipped or imported.
    /// </summary>
    /// <remarks>
    /// Tiles for charms that are already here are kept rather than remade, so importing
    /// does not re-decode eighty-one thumbnails to add one.
    /// </remarks>
    private void RebuildTiles()
    {
        tiles.Clear();
        foreach (CharmCatalogEntry entry in environment.Charms.All)
        {
            if (!tilesById.TryGetValue(entry.Id, out CharmTile? tile))
            {
                tile = new CharmTile(entry);
                tilesById[entry.Id] = tile;
            }

            tiles.Add(tile);
        }

        // A tile whose charm has been deleted must not linger in the dictionary, or the
        // next import of the same id would show the old drawing.
        var live = environment.Charms.All.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string stale in tilesById.Keys.Where(id => !live.Contains(id)).ToList())
        {
            tilesById.Remove(stale);
        }
    }

    /// <summary>All, the two saved sets, then every category.</summary>
    /// <summary>The collection cards, built once from the catalogue's own table.</summary>
    private void BuildCollections()
    {
        var cards = new List<CollectionCard>();

        // Custom comes first when it exists: it is the one collection that is yours, and
        // it is the one you will be looking for.
        IReadOnlyList<CharmCollection> collections = environment.Charms.All
            .Any(entry => entry.CategoryId == CharmIndex.CustomCategoryId)
            ? [CharmIndex.CustomCollection, .. CharmCatalog.Collections]
            : CharmCatalog.Collections;

        foreach (CharmCollection collection in collections)
        {
            CharmTile[] members =
            [
                .. environment.Charms.All
                    .Where(entry => entry.CategoryId == collection.Id)
                    .Select(entry => tilesById.TryGetValue(entry.Id, out CharmTile? tile) ? tile : null)
                    .OfType<CharmTile>(),
            ];

            if (members.Length > 0)
            {
                cards.Add(new CollectionCard(collection, members));
            }
        }

        Collections.ItemsSource = cards;
    }

    /// <summary>Tapping a collection card filters to it, which is the card's whole job.</summary>
    private void OnCollectionTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is not FrameworkElement { DataContext: CollectionCard card })
        {
            return;
        }

        filter = CharmFilter.Category(card.Id);
        HighlightChips();
        ShowResults();
    }

    private void BuildFilterChips()
    {
        AddChip("All", CharmFilter.All);
        AddChip("Favourites", CharmFilter.Favourites);
        AddChip("Recent", CharmFilter.Recent);
        foreach (CharmCategory category in environment.Charms.Categories)
        {
            AddChip(category.Name, CharmFilter.Category(category.Id));
        }

        HighlightChips();
    }

    private void AddChip(string label, CharmFilter which)
    {
        var chip = new ToggleButton { Content = label, Tag = which };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(chip, $"Show {label}");
        chip.Click += (sender, _) =>
        {
            filter = which;
            HighlightChips();
            ShowResults();
        };

        chips.Add(chip);
        FilterChips.Children.Add(chip);
    }

    private void HighlightChips()
    {
        foreach (ToggleButton chip in chips)
        {
            chip.IsChecked = Equals(chip.Tag, filter);
        }
    }

    /// <summary>
    /// Applies the filter and the query, and regroups what survives.
    /// </summary>
    private void ShowResults()
    {
        AppSettings settings = store.Settings;
        IReadOnlyList<CharmCatalogEntry> matches = CharmSearch.Apply(
            environment.Charms,
            filter,
            query,
            settings.Library.FavouriteCharmIds,
            settings.Library.RecentCharmIds);

        var groups = new List<CharmGroup>();
        if (matches.Count > 0)
        {
            // Recents are already in the order that matters, so they are not regrouped:
            // splitting them by pack would throw away the only thing the list says.
            if (filter is CharmFilter.Recently)
            {
                groups.Add(new CharmGroup(
                    "Recently hung",
                    [.. matches.Select(entry => tilesById[entry.Id])]));
            }
            else
            {
                foreach (IGrouping<string, CharmCatalogEntry> pack in matches.GroupBy(PackOf))
                {
                    groups.Add(new CharmGroup(pack.Key, [.. pack.Select(entry => tilesById[entry.Id])]));
                }
            }
        }

        Packs.ItemsSource = groups;
        CollectionsScroller.Visibility = filter is CharmFilter.Everything && string.IsNullOrWhiteSpace(query)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ShowEmptyState(matches.Count == 0);
        MarkChosen();
        MarkFavourites();

        // Back to the top whenever the results change. Without this a filter applied
        // while scrolled down lands you in the middle of a different list, and the first
        // row of collection cards arrives already half out of view.
        ResultsScroller.ChangeView(null, 0, null, disableAnimation: true);
    }

    /// <summary>Which nothing this is, because they are not the same nothing.</summary>
    private void ShowEmptyState(bool isEmpty)
    {
        // Ropes are showing, so neither of these is. Without this guard every settings
        // change drew the charm results underneath the rope list; see ApplyBrowseMode.
        if (IsShowingRopes)
        {
            EmptyState.Visibility = Visibility.Collapsed;
            ResultsScroller.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        ResultsScroller.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        if (!isEmpty)
        {
            return;
        }

        if (query.Trim().Length > 0)
        {
            EmptyTitle.Text = "Nothing matches that";
            EmptyDetail.Text = $"No charm has \u201c{query.Trim()}\u201d in its name, its place or its materials.";
            return;
        }

        (EmptyTitle.Text, EmptyDetail.Text) = filter switch
        {
            CharmFilter.Favourite => (
                "No favourites yet",
                "Star a charm with the button in the corner of its tile and it will be waiting here."),
            CharmFilter.Recently => (
                "Nothing hung yet",
                "Charms you put on the cord show up here, most recent first."),
            _ => ("Nothing here", "This category has no charms in it."),
        };
    }

    private void MarkFavourites()
    {
        var favourites = store.Settings.Library.FavouriteCharmIds.ToHashSet(StringComparer.Ordinal);
        foreach (CharmTile tile in tiles)
        {
            tile.IsFavourite = favourites.Contains(tile.Id);
        }
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        // Typing is not a setting. This never touches the store.
        query = sender.Text;
        ShowResults();
    }

    private void OnFavouriteClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string id })
        {
            ToggleFavourite(id);
        }
    }

    /// <summary>Stars a charm, or unstars it: the tile's star and Ctrl+D both come here.</summary>
    private void ToggleFavourite(string id)
    {
        store.Update(settings => settings with
        {
            Library = settings.Library.WithFavouriteToggled(id),
        });

        MarkFavourites();
        RefreshDetailState();

        // Starring while looking at the favourites is a removal, and the tile should go.
        if (filter is CharmFilter.Favourite)
        {
            ShowResults();
        }
    }

    /// <summary>Which heading a charm is shown under.</summary>
    /// <remarks>
    /// Built-ins are grouped by the pack directory their artwork already sits in. An
    /// import is not in that folder at all — its file name is an absolute path with no
    /// forward slashes in it — so asking the path would have filed every imported charm
    /// under "Classics &amp; Collection", which it did until this was watched happening.
    /// </remarks>
    private static string PackOf(CharmCatalogEntry entry)
    {
        if (Hangly.Core.Models.CharmId.IsCustom(entry.Id))
        {
            return Hangly.Core.Models.CharmIndex.CustomCategory.Name;
        }

        int slash = entry.FileName.LastIndexOf('/');
        return slash < 0 ? "Classics & Collection" : entry.FileName[..slash];
    }

    private void BuildRopeChoices() => BrowseMode.SelectedIndex = 0;

    /// <summary>Fills the rope shelf from the settings: every rope, or only the starred ones.</summary>
    private void RefreshRopes()
    {
        OverlaySettings overlay = Overlay;
        IReadOnlyList<RopeStyle> favourites = store.Settings.Library.FavouriteRopes;
        bool onlyFavourites = RopeFilter.SelectedItem == RopeFavouritesFilter;
        List<RopeChoiceItem> items = [.. RopeStyleTable.All
            .Where(style => !onlyFavourites || favourites.Contains(style))
            .Select(style => new RopeChoiceItem(style, RopeSwatches.PathFor(style), favourites.Contains(style), style == overlay.RopeStyle))];

        RopeList.ItemsSource = items;
        RopeList.SelectedItem = items.FirstOrDefault(item => item.Style == overlay.RopeStyle);
        RopeFavouritesEmpty.Visibility = onlyFavourites && items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // The pane shows the rope in use, large, as it shows the charm in charm mode.
        Detail.ShowRope(RopeStyleTable.DisplayNameOf(overlay.RopeStyle), items.FirstOrDefault(item => item.Style == overlay.RopeStyle)?.Swatch
            ?? (RopeSwatches.PathFor(overlay.RopeStyle) is string path ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path)) : null));
    }

    private void OnRopeFilterChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (IsShowingRopes)
        {
            RefreshRopes();
        }
    }

    /// <summary>The star on a rope card. A Library write, like starring a charm.</summary>
    private void OnRopeFavouriteClicked(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.Tag is RopeStyle style)
        {
            store.Update(settings => settings with { Library = settings.Library.WithFavouriteRopeToggled(style) });
        }
    }

    /// <summary>
    /// Swaps the browse area between charms and ropes.
    /// </summary>
    /// <remarks>
    /// The two share the space rather than sitting side by side, because they are
    /// alternatives: nobody is choosing a rope and a charm in the same glance. Everything
    /// that only applies to charms — the search box, the collection chips, importing —
    /// goes with them.
    /// </remarks>
    private void OnBrowseModeChanged(object sender, SelectionChangedEventArgs args) => ApplyBrowseMode();

    /// <summary>Whether the browse area is showing ropes rather than charms.</summary>
    private bool IsShowingRopes => BrowseMode.SelectedIndex == 1;

    /// <summary>
    /// The one place that decides what the browse area is showing.
    /// </summary>
    /// <remarks>
    /// <b>There were two, and they disagreed.</b> Switching to Ropes collapsed the charm
    /// results here, and <see cref="ShowEmptyState"/> set them visible again — and that
    /// runs on every settings change, because the store raises and this window reloads. So
    /// switching to Ropes and then changing the number of charms on the cord put both
    /// views in the same grid cell at once, with rope names drawn through collection
    /// cards. Reported as "the tabs overlap", and it was.
    ///
    /// <para>Visibility is a function of the mode now, and the mode is asked rather than
    /// remembered. <see cref="ShowEmptyState"/> only chooses between the results and the
    /// empty state, and only while charms are the thing being shown.</para>
    /// </remarks>
    private void ApplyBrowseMode()
    {
        bool ropes = IsShowingRopes;

        CharmTools.Visibility = ropes ? Visibility.Collapsed : Visibility.Visible;
        FilterChips.Visibility = ropes ? Visibility.Collapsed : Visibility.Visible;
        RopesScroller.Visibility = ropes ? Visibility.Visible : Visibility.Collapsed;

        if (ropes)
        {
            ResultsScroller.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            RefreshRopes();
            return;
        }

        ShowSelectedSlotInDetail();
        ShowResults();
    }

    /// <summary>Choosing a rope from the Library, the one page that offers it; the tray's
    /// Rope menu goes through the same one write path.</summary>
    private void OnRopeListClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not RopeChoiceItem item || item.Style == Overlay.RopeStyle)
        {
            return;
        }

        store.UpdateOverlay(overlay => overlay with { RopeStyle = item.Style });
    }

    private void BuildAnchorChoices()
    {
        // Nothing to build: where the charm hangs is a slider now.
    }

    /// <summary>Puts every control where the stored settings say it should be.</summary>
    private void Load()
    {
        isLoading = true;
        try
        {
            OverlaySettings overlay = Overlay;

            CountChoice.SelectedIndex = overlay.CharmIds.Count - 1;
            if (IsShowingRopes)
            {
                RefreshRopes();
            }
            PositionSlider.Value = Math.Round(overlay.Position * 100);
            ShowPositionLabel();
            VerticalSlider.Value = Math.Clamp(overlay.OffsetY, VerticalSlider.Minimum, VerticalSlider.Maximum);
            ShowVerticalLabel();

            SizeSlider.Value = overlay.CharmSize;
            LengthSlider.Value = overlay.RopeLength;
            OpacitySlider.Value = overlay.Opacity;
            UpdateSliderLabels();

            ShowToggle.IsOn = overlay.IsEnabled;
            MotionChoice.SelectedIndex = (int)overlay.Motion;
            GlowChoice.SelectedIndex = (int)overlay.Glow;
            WindowModeChoice.SelectedIndex = (int)overlay.WindowMode;
            InteractionChoice.SelectedIndex = (int)overlay.Interaction;
            RopePhysicsChoice.SelectedIndex = (int)overlay.RopePhysics;
            StartupToggle.IsOn = overlay.StartupAnimation;
            Visibility offered = overlay.CharmIds.Any(Hangly.Core.Models.IntroTable.IsSpiderMan)
                ? Visibility.Visible
                : Visibility.Collapsed;
            SpiderManSection.Visibility = offered;
            FullscreenToggle.IsOn = overlay.HidesDuringFullscreenVideo;
            SoundToggle.IsOn = store.Settings.SoundEffectsEnabled;
            VolumeSlider.Value = Math.Round(store.Settings.SoundVolume * 100);
            VolumeSlider.IsEnabled = SoundToggle.IsOn;
            VolumeLabel.Text = $"Volume — {VolumeSlider.Value:0}%";
            LoginToggle.IsOn = store.Settings.LaunchAtLogin;
            NameBox.MaxLength = AppSettings.DisplayNameLimit;
            NameBox.Text = store.Settings.DisplayName;

            RebuildSlots();
            ShowResults();
        }
        finally
        {
            isLoading = false;
        }
    }

    private void OnNameKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            CommitName();
            args.Handled = true;
        }
    }

    private void OnNameCommitted(object sender, RoutedEventArgs args) => CommitName();

    /// <summary>Saves a changed nickname. The installation registry notices and sends it.</summary>
    /// <remarks>
    /// An empty box puts the old name back rather than saving nothing: the name is required
    /// everywhere else, and clearing it here would be a way round that.
    /// </remarks>
    private void CommitName()
    {
        if (isLoading)
        {
            return;
        }

        string chosen = NameBox.Text.Trim();
        if (chosen.Length == 0 || chosen == store.Settings.DisplayName)
        {
            NameBox.Text = store.Settings.DisplayName;
            return;
        }

        store.Update(settings => settings with { DisplayName = chosen });
        Diagnostics.Log("name changed");
    }

    private void UpdateSliderLabels()
    {
        SizeLabel.Text = $"Charm size — {SizeSlider.Value:P0}";
        LengthLabel.Text = $"Rope length — {LengthSlider.Value:P0}";
        OpacityLabel.Text = $"Opacity — {OpacitySlider.Value:P0}";
    }

    /// <summary>One button per charm on the cord; clicking one says which a pick replaces.</summary>
    private void RebuildSlots()
    {
        CharmStackState stack = Overlay.Stack;
        IReadOnlyList<RopeCharm> places = stack.Places;
        selectedSlot = Math.Clamp(selectedSlot, 0, places.Count - 1);

        // Rebuilt wholesale rather than edited in place. The strip is at most three
        // tiles, and the alternative is keeping a collection in step with a settings
        // document that other surfaces also write to.
        // An observable collection, not a list: a ListView will not reorder an items
        // source it cannot write back to, which is why dragging did nothing at first.
        isRebuildingSlots = true;
        slotTiles.Clear();
        for (int index = 0; index < places.Count; index++)
        {
            slotTiles.Add(new SlotTile(
                index,
                environment.Charms.Find(places[index].Id).DisplayName,
                tilesById.GetValueOrDefault(places[index].Id)?.Image));
        }

        SlotList.ItemsSource ??= slotTiles;

        SlotList.SelectedIndex = selectedSlot;
        isRebuildingSlots = false;

        MoveUpButton.IsEnabled = selectedSlot > 0;
        MoveDownButton.IsEnabled = selectedSlot < places.Count - 1;

        ShowSlotSize();
        ShowSelectedSlotInDetail();
        CordSummary.Text = places.Count == 1
            ? "One charm hangs on the cord."
            : $"{places.Count} charms hang on the cord, from the top down.";
    }

    /// <summary>The shared shortcut table (<see cref="Hangly.Core.Lifecycle.HanglyShortcut"/>), bound to this window.</summary>
    /// <remarks>
    /// On the navigation view, which is the root of the window, so they work wherever the
    /// keyboard is — except that a text box keeps its own Ctrl+D and Ctrl+F. Decision B1:
    /// nothing global.
    ///
    /// <para>Alt+Up and Alt+Down are not accelerators. A list with focus — the navigation
    /// on the left, the cord's places — takes Alt+Down as Down and moves its own focus before
    /// an accelerator is looked for, so the move only worked from some places in the window.
    /// They are caught on the way down instead (<see cref="OnMoveKeys"/>), and the buttons'
    /// tooltips name them.</para>
    /// </remarks>
    private void BuildShortcuts()
    {
        void Bind(Windows.System.VirtualKey key, Windows.System.VirtualKeyModifiers modifiers, Action action)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (_, args) =>
            {
                args.Handled = true;
                action();
            };
            Nav.KeyboardAccelerators.Add(accelerator);
        }

        const Windows.System.VirtualKeyModifiers Ctrl = Windows.System.VirtualKeyModifiers.Control;
        Bind(Windows.System.VirtualKey.Number1, Ctrl, () => ShowSection("charms"));
        Bind(Windows.System.VirtualKey.Number2, Ctrl, () => ShowSection("create"));
        Bind(Windows.System.VirtualKey.Number3, Ctrl, () => ShowSection("appearance"));
        Bind(Windows.System.VirtualKey.Number4, Ctrl, () => ShowSection("about"));
        Bind(Windows.System.VirtualKey.O, Ctrl | Windows.System.VirtualKeyModifiers.Shift,
            () => store.UpdateOverlay(overlay => overlay with { IsEnabled = !overlay.IsEnabled }));
        Bind(Windows.System.VirtualKey.F, Ctrl, () =>
        {
            ShowSection("charms");
            SearchBox.Focus(FocusState.Keyboard);
        });
        Bind(Windows.System.VirtualKey.D, Ctrl, FavouriteSelection);
        Bind(Windows.System.VirtualKey.W, Ctrl, () => AppWindow.Hide());

        Nav.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnMoveKeys), handledEventsToo: true);
        ToolTipService.SetToolTip(MoveUpButton, $"Move up ({HanglyShortcuts.WindowsKeysOf(HanglyShortcut.MoveUp)})");
        ToolTipService.SetToolTip(MoveDownButton, $"Move down ({HanglyShortcuts.WindowsKeysOf(HanglyShortcut.MoveDown)})");
    }

    /// <summary>Alt+Up / Alt+Down: the chosen place up or down the cord, from anywhere on the Library page.</summary>
    private void OnMoveKeys(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is not (Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down)
            || !IsDown(Windows.System.VirtualKey.Menu)
            || IsDown(Windows.System.VirtualKey.Control)
            || IsDown(Windows.System.VirtualKey.Shift)
            || CharmsPage.Visibility != Visibility.Visible)
        {
            return;
        }

        // A text box keeps Alt+Down: in the search box it opens the suggestions.
        if (FocusManager.GetFocusedElement(Content.XamlRoot) is TextBox)
        {
            return;
        }

        Button button = args.Key == Windows.System.VirtualKey.Up ? MoveUpButton : MoveDownButton;
        if (!button.IsEnabled)
        {
            return;
        }

        args.Handled = true;
        MoveSlot(args.Key == Windows.System.VirtualKey.Up ? -1 : 1);

        static bool IsDown(Windows.System.VirtualKey key) =>
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    /// <summary>Ctrl+D: the charm or rope the Library is showing, starred or unstarred.</summary>
    private void FavouriteSelection()
    {
        if (CharmsPage.Visibility != Visibility.Visible)
        {
            return;
        }

        if (IsShowingRopes)
        {
            RopeStyle style = Overlay.RopeStyle;
            store.Update(settings => settings with { Library = settings.Library.WithFavouriteRopeToggled(style) });
            return;
        }

        // The charm the pane shows, which is not always one that was clicked: opening the
        // Library, or choosing a place on the cord, shows that place's charm.
        if (detailed is not null)
        {
            ToggleFavourite(detailed.Id);
        }
    }

    private void OnMoveSlotUp(object sender, RoutedEventArgs args) => MoveSlot(-1);

    private void OnMoveSlotDown(object sender, RoutedEventArgs args) => MoveSlot(1);

    /// <summary>Moves the chosen place along the cord, and follows it with the selection.</summary>
    private void MoveSlot(int delta)
    {
        CharmStackState stack = Overlay.Stack;
        int destination = selectedSlot + delta;
        if (destination < 0 || destination >= stack.Count)
        {
            return;
        }

        int source = selectedSlot;
        store.UpdateOverlay(overlay => overlay.WithStack(overlay.Stack.Moved(source, destination)));

        // The selection follows the charm rather than staying where the charm was: the
        // person is moving a thing, not a slot, and having the panel jump to a different
        // charm mid-move reads as the app losing track.
        selectedSlot = destination;
        RebuildSlots();
    }

    /// <summary>Puts the size slider on the chosen place.</summary>
    private void ShowSlotSize()
    {
        CharmStackState stack = Overlay.Stack;
        double size = stack.SizeAt(selectedSlot);

        isLoadingSlotSize = true;
        SlotSizeSlider.Value = size;
        isLoadingSlotSize = false;

        SlotSizeLabel.Text = $"Size of {environment.Charms.Find(stack.Ids[selectedSlot]).DisplayName} — {size:P0} of its own";
    }

    private void OnSlotSizeChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (isLoading || isLoadingSlotSize)
        {
            return;
        }

        int slot = selectedSlot;
        double size = SlotSizeSlider.Value;
        store.UpdateOverlay(overlay => overlay.WithStack(overlay.Stack.WithSize(slot, size)));
        ShowSlotSize();
    }

    /// <summary>Clicking a tile chooses the place a pick replaces, as the buttons did.</summary>
    private void OnSlotItemClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is SlotTile tile)
        {
            selectedSlot = tile.Index;
            SlotList.SelectedIndex = selectedSlot;
            ShowSlotSize();
            ShowSelectedSlotInDetail();
        }
    }

    /// <summary>The strip's order changed: the strip's order is the rope's order.</summary>
    /// <remarks>
    /// Hooked to the collection rather than to <c>DragItemsCompleted</c>, and that is the
    /// difference between reordering working one way and working every way. A ListView
    /// reorders its own items source, so this fires whether the move came from a drag or
    /// from the keyboard — and keyboard reordering is the accessible path, which a
    /// drag-only handler would have left broken.
    ///
    /// <para>Each tile still carries the index it had when the strip was built, so the
    /// collection's new order <em>is</em> the permutation to apply to the stack.</para>
    /// </remarks>
    private void OnSlotsReordered(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
    {
        if (args.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        {
            return;
        }

        if (isRebuildingSlots || isLoading)
        {
            return;
        }

        var order = slotTiles.Select(tile => tile.Index).ToList();
        if (order.SequenceEqual(Enumerable.Range(0, order.Count)))
        {
            return;
        }

        store.UpdateOverlay(overlay =>
        {
            IReadOnlyList<RopeCharm> before = overlay.Stack.Places;
            var reordered = order
                .Where(index => index >= 0 && index < before.Count)
                .Select(index => before[index])
                .ToList();

            return reordered.Count == before.Count
                ? overlay.WithStack(CharmStackState.FromPlaces(reordered))
                : overlay;
        });

        RebuildSlots();
    }


    private void MarkChosen()
    {
        var chosen = Overlay.CharmIds.ToHashSet(StringComparer.Ordinal);
        foreach (CharmTile tile in tiles)
        {
            tile.IsChosen = chosen.Contains(tile.Id);
        }
    }

    /// <summary>Points the panel at whatever the chosen slot is carrying.</summary>
    /// <remarks>
    /// Which is what makes the panel open describing something rather than empty, and
    /// what keeps it honest when the slot changes under it — the panel reports the
    /// selection, so the selection has to reach it from every place it can move.
    /// </remarks>
    private void ShowSelectedSlotInDetail()
    {
        IReadOnlyList<string> ids = Overlay.CharmIds;
        if (selectedSlot >= 0 && selectedSlot < ids.Count)
        {
            ShowDetail(environment.Charms.Find(ids[selectedSlot]));
        }
    }

    private void OnSlotClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: int index })
        {
            selectedSlot = index;
                ShowSelectedSlotInDetail();
        }
    }

    private void OnSectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string page = (args.SelectedItem as NavigationViewItem)?.Tag as string ?? "charms";
        ApplySection(page);
    }

    private void ApplySection(string page)
    {
        CharmsPage.Visibility = page == "charms" ? Visibility.Visible : Visibility.Collapsed;
        CreatePage.Visibility = page == "create" ? Visibility.Visible : Visibility.Collapsed;
        if (page == "create")
        {
            StudioPane.Resume();
        }
        AppearancePage.Visibility = page == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;

        // The cord and the charm's story belong to the Library and nowhere else. They sit
        // in the navigation pane, which every page shares, so they followed somebody onto
        // Create, Appearance and About and sat there describing a charm that page had
        // nothing to do with. Hidden with the page they belong to, which also gives the
        // other three the full width.
        DetailPanel.Visibility = page == "charms" ? Visibility.Visible : Visibility.Collapsed;

        if (page == "about")
        {
            LoadInstallation();
        }
    }

    // --- Updates ------------------------------------------------------------------

    private Services.Updater updater => environment.Updates;

    /// <summary>Shows the welcome card again, from the beginning.</summary>
    /// <remarks>
    /// The name is already known, so the card opens on its second step — the part that
    /// says what Hangly is and where it lives. Asking somebody to retype a name they gave
    /// once would be a strange way to answer "how do I get back to that screen".
    /// </remarks>
    private void OnShowWelcomeClicked(object sender, RoutedEventArgs args) =>
        environment.ShowWelcomeAgain();

    private async void OnCheckForUpdates(object sender, RoutedEventArgs args)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateMessage.Text = "Checking…";

        ShowUpdateResult(await updater.CheckAsync(Hangly.Core.Analytics.UpdateTrigger.Manual));
        CheckUpdateButton.IsEnabled = true;
    }

    /// <summary>Puts the result of a check on the About page.</summary>
    private void ShowUpdateResult(Services.UpdateCheck result)
    {
        UpdateMessage.Text = result.Message;
        InstallUpdateButton.Visibility = result.HasUpdate ? Visibility.Visible : Visibility.Collapsed;

        UpdateNotes.Text = result.HasNotes ? Services.Updater.PlainNotes(result.Notes!) : string.Empty;
        UpdateNotesPanel.Visibility = UpdateNotes.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Opens the About page showing an update the quiet background check already found.
    /// </summary>
    /// <remarks>
    /// The tray line and this page have to agree, so the result found at launch is handed
    /// over rather than fetched again: a second check moments later could answer
    /// differently if a release were being published at that exact moment, and the one
    /// thing worse than no news is two versions of it.
    /// </remarks>
    public void ShowUpdates(Services.UpdateCheck found)
    {
        ShowSection("about");
        ShowUpdateResult(found);
    }

    /// <summary>Puts the window on the Library, wherever it was left.</summary>
    /// <remarks>
    /// The window is built once and hidden on close, so it comes back showing whatever
    /// page was open when it was dismissed. That is right for the title-bar X and wrong
    /// for a menu entry that names a page: somebody who last read About and then picked
    /// Library off the tray menu got About again, and reasonably called it a bug.
    /// </remarks>
    public void ShowLibrary() => ShowSection("charms");

    /// <summary>Opens a section by its tag: charms, create, appearance or about.</summary>
    public void ShowSectionNamed(string tag) => ShowSection(tag);

    /// <summary>Selects the navigation item carrying <paramref name="tag"/>.</summary>
    /// <remarks>
    /// Selecting the item is what runs <see cref="OnSectionChanged"/>, which owns page
    /// visibility. Setting the pages directly here would leave the pane highlighting one
    /// page while another was on screen. When the wanted item is already selected the
    /// selection does not change and no event is raised, so the pages are reconciled
    /// directly in that case.
    /// </remarks>
    private void ShowSection(string tag)
    {
        foreach (object item in Nav.MenuItems)
        {
            if (item is not NavigationViewItem entry || (entry.Tag as string) != tag)
            {
                continue;
            }

            if (ReferenceEquals(Nav.SelectedItem, entry))
            {
                ApplySection(tag);
            }
            else
            {
                Nav.SelectedItem = entry;
            }

            return;
        }
    }

    private async void OnInstallUpdate(object sender, RoutedEventArgs args)
    {
        InstallUpdateButton.IsEnabled = false;
        UpdateMessage.Text = "Downloading…";

        // If this succeeds the process is replaced and nothing after it runs. If it
        // fails, the installed copy is untouched and the message says so.
        UpdateMessage.Text = await updater.DownloadAndApplyAsync();
        InstallUpdateButton.IsEnabled = true;
        UpdateNotesPanel.Visibility = Visibility.Collapsed;
    }

    // --- Create -------------------------------------------------------------------

    /// <summary>Creator Studio's state, kept for the life of the window as macOS keeps it.</summary>
    private Studio.StudioSession? studio;

    /// <summary>The place a drop on the rope came from, which Save fills; null for the Library's choice.</summary>
    private int? studioSlot;

    private void BuildStudio()
    {
        studio = new Studio.StudioSession(
            () => Studio.OnnxSegmenter.IsInstalled ? new Studio.OnnxSegmenter() : null,
            SaveFromStudio);
        OverlaySettings overlay = store.Settings.Overlay;
        StudioPane.Attach(
            studio,
            overlay.RopeStyle,
            RopeMotionTable.Resolve(overlay.Motion, Services.SystemMotion.ReducesMotion),
            () => WinRT.Interop.WindowNative.GetWindowHandle(this));
        StudioPane.DoneRequested += () =>
        {
            studioSlot = null;
            AppWindow.Hide();
        };
        AppWindow.Changed += (_, change) =>
        {
            // Hidden is closed, as far as the model is concerned: 170 MB is only held while
            // somebody could be using it.
            if (change.DidVisibilityChange && !AppWindow.IsVisible)
            {
                StudioPane.Release();
            }
        };
    }

    /// <summary>Opens a picture in Creator Studio: a drop on the rope, the tray, or a paste.</summary>
    /// <param name="slot">The place on the rope the drop landed on, which Save fills.</param>
    public void OpenInStudio(string path, int? slot)
    {
        studioSlot = slot;
        ShowSection("create");
        _ = StudioPane.OpenAsync(path);
    }

    /// <summary>Stores a Studio draft through the same store every import uses, and hangs it if asked.</summary>
    /// <remarks>
    /// On the UI thread: the settings write raises the store's change, which updates this
    /// window's controls, and those belong to this thread. Hanging replaces
    /// a place rather than adding one, exactly as a drop or a pick from the grid does: the
    /// place a drop came from, otherwise the one chosen on the Library page.
    /// </remarks>
    private string SaveFromStudio(string markup, StudioDraft draft, string name, bool hang)
    {
        CustomCharmEntry entry = environment.CustomCharmsStore.Add(
            markup,
            name,
            draft.Metrics,
            draft.Palette);
        environment.CharmsChanged();

        // The Library first: the settings write below refreshes it, and that refresh looks
        // the new charm's tile up, so the tile has to exist before the write.
        RebuildTiles();
        BuildCollections();
        BuildFilterChips();
        ShowResults();

        string id = CharmId.ForCustom(entry.Id);
        int slot = studioSlot ?? selectedSlot;
        // Hung through Hanging like every other way onto the rope. Saved without hanging,
        // it is in the Library but has not been hung, so it is not "Recently hung" yet.
        if (hang)
        {
            store.Update(settings => Hanging.Hang(settings, slot, id));
        }

        return entry.Name;
    }

    /// <summary>The parts of About that never change while the window is open.</summary>
    private void BuildAbout()
    {
        // The same icon the executable carries, so there is one image and not two that
        // could drift. Extracted to a file once because XAML loads images by URI.
        string? icon = AppIconImage.Path();
        if (icon is not null)
        {
            AppIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(icon));
        }

        VersionLine.Text = $"Version {AppInfo.Version} (build {AppInfo.BuildNumber})";
        CopyrightLine.Text = AppInfo.Copyright;

        WebsiteLink.NavigateUri = new Uri(AppInfo.WebsiteUrl);
        GitHubLink.NavigateUri = new Uri(AppInfo.GitHubUrl);
        ReleaseNotesLink.NavigateUri = new Uri(AppInfo.ReleaseNotesUrl);
        InstagramLink.NavigateUri = new Uri(AppInfo.InstagramUrl);
        CreatorHandleLink.NavigateUri = new Uri(AppInfo.InstagramUrl);
        CreatorSiteLink.NavigateUri = new Uri(AppInfo.CreatorSiteUrl);
        CreatorName.Text = AppInfo.CreatorHandle;
        ShowMilestones();
    }

    /// <summary>The four numbers the About page keeps.</summary>
    /// <remarks>
    /// The shared statistics model, the same four on macOS in the same order: Launches,
    /// Charms hung (every hang that changed a place, through <c>Hanging</c>), Swings
    /// survived (<c>SwingCounter</c>: crossings of the vertical) and Secrets found.
    /// </remarks>
    private void ShowMilestones()
    {
        // Up to the moment: whatever the overlay has counted since it last saved.
        environment.BankSwings();

        MilestoneSettings milestones = store.Settings.Milestones;
        StatLaunches.Text = milestones.LaunchCount.ToString("N0", CultureInfo.CurrentCulture);
        StatCharms.Text = milestones.CharmsHung.ToString("N0", CultureInfo.CurrentCulture);
        StatSwings.Text = milestones.SwingsSurvived.ToString("N0", CultureInfo.CurrentCulture);
        StatSecrets.Text = milestones.SecretsFound.ToString("N0", CultureInfo.CurrentCulture);
    }

    /// <summary>Hands out a secret, and pushes the rope as macOS says it does.</summary>
    private void OnSecretClicked(object sender, RoutedEventArgs args)
    {
        string secret = SecretVault.Reveal(Random.Shared, lastSecret);
        lastSecret = secret;
        SecretText.Text = secret;

        store.Update(settings => settings with
        {
            Milestones = settings.Milestones with
            {
                SecretsFound = settings.Milestones.SecretsFound + 1,
            },
        });

        ShowMilestones();
        environment.Overlay?.Nudge();
    }

    private void OnSuggestClicked(object sender, RoutedEventArgs args) =>
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(AppInfo.SuggestMailUrl));

    /// <summary>What the installation registry holds for this machine, for whoever wants to check.</summary>
    /// <remarks>
    /// macOS's <c>InstallationPanel</c>. In the app rather than behind a developer flag because the argument for
    /// registering anything at all is that it can be inspected. The installation ID is masked: enough to tell two
    /// machines apart, not worth writing down.
    /// </remarks>
    private void LoadInstallation()
    {
        Hangly.Core.Registry.InstallationRecord? record = registry.Store.Record;
        AnalyticsState.Text = !registry.IsConfigured
            ? "Not configured in this build"
            : record?.Uploaded is null
                ? registry.LastFailure is null ? "Waiting to register" : "Will retry when online"
                : $"Registered — last seen {record.LastSeen?.LocalDateTime:d}";
        AnalyticsEndpoint.Text = string.Join(", ", new[] { record?.City, record?.Region, record?.Country }.Where(part => !string.IsNullOrEmpty(part)))
            is { Length: > 0 } place ? place : "Not known yet";
        string id = record?.InstallationId.ToString("D") ?? string.Empty;
        AnalyticsIdentifier.Text = id.Length > 0 ? $"{id[..8]}-••••-••••-••••-••••••••{id[^4..]}" : "none yet";
        AnalyticsLastSent.Text = registry.LastAcceptedAt is DateTimeOffset at ? $"{at.LocalDateTime:HH:mm:ss}" : "nothing this session";
        AnalyticsUserName.Text = record is { Nickname.Length: > 0 } ? record.Nickname : "(not set)";
        AnalyticsFields.Text = "installationId, nickname, city, region, country, platform, osName, osVersion, appVersion, "
            + "architecture, firstSeen, lastSeen, activeDays, retentionDays, crash reports";
    }

    /// <summary>Appearance → Privacy → View privacy details: About's Installation panel, opened.</summary>
    private void OnPrivacyDetails(object sender, RoutedEventArgs args)
    {
        ShowSection("about");
        AnalyticsSection.IsExpanded = true;
        AnalyticsSection.StartBringIntoView();
        LoadInstallation();
    }

    private void OnRefreshAnalytics(object sender, RoutedEventArgs args) => LoadInstallation();

    private async void OnSupportClicked(object sender, RoutedEventArgs args)
    {
        Hangly.Core.Analytics.HanglyAnalytics.Log(
            Hangly.Core.Analytics.AnalyticsEvent.SupportClicked(Hangly.Core.Analytics.SupportSurface.About));
        try
        {
            await SupportSheet.ShowAsync(Root);
        }
        catch (Exception exception)
        {
            // A dialog that cannot open must not take the window with it: this is the
            // one handler reached from a button that does nothing else.
            Diagnostics.Failure("support sheet", exception);
        }
    }

    private void OnCharmClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not CharmTile tile)
        {
            return;
        }

        UpdateDeleteButton(tile.Id);
        ShowDetail(tile.Entry);

        // One act, one write: the rope, Recent and "Charms hung" together (Hanging).
        store.Update(settings => Hanging.Hang(settings, selectedSlot, tile.Id));
    }

    /// <summary>Points the detail panel at a charm, and remembers which one.</summary>
    private void ShowDetail(CharmCatalogEntry? entry)
    {
        detailed = entry;
        Detail.Show(entry, entry is null ? null : tilesById.GetValueOrDefault(entry.Id)?.Image);
        RefreshDetailState();
    }

    /// <summary>Re-reads the two states the panel reports but does not own.</summary>
    private void RefreshDetailState()
    {
        if (detailed is null)
        {
            return;
        }

        AppSettings settings = store.Settings;
        Detail.IsOnRope = settings.Overlay.CharmIds.Contains(detailed.Id);
        Detail.IsFavourite = settings.Library.FavouriteCharmIds.Contains(detailed.Id);
    }

    private void OnCountChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isLoading || CountChoice.SelectedIndex < 0)
        {
            return;
        }

        // Nothing is discarded. The stack keeps three places whether or not they all
        // hang, so turning the count down hides places from the top and turning it back
        // up brings back exactly what was hidden — which is what macOS does, and what
        // this used to get wrong by duplicating the bottom charm on the way up.
        int wanted = CountChoice.SelectedIndex + 1;
        store.UpdateOverlay(overlay => overlay.WithStack(overlay.Stack.WithCount(wanted)));
    }

    /// <summary>Moves the charm along the top of the display, live.</summary>
    /// <remarks>
    /// Written straight through on every step, like every other control here, because the
    /// charm is on screen behind this window and watching it move is the point of dragging
    /// the slider.
    /// </remarks>
    private void OnPositionChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        ShowPositionLabel();

        if (isLoading)
        {
            return;
        }

        double at = Math.Clamp(args.NewValue / 100, 0, 1);
        if (Math.Abs(Overlay.Position - at) < 0.0005)
        {
            return;
        }

        store.UpdateOverlay(overlay => overlay with { HorizontalPosition = at });
    }

    private void ShowPositionLabel() =>
        PositionLabel.Text = $"Horizontal position \u2014 {(int)Math.Round(PositionSlider.Value)}%";

    private void ShowVerticalLabel() =>
        VerticalLabel.Text = $"Vertical position \u2014 {(int)Math.Round(VerticalSlider.Value)} pt from the top";

    private void OnVerticalChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        ShowVerticalLabel();
        if (isLoading || Math.Abs(Overlay.OffsetY - args.NewValue) < 0.5)
        {
            return;
        }

        store.UpdateOverlay(overlay => overlay with { OffsetY = Math.Round(args.NewValue) });
    }

    /// <summary>Back to where a new install hangs it.</summary>
    private void OnResetPosition(object sender, RoutedEventArgs args)
    {
        OverlaySettings fresh = AppSettings.Defaults.Overlay;
        store.UpdateOverlay(overlay => overlay with { HorizontalPosition = fresh.HorizontalPosition, OffsetY = fresh.OffsetY });
    }

    private void OnSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        UpdateSliderLabels();
        if (!isLoading)
        {
            store.UpdateOverlay(overlay => overlay with { CharmSize = SizeSlider.Value });
        }
    }

    private void OnLengthChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        UpdateSliderLabels();
        if (!isLoading)
        {
            store.UpdateOverlay(overlay => overlay with { RopeLength = LengthSlider.Value });
        }
    }

    private void OnOpacityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        UpdateSliderLabels();
        if (!isLoading)
        {
            store.UpdateOverlay(overlay => overlay with { Opacity = OpacitySlider.Value });
        }
    }

    private void OnFullscreenToggled(object sender, RoutedEventArgs args)
    {
        if (!isLoading)
        {
            store.UpdateOverlay(overlay => overlay with { HidesDuringFullscreenVideo = FullscreenToggle.IsOn });
        }
    }

    private void OnSoundToggled(object sender, RoutedEventArgs args)
    {
        VolumeSlider.IsEnabled = SoundToggle.IsOn;
        if (!isLoading)
        {
            store.Update(settings => settings with { SoundEffectsEnabled = SoundToggle.IsOn });
        }
    }

    private void OnVolumeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        VolumeLabel.Text = $"Volume — {VolumeSlider.Value:0}%";
        if (!isLoading)
        {
            store.Update(settings => settings with { SoundVolume = VolumeSlider.Value / 100 });
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs args)
    {
        if (!isLoading)
        {
            bool on = StartupToggle.IsOn;
            store.UpdateOverlay(overlay => overlay with { StartupAnimation = on });
        }
    }

    private void OnRopePhysicsChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!isLoading && RopePhysicsChoice.SelectedIndex is 0 or 1)
        {
            var physics = (Hangly.Core.Models.RopePhysics)RopePhysicsChoice.SelectedIndex;
            store.UpdateOverlay(overlay => overlay with { RopePhysics = physics });
        }
    }

    private void OnInteractionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!isLoading && InteractionChoice.SelectedIndex is 0 or 1)
        {
            var mode = (Hangly.Core.Models.InteractionMode)InteractionChoice.SelectedIndex;
            store.UpdateOverlay(overlay => overlay with { Interaction = mode });
        }
    }

    private void OnWindowModeChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!isLoading && WindowModeChoice.SelectedIndex is 0 or 1)
        {
            var mode = (Hangly.Core.Models.WindowMode)WindowModeChoice.SelectedIndex;
            store.UpdateOverlay(overlay => overlay with { WindowMode = mode });
        }
    }

    private void OnGlowChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!isLoading && GlowChoice.SelectedIndex is >= 0 and <= 2)
        {
            var glow = (Hangly.Core.Models.GlowLevel)GlowChoice.SelectedIndex;
            store.UpdateOverlay(overlay => overlay with { Glow = glow });
        }
    }

    private void OnMotionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!isLoading && MotionChoice.SelectedIndex is >= 0 and <= 2)
        {
            var motion = (Hangly.Core.Models.MotionPreference)MotionChoice.SelectedIndex;
            store.UpdateOverlay(overlay => overlay with { Motion = motion });
        }
    }

    private void OnShowToggled(object sender, RoutedEventArgs args)
    {
        if (!isLoading)
        {
            store.UpdateOverlay(overlay => overlay with { IsEnabled = ShowToggle.IsOn });
        }
    }

    private void OnLoginToggled(object sender, RoutedEventArgs args)
    {
        if (isLoading)
        {
            return;
        }

        // The registry is the truth here, so it is written first and the document records
        // what the system actually ended up saying.
        launchAtLogin.SetEnabled(LoginToggle.IsOn);
        store.Update(settings => settings with { LaunchAtLogin = launchAtLogin.IsEnabled });
    }

    /// <summary>Puts the rope back to exactly what a new install hangs.</summary>
    /// <remarks>
    /// <b>The charms go back too.</b> This used to keep them — macOS's confirmation says
    /// cord, size and position go back and your charms stay — but a button called
    /// "Restore defaults" that leaves three charms on the cord has not restored the
    /// defaults, and that is what it was asked to do.
    ///
    /// <para><see cref="AppSettings.Defaults"/> rather than <c>new OverlaySettings()</c>,
    /// which is the same distinction the store draws when there is no file: the plain
    /// record leaves the position null, meaning "not chosen", and the charm would land
    /// hard against the right edge instead of at the 87% a new install gets.</para>
    ///
    /// <para>What is <em>not</em> restored: the name, the analytics identifier, the
    /// milestones, the favourites and the recents. None of those is a default anybody is
    /// asking to go back to, and two of them cannot be recovered once discarded.</para>
    /// </remarks>
    private void OnReset(object sender, RoutedEventArgs args) =>
        store.UpdateOverlay(_ => AppSettings.Defaults.Overlay);

    /// <summary>Only an imported charm can be deleted, so the button only appears for one.</summary>
    private void UpdateDeleteButton(string charmId)
    {
        selectedCharmId = charmId;
        DeleteButton.Visibility = Hangly.Core.Models.CharmId.IsCustom(charmId)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnImportClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            Import();
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Failure("import", exception);
            ImportMessage.Text = "That charm couldn't be imported.";
        }
    }

    private void Import()
    {
        string? path = Interop.FileDialog.OpenFile(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            "Import a charm",
            ("SVG drawings", "*.svg"));

        Services.Diagnostics.Log($"import: chose {path ?? "nothing"}");
        if (path is null)
        {
            return;
        }

        ImportOutcome outcome = environment.ImportCharm(path);
        ImportMessage.Text = outcome.Message;

        if (!outcome.IsAccepted || outcome.Entry is null)
        {
            return;
        }

        // Shown straight away, without a restart: the tiles are rebuilt, the chips get
        // "Yours" if this was the first one, and the new charm goes on the cord.
        RebuildTiles();
        RebuildChips();
        filter = CharmFilter.Category(Hangly.Core.Models.CharmIndex.CustomCategoryId);
        HighlightChips();
        UpdateDeleteButton(outcome.Entry.CharmId);

        // The chips scroll, and "Yours" is at the far end of them — so the one chip that
        // just became relevant is the one that would be off the edge.
        chips.LastOrDefault()?.StartBringIntoView();

        // Through Hanging like every other way onto the rope, which also keeps the place's
        // size — writing the id list directly used to drop it.
        store.Update(settings => Hanging.Hang(settings, selectedSlot, outcome.Entry.CharmId));

        ShowResults();
    }

    private void OnDeleteImportClicked(object sender, RoutedEventArgs args)
    {
        if (selectedCharmId is string id)
        {
            _ = ConfirmAndDeleteAsync(id);
        }
    }

    private void OnDeleteMenuClicked(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: string id })
        {
            _ = ConfirmAndDeleteAsync(id);
        }
    }

    private void OnFavouriteMenuClicked(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: string id })
        {
            ToggleFavourite(id);
        }
    }

    /// <summary>Deletes one of the user's own charms once they say so; a built-in is ignored.</summary>
    /// <remarks>
    /// Asked first, as macOS does: the toolbar button used to delete on the click, and the
    /// image goes with the charm, so there is nothing to get it back from.
    /// </remarks>
    private async Task ConfirmAndDeleteAsync(string id)
    {
        if (!Hangly.Core.Models.CharmId.IsCustom(id))
        {
            return;
        }

        CustomCharmEntry? entry = environment.CustomCharms.Entries
            .FirstOrDefault(candidate => candidate.CharmId == id);

        if (entry is null)
        {
            return;
        }

        var confirm = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = $"Delete “{entry.Name}”?",
            Content = "The charm and its image are removed from Hangly. This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(confirm, "DeleteCharmDialog");

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        environment.DeleteCharm(entry.Id);
        ImportMessage.Text = $"“{entry.Name}” was deleted.";
        if (selectedCharmId == id)
        {
            selectedCharmId = null;
            DeleteButton.Visibility = Visibility.Collapsed;
        }

        RebuildTiles();
        RebuildChips();
        if (filter is CharmFilter.OfCategory category
            && category.Id == Hangly.Core.Models.CharmIndex.CustomCategoryId
            && environment.Charms.Custom.Count == 0)
        {
            filter = CharmFilter.All;
        }

        HighlightChips();
        ShowResults();
    }

    /// <summary>Rebuilds the chips, because "Yours" appears and disappears with the imports.</summary>
    private void RebuildChips()
    {
        chips.Clear();
        FilterChips.Children.Clear();
        BuildFilterChips();
    }

    private void OnStoreChanged(AppSettings settings) => Load();
}
