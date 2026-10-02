//
//  OnnxSegmenter.cs
//  Hangly
//
//  Finding the subject of a picture, on this machine, with nothing sent anywhere.
//

using System.Diagnostics;
using Hangly.App.Services;
using Hangly.Core.Studio;
using Microsoft.ML.OnnxRuntime;

namespace Hangly.App.Studio;

/// <summary>The shipped subject model, run through ONNX Runtime: GPU where there is one, CPU where not.</summary>
/// <remarks>
/// <b>Which model, and why.</b> IS-Net (DIS, general use), Apache-2.0. A bake-off on 36
/// charm composites with exact ground truth and 18 real photos compared against the macOS
/// Vision cut-outs put it level with Vision on the composites (IoU 0.933 against 0.932).
/// BiRefNet was a little more accurate and about fourteen times slower on a CPU — 58 s a
/// picture in the Windows VM against 4.2 s — so IS-Net is the default and BiRefNet is a
/// possible later High Quality mode behind this same interface (D1 follow-up).
///
/// <para><b>Lifetime.</b> One instance per Studio window: the model is loaded when the
/// Studio opens and released when it closes, so the 170 MB of weights are resident only
/// while someone is making a charm, and idle memory is what it was before (M3). Loading is
/// on a worker thread; the Studio shows its own "finding the subject" state meanwhile.</para>
///
/// <para><b>Speed.</b> DirectML is tried first and the CPU is the fallback; either way the
/// Studio never waits on this to be usable: see <see cref="StudioSession"/>.</para>
///
/// <para><b>Idle CPU.</b> ONNX Runtime's worker threads spin after a run by default, waiting
/// for the next one. Spinning is switched off, so between cut-outs the threads sleep.</para>
/// </remarks>
internal sealed class OnnxSegmenter : ISubjectSegmenter, IDisposable
{
    /// <summary>
    /// For comparing models on real hardware only: <c>HANGLY_STUDIO_MODEL</c> names another
    /// .onnx file, with <c>birefnet</c> in its name selecting BiRefNet's input shape. Inert when unset.
    /// </summary>
    private static readonly string? OverrideModel = Environment.GetEnvironmentVariable("HANGLY_STUDIO_MODEL") is { Length: > 0 } path ? path : null;

    private static readonly SegmentationModelShape Shape =
        OverrideModel?.Contains("birefnet", StringComparison.OrdinalIgnoreCase) == true
            ? SegmentationModelShape.BiRefNet
            : SegmentationModelShape.IsNet;

    private readonly Lazy<InferenceSession> session;
    private readonly SemaphoreSlim oneAtATime = new(1, 1);
    private bool disposed;

    public OnnxSegmenter(string? modelPath = null)
    {
        string path = modelPath ?? OverrideModel ?? DefaultModelPath;
        session = new Lazy<InferenceSession>(() => Load(path), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Where the install puts the model.</summary>
    public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "Models", "subject.onnx");

    /// <summary>Whether this install has the model at all; a developer build might not.</summary>
    public static bool IsInstalled => File.Exists(DefaultModelPath);

    public string Name => $"IS-Net (ONNX Runtime, {Provider})";

    /// <summary>Which execution provider the model ended up on, once loaded.</summary>
    public string Provider { get; private set; } = "not loaded";

    /// <summary>Loads the model now, off the caller's thread, so the first cut-out does not wait for it.</summary>
    public Task WarmUpAsync() => Task.Run(() => _ = session.Value);

    public async Task<SubjectMask?> SegmentAsync(StudioImage image, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(image);
        ObjectDisposedException.ThrowIf(disposed, this);

        await oneAtATime.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Run(image, cancellation), cancellation).ConfigureAwait(false);
        }
        finally
        {
            oneAtATime.Release();
        }
    }

    private SubjectMask? Run(StudioImage image, CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        float[] input = SegmentationTensors.Input(image, Shape);
        long prepared = clock.ElapsedMilliseconds;

        InferenceSession model = session.Value;
        using var options = new RunOptions();
        using CancellationTokenRegistration stop = cancellation.Register(() => options.Terminate = true);
        using var tensor = OrtValue.CreateTensorValueFromMemory(input, [1, 3, Shape.Side, Shape.Side]);
        using IDisposableReadOnlyCollection<OrtValue> outputs = Infer(model, options, tensor, cancellation);
        cancellation.ThrowIfCancellationRequested();
        long inferred = clock.ElapsedMilliseconds;

        SubjectMask mask = SegmentationTensors.Mask(outputs[0].GetTensorDataAsSpan<float>(), Shape, image.Width, image.Height);
        Diagnostics.Log($"studio: subject {image.Width}×{image.Height} on {Provider} in {clock.ElapsedMilliseconds} ms (prepare {prepared}, model {inferred - prepared})");
        return mask;
    }

    /// <summary>Runs the model once; a run stopped through <paramref name="options"/> comes back as a cancellation.</summary>
    private static IDisposableReadOnlyCollection<OrtValue> Infer(
        InferenceSession model, RunOptions options, OrtValue tensor, CancellationToken cancellation)
    {
        try
        {
            return model.Run(options, [model.InputNames[0]], [tensor], [model.OutputNames[0]]);
        }
        catch (OnnxRuntimeException) when (cancellation.IsCancellationRequested)
        {
            // Terminate stops the run by throwing "Exiting due to terminate flag being set to true",
            // not a cancellation: say what it is, so it never reaches the crash reports as a failure.
            throw new OperationCanceledException(cancellation);
        }
    }

    private InferenceSession Load(string path)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            // DirectML's documented requirements: no memory pattern, sequential execution.
            using SessionOptions gpu = Options();
            gpu.AppendExecutionProvider_DML(0);
            var loaded = new InferenceSession(path, gpu);
            Provider = "DirectML";
            Diagnostics.Log($"studio: subject model loaded on DirectML in {clock.ElapsedMilliseconds} ms");
            return loaded;
        }
        catch (OnnxRuntimeException exception)
        {
            // No DirectX 12 GPU, a driver DirectML refuses, or a remote session: the CPU is
            // slower and always there.
            Diagnostics.Log($"studio: DirectML unavailable ({exception.Message.Split('\n')[0]}); using the CPU");
        }

        using SessionOptions cpu = Options();
        var fallback = new InferenceSession(path, cpu);
        Provider = "CPU";
        Diagnostics.Log($"studio: subject model loaded on the CPU in {clock.ElapsedMilliseconds} ms");
        return fallback;
    }

    private static SessionOptions Options()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,

            // Without these the CPU provider reserved about 8 GB of commit for one
            // 1024² picture and kept it until the session was released (measured in the VM).
            EnableCpuMemArena = false,
            EnableMemoryPattern = false,
        };
        options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        options.AddSessionConfigEntry("session.inter_op.allow_spinning", "0");
        return options;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        // Waits out a cut-out in progress rather than pull the weights from under it.
        oneAtATime.Wait();
        if (session.IsValueCreated)
        {
            session.Value.Dispose();
        }

        oneAtATime.Dispose();
    }
}
