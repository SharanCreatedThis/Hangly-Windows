//
//  StudioView.xaml.cs
//  Hangly
//
//  Creator Studio's surface: shows the session, and turns input into changes to it.
//

using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Hangly.Core.Models;
using Hangly.Core.Studio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Hangly.App.Studio;

/// <summary>The Studio, as a control the Create page hosts.</summary>
/// <remarks>
/// Holds no state of its own beyond what is on screen: everything is the session's, and
/// <see cref="Render"/> redraws from it whenever it changes. The one thing the view keeps
/// is which preview is open, which — as on macOS — is a thing about the window, not about
/// the charm.
/// </remarks>
public sealed partial class StudioView : UserControl
{
    private StudioSession? session;
    private StudioPreview? preview;
    private Func<IntPtr>? owner;
    private bool rendering;
    private StudioImage? shownSource;
    private StudioDraft? shownDraft;

    public StudioView()
    {
        InitializeComponent();
        PointerCaptureLost += (_, _) => session?.EndEditing();
        foreach (Slider slider in new[] { SizeSlider, WeightSlider, FillSlider, ToleranceSlider })
        {
            // One drag is one undo step: the drag starts when the pointer goes down on the
            // slider and ends when it comes up, the macOS SliderRow's editing callback.
            slider.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => session?.BeginEditing()), true);
            slider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => session?.EndEditing()), true);
        }
    }

    /// <summary>Asked when Done is pressed, so the host can close or move on.</summary>
    public event Action? DoneRequested;

    /// <summary>Connects the view to its session. Called once by the host.</summary>
    internal void Attach(StudioSession studio, RopeStyle style, RopeMotion motion, Func<IntPtr> window)
    {
        session = studio;
        owner = window;
        preview = new StudioPreview(style, motion);
        CanvasHost.Child = preview.View;
        session.Changed += Render;
        Render();
    }

    /// <summary>Opens a file in the Studio, as a drop or the tray does.</summary>
    internal Task OpenAsync(string path) => session?.OpenAsync(path) ?? Task.CompletedTask;

    /// <summary>Called when the Studio comes back on screen.</summary>
    internal void Resume() => session?.ResumeDetection();

    /// <summary>Releases the model, when the host is going away for a while.</summary>
    internal void Release()
    {
        session?.ReleaseModel();
    }

    private void Render()
    {
        if (session is null || preview is null)
        {
            return;
        }

        rendering = true;
        try
        {
            StudioSession s = session;
            bool working = s.HasImage && s.Current is not StudioSession.Stage.Loading;
            DropZone.Visibility = working ? Visibility.Collapsed : Visibility.Visible;
            Workspace.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
            LoadingPanel.Visibility = s.Current == StudioSession.Stage.Loading ? Visibility.Visible : Visibility.Collapsed;

            HeaderName.Text = s.HasImage ? s.EffectiveName : "Create";
            UndoButton.IsEnabled = s.History.CanUndo;
            RedoButton.IsEnabled = s.History.CanRedo;
            UseOnRopeBox.IsEnabled = s.HasImage;
            RemoveButton.IsEnabled = s.HasImage;
            UseOnRopeBox.IsChecked = s.UseOnRopeAfterSave;
            SaveButton.IsEnabled = s.CanSave;

            if (!ReferenceEquals(shownSource, s.Source))
            {
                shownSource = s.Source;
                SourceImage.Source = s.Source is null ? null : Bitmap(s.Source);
                SourceName.Text = s.SourcePath is null ? string.Empty : Path.GetFileName(s.SourcePath);
                SourceSize.Text = s.Source is null ? string.Empty : $"{s.Source.Width} × {s.Source.Height}";
                if (s.Source is not null)
                {
                    AutomationPropertiesName(SourceImage, $"Source image, {s.Source.Width} by {s.Source.Height} pixels");
                }
            }

            RenderControls(s);

            preview.ShowCutout(s.Isolated);
            if (!ReferenceEquals(shownDraft, s.Draft))
            {
                shownDraft = s.Draft;
                preview.Show(s.Draft, s.EffectiveName);
            }

            PhysicsPanel.Visibility = s.Draft is null ? Visibility.Collapsed : Visibility.Visible;
            if (s.Draft is StudioDraft draft)
            {
                MassValue.Text = draft.Metrics.Mass.ToString("0.00", CultureInfo.CurrentCulture);
                AnalysedValue.Text = draft.AnalysedMass.ToString("0.00", CultureInfo.CurrentCulture);
            }

            SummaryIcon.Visibility = s.HasImage ? Visibility.Visible : Visibility.Collapsed;
            SubjectSummary.Text = s.SubjectSummary;
            ProcessingRing.IsActive = (s.IsProcessing || s.IsDetecting) && s.ErrorMessage is null;
            StatusText.Text = s.ErrorMessage ?? (s.IsProcessing ? "Updating preview…" : string.Empty);
            StatusText.Foreground = s.ErrorMessage is null
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
            StatusHelp.Visibility = s.ErrorHelp is null ? Visibility.Collapsed : Visibility.Visible;
            StatusHelp.Content = "Get it";

            SavedOverlay.Visibility = s.Current == StudioSession.Stage.Saved ? Visibility.Visible : Visibility.Collapsed;
            if (s.Current == StudioSession.Stage.Saved)
            {
                SavedTitle.Text = $"Saved “{s.SavedName ?? s.EffectiveName}” to your library";
                SavedOnRope.Visibility = s.UseOnRopeAfterSave ? Visibility.Visible : Visibility.Collapsed;
                SavedImage.Source = s.Draft is null ? null : Bitmap(s.Draft.Square);
            }
        }
        finally
        {
            rendering = false;
        }
    }

    private void RenderControls(StudioSession s)
    {
        StudioAdjustments a = s.Adjustments;
        RemovalChoice.SelectedIndex = a.Removal switch
        {
            SubjectRemoval.Automatic => 0,
            SubjectRemoval.DetectedSubject => 1,
            SubjectRemoval.FlatBackground => 2,
            _ => 3,
        };

        SubjectPickerPanel.Visibility = a.Removal is SubjectRemoval.DetectedSubject && s.Detection.InstanceCount > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (SubjectPickerPanel.Visibility == Visibility.Visible)
        {
            if (SubjectPicker.Items.Count != s.Detection.InstanceCount + 1)
            {
                SubjectPicker.Items.Clear();
                SubjectPicker.Items.Add(new SelectorBarItem { Text = "All", Tag = 0 });
                for (int number = 1; number <= s.Detection.InstanceCount; number++)
                {
                    SubjectPicker.Items.Add(new SelectorBarItem { Text = number.ToString(CultureInfo.CurrentCulture), Tag = number });
                }
            }

            int chosen = (a.Removal as SubjectRemoval.DetectedSubject)?.Instance ?? 0;
            SubjectPicker.SelectedItem = SubjectPicker.Items[Math.Clamp(chosen, 0, SubjectPicker.Items.Count - 1)];
        }

        TolerancePanel.Visibility = a.Removal is SubjectRemoval.FlatBackground ? Visibility.Visible : Visibility.Collapsed;
        if (a.Removal is SubjectRemoval.FlatBackground flat)
        {
            ToleranceSlider.Value = flat.Tolerance;
            ToleranceValue.Text = flat.Tolerance.ToString(CultureInfo.CurrentCulture);
        }

        RemovalDetail.Text = a.Removal switch
        {
            SubjectRemoval.DetectedSubject when !s.Detection.HasSubject => "No subject was detected in this image.",
            SubjectRemoval.DetectedSubject => string.Empty,
            SubjectRemoval.FlatBackground => "How different from the corner colour a pixel may be and still count as background.",
            SubjectRemoval.Automatic => "Existing transparency is kept. Otherwise the detected subject is used, then a flat-background fill.",
            _ => "The image is used exactly as it is.",
        };
        RemovalDetail.Visibility = RemovalDetail.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (!ReferenceEquals(Focused(), NameBox))
        {
            NameBox.Text = a.Name;
        }

        NameBox.PlaceholderText = s.EffectiveName;
        SizeSlider.Value = a.SizeRatio;
        SizeValue.Text = a.SizeRatio.ToString("P0", CultureInfo.CurrentCulture);
        WeightSlider.Value = a.WeightScale;
        WeightValue.Text = "×" + a.WeightScale.ToString("0.00", CultureInfo.CurrentCulture);
        FillSlider.Value = a.Fill;
        FillValue.Text = a.Fill.ToString("P0", CultureInfo.CurrentCulture);
    }

    // --- Input ----------------------------------------------------------------------

    private void OnOpen(object sender, RoutedEventArgs args) => ChooseAndOpen();

    private void ChooseAndOpen()
    {
        if (session is null || owner is null)
        {
            return;
        }

        string? path = Interop.FileDialog.OpenFile(owner(), "Choose an image", ("Images", StudioImageLoader.DialogPattern));
        if (path is not null)
        {
            _ = session.OpenAsync(path);
        }
    }

    private void OnUndo(object sender, RoutedEventArgs args) => session?.Undo();

    private void OnRedo(object sender, RoutedEventArgs args) => session?.Redo();

    private void OnSave(object sender, RoutedEventArgs args) => _ = session?.SaveAsync();

    private void OnUseOnRope(object sender, RoutedEventArgs args)
    {
        if (session is not null)
        {
            session.UseOnRopeAfterSave = UseOnRopeBox.IsChecked == true;
        }
    }

    private void OnMakeAnother(object sender, RoutedEventArgs args) => session?.Clear();

    /// <summary>Drops an image the user does not want, without saving anything.</summary>
    /// <remarks>
    /// Nothing has been written yet, so there is nothing to confirm or undo: the image is
    /// still wherever it was opened from, and opening it again starts over.
    /// </remarks>
    private void OnRemove(object sender, RoutedEventArgs args) => session?.Clear();

    private void OnDone(object sender, RoutedEventArgs args)
    {
        session?.Clear();
        DoneRequested?.Invoke();
    }

    private void OnOpenAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ChooseAndOpen();
    }

    private void OnUndoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // A text box keeps its own undo while it has the keyboard.
        if (Focused() is not TextBox)
        {
            args.Handled = true;
            session?.Undo();
        }
    }

    private void OnRedoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (Focused() is not TextBox)
        {
            args.Handled = true;
            session?.Redo();
        }
    }

    private void OnSaveAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = session?.SaveAsync();
    }

    private async void OnPasteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (Focused() is TextBox)
        {
            return;
        }

        args.Handled = true;
        await PasteAsync();
    }

    /// <summary>Opens whatever image is on the clipboard: a copied file, or copied pixels.</summary>
    internal async Task PasteAsync()
    {
        if (session is null)
        {
            return;
        }

        try
        {
            DataPackageView content = Clipboard.GetContent();
            if (await FirstImageFileAsync(content) is string path)
            {
                await session.OpenAsync(path);
                return;
            }

            if (content.Contains(StandardDataFormats.Bitmap))
            {
                RandomAccessStreamReference reference = await content.GetBitmapAsync();
                using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
                var bytes = new byte[stream.Size];
                await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
                await session.OpenAsync("Pasted image", StudioImageLoader.FromEncoded(bytes));
            }
        }
        catch (Exception exception) when (exception is StudioLoadException or System.Runtime.InteropServices.COMException or IOException)
        {
            Services.Diagnostics.Log($"studio: paste failed: {exception.GetType().Name}");
        }
    }

    private void OnDragOver(object sender, DragEventArgs args)
    {
        if (args.DataView.Contains(StandardDataFormats.StorageItems) || args.DataView.Contains(StandardDataFormats.Bitmap))
        {
            args.AcceptedOperation = DataPackageOperation.Copy;
            args.DragUIOverride.Caption = "Make a charm";
        }
    }

    private async void OnDrop(object sender, DragEventArgs args)
    {
        if (session is not null && await FirstImageFileAsync(args.DataView) is string path)
        {
            await session.OpenAsync(path);
        }
    }

    private static async Task<string?> FirstImageFileAsync(DataPackageView view)
    {
        if (!view.Contains(StandardDataFormats.StorageItems))
        {
            return null;
        }

        IReadOnlyList<IStorageItem> items = await view.GetStorageItemsAsync();
        return items.OfType<StorageFile>().Select(file => file.Path).FirstOrDefault(StudioImageLoader.Handles);
    }

    private void OnRemovalChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering || session is null)
        {
            return;
        }

        SubjectRemoval removal = RemovalChoice.SelectedIndex switch
        {
            1 => new SubjectRemoval.DetectedSubject(null),
            2 => new SubjectRemoval.FlatBackground(SubjectRemoval.DefaultTolerance),
            3 => new SubjectRemoval.KeepOriginal(),
            _ => new SubjectRemoval.Automatic(),
        };
        session.Apply(a => a with { Removal = removal });
    }

    private void OnSubjectChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (rendering || session is null || sender.SelectedItem?.Tag is not int number)
        {
            return;
        }

        session.Apply(a => a with { Removal = new SubjectRemoval.DetectedSubject(number == 0 ? null : number) });
    }

    private void OnToleranceChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (!rendering && session is not null)
        {
            session.Apply(a => a with { Removal = new SubjectRemoval.FlatBackground((int)Math.Round(args.NewValue)) }, recordUndo: false);
        }
    }

    private void OnSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (!rendering)
        {
            session?.Apply(a => a with { SizeRatio = args.NewValue }, recordUndo: false);
        }
    }

    private void OnWeightChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (!rendering)
        {
            session?.Apply(a => a with { WeightScale = args.NewValue }, recordUndo: false);
        }
    }

    private void OnFillChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (!rendering)
        {
            session?.Apply(a => a with { Fill = args.NewValue }, recordUndo: false);
        }
    }

    private void OnNameChanged(object sender, TextChangedEventArgs args)
    {
        if (!rendering)
        {
            session?.Apply(a => a with { Name = NameBox.Text }, recordUndo: false);
        }
    }

    private void OnNameFocus(object sender, RoutedEventArgs args) => session?.BeginEditing();

    private void OnNameBlur(object sender, RoutedEventArgs args) => session?.EndEditing();

    private void OnPreviewModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (preview is null)
        {
            return;
        }

        preview.Mode = (sender.SelectedItem?.Tag as string) switch
        {
            "cutout" => StudioPreviewMode.Cutout,
            "rope" => StudioPreviewMode.OnRope,
            _ => StudioPreviewMode.Charm,
        };
        PreviewCaption.Text = preview.Mode switch
        {
            StudioPreviewMode.Cutout => "Check the edges of the cut-out.",
            StudioPreviewMode.OnRope => "Push it to see how its weight swings.",
            _ => "The charm as the rope will draw it.",
        };
    }

    private async void OnStatusHelp(object sender, RoutedEventArgs args)
    {
        if (session?.ErrorHelp is Uri help)
        {
            await Windows.System.Launcher.LaunchUriAsync(help);
        }
    }

    /// <summary>What has the keyboard, or null before the view is in a window.</summary>
    private object? Focused() => XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot);

    private static void AutomationPropertiesName(UIElement element, string name) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, name);

    /// <summary>A XAML image of premultiplied RGBA pixels.</summary>
    private static WriteableBitmap Bitmap(StudioImage image)
    {
        var bitmap = new WriteableBitmap(image.Width, image.Height);
        byte[] bgra = new byte[image.Pixels.Length];
        for (int index = 0; index < bgra.Length; index += 4)
        {
            bgra[index] = image.Pixels[index + 2];
            bgra[index + 1] = image.Pixels[index + 1];
            bgra[index + 2] = image.Pixels[index];
            bgra[index + 3] = image.Pixels[index + 3];
        }

        using (Stream stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(bgra, 0, bgra.Length);
        }

        bitmap.Invalidate();
        return bitmap;
    }
}
