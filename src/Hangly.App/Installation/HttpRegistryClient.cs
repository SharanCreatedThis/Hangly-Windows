//
//  HttpRegistryClient.cs
//  Hangly
//
//  Posts the installation record, and crash reports, to the registry functions.
//

using System.Net.Http.Json;
using System.Text.Json;
using Hangly.Core.Crashes;
using Hangly.Core.Registry;

namespace Hangly.App.Installation;

/// <summary>The registry functions over HTTPS. macOS's <c>HTTPRegistryClient</c>, plus crash reports.</summary>
/// <remarks>
/// The URL comes from the build (<c>HanglyRegistryUrl</c>, injected from a repository secret), never from the source:
/// a build without one has no registry and sends nothing. It is the <c>registry</c> function's URL,
/// <c>https://asia-south1-&lt;project&gt;.cloudfunctions.net/registry</c>; the crash function sits beside it as
/// <c>…/crashReport</c>.
/// </remarks>
internal sealed class HttpRegistryClient : IRegistryClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly Uri url;
    private readonly Uri crashUrl;

    private HttpRegistryClient(Uri url)
    {
        this.url = url;
        crashUrl = new Uri(url, "crashReport");
    }

    /// <summary>A client for <paramref name="configured"/>, or null when it is empty or not an https URL.</summary>
    public static HttpRegistryClient? For(string configured) =>
        Uri.TryCreate(configured, UriKind.Absolute, out Uri? url) && url.Scheme == Uri.UriSchemeHttps
            ? new HttpRegistryClient(url)
            : null;

    public async Task<RegistryResponse> SubmitAsync(RegistryRequest request, CancellationToken cancellation = default)
    {
        using HttpResponseMessage response = await Post(url, request, cancellation).ConfigureAwait(false);
        RegistryResponse? body = await response.Content
            .ReadFromJsonAsync<RegistryResponse>(Json, cancellation)
            .ConfigureAwait(false);
        return body ?? throw new RegistryException(RegistryFailure.Busy, (int)response.StatusCode);
    }

    public async Task SubmitCrashAsync(InstallationRecord record, CrashReport report, CancellationToken cancellation = default)
    {
        var body = new
        {
            installationId = record.InstallationId.ToString("D").ToLowerInvariant(),
            writeKey = record.WriteKey,
            report = new
            {
                occurredAt = report.OccurredAt.ToUniversalTime().ToString("O"),
                fatal = report.Fatal,
                exceptionType = report.ExceptionType,
                message = report.Message,
                stackTrace = report.StackTrace,
                source = report.Source,
                appVersion = report.AppVersion,
                osVersion = report.OsVersion,
                architecture = report.Architecture,
            },
        };
        using HttpResponseMessage response = await Post(crashUrl, body, cancellation).ConfigureAwait(false);
    }

    /// <summary>Posts, and turns anything but success into the <see cref="RegistryFailure"/> that decides the retry.</summary>
    private async Task<HttpResponseMessage> Post<T>(Uri target, T body, CancellationToken cancellation)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(target, body, Json, cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new RegistryException(RegistryFailure.Unreachable);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        int status = (int)response.StatusCode;
        response.Dispose();
        throw new RegistryException(status is 429 or >= 500 ? RegistryFailure.Busy : RegistryFailure.Refused, status);
    }
}
