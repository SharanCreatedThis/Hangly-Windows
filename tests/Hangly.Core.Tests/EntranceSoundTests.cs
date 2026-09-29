using Hangly.Core.Audio;
using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The entrance sound. The macOS suite runs the same cases.</summary>
public sealed class EntranceSoundTests
{
    /// <summary>A WAV: silence, then a burst at <paramref name="burstAt"/> seconds.</summary>
    private static byte[] Wav(int rate, int channels, double seconds, double burstAt)
    {
        int frames = (int)(rate * seconds);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + (frames * channels * 2));
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(rate);
        writer.Write(rate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(frames * channels * 2);
        for (int frame = 0; frame < frames; frame++)
        {
            double time = frame / (double)rate;
            short value = time >= burstAt ? (short)(Math.Sin(2 * Math.PI * 440 * time) * 20000) : (short)0;
            for (int channel = 0; channel < channels; channel++)
            {
                writer.Write(value);
            }
        }

        return stream.ToArray();
    }

    [Fact]
    public void AWavIsReadAsMonoAtItsOwnRate()
    {
        (float[] samples, int rate) = WavReader.MonoSamples(Wav(48_000, 2, 1, 0.5))!.Value;
        Assert.Equal(48_000, rate);
        Assert.Equal(48_000, samples.Length);
        Assert.Equal(0, samples[1000]);
        Assert.Null(WavReader.MonoSamples([1, 2, 3]));
    }

    [Fact]
    public void TheAttackLandsJustAfterTheStartWhateverTheRecordingPutsBeforeIt()
    {
        foreach (double burstAt in new[] { 0.0, 0.5, 1.7 })
        {
            (float[] samples, int rate) = WavReader.MonoSamples(Wav(48_000, 1, 3.6, burstAt))!.Value;
            float[] output = EntranceSound.Prepare(samples, rate, 44_100);
            int firstLoud = Array.FindIndex(output, sample => Math.Abs(sample) > 0.1f);
            double at = firstLoud / 44_100.0;
            double expected = burstAt == 0 ? 0 : EntranceSound.PreRoll;
            Assert.InRange(at, expected - 0.006, expected + 0.006);
        }
    }

    [Fact]
    public void ItLastsTheEntranceAndATailAndFadesToNothing()
    {
        (float[] samples, int rate) = WavReader.MonoSamples(Wav(48_000, 1, 3.6, 0.5))!.Value;
        float[] output = EntranceSound.Prepare(samples, rate, 44_100);
        Assert.Equal((int)Math.Round(EntranceSound.Length * 44_100), output.Length);
        Assert.True(EntranceSound.Length > IntroTable.Duration);
        Assert.True(Math.Abs(output[^1]) < 1e-4);
        Assert.True(output.Skip(output.Length - 200).All(sample => Math.Abs(sample) < 0.02f));
    }

    [Fact]
    public void SilenceOrNothingPlaysNothing()
    {
        Assert.Empty(EntranceSound.Prepare(new float[48_000], 48_000, 44_100));
        Assert.Empty(EntranceSound.Prepare([], 48_000, 44_100));
    }
}
