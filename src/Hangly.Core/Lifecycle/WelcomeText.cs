//
//  WelcomeText.cs
//  Hangly
//
//  The welcome card's words, the same on both apps.
//

namespace Hangly.Core.Lifecycle;

/// <summary>What the second step of the welcome card says. The macOS <c>WelcomeText</c>, word for word.</summary>
/// <remarks>
/// One text, so the two apps introduce themselves the same way: two lines and no list — what
/// Hangly is for, then the invitation. Only where Hangly lives differs, because a
/// notification area is not a menu bar.
/// </remarks>
public static class WelcomeText
{
    public static string Title(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Welcome to Hangly" : $"Welcome, {name.Trim()}";

    public const string Tagline = "A little delight, every time you look up.";

    public const string Invitation = "Choose your charm. Make every detail yours.";

    public const string Explore = "Explore Library";

    /// <summary>Decision B5: the words on both apps.</summary>
    public const string Start = "Start Using Hangly";
}
