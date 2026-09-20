//
//  CustomizeWindow.xaml.cs
//  Hangly
//
//  Where everything about Hangly is changed.
//

using Hangly.App.Services;
using Hangly.Core.Models;
using Hangly.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    private readonly List<CharmTile> tiles = [];
    private readonly List<Button> slots = [];

    private bool isClosingForReal;
    private bool isLoading;

    /// <summary>Which charm on the cord a click in the grid replaces.</summary>
    private int selectedSlot;

    public CustomizeWindow(SettingsStore store, ILaunchAtLogin launchAtLogin)
    {
        this.store = store;
        this.launchAtLogin = launchAtLogin;

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
        Load();

        store.Changed += OnStoreChanged;
        Closed += (_, _) => store.Changed -= OnStoreChanged;
    }

    /// <summary>Opens at a size the charm grid reads well at.</summary>
    /// <remarks>
    /// <c>AppWindow.Resize</c> is in physical pixels, not DIPs, so a fixed number opens a
    /// window half the intended size on a 200% display and a quarter of it at 400%.
    /// WinUI's own default is a fraction of the desktop, which on a large monitor is a
    /// settings window the size of a wall.
    /// </remarks>
    private void ResizeToDefault()
    {
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = Interop.NativeMethods.GetDpiForWindow(handle) / 96.0;
        if (scale <= 0)
        {
            scale = 1;
        }

        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)Math.Round(1120 * scale),
            (int)Math.Round(800 * scale)));
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

    private void BuildCharmGrid()
    {
        // Grouped by the pack directory the artwork already sits in, so the grouping is
        // the catalogue's own rather than a second list to keep in step with it.
        var groups = new List<CharmGroup>();
        foreach (IGrouping<string, CharmCatalogEntry> pack in CharmCatalog.All.GroupBy(PackOf))
        {
            var packTiles = new List<CharmTile>();
            foreach (CharmCatalogEntry entry in pack)
            {
                var tile = new CharmTile(entry);
                packTiles.Add(tile);
                tiles.Add(tile);
            }

            groups.Add(new CharmGroup(pack.Key, packTiles));
        }

        Packs.ItemsSource = groups;
    }

    private static string PackOf(CharmCatalogEntry entry)
    {
        int slash = entry.FileName.LastIndexOf('/');
        return slash < 0 ? "Classics & Collection" : entry.FileName[..slash];
    }

    private void BuildRopeChoices()
    {
        foreach (RopeStyle style in RopeStyleTable.All)
        {
            RopeChoice.Items.Add(RopeStyleTable.DisplayNameOf(style));
        }
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
            RopeChoice.SelectedIndex = RopeStyleTable.All.ToList().IndexOf(overlay.RopeStyle);
            RopeDescription.Text = RopeStyleTable.SummaryOf(overlay.RopeStyle);
            AnchorChoice.SelectedIndex = Array.IndexOf(Enum.GetValues<OverlayAnchor>(), overlay.Anchor);

            SizeSlider.Value = overlay.CharmSize;
            LengthSlider.Value = overlay.RopeLength;
            OpacitySlider.Value = overlay.Opacity;
            UpdateSliderLabels();

            ShowToggle.IsOn = overlay.IsEnabled;
            LoginToggle.IsOn = store.Settings.LaunchAtLogin;

            RebuildSlots();
            MarkChosen();
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
        slots.Clear();
        SlotButtons.Children.Clear();

        IReadOnlyList<string> ids = Overlay.CharmIds;
        selectedSlot = Math.Clamp(selectedSlot, 0, ids.Count - 1);

        for (int index = 0; index < ids.Count; index++)
        {
            var button = new Button
            {
                Content = $"{index + 1}. {CharmCatalog.Find(ids[index]).DisplayName}",
                Tag = index,
            };
            button.Click += OnSlotClicked;
            slots.Add(button);
            SlotButtons.Children.Add(button);
        }

        HighlightSlot();
        CordSummary.Text = ids.Count == 1
            ? "One charm hangs on the cord."
            : $"{ids.Count} charms hang on the cord, from the top down.";
    }

    private void HighlightSlot()
    {
        for (int index = 0; index < slots.Count; index++)
        {
            slots[index].Style = index == selectedSlot
                ? (Style)Application.Current.Resources["AccentButtonStyle"]
                : (Style)Application.Current.Resources["DefaultButtonStyle"];
        }
    }

    private void MarkChosen()
    {
        var chosen = Overlay.CharmIds.ToHashSet(StringComparer.Ordinal);
        foreach (CharmTile tile in tiles)
        {
            tile.IsChosen = chosen.Contains(tile.Id);
        }
    }

    private void OnSlotClicked(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: int index })
        {
            selectedSlot = index;
            HighlightSlot();
        }
    }

    private void OnSectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        bool appearance = (args.SelectedItem as NavigationViewItem)?.Tag as string == "appearance";
        CharmsPage.Visibility = appearance ? Visibility.Collapsed : Visibility.Visible;
        AppearancePage.Visibility = appearance ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCharmClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not CharmTile tile)
        {
            return;
        }

        store.UpdateOverlay(overlay =>
        {
            var ids = overlay.CharmIds.ToList();
            if (selectedSlot >= 0 && selectedSlot < ids.Count)
            {
                ids[selectedSlot] = tile.Id;
            }

            return overlay with { CharmIds = ids };
        });
    }

    private void OnCountChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isLoading || CountChoice.SelectedIndex < 0)
        {
            return;
        }

        int wanted = CountChoice.SelectedIndex + 1;
        store.UpdateOverlay(overlay =>
        {
            var ids = overlay.CharmIds.ToList();

            // Growing repeats the last charm rather than picking one: the user is asking
            // for another charm, not for a particular one, and a repeat is obvious on
            // screen and one click from being what they wanted.
            while (ids.Count < wanted)
            {
                ids.Add(ids[^1]);
            }

            while (ids.Count > wanted)
            {
                ids.RemoveAt(ids.Count - 1);
            }

            return overlay with { CharmIds = ids };
        });
    }

    private void OnRopeChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isLoading || RopeChoice.SelectedIndex < 0)
        {
            return;
        }

        RopeStyle style = RopeStyleTable.All[RopeChoice.SelectedIndex];
        RopeDescription.Text = RopeStyleTable.SummaryOf(style);
        store.UpdateOverlay(overlay => overlay with { RopeStyle = style });
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

    private void OnStoreChanged(AppSettings settings) => Load();
}
