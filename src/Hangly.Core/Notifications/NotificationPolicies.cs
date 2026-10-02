//
//  NotificationPolicies.cs
//  Hangly
//
//  When to fetch, where the card goes, and where a link may lead.
//

using System.Text.RegularExpressions;

namespace Hangly.Core.Notifications;

/// <summary>What asked for the feed.</summary>
public enum FetchTrigger
{
    Launch,
    Timer,
    Network,
    Wake,
    CharmVisible,
}

/// <summary>When to ask the announcements endpoint, and when not to. macOS's <c>FetchPolicy</c>, rule for rule.</summary>
/// <remarks>
/// Asked at launch, when the network comes back, on waking from sleep, when the charm reappears after five minutes
/// away, and every fifteen minutes (a minute either side, so a fleet of PCs that woke together does not ask together).
/// Never twice within a minute, whatever asks. After a failure the timer backs off — one minute, two, four, eight, then
/// fifteen — and a returning network or a wake still asks at once.
/// </remarks>
public static class FetchPolicy
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MinimumGap = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Jitter = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan VisibleRefresh = TimeSpan.FromMinutes(5);

    public static bool ShouldFetch(FetchTrigger trigger, DateTimeOffset now, DateTimeOffset? lastAttempt, DateTimeOffset? lastSuccess)
    {
        if (lastAttempt is { } attempt && now - attempt < MinimumGap)
        {
            return false;
        }

        return trigger != FetchTrigger.CharmVisible || lastSuccess is not { } success || now - success >= VisibleRefresh;
    }

    /// <summary>When the timer should next ask. <paramref name="random"/> is in -1…1.</summary>
    public static DateTimeOffset NextTimer(DateTimeOffset now, int consecutiveFailures, double random)
    {
        if (consecutiveFailures <= 0)
        {
            return now + Interval + (Jitter * Math.Clamp(random, -1, 1));
        }

        double backoff = Math.Min(Interval.TotalSeconds, 60 * Math.Pow(2, Math.Min(consecutiveFailures, 8) - 1));
        return now + TimeSpan.FromSeconds(backoff);
    }
}

/// <summary>Which side of the charms the card went.</summary>
public enum CardSide
{
    Below,
    Right,
    Left,
    Clamped,
}

/// <summary>Where the card goes: below the charms if it fits there, otherwise beside them, never over them.</summary>
/// <remarks>
/// In screen coordinates (y down). <c>column</c> is the rope and its charms at rest, from the anchor to the bottom of the
/// lowest charm and as wide as the widest. macOS's <c>CardPlacement</c>, rule for rule.
/// </remarks>
public static class CardPlacement
{
    public static (double X, double Y, CardSide Side) Place(
        double cardWidth, double cardHeight, Geometry.Rect column, Geometry.Rect area, double gap)
    {
        double belowY = column.Bottom + gap;
        if (belowY + cardHeight <= area.Bottom)
        {
            double x = Math.Min(Math.Max(column.Left + (column.Width / 2) - (cardWidth / 2), area.Left), area.Right - cardWidth);
            return (x, belowY, CardSide.Below);
        }

        double y = Math.Min(Math.Max(column.Bottom - cardHeight, area.Top), area.Bottom - cardHeight);
        if (column.Right + gap + cardWidth <= area.Right)
        {
            return (column.Right + gap, y, CardSide.Right);
        }

        if (column.Left - gap - cardWidth >= area.Left)
        {
            return (column.Left - gap - cardWidth, y, CardSide.Left);
        }

        double centred = Math.Min(Math.Max(column.Left + (column.Width / 2) - (cardWidth / 2), area.Left), area.Right - cardWidth);
        return (centred, Math.Max(area.Top, area.Bottom - cardHeight), CardSide.Clamped);
    }
}

/// <summary>
/// Where "Open a link" may go: Hangly's own sites, Instagram and YouTube, and their subdomains. The same list as
/// <c>firestore.rules</c>, the endpoint and macOS: a stolen dashboard session cannot send everyone to a phishing page.
/// </summary>
public static partial class LinkPolicy
{
    public static readonly IReadOnlyList<string> Hosts = ["sharancreatedthis.in", "instagram.com", "youtube.com", "youtu.be"];

    public static bool Allows(string target)
    {
        if (target.Length > AnnouncementLimits.ActionTarget || !Shape().IsMatch(target)
            || !Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length > 0 || !uri.IsDefaultPort)
        {
            return false;
        }

        string host = uri.Host;
        return Hosts.Any(allowed => host == allowed || host.EndsWith("." + allowed, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"^https://[a-z0-9.-]+(/[^\s]*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
