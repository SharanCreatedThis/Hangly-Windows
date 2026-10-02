//
//  AnnouncementFeed.cs
//  Hangly
//
//  Fetching the dashboard's broadcasts, rarely and quietly.
//

using System.Net;
using Hangly.App.Services;
using Hangly.Core.Notifications;

namespace Hangly.App.Notifications;

/// <summary>Asks the announcements endpoint what is live, and hands the answer to the store.</summary>
/// <remarks>
/// <b>Rarely.</b> Twenty seconds after launch if the last answer is six hours old, then every six hours while Hangly
/// runs, and once when the network comes back after a failure — never a loop. The endpoint reads Firestore at most
/// once per instance every five minutes, so the number of people running Hangly does not change what Firestore costs.
///
/// <para><b>Anonymous.</b> The request names the platform and the version, nothing about who is asking: no
/// installation ID and no cookies. <b>Quiet:</b> every failure is logged and otherwise ignored, and what was received
/// before stands. macOS's <c>AnnouncementFeed</c>.</para>
/// </remarks>
public sealed class AnnouncementFeed : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    public static readonly TimeSpan LaunchDelay = TimeSpan.FromSeconds(20);

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
    private int isFetching;
    private volatile bool failedSinceLastSuccess;

    public AnnouncementFeed(NotificationStore store, Func<Action, bool> onXamlThread, Uri? endpoint = null, bool? isTester = null)
    {
        this.store = store;
        this.onXamlThread = onXamlThread;
        Endpoint = endpoint ?? DefaultEndpoint;
        IsTester = isTester ?? DefaultIsTester;
    }

    /// <summary>Where the endpoint is, or null for a build that has none (every developer build).</summary>
    public Uri? Endpoint { get; }

    /// <summary>Whether test announcements are shown here too.</summary>
    public bool IsTester { get; }

    /// <summary>Raised on the XAML thread after every successful fetch.</summary>
    public event Action? Received;

    public void Start()
    {
        if (Endpoint is null)
        {
            Diagnostics.Log("announcements: no endpoint in this build");
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(LaunchDelay, stopping.Token).ConfigureAwait(false);
                while (!stopping.IsCancellationRequested)
                {
                    DateTimeOffset due = (store.State.LastFetchAt ?? DateTimeOffset.MinValue) + Interval;
                    if (due <= DateTimeOffset.Now)
                    {
                        await FetchAsync().ConfigureAwait(false);
                        await Task.Delay(Interval, stopping.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        TimeSpan wait = due - DateTimeOffset.Now;
                        await Task.Delay(wait < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : wait, stopping.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Quitting.
            }
        });
    }

    /// <summary>The network is back: a failed fetch is tried again now, rather than in six hours.</summary>
    public void NetworkBecameAvailable()
    {
        if (failedSinceLastSuccess && Endpoint is not null)
        {
            _ = Task.Run(FetchAsync);
        }
    }

    /// <summary>One request. Never throws.</summary>
    public async Task FetchAsync()
    {
        if (RequestUri() is not Uri uri || Interlocked.Exchange(ref isFetching, 1) == 1)
        {
            return;
        }

        try
        {
            using HttpResponseMessage response = await Client.GetAsync(uri, stopping.Token).ConfigureAwait(false);
            IReadOnlyList<Announcement> announcements;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Not deployed yet, or withdrawn: the same as an empty feed.
                announcements = [];
            }
            else
            {
                response.EnsureSuccessStatusCode();
                announcements = AnnouncementFeedParser.Parse(await response.Content.ReadAsStringAsync(stopping.Token).ConfigureAwait(false));
            }

            failedSinceLastSuccess = false;
            onXamlThread(() =>
            {
                store.Receive(announcements, IsTester, DateTimeOffset.Now);
                Received?.Invoke();
            });
            Diagnostics.Log($"announcements: {announcements.Count} received");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            failedSinceLastSuccess = true;
            Diagnostics.Log($"announcements fetch failed: {exception.GetType().Name}");
        }
        finally
        {
            Interlocked.Exchange(ref isFetching, 0);
        }
    }

    public Uri? RequestUri()
    {
        if (Endpoint is null)
        {
            return null;
        }

        string query = $"platform={Announcement.Platform}&version={Uri.EscapeDataString(AppInfo.Version)}" + (IsTester ? "&audience=test" : string.Empty);
        return new UriBuilder(Endpoint) { Query = query }.Uri;
    }

    /// <summary>
    /// The registry's address with its last part changed: <c>…/registry</c> → <c>…/announcements</c>. A build may be
    /// pointed elsewhere with <c>HANGLY_ANNOUNCEMENTS_URL</c>.
    /// </summary>
    public static Uri? DefaultEndpoint =>
        Environment.GetEnvironmentVariable("HANGLY_ANNOUNCEMENTS_URL") is { Length: > 0 } overridden
            && Uri.TryCreate(overridden, UriKind.Absolute, out Uri? custom)
            && (custom.Scheme == Uri.UriSchemeHttps || custom.IsLoopback)
            ? custom
            : FromRegistry(AppInfo.RegistryUrl);

    public static Uri? FromRegistry(string registry)
    {
        if (!Uri.TryCreate(registry, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal))
        {
            return null;
        }

        string path = uri.AbsolutePath[..^"registry".Length] + "announcements";
        return new UriBuilder(uri) { Path = path, Query = string.Empty }.Uri;
    }

    /// <summary><c>HANGLY_NOTIFICATION_TESTER=1</c> makes this PC one that sees <c>test</c> announcements.</summary>
    public static bool DefaultIsTester => Environment.GetEnvironmentVariable("HANGLY_NOTIFICATION_TESTER") == "1";

    public void Dispose()
    {
        stopping.Cancel();
        stopping.Dispose();
    }
}
