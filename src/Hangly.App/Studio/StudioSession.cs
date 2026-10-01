//
//  StudioSession.cs
//  Hangly
//
//  Presentation state and the undo history for Creator Studio.
//

using Hangly.Core.Studio;

namespace Hangly.App.Studio;

/// <summary>Backs the Studio; the macOS <c>CharmStudioViewModel</c>, rule for rule.</summary>
/// <remarks>
/// The pipeline is held as its stages — source, detection, isolated image, draft — so an
/// adjustment re-runs only what it touches: changing the background method re-isolates,
/// moving a slider only re-fits. The model runs once per source.
///
/// <para>Adjustments are a value with an undo stack. Discrete changes record a step each; a
/// slider drag records one step for the whole drag, via <see cref="BeginEditing"/> and
/// <see cref="EndEditing"/>. Reprocessing is debounced and generation-checked, so a stale
/// result from a superseded adjustment can never land in the preview.</para>
///
/// <para>Every member is called on the UI thread, and <see cref="Changed"/> is raised there.
/// The heavy steps run on the thread pool and come back.</para>
/// </remarks>
internal sealed class StudioSession : IDisposable
{
    public enum Stage
    {
        Empty,
        Loading,
        Ready,
        Saving,
        Saved,
    }

    /// <summary>Slider moves within this window collapse into one reprocess.</summary>
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(60);

    private readonly Func<ISubjectSegmenter?> makeSegmenter;
    private readonly Func<string, StudioDraft, string, bool, string> save;
    private ISubjectSegmenter? segmenter;
    private CancellationTokenSource? processing;
    private CancellationTokenSource? detecting;
    private int openings;
    private Task pending = Task.CompletedTask;
    private SubjectRemoval? isolatedFor;
    private StudioAdjustments? editingSnapshot;
    private int generation;

    /// <param name="makeSegmenter">Makes the model when a picture is opened; null when none is installed.</param>
    /// <param name="save">
    /// Stores a draft's SVG under a name, hanging it if asked, and returns the saved name.
    /// Called on the UI thread, because saving writes settings and settings changes update
    /// XAML; the slow part, encoding, has already been done off it.
    /// </param>
    public StudioSession(Func<ISubjectSegmenter?> makeSegmenter, Func<string, StudioDraft, string, bool, string> save)
    {
        this.makeSegmenter = makeSegmenter;
        this.save = save;
    }

    /// <summary>Raised on the UI thread whenever anything shown has changed.</summary>
    public event Action? Changed;

    public Stage Current { get; private set; } = Stage.Empty;

    public string? SourcePath { get; private set; }

    public StudioImage? Source { get; private set; }

    public SubjectDetection Detection { get; private set; } = SubjectDetection.None;

    public StudioImage? Isolated { get; private set; }

    public StudioDraft? Draft { get; private set; }

    public StudioAdjustments Adjustments { get; private set; } = new();

    public bool IsProcessing { get; private set; }

    /// <summary>Whether the subject is still being looked for.</summary>
    public bool IsDetecting { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>Where to send someone to fix <see cref="ErrorMessage"/>, when that is possible.</summary>
    public Uri? ErrorHelp { get; private set; }

    public string? SavedName { get; private set; }

    public UndoStack<StudioAdjustments> History { get; } = new();

    /// <summary>Whether saving also puts the charm on the rope. On by default, as on macOS.</summary>
    public bool UseOnRopeAfterSave { get; set; } = true;

    public bool HasImage => Source is not null;

    public bool CanSave => Current == Stage.Ready && Draft is not null && !IsProcessing && !WaitsForSubject;

    /// <summary>
    /// Whether the result on screen is a stand-in until the subject arrives: the method uses
    /// the subject, and the picture has no cut-out of its own for Automatic to keep.
    /// </summary>
    private bool WaitsForSubject => IsDetecting && (Adjustments.Removal is SubjectRemoval.DetectedSubject
        || (Adjustments.Removal is SubjectRemoval.Automatic && Source is StudioImage image && !StudioPipeline.HasMeaningfulAlpha(image)));

    /// <summary>The name that will be saved: the field, or a name derived from the file.</summary>
    public string EffectiveName
    {
        get
        {
            string trimmed = Adjustments.Name.Trim();
            return trimmed.Length > 0 ? trimmed
                : SourcePath is not null ? StudioPipeline.SuggestedName(SourcePath)
                : "Custom Charm";
        }
    }

    public string SubjectSummary => !HasImage ? string.Empty : IsDetecting ? "Finding the subject…" : Detection.InstanceCount switch
    {
        0 => "No subject detected",
        1 => "1 subject detected",
        int count => $"{count} subjects detected",
    };

    /// <summary>Opens a file. <paramref name="name"/> is where it came from, for the name and the log.</summary>
    public Task OpenAsync(string path) => OpenAsync(path, () => StudioImageLoader.LoadAsync(path));

    /// <summary>Opens pixels that came without a file, such as a paste.</summary>
    public Task OpenAsync(string displayName, StudioImage image) => OpenAsync(displayName, () => Task.FromResult(image));

    private async Task OpenAsync(string path, Func<Task<StudioImage>> load)
    {
        processing?.Cancel();
        detecting?.Cancel();
        int mine = ++generation;
        int opened = ++openings;
        Current = Stage.Loading;
        ErrorMessage = null;
        ErrorHelp = null;
        SavedName = null;
        Draft = null;
        Isolated = null;
        isolatedFor = null;
        Detection = SubjectDetection.None;
        IsDetecting = false;
        SourcePath = path;
        Raise();

        StudioImage image;
        try
        {
            image = await load().ConfigureAwait(true);
        }
        catch (Exception exception) when (mine == generation)
        {
            Current = Stage.Empty;
            Source = null;
            SourcePath = null;
            (ErrorMessage, ErrorHelp) = exception is StudioLoadException refused
                ? (refused.Message, refused.Help)
                : ("That picture couldn't be opened.", null);
            Services.Diagnostics.Log($"studio: could not open: {exception.GetType().Name}: {exception.Message}");
            Raise();
            return;
        }

        if (mine != generation)
        {
            return;
        }

        // Ready at once, before the subject is known. On macOS the subject takes a few
        // milliseconds and the Studio simply waits for it; here it can take seconds on a
        // CPU, so the picture is shown and worked on meanwhile — Automatic shows the flat-
        // background result until the subject arrives and replaces it. Saving waits.
        Source = image;
        Adjustments = new StudioAdjustments { Name = StudioPipeline.SuggestedName(path) };
        History.Clear();
        editingSnapshot = null;
        Current = Stage.Ready;
        segmenter ??= makeSegmenter();
        // Always looked for, as macOS always asks Vision: a drawing that already has a cut-out
        // keeps it under Automatic, but can still offer its subjects one by one.
        IsDetecting = segmenter is not null;
        Services.Diagnostics.Log($"studio: opened {Path.GetExtension(path)} {image.Width}×{image.Height}");
        await ReprocessNowAsync().ConfigureAwait(true);

        if (IsDetecting && opened == openings)
        {
            await DetectAsync(image, opened).ConfigureAwait(true);
        }
    }

    /// <summary>Finds the subject in the background and, when it lands, re-runs isolation with it.</summary>
    private async Task DetectAsync(StudioImage image, int opened)
    {
        var cancel = new CancellationTokenSource();
        detecting = cancel;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        SubjectDetection found = SubjectDetection.None;
        try
        {
            SubjectMask? mask = await segmenter!.SegmentAsync(image, cancel.Token).ConfigureAwait(true);
            found = await Task.Run(() => StudioPipeline.Detect(image, mask), cancel.Token).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is OperationCanceledException || cancel.IsCancellationRequested)
        {
            // A newer image, or closing the studio, stopped this run; whatever it threw is not a failure.
            return;
        }
        catch (Exception exception)
        {
            // No subject is an answer, not a failure: the flat-background result stands.
            Services.Diagnostics.Failure("studio subject detection", exception);
        }

        if (opened != openings || cancel.IsCancellationRequested)
        {
            return;
        }

        Detection = found;
        IsDetecting = false;
        Services.Diagnostics.Log($"studio: {SubjectSummary} after {clock.ElapsedMilliseconds} ms");

        // Whatever was isolated without the subject is stale if the method looks at it.
        if (Adjustments.Removal is SubjectRemoval.Automatic or SubjectRemoval.DetectedSubject)
        {
            Isolated = null;
            isolatedFor = null;
            await ReprocessNowAsync().ConfigureAwait(true);
        }
        else
        {
            Raise();
        }
    }

    /// <summary>Looks for the subject again if closing the Studio stopped the last search.</summary>
    public void ResumeDetection()
    {
        if (Source is StudioImage image && Current == Stage.Ready && !IsDetecting
            && !Detection.HasSubject)
        {
            segmenter ??= makeSegmenter();
            if (segmenter is not null)
            {
                IsDetecting = true;
                Raise();
                _ = DetectAsync(image, openings);
            }
        }
    }

    /// <summary>Back to the empty state, ready for another image.</summary>
    public void Clear()
    {
        processing?.Cancel();
        detecting?.Cancel();
        generation++;
        openings++;
        IsDetecting = false;
        Current = Stage.Empty;
        SourcePath = null;
        Source = null;
        Detection = SubjectDetection.None;
        Isolated = null;
        isolatedFor = null;
        Draft = null;
        Adjustments = new();
        History.Clear();
        editingSnapshot = null;
        ErrorMessage = null;
        ErrorHelp = null;
        SavedName = null;
        IsProcessing = false;
        Raise();
    }

    /// <summary>Applies a change. Records an undo step unless the caller is mid-drag.</summary>
    public void Apply(Func<StudioAdjustments, StudioAdjustments> change, bool recordUndo = true)
    {
        StudioAdjustments next = change(Adjustments).Clamped();
        if (next == Adjustments)
        {
            return;
        }

        if (recordUndo && editingSnapshot is null)
        {
            History.Record(Adjustments);
        }

        bool affectsImage = next.Removal != Adjustments.Removal
            || next.Fill != Adjustments.Fill
            || next.SizeRatio != Adjustments.SizeRatio
            || next.WeightScale != Adjustments.WeightScale;
        Adjustments = next;
        Raise();
        if (affectsImage)
        {
            ScheduleReprocess();
        }
    }

    /// <summary>Marks the start of a continuous edit such as a slider drag.</summary>
    public void BeginEditing() => editingSnapshot ??= Adjustments;

    /// <summary>Ends a continuous edit, recording the whole drag as one undo step.</summary>
    public void EndEditing()
    {
        if (editingSnapshot is not StudioAdjustments snapshot)
        {
            return;
        }

        editingSnapshot = null;
        if (snapshot != Adjustments)
        {
            History.Record(snapshot);
        }

        Raise();
    }

    public void Undo()
    {
        EndEditing();
        if (History.TryUndo(Adjustments, out StudioAdjustments previous))
        {
            Adjustments = previous;
            Raise();
            ScheduleReprocess();
        }
    }

    public void Redo()
    {
        EndEditing();
        if (History.TryRedo(Adjustments, out StudioAdjustments next))
        {
            Adjustments = next;
            Raise();
            ScheduleReprocess();
        }
    }

    private void ScheduleReprocess()
    {
        processing?.Cancel();
        var cancel = new CancellationTokenSource();
        processing = cancel;
        pending = DebouncedAsync(cancel.Token);
    }

    private async Task DebouncedAsync(CancellationToken cancellation)
    {
        try
        {
            await Task.Delay(Debounce, cancellation).ConfigureAwait(true);
            await ReprocessNowAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer change.
        }
    }

    private async Task ReprocessNowAsync()
    {
        if (Source is not StudioImage source)
        {
            return;
        }

        int mine = ++generation;
        StudioAdjustments current = Adjustments;
        IsProcessing = true;
        Raise();

        try
        {
            StudioImage isolated;
            if (Isolated is not null && isolatedFor == current.Removal)
            {
                isolated = Isolated;
            }
            else
            {
                SubjectDetection detection = Detection;
                isolated = await Task.Run(() => StudioPipeline.Isolate(source, detection, current.Removal)).ConfigureAwait(true);
                if (mine != generation)
                {
                    return;
                }

                Isolated = isolated;
                isolatedFor = current.Removal;
            }

            StudioDraft draft = await Task.Run(() => StudioPipeline.Draft(StudioRaster.FitToSquare(isolated, current.Fill), current)).ConfigureAwait(true);
            if (mine != generation)
            {
                return;
            }

            Draft = draft;
            ErrorMessage = null;
            ErrorHelp = null;
        }
        catch (Exception exception) when (mine == generation)
        {
            Draft = null;
            Isolated = null;
            isolatedFor = null;
            ErrorMessage = exception switch
            {
                StudioLoadException load => load.Message,
                InvalidOperationException invalid => invalid.Message,
                _ => "That setting couldn't be applied to this picture.",
            };
        }
        finally
        {
            if (mine == generation)
            {
                IsProcessing = false;
                Raise();
            }
        }
    }

    public async Task SaveAsync()
    {
        // Let a pending slider settle, exactly as macOS flushes before saving.
        await pending.ConfigureAwait(true);

        if (!CanSave || Draft is not StudioDraft draft)
        {
            return;
        }

        Current = Stage.Saving;
        Raise();
        try
        {
            string name = EffectiveName;
            bool hang = UseOnRopeAfterSave;
            string markup = await Task.Run(() => StudioRaster.ToSvg(draft.Square)).ConfigureAwait(true);
            SavedName = save(markup, draft, name, hang);
            Current = Stage.Saved;
            Services.Diagnostics.Log("studio: saved a charm");
        }
        catch (Exception exception)
        {
            ErrorMessage = "That charm couldn't be saved.";
            Current = Stage.Ready;
            Services.Diagnostics.Failure("studio save", exception);
        }

        Raise();
    }

    /// <summary>Gives the model and the pictures back, as closing the Studio should.</summary>
    /// <remarks>
    /// An image opened and not saved is kept, as on macOS — coming back to Create finds it
    /// where it was left — but the model is released either way: it is the 170 MB part,
    /// and it is quick to load again.
    /// </remarks>
    public void ReleaseModel()
    {
        detecting?.Cancel();
        if (IsDetecting)
        {
            // Picked up again with a fresh model the next time a picture is opened.
            IsDetecting = false;
            openings++;
            Raise();
        }

        bool hadModel = segmenter is not null;
        (segmenter as IDisposable)?.Dispose();
        segmenter = null;
        if (Current == Stage.Saved)
        {
            Clear();
        }

        if (hadModel)
        {
            // Once, on close, never per frame: the pictures are large-object-heap arrays the
            // runtime would otherwise keep until memory was short, and "closing the Studio
            // gives the memory back" should be true as measured, not eventually.
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            Services.Diagnostics.Log($"studio: model released; managed heap {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        }
    }

    public void Dispose()
    {
        processing?.Cancel();
        detecting?.Cancel();
        (segmenter as IDisposable)?.Dispose();
        segmenter = null;
    }

    private void Raise() => Changed?.Invoke();
}
