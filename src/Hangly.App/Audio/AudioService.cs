//
//  AudioService.cs
//  Hangly
//
//  Playing a material's sound, and holding nothing open when there is none.
//

using System.Runtime.InteropServices;
using Hangly.App.Services;
using Hangly.Core.Audio;
using Hangly.Core.Models;
using Hangly.Core.Settings;

namespace Hangly.App.Audio;

/// <summary>Sound effects, gated by the settings and by the system being quiet.</summary>
/// <remarks>
/// <b>What it is built on, and why.</b> One winmm <c>waveOut</c> handle per sound: opened
/// when a sound starts, closed when it ends. Windows mixes simultaneous handles, so a knock
/// can ring over a throw as it does on macOS, and between sounds nothing is open — no
/// device, no render thread, no timer. That is the idle-CPU budget met by construction
/// rather than by a timeout. winmm is documented and supported, and routes through the
/// same mixer as everything else, so the system's mute and volume apply.
///
/// <para>AudioGraph was the first choice and is not used: feeding it samples needs a
/// buffer's address through <c>IMemoryBufferByteAccess</c>, and CsWinRT will not cast a
/// projected object to a ComImport interface — the same wall the overlay surface hit.</para>
///
/// <para>Samples come from <see cref="SoundSynthesizer"/>, verified identical to macOS, and
/// are cached per material. The volume is applied to a copy of the samples at play time,
/// because a handle's own volume control changes the whole device's.</para>
///
/// <para>Every decision about whether to play is <see cref="SoundPolicy"/>'s; this only
/// plays. A device that refuses to open marks audio unavailable for the session, as on
/// macOS, rather than failing on every swing.</para>
/// </remarks>
public sealed class AudioService : IDisposable
{
    private readonly SettingsStore store;
    private readonly Dictionary<CharmSound, float[]> samples = [];
    private readonly List<Playing> playing = [];
    private readonly object gate = new();
    private long lastPlayTicks;
    private bool isAvailable = true;

    internal AudioService(SettingsStore store) => this.store = store;

    /// <summary>Plays <paramref name="sound"/> at <paramref name="intensity"/> of the user's volume, if it should.</summary>
    /// <remarks>Safe from any thread; returns at once.</remarks>
    public void Play(CharmSound sound, double intensity)
    {
        AppSettings settings = store.Settings;
        double sinceLast = (Environment.TickCount64 - Interlocked.Read(ref lastPlayTicks)) / 1000.0;

        // Cheap checks first; the system's quiet state is one shell call, made only when a
        // sound is otherwise about to play.
        if (!isAvailable || !settings.SoundEffectsEnabled || sinceLast < SoundPolicy.Cooldown)
        {
            return;
        }

        double? volume = SoundPolicy.Volume(true, settings.SoundVolume, intensity, sinceLast, QuietState.IsQuiet());
        if (volume is not double level)
        {
            return;
        }

        Interlocked.Exchange(ref lastPlayTicks, Environment.TickCount64);
        try
        {
            Start(Scaled(SamplesFor(sound), (float)level));
            if (LogsEachSound)
            {
                Diagnostics.Log($"audio: {sound} at {level:0.000}");
            }
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            isAvailable = false;
            Diagnostics.Log($"audio unavailable; sounds off for this session: {exception.Message}");
        }
    }

    /// <summary>The Spider-Man entrance's sound, from the asset slot; silent when the slot is empty.</summary>
    /// <remarks>
    /// Under the same rules as every other sound — Play sound effects, the volume, and the
    /// system's quiet state — and played once, from samples prepared for this call and let go
    /// when it ends: nothing is kept. The entrance itself never plays under reduced motion, so
    /// neither does this.
    /// </remarks>
    public void PlayEntrance()
    {
        AppSettings settings = store.Settings;
        if (!isAvailable || !settings.SoundEffectsEnabled)
        {
            return;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", EntranceSound.FileName);
        if (!File.Exists(path))
        {
            return;
        }

        double? volume = SoundPolicy.Volume(true, settings.SoundVolume, 1, double.MaxValue, QuietState.IsQuiet());
        if (volume is not double level)
        {
            return;
        }

        try
        {
            if (WavReader.MonoSamples(File.ReadAllBytes(path)) is not (float[] samples, int rate))
            {
                Diagnostics.Log("audio: the entrance sound is not a WAV this build can read; silent");
                return;
            }

            float[] prepared = EntranceSound.Prepare(samples, rate, SoundSynthesizer.SampleRate);
            if (prepared.Length > 0)
            {
                Interlocked.Exchange(ref lastPlayTicks, Environment.TickCount64);
                Start(Scaled(prepared, (float)level));
                if (LogsEachSound)
                {
                    Diagnostics.Log($"audio: entrance at {level:0.000}, {prepared.Length / SoundSynthesizer.SampleRate:0.00} s");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Diagnostics.Log($"audio: could not read the entrance sound: {exception.Message}");
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            isAvailable = false;
            Diagnostics.Log($"audio unavailable; sounds off for this session: {exception.Message}");
        }
    }

    /// <summary>For audits only: <c>HANGLY_AUDIT_SOUND</c> logs every sound played.</summary>
    private static readonly bool LogsEachSound = Environment.GetEnvironmentVariable("HANGLY_AUDIT_SOUND") is { Length: > 0 };

    /// <summary>How many sounds are playing, for audits: zero means nothing is open.</summary>
    public int OpenHandles
    {
        get
        {
            lock (gate)
            {
                return playing.Count;
            }
        }
    }

    private float[] SamplesFor(CharmSound sound)
    {
        lock (gate)
        {
            if (!samples.TryGetValue(sound, out float[]? cached))
            {
                cached = SoundSynthesizer.Samples(sound);
                samples[sound] = cached;
            }

            return cached;
        }
    }

    private static short[] Scaled(float[] source, float volume)
    {
        var pcm = new short[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            pcm[index] = (short)Math.Clamp(source[index] * volume * short.MaxValue, short.MinValue, short.MaxValue);
        }

        return pcm;
    }

    /// <summary>Opens a handle, writes the whole sound, and closes it when Windows says it is done.</summary>
    private void Start(short[] pcm)
    {
        Reap();

        var format = new WaveFormat
        {
            FormatTag = 1, // PCM
            Channels = 1,
            SamplesPerSecond = (int)SoundSynthesizer.SampleRate,
            BitsPerSample = 16,
            BlockAlign = 2,
            AverageBytesPerSecond = (int)SoundSynthesizer.SampleRate * 2,
        };

        Check(WaveOutOpen(out IntPtr handle, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0), "waveOutOpen");

        var data = GCHandle.Alloc(pcm, GCHandleType.Pinned);
        IntPtr header = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
        Marshal.StructureToPtr(
            new WaveHeader { Data = data.AddrOfPinnedObject(), BufferLength = (uint)(pcm.Length * 2) },
            header,
            false);

        var entry = new Playing(handle, header, data, Environment.TickCount64 + (pcm.Length * 1000L / (long)SoundSynthesizer.SampleRate) + 250);
        try
        {
            Check(WaveOutPrepareHeader(handle, header, (uint)Marshal.SizeOf<WaveHeader>()), "waveOutPrepareHeader");
            Check(WaveOutWrite(handle, header, (uint)Marshal.SizeOf<WaveHeader>()), "waveOutWrite");
        }
        catch
        {
            entry.Close();
            throw;
        }

        lock (gate)
        {
            playing.Add(entry);
        }

        // Closed after it has played. A one-shot timer rather than a thread or a poll: it
        // exists only while a sound does.
        _ = Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, entry.EndsAt - Environment.TickCount64))).ContinueWith(_ => Reap(), TaskScheduler.Default);
    }

    /// <summary>Closes every handle whose sound has finished.</summary>
    private void Reap()
    {
        lock (gate)
        {
            for (int index = playing.Count - 1; index >= 0; index--)
            {
                if (playing[index].IsDone || Environment.TickCount64 > playing[index].EndsAt + 2000)
                {
                    playing[index].Close();
                    playing.RemoveAt(index);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (Playing entry in playing)
            {
                WaveOutReset(entry.Handle);
                entry.Close();
            }

            playing.Clear();
        }
    }

    private static void Check(int result, string what)
    {
        if (result != 0)
        {
            throw new InvalidOperationException($"{what} failed: {result}");
        }
    }

    private sealed class Playing(IntPtr handle, IntPtr header, GCHandle data, long endsAt)
    {
        public IntPtr Handle { get; } = handle;

        public long EndsAt { get; } = endsAt;

        /// <summary>WHDR_DONE: the device has finished with the buffer.</summary>
        public bool IsDone => (Marshal.PtrToStructure<WaveHeader>(header).Flags & 0x1) != 0;

        public void Close()
        {
            WaveOutUnprepareHeader(Handle, header, (uint)Marshal.SizeOf<WaveHeader>());
            WaveOutClose(Handle);
            Marshal.FreeHGlobal(header);
            if (data.IsAllocated)
            {
                data.Free();
            }
        }
    }

    private const uint WaveMapper = unchecked((uint)-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public short FormatTag;
        public short Channels;
        public int SamplesPerSecond;
        public int AverageBytesPerSecond;
        public short BlockAlign;
        public short BitsPerSample;
        public short ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [DllImport("winmm.dll", EntryPoint = "waveOutOpen")]
    private static extern int WaveOutOpen(out IntPtr handle, uint device, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll", EntryPoint = "waveOutPrepareHeader")]
    private static extern int WaveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);

    [DllImport("winmm.dll", EntryPoint = "waveOutWrite")]
    private static extern int WaveOutWrite(IntPtr handle, IntPtr header, uint size);

    [DllImport("winmm.dll", EntryPoint = "waveOutUnprepareHeader")]
    private static extern int WaveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);

    [DllImport("winmm.dll", EntryPoint = "waveOutReset")]
    private static extern int WaveOutReset(IntPtr handle);

    [DllImport("winmm.dll", EntryPoint = "waveOutClose")]
    private static extern int WaveOutClose(IntPtr handle);
}

/// <summary>Whether Windows is keeping things quiet right now.</summary>
/// <remarks>
/// <c>SHQueryUserNotificationState</c>, the documented answer to "would Windows show a
/// notification now". It says no while a full-screen app, a game or a presentation is in
/// front, and in the quiet hour after a new sign-in; Hangly stays silent in the same
/// states. Windows has no documented way to read Do Not Disturb itself, and the
/// undocumented one can break between builds, so it is deliberately not used — the
/// decision recorded as D4.
/// </remarks>
internal static class QuietState
{
    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    private const int NotPresent = 1;
    private const int AcceptsNotifications = 5;

    public static bool IsQuiet() =>
        SHQueryUserNotificationState(out int state) == 0 && state is not (NotPresent or AcceptsNotifications);
}
