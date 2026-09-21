//
//  Updater.cs
//  Hangly
//
//  Checking whether there is a newer Hangly, and becoming it.
//

using Velopack;
using Velopack.Sources;

namespace Hangly.App.Services;

/// <summary>What a check found.</summary>
/// <param name="Version">The version available, or null when there is none.</param>
/// <param name="Message">What to tell the person.</param>
public readonly record struct UpdateCheck(string? Version, string Message, string? Notes = null)
{
    public bool HasUpdate => Version is not null;

    /// <summary>Whether there is anything to read about this release.</summary>
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
}

/// <summary>Finds updates, fetches them, and hands over to Velopack to apply.</summary>
/// <remarks>
/// <b>What this is not.</b> It does not move files, replace the executable or restart
/// anything itself. Velopack's own hook — run before everything else in
/// <see cref="Program"/> — does all of that in a separate process launch. This only asks
/// what exists, downloads it, and says "now".
///
/// <para><b>One channel per architecture.</b> An ARM64 machine must never be offered an
/// x64 package, and a channel is a single line of releases, so the channel name carries
/// the runtime identifier. The feed for this build is therefore
/// <c>releases.win-arm64.json</c> or <c>releases.win-x64.json</c>, chosen here rather
/// than by whatever the server happens to serve.</para>
///
/// <para><b>Nothing is sent.</b> A check is a GET for a static file. There is no server
/// component and no identifier — the same promise the macOS build's appcast makes, and
/// the reason PRIVACY.md can say an update check carries nothing.</para>
///
/// <para><b>Every failure is quiet.</b> A machine with no network, a feed that has not
/// been published yet, and a release that will not parse are all the same answer: there
/// is no update today. None of them is worth interrupting someone over, and none of them
/// may take the app down — an update is the one feature whose failure must never cost
/// you the thing you already have.</para>
/// </remarks>
public sealed class Updater
{
    /// <summary>The channel this build belongs to: one per architecture.</summary>
    public static string Channel =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
            == System.Runtime.InteropServices.Architecture.Arm64
            ? "win-arm64"
            : "win-x64";

    private readonly string feedUrl;
    private UpdateInfo? pending;

    public Updater(string feedUrl) => this.feedUrl = feedUrl;

    /// <summary>The manager this build asks, pointed at its own channel.</summary>
    /// <remarks>
    /// <b>A GitHub source, not a plain web one.</b> Handing the repository URL to
    /// <see cref="UpdateManager"/> as a string makes a <c>SimpleWebSource</c>, which would
    /// fetch <c>https://github.com/owner/repo/releases.win-arm64.json</c> — a page that
    /// does not exist. Release assets live under a tag, so finding them means asking the
    /// Releases API, which is what <see cref="GithubSource"/> does. The distinction costs
    /// nothing to get right here and is invisible until the day a release is published
    /// and nobody is offered it.
    ///
    /// <para>No access token: unauthenticated requests are enough for a public repository,
    /// and a token in a shipped binary is a token that has been given away.</para>
    ///
    /// <para><b>Pre-releases are included, and that is not the obvious choice.</b> With
    /// them excluded, <see cref="GithubSource"/> asks GitHub for
    /// <c>/releases/latest</c> — an endpoint that <em>omits pre-releases entirely</em> and
    /// answers <b>404</b> when every release is one. Velopack turns that into an
    /// exception, so the check does not report "up to date"; it fails. Measured the
    /// moment v0.9.0 went out as a pre-release: <c>update check failed:
    /// HttpRequestException</c>, on every launch, for every tester, for as long as the
    /// beta is the newest thing published.</para>
    ///
    /// <para>Including them makes the source enumerate <c>/releases</c> instead, which
    /// lists everything, and Velopack then picks the highest version — so a stable v1.0
    /// still wins over any 0.9.x beta. <b>The cost is real and belongs to the future:</b>
    /// once people are running a stable release, publishing a pre-release will offer it to
    /// them. Either stop publishing pre-releases at that point, or set this back to false
    /// — see <c>Docs/DISTRIBUTION.md</c> §2.</para>
    /// </remarks>
    private UpdateManager Manager() => new(
        new GithubSource(feedUrl, accessToken: null, prerelease: true),
        new UpdateOptions { ExplicitChannel = Channel });

    /// <summary>Whether this copy can update itself at all.</summary>
    /// <remarks>
    /// False for a copy that was unzipped rather than installed, and for every run from
    /// a build directory. Velopack has nothing to replace in those cases, and offering
    /// an update that cannot be applied is worse than offering none.
    /// </remarks>
    public static bool IsInstalled
    {
        get
        {
            try
            {
                return new UpdateManager(new GithubSource("https://github.com", null, false)).IsInstalled;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Asks the feed what exists. Never throws.</summary>
    public async Task<UpdateCheck> CheckAsync()
    {
        try
        {
            UpdateManager manager = Manager();
            if (!manager.IsInstalled)
            {
                return new UpdateCheck(null, "Updates apply to installed copies only.");
            }

            pending = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (pending is null)
            {
                return new UpdateCheck(null, "Hangly is up to date.");
            }

            string version = pending.TargetFullRelease.Version.ToString();

            // Whatever the release was packaged with, if anything. A release with no
            // notes is ordinary rather than an error, and shows the version alone.
            string? notes = pending.TargetFullRelease.NotesMarkdown;
            return new UpdateCheck(version, $"Hangly {version} is available.", notes);
        }
        catch (Exception exception)
        {
            Diagnostics.Log($"update check failed: {exception.GetType().Name}");
            return new UpdateCheck(null, "Couldn't check for updates just now.");
        }
    }

    /// <summary>Release notes as plain text, ready for a text box.</summary>
    /// <remarks>
    /// The work is <see cref="Hangly.Core.Text.ReleaseNotes"/>'s, where it can be tested;
    /// this is here so the About page has one place to call and does not have to know
    /// that notes arrive as Markdown.
    /// </remarks>
    public static string PlainNotes(string markdown) => Hangly.Core.Text.ReleaseNotes.Plain(markdown);

    /// <summary>
    /// Downloads what the last check found and applies it, which ends this process.
    /// </summary>
    /// <remarks>
    /// The restart is Velopack's: it relaunches the app after the files are in place.
    /// Anything that fails before that point leaves the installed copy exactly as it was,
    /// because nothing is replaced until the whole package has arrived.
    /// </remarks>
    public async Task<string> DownloadAndApplyAsync()
    {
        if (pending is null)
        {
            return "There's nothing to install.";
        }

        try
        {
            UpdateManager manager = Manager();
            await manager.DownloadUpdatesAsync(pending).ConfigureAwait(false);

            Diagnostics.Log($"applying update {pending.TargetFullRelease.Version}");
            manager.ApplyUpdatesAndRestart(pending);
            return "Restarting…";
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("applying an update", exception);
            return "That update couldn't be installed. Your copy is unchanged.";
        }
    }
}
