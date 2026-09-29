//
//  EntranceIntroduction.cs
//  Hangly
//
//  The update that brings the Spider-Man entrance shows it off, then gives the look back.
//

using Hangly.Core.Models;

namespace Hangly.Core.Settings;

/// <summary>Shows the Spider-Man entrance to everyone updating: the shipped look for two launches, then theirs. macOS's <c>EntranceIntroduction</c>, rule for rule.</summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Launch 1</b> (usually the quiet restart after the update installs itself) <b>and launch 2</b>: the shipped
/// look and sound — Spider-Man on Spider Thread at the shipped size and position, default opacity and glow, the
/// entrance on, sound on at half volume — so the entrance plays, with its sound, both times.</item>
/// <item><b>Launch 3</b>: everything in <see cref="ShowcaseLook"/> they did not change during those two launches goes
/// back to what they had; what they changed stays. <b>Spider-Man stays</b> until they choose another charm, and the
/// entrance keeps playing for as long as he is on the rope; with any other charm it never plays.</item>
/// </list>
/// <para>Their name, favourites, imports, launch at login and privacy are never touched. A new install already starts
/// on the shipped look (<see cref="AppSettings.Defaults"/>) and is left alone.</para>
/// </remarks>
public static class EntranceIntroduction
{
    /// <summary>Launches the shipped look is lent for: the first after the update and the next.</summary>
    public const int ShowcaseLaunches = 2;

    /// <summary>Every install's volume before 2.1.0 unless someone moved the slider: too quiet to hear the entrance.</summary>
    /// <remarks>Treated as never chosen, so launch 3 keeps the new half volume rather than giving this back.</remarks>
    public const double LegacyDefaultVolume = 0.14;

    /// <summary>Lends the shipped look if this install is owed the introduction: launch 1.</summary>
    /// <remarks>Not marked done here — <see cref="Complete"/> does that once the overlay is up. A launch that fails
    /// before then finds the showcase already under way and changes nothing.</remarks>
    public static (AppSettings Settings, bool Introduced) Apply(AppSettings settings)
    {
        bool isExistingUser = settings.Milestones.LaunchCount > 0 || settings.HasSeenWelcome;
        if (settings.Milestones.SpiderManIntroCompleted || settings.Milestones.EntranceShowcase is not null || !isExistingUser)
        {
            return (settings, false);
        }

        ShowcaseLook before = ShowcaseLook.Of(settings);
        OverlaySettings shipped = AppSettings.Defaults.Overlay;
        AppSettings lent = settings with
        {
            Overlay = settings.Overlay.WithStack(shipped.Stack) with
            {
                RopeStyle = RopeStyle.SpiderThread,
                Anchor = shipped.Anchor,
                HorizontalPosition = shipped.HorizontalPosition,
                OffsetX = shipped.OffsetX,
                OffsetY = shipped.OffsetY,
                CharmSize = shipped.CharmSize,
                RopeLength = shipped.RopeLength,
                Opacity = shipped.Opacity,
                Glow = shipped.Glow,
                StartupAnimation = true,
            },
            SoundEffectsEnabled = true,
            SoundVolume = AppSettings.Defaults.SoundVolume,
        };
        return (lent with
        {
            Milestones = lent.Milestones with
            {
                EntranceShowcase = new EntranceShowcase(before, ShowcaseLook.Of(lent), Launch: 1),
            },
        }, true);
    }

    /// <summary>Counts a launch while the look is lent, and on launch 3 gives back what was not changed.</summary>
    /// <remarks>Called at the start of every launch, before <see cref="Apply"/>.</remarks>
    public static (AppSettings Settings, bool GaveBack) Advance(AppSettings settings)
    {
        if (settings.Milestones.EntranceShowcase is not { } showcase)
        {
            return (settings, false);
        }

        int launch = showcase.Launch + 1;
        if (launch <= ShowcaseLaunches)
        {
            return (settings with { Milestones = settings.Milestones with { EntranceShowcase = showcase with { Launch = launch } } }, false);
        }

        AppSettings given = showcase.Before.GiveBack(settings, showcase.Lent);
        return (given with { Milestones = given.Milestones with { EntranceShowcase = null } }, true);
    }

    /// <summary>Records that the introduction has happened, for good.</summary>
    public static AppSettings Complete(AppSettings settings) => settings with
    {
        Milestones = settings.Milestones with { SpiderManIntroCompleted = true },
    };
}

/// <summary>The look is lent: what it was before, what was lent, and which launch this is.</summary>
public sealed record EntranceShowcase(ShowcaseLook Before, ShowcaseLook Lent, int Launch);

/// <summary>The settings the showcase lends and gives back: how the charm looks and how Hangly sounds.</summary>
/// <remarks>Not the charm and rope — Spider-Man stays until somebody chooses another — and nothing that is not look or sound.</remarks>
public sealed record ShowcaseLook(
    OverlayAnchor Anchor,
    double? HorizontalPosition,
    double OffsetX,
    double OffsetY,
    double CharmSize,
    double? PlaceSize,
    double RopeLength,
    double Opacity,
    GlowLevel Glow,
    bool StartupAnimation,
    bool SoundEffectsEnabled,
    double SoundVolume)
{
    public static ShowcaseLook Of(AppSettings settings)
    {
        OverlaySettings overlay = settings.Overlay;
        IReadOnlyList<RopeCharm> places = overlay.Stack.Places;
        return new(
            overlay.Anchor,
            overlay.HorizontalPosition,
            overlay.OffsetX,
            overlay.OffsetY,
            overlay.CharmSize,
            places.Count > 0 ? places[^1].Size : null,
            overlay.RopeLength,
            overlay.Opacity,
            overlay.Glow,
            overlay.StartupAnimation,
            settings.SoundEffectsEnabled,
            settings.SoundVolume);
    }

    /// <summary>This look written back over <paramref name="settings"/>, wherever a setting still holds what was lent.</summary>
    /// <remarks>A setting somebody changed while it was lent is theirs now, and stays.</remarks>
    public AppSettings GiveBack(AppSettings settings, ShowcaseLook lent)
    {
        ShowcaseLook now = Of(settings);
        OverlaySettings overlay = settings.Overlay with
        {
            Anchor = now.Anchor == lent.Anchor ? Anchor : settings.Overlay.Anchor,
            HorizontalPosition = now.HorizontalPosition == lent.HorizontalPosition ? HorizontalPosition : settings.Overlay.HorizontalPosition,
            OffsetX = now.OffsetX == lent.OffsetX ? OffsetX : settings.Overlay.OffsetX,
            OffsetY = now.OffsetY == lent.OffsetY ? OffsetY : settings.Overlay.OffsetY,
            CharmSize = now.CharmSize == lent.CharmSize ? CharmSize : settings.Overlay.CharmSize,
            RopeLength = now.RopeLength == lent.RopeLength ? RopeLength : settings.Overlay.RopeLength,
            Opacity = now.Opacity == lent.Opacity ? Opacity : settings.Overlay.Opacity,
            Glow = now.Glow == lent.Glow ? Glow : settings.Overlay.Glow,
            StartupAnimation = now.StartupAnimation == lent.StartupAnimation ? StartupAnimation : settings.Overlay.StartupAnimation,
        };

        if (now.PlaceSize == lent.PlaceSize && PlaceSize is double size)
        {
            RopeCharm[] places = [.. overlay.Stack.Places];
            if (places.Length > 0)
            {
                places[^1] = places[^1] with { Size = size };
                overlay = overlay.WithStack(CharmStackState.FromPlaces(places));
            }
        }

        return settings with
        {
            Overlay = overlay,
            SoundEffectsEnabled = now.SoundEffectsEnabled == lent.SoundEffectsEnabled ? SoundEffectsEnabled : settings.SoundEffectsEnabled,
            SoundVolume = now.SoundVolume == lent.SoundVolume && SoundVolume != EntranceIntroduction.LegacyDefaultVolume
                ? SoundVolume
                : settings.SoundVolume,
        };
    }
}
