//
//  WelcomeText.cs
//  Hangly
//
//  The welcome card's words, the same on both apps.
//

namespace Hangly.Core.Lifecycle;

/// <summary>What the second step of the welcome card says. The macOS <c>WelcomeText</c>, word for word.</summary>
/// <remarks>
/// One text, so the two apps introduce themselves the same way. The middle line teaches the
/// primary way in to Creator Studio (D7): drop a picture on the charm. Only where Hangly
/// lives differs, because a notification area is not a menu bar.
/// </remarks>
public static class WelcomeText
{
    public static string Title(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Welcome to Hangly" : $"Welcome, {name.Trim()}";

    public const string Tagline = "A tiny charm that hangs from your screen.";

    public static IReadOnlyList<string> Things { get; } =
    [
        "Browse charms from around the world",
        "Drop a picture on the charm to make your own",
        "Change the rope, where it hangs and how big it is",
    ];

    public const string Explore = "Explore Library";

    /// <summary>Decision B5: the words on both apps.</summary>
    public const string Start = "Start Using Hangly";
}
