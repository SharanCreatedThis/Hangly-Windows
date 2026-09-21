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
using Hangly.Core.Models;
using Hangly.Core.Settings;
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
    private readonly AnalyticsManager analytics;
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
        AnalyticsManager analytics,
        AppEnvironment environment)
    {
        this.store = store;
        this.launchAtLogin = launchAtLogin;
        this.analytics = analytics;
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
        AppWindow.Closing += OnClosing;

        ResizeToDefault();

        ConfigureSliders();
        BuildCharmGrid();
        BuildRopeChoices();
        BuildAnchorChoices();
        BuildAbout();
        Load();

        store.Changed += OnStoreChanged;
        analytics.Changed += OnAnalyticsChanged;
        Closed += (_, _) =>
        {
            store.Changed -= OnStoreChanged;
            analytics.Changed -= OnAnalyticsChanged;
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
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = Interop.NativeMethods.GetDpiForWindow(handle) / 96.0;
        if (scale <= 0)
        {
            scale = 1;
        }

        var size = new Windows.Graphics.SizeInt32(
            (int)Math.Round(1120 * scale),
            (int)Math.Round(800 * scale));

        AppWindow.Resize(size);
        CentreOnDisplay(size);
    }

    /// <summary>Puts the window in the middle of the display it opened on.</summary>
    /// <remarks>
    /// <b>Nothing was positioning it at all.</b> The window was resized and never moved,
    /// so it opened wherever Windows put it — which for a new top-level window is a
    /// cascade from the top-left corner, and for a window this size on a scaled display is
    /// most of the way off the edge. Testers described it as landing "near screen edges"
    /// and "random", and both were fair: the placement was whatever the shell felt like,
    /// and it moved every time.
    ///
    /// <para>Only on the way up. The window hides rather than closes, so from the second
    /// time onwards it comes back exactly where it was left — which is the behaviour
    /// somebody who moved it deliberately expects, and re-centring on every open would
    /// throw that away.</para>
    /// </remarks>
    private void CentreOnDisplay(Windows.Graphics.SizeInt32 size)
    {
        try
        {
            Microsoft.UI.Windowing.DisplayArea area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                AppWindow.Id,
                Microsoft.UI.Windowing.DisplayAreaFallback.Primary);

            // The work area, not the whole display, so a taskbar does not push the window
            // down by its own height.
            Windows.Graphics.RectInt32 work = area.WorkArea;

            AppWindow.Move(new Windows.Graphics.PointInt32(
                work.X + Math.Max(0, (work.Width - size.Width) / 2),
                work.Y + Math.Max(0, (work.Height - size.Height) / 2)));
        }
        catch (Exception exception)
        {
            // A window in the wrong place is still a usable window.
            Services.Diagnostics.Failure("centring the customize window", exception);
        }
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
            [(SizeSlider, 0.5, 2.0), (LengthSlider, 0.5, 2.0), (OpacitySlider, 0.2, 1.0)])
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

        // Anything cached for a charm that is gone -- a deleted import, or one of the
        // eleven the seasonal cut removed -- goes with it.
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
        analytics.Track(Events.CollectionOpened(card.Name));
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
        Collections.Visibility = filter is CharmFilter.Everything && string.IsNullOrWhiteSpace(query)
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
        if (sender is not Button { Tag: string id })
        {
            return;
        }

        store.Update(settings => settings with
        {
            Library = settings.Library.WithFavouriteToggled(id),
        });

        MarkFavourites();

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

    private void BuildRopeChoices()
    {
        foreach (RopeStyle style in RopeStyleTable.All)
        {
            RopeList.Items.Add(new RopeChoiceItem(
                RopeStyleTable.DisplayNameOf(style),
                RopeStyleTable.SummaryOf(style)));
        }

        BrowseMode.SelectedIndex = 0;
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
            RopeList.SelectedIndex = RopeStyleTable.All.ToList().IndexOf(Overlay.RopeStyle);
            return;
        }

        ShowResults();
    }

    /// <summary>Choosing a rope from the Library, which is the same act as choosing it
    /// from Appearance and goes through the same one write path.</summary>
    private void OnRopeListClicked(object sender, ItemClickEventArgs args)
    {
        int index = RopeList.Items.IndexOf(args.ClickedItem);
        if (index < 0 || index >= RopeStyleTable.All.Count)
        {
            return;
        }

        RopeStyle style = RopeStyleTable.All[index];
        if (style == Overlay.RopeStyle)
        {
            return;
        }

        store.UpdateOverlay(overlay => overlay with { RopeStyle = style });
        analytics.Track(Events.RopeStyleChanged(style));
        RopeList.SelectedIndex = index;
    }

    private void BuildAnchorChoices()
    {
        foreach (OverlayAnchor anchor in Enum.GetValues<OverlayAnchor>())
        {
            AnchorChoice.Items.Add(OverlayAnchorTable.DisplayNameOf(anchor));
        }
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
                RopeList.SelectedIndex = RopeStyleTable.All.ToList().IndexOf(overlay.RopeStyle);
            }
            AnchorChoice.SelectedIndex = Array.IndexOf(Enum.GetValues<OverlayAnchor>(), overlay.Anchor);

            SizeSlider.Value = overlay.CharmSize;
            LengthSlider.Value = overlay.RopeLength;
            OpacitySlider.Value = overlay.Opacity;
            UpdateSliderLabels();

            ShowToggle.IsOn = overlay.IsEnabled;
            LoginToggle.IsOn = store.Settings.LaunchAtLogin;

            RebuildSlots();
            ShowResults();
        }
        finally
        {
            isLoading = false;
        }
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
        analytics.Track(Events.CharmReordered(source, destination));

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

        analytics.Track(Events.CharmReordered(args.OldStartingIndex, args.NewStartingIndex));
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
        CharmsPage.Visibility = page == "charms" ? Visibility.Visible : Visibility.Collapsed;
        CreatePage.Visibility = page == "create" ? Visibility.Visible : Visibility.Collapsed;
        AppearancePage.Visibility = page == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "about" ? Visibility.Visible : Visibility.Collapsed;

        if (page == "about")
        {
            LoadAnalytics();
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

        ShowUpdateResult(await updater.CheckAsync());
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
        foreach (object item in Nav.MenuItems)
        {
            if (item is NavigationViewItem entry && (entry.Tag as string) == "about")
            {
                Nav.SelectedItem = entry;
                break;
            }
        }

        ShowUpdateResult(found);
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

    /// <summary>The picture chosen on the Create page, before it becomes a charm.</summary>
    private string? creating;

    private void OnCreateChoose(object sender, RoutedEventArgs args)
    {
        analytics.Track(Events.AirdropPickerOpened);

        string? path = Interop.FileDialog.OpenFile(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            "Choose a picture",
            ("Pictures", "*.png;*.jpg;*.jpeg;*.svg"));

        if (path is null)
        {
            return;
        }

        ShowCreatePreview(path);
    }

    /// <summary>
    /// Shows the chosen picture, and offers a name taken from its file.
    /// </summary>
    /// <remarks>
    /// The preview is the file itself rather than the charm it will become. Rendering the
    /// finished charm would mean running the whole importer before anyone had said they
    /// wanted it, and the thing someone needs to see here is whether they picked the right
    /// picture.
    /// </remarks>
    private void ShowCreatePreview(string path)
    {
        creating = path;
        CreateMessage.Text = string.Empty;

        try
        {
            var image = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path));
            CreatePreview.Source = image;
            CreatePreviewFrame.Visibility = Visibility.Visible;
        }
        catch (Exception exception)
        {
            // A picture that will not preview may still import — an SVG does not preview
            // here at all — so this is a missing preview rather than a refusal.
            Services.Diagnostics.Log($"create preview failed: {exception.GetType().Name}");
            CreatePreviewFrame.Visibility = Visibility.Collapsed;
        }

        CreateNameBox.Text = Hangly.App.Import.CharmImporter.NameFor(path);
        CreateDetails.Visibility = Visibility.Visible;
        UpdateCreateButtons();
    }

    private void OnCreateNameChanged(object sender, TextChangedEventArgs args) => UpdateCreateButtons();

    private void UpdateCreateButtons()
    {
        bool ready = creating is not null && CreateNameBox.Text.Trim().Length > 0;
        CreateAndHangButton.IsEnabled = ready;
        CreateAndSaveButton.IsEnabled = ready;
    }

    private void OnCreateAndSave(object sender, RoutedEventArgs args) => Create(hang: false);

    private void OnCreateAndHang(object sender, RoutedEventArgs args) => Create(hang: true);

    /// <summary>Makes the charm, and optionally puts it straight on the rope.</summary>
    /// <remarks>
    /// Both buttons run the same import. Hanging is one extra write afterwards, into the
    /// place currently chosen on the Library page — the same place a pick from the grid
    /// would have replaced — so a created charm arrives with that place's size and the
    /// rope's order untouched.
    /// </remarks>
    private void Create(bool hang)
    {
        if (creating is not string path)
        {
            return;
        }

        string name = CreateNameBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }

        Hangly.App.Import.ImportOutcome outcome;
        try
        {
            outcome = Hangly.App.Import.CharmImporter.ImportAny(path, name, environment.CustomCharmsStore);
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Failure("create", exception);
            CreateMessage.Text = "That picture couldn't be made into a charm.";
            return;
        }

        CreateMessage.Text = outcome.Message;
        if (!outcome.IsAccepted || outcome.Entry is null)
        {
            return;
        }

        environment.CharmsChanged();
        analytics.Track(Events.CharmImported);
        analytics.Track(Events.CharmSaved);

        string id = Hangly.Core.Models.CharmId.ForCustom(outcome.Entry.Id);
        store.Update(settings => settings with
        {
            Overlay = hang
                ? settings.Overlay.WithStack(settings.Overlay.Stack.WithCharm(selectedSlot, id))
                : settings.Overlay,
            Library = settings.Library.WithRecent(id),
        });

        // The grid, the chips and the collection cards all have a new charm in them.
        RebuildTiles();
        BuildCollections();
        BuildFilterChips();
        ShowResults();

        creating = null;
        CreatePreviewFrame.Visibility = Visibility.Collapsed;
        CreateDetails.Visibility = Visibility.Collapsed;
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
        ShowMilestones();
    }

    /// <summary>The four numbers the About page keeps.</summary>
    /// <remarks>
    /// macOS's <c>AppMilestones</c> carries <c>charms</c>, <c>launches</c> and
    /// <c>secretsFound</c>; the fourth, swings, is named only by the statistics label
    /// "Swings survived: ". The counting rule for it is this build's own — see
    /// OverlayWindow — because macOS's is not in the repository.
    /// </remarks>
    private void ShowMilestones()
    {
        // Swings live in the overlay until someone looks, then they are banked. Taking
        // them zeroes the overlay's counter, so this cannot count the same swing twice.
        long swung = environment.Overlay?.TakeSwings() ?? 0;
        if (swung > 0)
        {
            store.Update(settings => settings with
            {
                Milestones = settings.Milestones with
                {
                    SwingsSurvived = settings.Milestones.SwingsSurvived + swung,
                },
            });
        }

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

    /// <summary>
    /// The analytics inspector.
    /// </summary>
    /// <remarks>
    /// PRIVACY.md says this shows whether sharing is on, whether a destination is
    /// configured and which, the installation identifier masked, and the last event sent
    /// and when. It is in the app rather than behind a developer flag because the
    /// argument for collecting anything at all is that it can be inspected.
    /// </remarks>
    private void LoadAnalytics()
    {
        bool wasLoading = isLoading;
        isLoading = true;
        try
        {
            AnalyticsToggle.IsOn = analytics.IsEnabled;
            AnalyticsState.Text = analytics.IsEnabled ? "On" : "Off";
            AnalyticsUserName.Text = store.Settings.DisplayName.Length > 0
                ? store.Settings.DisplayName
                : "(not set)";

            // Built from the manager rather than typed here, so the list cannot drift
            // from what is actually sent.
            AnalyticsFields.Text = string.Join(
                ", ",
                analytics.PersonProperties().Keys.Concat(["install_id", "event name", "event properties"]).Distinct());
            AnalyticsEndpoint.Text = analytics.Connection.Summary;
            AnalyticsIdentifier.Text = analytics.MaskedIdentifier ?? "none yet";
            AnalyticsLastEvent.Text = analytics.LastEventName is null
                ? "nothing sent"
                : $"{analytics.LastEventName} — {analytics.LastEventAt:HH:mm:ss}";
            AnalyticsSentCount.Text = analytics.SentCount.ToString(
                System.Globalization.CultureInfo.CurrentCulture);
        }
        finally
        {
            isLoading = wasLoading;
        }
    }

    private void OnAnalyticsChanged()
    {
        if (AboutPage.Visibility == Visibility.Visible)
        {
            LoadAnalytics();
        }
    }

    private void OnAnalyticsToggled(object sender, RoutedEventArgs args)
    {
        if (isLoading)
        {
            return;
        }

        analytics.SetEnabled(AnalyticsToggle.IsOn);
        LoadAnalytics();
    }

    private void OnRefreshAnalytics(object sender, RoutedEventArgs args) => LoadAnalytics();

    private void OnInstagramClicked(object sender, RoutedEventArgs args) =>
        analytics.Track(Events.FollowInstagramClicked);

    private async void OnCoffeeClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            await BuyCoffeeSheet.ShowAsync(Root, analytics, "about");
        }
        catch (Exception exception)
        {
            // A dialog that cannot open must not take the window with it: this is the
            // one handler reached from a button that does nothing else.
            Diagnostics.Failure("coffee sheet", exception);
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

        store.Update(settings =>
        {
            // Writing a charm into a place leaves that place's size alone: the size
            // describes the composition, and changing your mind about which charm is in
            // the middle is not a decision to make the middle large again.
            // The rope and the recents change together, in one write, so the file is
            // written once for one act rather than twice.
            return settings with
            {
                Overlay = settings.Overlay.WithStack(
                    settings.Overlay.Stack.WithCharm(selectedSlot, tile.Id)),
                Library = settings.Library.WithRecent(tile.Id),
                Milestones = settings.Milestones with
                {
                    CharmsHung = settings.Milestones.CharmsHung + 1,
                },
            };
        });
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

    private void OnDetailFavouriteClicked(object sender, RoutedEventArgs args)
    {
        if (detailed is null)
        {
            return;
        }

        string id = detailed.Id;
        store.Update(settings => settings with
        {
            Library = settings.Library.WithFavouriteToggled(id),
        });

        RefreshDetailState();
        MarkFavourites();
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

    private void OnAnchorChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isLoading || AnchorChoice.SelectedIndex < 0)
        {
            return;
        }

        OverlayAnchor anchor = Enum.GetValues<OverlayAnchor>()[AnchorChoice.SelectedIndex];
        store.UpdateOverlay(overlay => overlay with { Anchor = anchor });
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

    private void OnReset(object sender, RoutedEventArgs args)
    {
        // The charms are not part of "appearance", and the macOS window says so on the
        // confirmation: cord, size and position go back, your charms stay.
        IReadOnlyList<string> keep = Overlay.CharmIds;
        store.UpdateOverlay(_ => new OverlaySettings { CharmIds = keep });
    }

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
        analytics.Track(Events.AirdropPickerOpened);
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

        store.Update(settings =>
        {
            var ids = settings.Overlay.CharmIds.ToList();
            if (selectedSlot >= 0 && selectedSlot < ids.Count)
            {
                ids[selectedSlot] = outcome.Entry.CharmId;
            }

            return settings with
            {
                Overlay = settings.Overlay with { CharmIds = ids },
                Library = settings.Library.WithRecent(outcome.Entry.CharmId),
            };
        });

        ShowResults();
    }

    private void OnDeleteImportClicked(object sender, RoutedEventArgs args)
    {
        if (selectedCharmId is not string id || !Hangly.Core.Models.CharmId.IsCustom(id))
        {
            return;
        }

        CustomCharmEntry? entry = environment.CustomCharms.Entries
            .FirstOrDefault(candidate => candidate.CharmId == id);

        if (entry is null)
        {
            return;
        }

        environment.DeleteCharm(entry.Id);
        ImportMessage.Text = $"“{entry.Name}” was deleted.";
        selectedCharmId = null;
        DeleteButton.Visibility = Visibility.Collapsed;

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
