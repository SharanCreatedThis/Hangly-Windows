//
//  SoundSynthesizer.cs
//  Hangly
//
//  Five materials, made from sine waves and a little noise.
//

namespace Hangly.Core.Audio;

/// <summary>Produces mono PCM samples for each <see cref="CharmSound"/>.</summary>
/// <remarks>
/// A line-for-line port of the macOS <c>SoundSynthesizer</c>: the same recipes, the same
/// fixed-seed noise, the same envelopes and the same normalisation, so a charm sounds the
/// same on both platforms. Additive synthesis — a sum of exponentially decaying sine
/// partials, plus filtered noise for the percussive materials. Deterministic, so a test can
/// pin the output and two launches sound identical. A few hundred kilobytes in all,
/// generated once and cached by whoever plays them.
///
/// <para>Computed in <see cref="double"/> and stored as <see cref="float"/>, as Swift does;
/// the arithmetic follows the Swift expression order so the two agree to the last bit or
/// within one float ulp.</para>
/// </remarks>
public static class SoundSynthesizer
{
    public const double SampleRate = 44_100.0;

    /// <summary>One sine component of a struck object.</summary>
    /// <param name="Ratio">Multiple of the fundamental; inharmonic ratios make metal sound like metal.</param>
    /// <param name="Decay">Time constant of the exponential decay, in seconds.</param>
    public readonly record struct Partial(double Ratio, double Amplitude, double Decay);

    /// <summary>A recipe: a fundamental, its partials, how long it lasts, and its fade-in.</summary>
    public sealed record Voice(double Fundamental, IReadOnlyList<Partial> Partials, double Duration, double Attack);

    /// <summary>Full-scale samples in -1…1, peak-normalised per sound.</summary>
    public static float[] Samples(CharmSound sound) => sound switch
    {
        CharmSound.Bell => Normalized(Render(Bell), 0.85f),
        CharmSound.Glass => Normalized(Render(Glass), 0.7f),
        CharmSound.Metal => Normalized(Render(Metal), 0.75f),
        CharmSound.Wood => Normalized(Mix(NoiseBurst(0.07, 0.012, 0.25, 0.7), Render(Wood)), 0.8f),
        _ => Normalized(Mix(Render(Soft), NoiseBurst(0.09, 0.006, 0.08, 0.15)), 0.5f),
    };

    public static readonly Voice Bell = new(
        1046,
        [
            new(1.00, 1.00, 1.30),
            new(2.00, 0.55, 0.90),
            new(2.41, 0.40, 0.70),
            new(3.00, 0.30, 0.50),
            new(4.52, 0.18, 0.35),
            new(5.19, 0.10, 0.25),
        ],
        1.5,
        0.003);

    public static readonly Voice Glass = new(
        2600,
        [
            new(1.00, 1.0, 0.28),
            new(1.90, 0.5, 0.20),
            new(2.75, 0.3, 0.14),
        ],
        0.35,
        0.001);

    public static readonly Voice Metal = new(
        1800,
        [
            new(1.00, 1.00, 0.45),
            new(1.56, 0.60, 0.32),
            new(2.31, 0.35, 0.22),
            new(3.10, 0.20, 0.15),
        ],
        0.55,
        0.001);

    public static readonly Voice Wood = new(
        190,
        [
            new(1.0, 1.0, 0.03),
            new(1.7, 0.4, 0.02),
        ],
        0.07,
        0.0005);

    public static readonly Voice Soft = new(160, [new(1, 1, 0.045)], 0.09, 0.002);

    /// <summary>Tail fade, so a still-ringing sound never ends on a cut, which would click.</summary>
    public const double Release = 0.12;

    /// <summary>Decaying sines with a short linear attack and a release fade at the end.</summary>
    public static float[] Render(Voice voice)
    {
        ArgumentNullException.ThrowIfNull(voice);
        int count = (int)(voice.Duration * SampleRate);
        double release = Math.Min(Release, voice.Duration * 0.3);
        var output = new float[count];
        for (int index = 0; index < count; index++)
        {
            double time = index / SampleRate;
            double attack = voice.Attack > 0 ? Math.Min(1, time / voice.Attack) : 1;
            double fade = Math.Min(1, (voice.Duration - time) / release);
            double envelope = attack * Math.Max(0, fade);
            double value = 0.0;
            foreach (Partial partial in voice.Partials)
            {
                double amplitude = partial.Amplitude * Math.Exp(-time / partial.Decay);
                value += amplitude * Math.Sin(2 * Math.PI * voice.Fundamental * partial.Ratio * time);
            }

            output[index] = (float)(value * envelope);
        }

        return output;
    }

    /// <summary>White noise from a fixed-seed generator, one-pole low-passed and decayed.</summary>
    public static float[] NoiseBurst(double duration, double decay, double smoothing, double gain)
    {
        int count = (int)(duration * SampleRate);
        var output = new float[count];
        uint state = 0x9E37_79B9;
        double filtered = 0.0;
        for (int index = 0; index < count; index++)
        {
            // Linear congruential generator, wrapping as Swift's &* and &+ do.
            unchecked
            {
                state = (state * 1_664_525u) + 1_013_904_223u;
            }

            double white = (state / (double)uint.MaxValue * 2) - 1;
            filtered += smoothing * (white - filtered);
            double time = index / SampleRate;
            output[index] = (float)(filtered * Math.Exp(-time / decay) * gain);
        }

        return output;
    }

    public static float[] Mix(float[] first, float[] second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var output = new float[Math.Max(first.Length, second.Length)];
        for (int index = 0; index < output.Length; index++)
        {
            float left = index < first.Length ? first[index] : 0;
            float right = index < second.Length ? second[index] : 0;
            output[index] = left + right;
        }

        return output;
    }

    /// <summary>Scales so the loudest sample sits at <paramref name="peak"/>.</summary>
    public static float[] Normalized(float[] samples, float peak)
    {
        ArgumentNullException.ThrowIfNull(samples);
        float loudest = samples.Length == 0 ? 0 : samples.Max(Math.Abs);
        if (loudest <= 0)
        {
            return samples;
        }

        float scale = peak / loudest;
        return [.. samples.Select(sample => sample * scale)];
    }
}
