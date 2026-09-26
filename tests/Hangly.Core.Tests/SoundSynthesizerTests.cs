using Hangly.Core.Audio;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The Windows synthesiser makes the macOS synthesiser's samples.</summary>
/// <remarks>
/// The fingerprints below were printed by the macOS <c>SoundSynthesizer.swift</c> itself,
/// compiled on its own on 26 September 2026: length, sum, energy and three samples per
/// material. Matching them is what "a charm sounds the same on both platforms" means.
/// </remarks>
public class SoundSynthesizerTests
{
    [Theory]
    [InlineData(CharmSound.Bell, 66150, 0.127696183, 3033.466367768, -0.032753363, -0.049176913, -0.00028821928)]
    [InlineData(CharmSound.Wood, 3087, 25.340021116, 119.184200606, -0.031250473, -0.1109335, 0.00045874)]
    [InlineData(CharmSound.Glass, 15434, -0.040788756, 546.166381833, 0.06294473, 0.019668607, 0.0001158905)]
    [InlineData(CharmSound.Metal, 24255, -0.144166106, 795.538913283, 0.33762398, 0.08152263, -0.00019515162)]
    [InlineData(CharmSound.Soft, 3969, 11.243583976, 139.528662381, 0.3962213, 0.19428515, 0.00048088233)]
    public void MatchesTheMacSamples(CharmSound sound, int length, double sum, double energy, double at100, double atMiddle, double nearEnd)
    {
        float[] samples = SoundSynthesizer.Samples(sound);

        Assert.Equal(length, samples.Length);
        Assert.Equal(sum, samples.Sum(sample => (double)sample), 3);
        Assert.Equal(energy, samples.Sum(sample => (double)sample * sample), 2);
        Assert.Equal(at100, samples[100], 5);
        Assert.Equal(atMiddle, samples[samples.Length / 2], 5);
        Assert.Equal(nearEnd, samples[^10], 5);
    }

    [Fact]
    public void EverySoundStaysInsideFullScale()
    {
        foreach (CharmSound sound in Enum.GetValues<CharmSound>())
        {
            Assert.All(SoundSynthesizer.Samples(sound), sample => Assert.InRange(sample, -1f, 1f));
        }
    }
}
