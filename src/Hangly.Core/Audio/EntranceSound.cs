//
//  EntranceSound.cs
//  Hangly
//
//  The Spider-Man entrance's sound: whatever file is in the slot, lined up with the web.
//

using Hangly.Core.Models;

namespace Hangly.Core.Audio;

/// <summary>Prepares the entrance sound. Identical to macOS's <c>EntranceSound</c>, pinned by the same tests.</summary>
/// <remarks>
/// <b>The asset is swappable; the timing is not in it.</b> The sound is read from a fixed
/// slot, <see cref="FileName"/>, and lined up by rule rather than by a number measured on one
/// recording: its attack — the first moment it gets properly loud — is placed a hair after
/// the entrance begins, as the web blooms; it then plays for the entrance and a short tail and
/// fades out. A different recording in the slot lines up the same way with nothing else
/// changed. With no file in the slot the entrance is silent.
/// </remarks>
public static class EntranceSound
{
    /// <summary>The slot: <c>Assets/Sounds/</c> beside the app on Windows, <c>Resources/Sounds/</c> in the bundle on macOS.</summary>
    public const string FileName = "SpiderManEntrance.wav";

    /// <summary>The attack is where the sound first reaches this fraction of its own peak.</summary>
    public const double OnsetFraction = 0.25;

    /// <summary>How much of the build-up before the attack is kept, in seconds.</summary>
    public const double PreRoll = 0.03;

    /// <summary>How long it plays: the entrance, and a short tail as the charm settles.</summary>
    public const double Length = IntroTable.Duration + 0.35;

    /// <summary>The fade at the end, in seconds, so a longer recording never stops dead.</summary>
    public const double FadeOut = 0.35;

    /// <summary>A few milliseconds in, so a cut into the build-up never clicks.</summary>
    public const double FadeIn = 0.005;

    /// <summary>
    /// The samples to play, at <paramref name="targetRate"/>: from just before the attack, for
    /// <see cref="Length"/>, faded in and out. Empty when there is nothing audible.
    /// </summary>
    public static float[] Prepare(float[] samples, int rate, double targetRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Length == 0 || rate <= 0 || targetRate <= 0)
        {
            return [];
        }

        float peak = 0;
        foreach (float sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        if (peak <= 1e-4f)
        {
            return [];
        }

        int onset = 0;
        float threshold = (float)(peak * OnsetFraction);
        while (onset < samples.Length && Math.Abs(samples[onset]) < threshold)
        {
            onset++;
        }

        double start = Math.Max(0, (onset / (double)rate) - PreRoll);
        int count = (int)Math.Round(Length * targetRate);
        var output = new float[count];
        for (int index = 0; index < count; index++)
        {
            // Linear resampling: the slot's rate need not be the player's.
            double position = (start + (index / targetRate)) * rate;
            int whole = (int)position;
            if (whole + 1 >= samples.Length)
            {
                break;
            }

            double fraction = position - whole;
            output[index] = (float)((samples[whole] * (1 - fraction)) + (samples[whole + 1] * fraction));
        }

        int fadeIn = (int)(FadeIn * targetRate);
        int fadeOut = (int)(FadeOut * targetRate);
        for (int index = 0; index < count; index++)
        {
            double gain = 1;
            if (index < fadeIn)
            {
                gain = index / (double)fadeIn;
            }

            int fromEnd = count - 1 - index;
            if (fromEnd < fadeOut)
            {
                // A cosine, so the tail thins out rather than stepping down.
                gain *= 0.5 - (0.5 * Math.Cos(Math.PI * fromEnd / fadeOut));
            }

            output[index] = (float)(output[index] * gain);
        }

        return output;
    }
}

/// <summary>Reads a WAV file's samples as mono floats: 16- or 24-bit PCM, or 32-bit float, any channel count.</summary>
public static class WavReader
{
    public static (float[] Samples, int Rate)? MonoSamples(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 12 || Tag(data, 0) != "RIFF" || Tag(data, 8) != "WAVE")
        {
            return null;
        }

        int format = 0, channels = 0, rate = 0, bits = 0;
        int offset = 12;
        while (offset + 8 <= data.Length)
        {
            string id = Tag(data, offset);
            int size = BitConverter.ToInt32(data, offset + 4);
            int body = offset + 8;
            if (size < 0 || body + size > data.Length)
            {
                size = data.Length - body;
            }

            if (id == "fmt " && size >= 16)
            {
                format = BitConverter.ToUInt16(data, body);
                channels = BitConverter.ToUInt16(data, body + 2);
                rate = BitConverter.ToInt32(data, body + 4);
                bits = BitConverter.ToUInt16(data, body + 14);
                if (format == 0xFFFE && size >= 26)
                {
                    format = BitConverter.ToUInt16(data, body + 24); // WAVE_FORMAT_EXTENSIBLE's sub-format
                }
            }
            else if (id == "data" && channels > 0 && rate > 0)
            {
                return (Decode(data, body, size, format, channels, bits), rate);
            }

            offset = body + size + (size & 1);
        }

        return null;
    }

    private static float[] Decode(byte[] data, int start, int size, int format, int channels, int bits)
    {
        int bytes = bits / 8;
        if (bytes == 0 || !((format == 1 && bits is 16 or 24) || (format == 3 && bits == 32)))
        {
            return [];
        }

        int frames = size / (bytes * channels);
        var mono = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            double sum = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                int at = start + (((frame * channels) + channel) * bytes);
                sum += format == 3
                    ? BitConverter.ToSingle(data, at)
                    : bits == 16
                        ? BitConverter.ToInt16(data, at) / 32768.0
                        : ((data[at] | (data[at + 1] << 8) | ((sbyte)data[at + 2] << 16)) / 8388608.0);
            }

            mono[frame] = (float)(sum / channels);
        }

        return mono;
    }

    private static string Tag(byte[] data, int offset) => System.Text.Encoding.ASCII.GetString(data, offset, 4);
}
