//
//  AnnouncementFeed.cs
//  Hangly
//
//  Fetching the dashboard's broadcasts: promptly, and cheaply.
//

using System.Net;
using System.Text.RegularExpressions;
using Hangly.App.Services;
using Hangly.Core.Notifications;

namespace Hangly.App.Notifications;

/// <summary>Asks the announcements endpoint what is live, and hands the answer to the store.</summary>
/// <remarks>
/// <b>Promptly.</b> At launch; when the network comes back; on waking from sleep; when the charm reappears after five
/// minutes away; and every fifteen minutes, give or take a minute (<see cref="FetchPolicy"/>). Never twice within a
/// minute.
///
/// <para><b>Cheaply.</b> Through Firebase Hosting's CDN (<c>&lt;project&gt;.web.app/announcements</c>), which answers
/// almost every request without running the function, with the last ETag, so an unchanged feed is a 304 of a few
/// hundred bytes. If Hosting cannot be reached the function is asked directly.</para>
///
/// <para><b>Anonymous:</b> the platform and nothing else — no installation ID, no cookies, no version. <b>Quiet:</b>
/// every failure is logged and otherwise ignored; what was received before stands. macOS's <c>AnnouncementFeed</c>.</para>
/// </remarks>
public sealed partial class AnnouncementFeed : IDisposable
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    private readonly NotificationStore store;
    private readonly Func<Action, bool> onXamlThread;
    private readonly CancellationTokenSource stopping = new();
    private readonly object gate = new();
    private CancellationTokenSource? sleeping;
    private DateTimeOffset? lastAttempt;
    private int consecutiveFailures;
    private int isFetching;

    public AnnouncementFeed(NotificationStore store, Func<Action, bool> onXamlThread, IReadOnlyList<Uri>? endpoints = null, bool? isTester = null)
    {
        this.store = store;
        this.onXamlThread = onXamlThread;
        Endpoints = endpoints ?? DefaultEndpoints;
        IsTester = isTester ?? DefaultIsTester;
    }

    /// <summary>Where the endpoint is, in the order to try: Hosting, then the function. Empty in a build that has none.</summary>
    public IReadOnlyList<Uri> Endpoints { get; }

    /// <summary>Whether test announcements are shown here too.</summary>
    public bool IsTester { get; }

    /// <summary>Raised on the XAML thread after every answer, changed or not.</summary>
    public event Action? Received;

    public void Start()
    {
        if (Endpoints.Count == 0)
        {
            Diagnostics.Log("announcements: no endpoint in this build");
            return;
        }

        Request(FetchTrigger.Launch);
        _ = Task.Run(async () =>
        {
            while (!stopping.IsCancellationRequested)
            {
                DateTimeOffset next;
                lock (gate)
                {
                    next = FetchPolicy.NextTimer(DateTimeOffset.Now, consecutiveFailures, (Random.Shared.NextDouble() * 2) - 1);
                    sleeping = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                }

                try
                {
                    TimeSpan wait = next - DateTimeOffset.Now;
                    await Task.Delay(wait > TimeSpan.FromSeconds(1) ? wait : TimeSpan.FromSeconds(1), sleeping.Token).ConfigureAwait(false);
                    Request(FetchTrigger.Timer);
                }
                catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
                {
                    // Re-planned: a failure changed the backoff.
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        });
    }

    /// <summary>Asks, if <see cref="FetchPolicy"/> says this trigger may. From any thread.</summary>
    public void Request(FetchTrigger trigger)
    {
        if (Endpoints.Count == 0 || Volatile.Read(ref isFetching) == 1)
        {
            return;
        }

        lock (gate)
        {
            if (!FetchPolicy.ShouldFetch(trigger, DateTimeOffset.Now, lastAttempt, store.State.LastFetchAt))
            {
                return;
            }

            lastAttempt = DateTimeOffset.Now;
        }

        _ = Task.Run(() => FetchAsync(trigger));
    }

    /// <summary>The network is back: ask now, after a moment for DHCP and DNS to settle.</summary>
    public void NetworkBecameAvailable() =>
        _ = Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ => Request(FetchTrigger.Network), TaskScheduler.Default);

    /// <summary>One request, through each endpoint in turn until one answers. Never throws.</summary>
    public async Task FetchAsync(FetchTrigger trigger = FetchTrigger.Launch)
    {
        if (Interlocked.Exchange(ref isFetching, 1) == 1)
        {
            return;
        }

        try
        {
            foreach (Uri endpoint in Endpoints)
            {
                if (await AskAsync(RequestUri(endpoint), trigger).ConfigureAwait(false))
                {
                    lock (gate)
                    {
                        consecutiveFailures = 0;
                    }

                    onXamlThread(() => Received?.Invoke());
                    return;
                }
            }

            lock (gate)
            {
                consecutiveFailures++;
                sleeping?.Cancel();
            }
        }
        finally
        {
            Interlocked.Exchange(ref isFetching, 0);
        }
    }

    /// <summary>One endpoint. False for anything that is not an answer: no network, Hosting not deployed, a server error.</summary>
    private async Task<bool> AskAsync(Uri uri, FetchTrigger trigger)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("application/json");
            if (store.State.FeedETag is { } etag)
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            using HttpResponseMessage response = await Client.SendAsync(request, stopping.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                onXamlThread(() => store.NoteUnchanged(DateTimeOffset.Now));
                Diagnostics.Log($"announcements ({trigger}): unchanged");
                return true;
            }

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            IReadOnlyList<Announcement> announcements =
                AnnouncementFeedParser.Parse(await response.Content.ReadAsStringAsync(stopping.Token).ConfigureAwait(false));
            string? newTag = response.Headers.ETag?.ToString();
            onXamlThread(() => store.Receive(announcements, IsTester, DateTimeOffset.Now, newTag));
            Diagnostics.Log($"announcements ({trigger}): {announcements.Count} received");
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            Diagnostics.Log($"announcements via {uri.Host} failed: {exception.GetType().Name}");
            return false;
        }
    }

    public Uri RequestUri(Uri endpoint) =>
        new UriBuilder(endpoint) { Query = $"platform={Announcement.Platform}" + (IsTester ? "&audience=test" : string.Empty) }.Uri;

    /// <summary>
    /// Hosting first, then the function itself; both worked out from the registry's address
    /// (<c>https://&lt;region&gt;-&lt;project&gt;.cloudfunctions.net/registry</c>). <c>HANGLY_ANNOUNCEMENTS_URL</c> replaces both.
    /// </summary>
    public static IReadOnlyList<Uri> DefaultEndpoints =>
        Environment.GetEnvironmentVariable("HANGLY_ANNOUNCEMENTS_URL") is { Length: > 0 } overridden
            && Uri.TryCreate(overridden, UriKind.Absolute, out Uri? custom)
            && (custom.Scheme == Uri.UriSchemeHttps || custom.IsLoopback)
            ? [custom]
            : FromRegistry(AppInfo.RegistryUrl);

    public static IReadOnlyList<Uri> FromRegistry(string registry)
    {
        if (!Uri.TryCreate(registry, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal))
        {
            return [];
        }

        string path = uri.AbsolutePath[..^"registry".Length] + "announcements";
        Uri direct = new UriBuilder(uri) { Path = path, Query = string.Empty }.Uri;
        return FunctionsHost().Match(uri.Host) is { Success: true } match
            ? [new Uri($"https://{match.Groups["project"].Value}.web.app/announcements"), direct]
            : [direct];
    }

    [GeneratedRegex(@"^[a-z]+-[a-z]+[0-9]+-(?<project>[a-z0-9-]+)\.cloudfunctions\.net$", RegexOptions.CultureInvariant)]
    private static partial Regex FunctionsHost();

    /// <summary><c>HANGLY_NOTIFICATION_TESTER=1</c> makes this PC one that sees <c>test</c> announcements.</summary>
    public static bool DefaultIsTester => Environment.GetEnvironmentVariable("HANGLY_NOTIFICATION_TESTER") == "1";

    public void Dispose()
    {
        stopping.Cancel();
        stopping.Dispose();
    }
}
