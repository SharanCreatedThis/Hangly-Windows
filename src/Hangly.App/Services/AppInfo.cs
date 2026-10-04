//
//  AppInfo.cs
//  Hangly
//
//  What this build is, read from the build rather than written twice.
//

using System.Reflection;

namespace Hangly.App.Services;

/// <summary>Identity and configuration, read out of the assembly.</summary>
/// <remarks>
/// Everything here is written once, in the project file, and read back at runtime. The
/// About page showing a version that disagrees with the binary is a small thing that
/// makes everything else on the page look untrustworthy.
/// </remarks>
public static class AppInfo
{
    private static readonly Assembly Self = typeof(AppInfo).Assembly;

    public static string Name => "Hangly";

    /// <summary>Marketing version, e.g. "2.0.0".</summary>
    public static string Version { get; } =
        Self.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { } informational && informational.Length > 0
            ? informational.Split('+')[0]
            : Self.GetName().Version?.ToString(3) ?? "0.0.0";

    public static string BuildNumber { get; } = Metadata("HanglyBuildNumber") is { Length: > 0 } build
        ? build
        : "1";

    public static string Copyright { get; } =
        Self.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
        ?? "Copyright © 2026 sharancreatedthis";

    /// <summary>The Google Analytics Web stream's Measurement ID (<c>G-…</c>), empty when this build has none.</summary>
    public static string Ga4MeasurementId { get; } = Metadata("HanglyGa4MeasurementId") ?? string.Empty;

    /// <summary>The stream's Measurement Protocol API secret, empty when this build has none.</summary>
    public static string Ga4ApiSecret { get; } = Metadata("HanglyGa4ApiSecret") ?? string.Empty;

    /// <summary>Major.minor.build.patch, such as <c>10.0.19045.6456</c>.</summary>
    /// <remarks>
    /// <see cref="Environment.OSVersion"/> always reports the last part as 0, so the registry
    /// could not tell a PC three years behind on updates from a current one. The patch level
    /// is the UBR value Windows keeps in the registry; without it, the old answer stands.
    /// </remarks>
    public static string WindowsVersion { get; } = ReadWindowsVersion();

    /// <summary>The installation registry's URL, empty when this build has none.</summary>
    public static string RegistryUrl { get; } = Metadata("HanglyRegistryUrl") ?? string.Empty;

    /// <summary>The machine's architecture in the registry's vocabulary: "arm64" or "x64", as on macOS.</summary>
    public static string Architecture { get; } =
        System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64
            ? "arm64"
            : "x64";

    public static string WebsiteUrl => "https://www.sharancreatedthis.in/products/hangly";

    /// <summary>The creator's site, as every creator credit spells it.</summary>
    /// <remarks>
    /// One spelling, with the <c>www</c> and without a path. <see cref="WebsiteUrl"/> is
    /// a different thing wearing a similar name — it is Hangly's own product page, which
    /// the About page's "Website" link has always meant — so the two are named apart
    /// rather than merged. A credit that says "Website" under the creator's handle means
    /// the creator's site, and now goes there.
    /// </remarks>
    public static string CreatorSiteUrl => "https://www.sharancreatedthis.in";

    /// <summary>The creator's handle, written the one way it is written.</summary>
    public static string CreatorHandle => "@sharan.created.this";

    /// <summary>The four things the creator makes, in the order macOS lists them.</summary>
    public static string CreatorDisciplines => "Photography • Film • Design • Code";

    public static string GitHubUrl => "https://github.com/SharanCreatedThis/Hangly-Windows";

    /// <summary>Where the update feed lives.</summary>
    /// <remarks>
    /// GitHub Releases, which is where the artefacts already go, and which serves them as
    /// plain static files over HTTPS — so an update check stays a GET with nothing
    /// attached to it, exactly as DISTRIBUTION.md promises. The per-architecture channel
    /// is chosen by the updater, not named here.
    /// </remarks>
    public static string UpdateFeedUrl => LocalUpdateFeed ?? GitHubUrl;

    /// <summary>A folder of packages this build updates from, if it was built with one.</summary>
    /// <remarks>
    /// Only ever set by a test build, through the <c>HanglyUpdateFeed</c> build property, so
    /// silent updating can be exercised end to end without publishing anything. A release
    /// build is made without it and release.yml refuses one that has it.
    /// </remarks>
    public static string? LocalUpdateFeed { get; } =
        Metadata("HanglyUpdateFeed") is { Length: > 0 } folder ? folder : null;

    public static string ReleaseNotesUrl =>
        "https://github.com/SharanCreatedThis/Hangly-Windows/releases";

    /// <summary>
    /// The creator's Instagram, spelled the way the macOS build spells it — its binary
    /// carries "Opens https://instagram.com/sharan.created.this". The two platforms
    /// pointed at different handles until this was checked against the shipping app.
    /// </summary>
    public static string InstagramUrl => "https://instagram.com/sharan.created.this";

    /// <summary>The creator's UPI address, as the macOS build carries it.</summary>
    public static string UpiId => "8870786087@yescred";

    public static string SupportUrl => "https://www.sharancreatedthis.in/coffee";

    /// <summary>Where "Suggest a charm" writes to.</summary>
    /// <remarks>
    /// The address is the one the macOS build names in its own menu item, "Suggest a
    /// charm to the creator (swarnsharan@gmail.com)", so both platforms land in the same
    /// inbox rather than in two.
    /// </remarks>
    public static string SuggestMailUrl =>
        "mailto:swarnsharan@gmail.com?subject=Hangly%20charm%20suggestion";

    private static string? Metadata(string key) => Self
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == key)
        ?.Value;

    private static string ReadWindowsVersion()
    {
        Version version = Environment.OSVersion.Version;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("UBR") is int patch and > 0)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}.{patch}";
            }
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Unreadable is not worth failing over; the build number alone is what was sent before.
        }

        return version.ToString();
    }
}
