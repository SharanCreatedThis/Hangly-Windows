//
//  IRegistryClient.cs
//  Hangly
//
//  The one request to the installation registry.
//

namespace Hangly.Core.Registry;

/// <summary>What is sent: the installation's facts and its write key. No location: the registry works that out.</summary>
/// <remarks>
/// <see cref="Crashes"/> is always 0 on Windows, whose crashes go to <c>crashReport</c> with their stack traces and are
/// counted there. <see cref="Event"/> is <c>"sync"</c>, or <c>"uninstall"</c> from Velopack's uninstall hook.
/// <see cref="InstalledAt"/> is ISO 8601 UTC, the local evidence of the original install, or null.
/// </remarks>
public sealed record RegistryRequest(
    string InstallationId,
    string WriteKey,
    string Nickname,
    string Platform,
    string OsName,
    string OsVersion,
    string AppVersion,
    string Architecture,
    int Crashes,
    string Event,
    string? InstalledAt = null)
{
    public static RegistryRequest From(InstallationRecord record, string registryEvent = "sync") => new(
        record.InstallationId.ToString("D").ToLowerInvariant(),
        record.WriteKey,
        record.Nickname,
        record.Platform,
        record.OsName,
        record.OsVersion,
        record.AppVersion,
        record.Architecture,
        0,
        registryEvent,
        record.InstalledAt?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>What the registry sends back: where it placed this installation, and when it last counted it as seen.</summary>
public sealed record RegistryResponse(string? City, string? Region, string? Country, DateTimeOffset LastSeen);

/// <summary>Why a request was not accepted.</summary>
public enum RegistryFailure
{
    /// <summary>No network, or the registry could not be reached. Retried when the network returns.</summary>
    Unreachable,

    /// <summary>Too many requests, or the registry failed. Retried later, with backoff.</summary>
    Busy,

    /// <summary>The registry refused the request as it stands. Not retried until something changes.</summary>
    Refused,
}

/// <summary>A request the registry did not accept.</summary>
public sealed class RegistryException(RegistryFailure failure, int status = 0)
    : Exception($"registry: {failure} ({status})")
{
    public RegistryFailure Failure { get; } = failure;

    public int Status { get; } = status;
}

/// <summary>Sends one request. The App's <c>HttpRegistryClient</c>, or a fake in tests.</summary>
public interface IRegistryClient
{
    Task<RegistryResponse> SubmitAsync(RegistryRequest request, CancellationToken cancellation = default);

    /// <summary>Sends one crash report for a registered installation; throws <see cref="RegistryException"/> if refused.</summary>
    Task SubmitCrashAsync(InstallationRecord record, Crashes.CrashReport report, CancellationToken cancellation = default);
}
