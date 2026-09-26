//
//  SystemMotion.cs
//  Hangly
//
//  Whether Windows has been asked to animate less.
//

using System.Runtime.InteropServices;
using Hangly.Core.Models;

namespace Hangly.App.Services;

/// <summary>Windows' own "Animation effects" switch, and what it means for the rope.</summary>
/// <remarks>
/// Settings → Accessibility → Visual effects → Animation effects, which is
/// <c>SPI_GETCLIENTAREAANIMATION</c> underneath and the same switch
/// <c>UISettings.AnimationsEnabled</c> reports. Off there is the Windows counterpart of
/// macOS's Reduce motion, so Follow System reads it. Changes arrive as
/// <c>WM_SETTINGCHANGE</c> with <c>SPI_SETCLIENTAREAANIMATION</c>, which the overlay window
/// already receives; nothing here polls.
/// </remarks>
internal static class SystemMotion
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    /// <summary>The <c>WM_SETTINGCHANGE</c> parameter that says this switch moved.</summary>
    internal const int SpiSetClientAreaAnimation = 0x1043;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);

    /// <summary>True when animation effects are switched off. False if Windows will not say.</summary>
    public static bool ReducesMotion =>
        SystemParametersInfo(SpiGetClientAreaAnimation, 0, out bool animates, 0) && !animates;

    /// <summary>How the rope should move, given the Appearance setting.</summary>
    public static RopeMotion Resolve(MotionPreference preference) =>
        RopeMotionTable.Resolve(preference, preference == MotionPreference.FollowSystem && ReducesMotion);
}
