using Hangly.Core.Audio;
using Hangly.Core.Models;
using Xunit;

namespace Hangly.Core.Tests;

/// <summary>The same cases as the macOS SoundPolicyTests.</summary>
public class SoundPolicyTests
{
    [Fact]
    public void AGentleReleaseIsSilentAndAHardOneIsLoud()
    {
        Assert.Null(SoundPolicy.ThrowIntensity(400));
        Assert.Equal(0.25, SoundPolicy.ThrowIntensity(600));
        Assert.Equal(1, SoundPolicy.ThrowIntensity(5000));
    }

    [Fact]
    public void BrushesAreSilentAndKnocksAreQuieterThanThrows()
    {
        Assert.Null(SoundPolicy.CollisionIntensity(250, RopeMotion.Full));
        Assert.Equal(0.2 * 0.7, SoundPolicy.CollisionIntensity(400, RopeMotion.Full)!.Value, 9);
        Assert.Equal(0.7, SoundPolicy.CollisionIntensity(5000, RopeMotion.Full)!.Value, 9);
    }

    [Fact]
    public void ReducedMotionSilencesCollisionsButNotWhatYouCaused()
    {
        Assert.Null(SoundPolicy.CollisionIntensity(5000, RopeMotion.Reduced));
        Assert.NotNull(SoundPolicy.ThrowIntensity(5000));
    }

    [Theory]
    [InlineData(CharmSound.Soft, CharmSound.Metal, CharmSound.Metal)]
    [InlineData(CharmSound.Glass, CharmSound.Metal, CharmSound.Glass)]
    [InlineData(CharmSound.Bell, CharmSound.Glass, CharmSound.Bell)]
    [InlineData(CharmSound.Wood, CharmSound.Soft, CharmSound.Wood)]
    [InlineData(CharmSound.Soft, CharmSound.Soft, CharmSound.Soft)]
    public void TheHarderMaterialCarries(CharmSound first, CharmSound second, CharmSound expected)
    {
        Assert.Equal(expected, SoundPolicy.Carrier(first, second));
        Assert.Equal(expected, SoundPolicy.Carrier(second, first));
    }

    [Fact]
    public void OffQuietOrTooSoonIsSilent()
    {
        Assert.Null(SoundPolicy.Volume(enabled: false, 1, 1, 10, systemQuiet: false));
        Assert.Null(SoundPolicy.Volume(enabled: true, 1, 1, 10, systemQuiet: true));
        Assert.Null(SoundPolicy.Volume(enabled: true, 1, 1, 0.05, systemQuiet: false));
        Assert.Null(SoundPolicy.Volume(enabled: true, 0.001, 1, 10, systemQuiet: false));
        Assert.Equal(0.14 * 0.6, SoundPolicy.Volume(enabled: true, 0.14, 0.6, 10, systemQuiet: false)!.Value, 9);
    }
}
