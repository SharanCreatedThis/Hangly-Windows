//
//  LaunchCounter.cs
//  Hangly
//
//  Counts launches, which the support card is scheduled off.
//

using Hangly.Core.Settings;

namespace Hangly.Core.Lifecycle;

/// <summary>Counts this launch, once. macOS's <c>LaunchCounter</c>.</summary>
/// <remarks>
/// The count lived in the analytics manager until PostHog was removed. It has nothing to do with analytics: the
/// Enjoying Hangly card appears on every third launch (<see cref="SupportCard.IsDue"/>), and the count is never reset.
/// </remarks>
public static class LaunchCounter
{
    public static void Count(SettingsStore store) => store.Update(settings => settings with
    {
        Milestones = settings.Milestones with { LaunchCount = settings.Milestones.LaunchCount + 1 },
    });
}
