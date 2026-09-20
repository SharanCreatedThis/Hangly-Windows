//
//  SecretVault.cs
//  Hangly
//
//  The things the charm will admit to, one at a time.
//

namespace Hangly.Core.Models;

/// <summary>The secrets the About page hands out, one per press.</summary>
/// <remarks>
/// <b>Where these came from.</b> Every line is a literal read out of the shipping macOS
/// 2.0.0 binary, not written here. The macOS symbol is <c>SecretVault</c> and the button
/// it feeds describes itself as "Reveals one of the app's secrets, and pushes the rope" —
/// which is why <see cref="Reveal"/> is only half the feature and the caller nudges the
/// overlay.
///
/// <para>The one thing inferred rather than read: macOS has the line "You found the rare
/// secret." alongside the rest, which reads like an outcome rather than a secret. It is
/// treated here as a rare draw. If that is wrong it is wrong in a way nobody can see
/// except by pressing the button a great many times.</para>
/// </remarks>
public static class SecretVault
{
    /// <summary>What the button says before it has ever been pressed.</summary>
    public const string Unrevealed = "No secret revealed yet";

    /// <summary>Roughly one press in forty.</summary>
    private const int RareOdds = 40;

    public const string Rare = "You found the rare secret.";

    /// <summary>The ordinary secrets, in the order the binary lists them.</summary>
    public static IReadOnlyList<string> Secrets { get; } =
    [
        "There is no secret.",
        "This rope secretly prefers neon.",
        "The charm believes in you.",
        "The charm has witnessed every tab you've left open.",
        "Some charms swing longer when nobody is watching.",
        "Every swing is calculated. The luck is not.",
        "The physics engine is working harder than it looks.",
        "The rope knows where it is. The rope knows where it isn't.",
        "Every charm on the rope is drawn at the size it hangs at. None of them is scaled to fit.",
        "The charms are real objects. People hang them on doors, mirrors, rear-view mirrors and babies. Yours hangs on a menu bar.",
        "This app began as: \"What if desktop icons needed emotional support?\"",
        "This rope has survived more swings than most relationships.",
    ];

    /// <summary>Draws one, avoiding the one just shown.</summary>
    /// <remarks>
    /// Not repeating is the whole trick: a vault of twelve that can say the same thing
    /// twice in a row reads as broken rather than random, which is a well-worn finding
    /// about shuffle buttons and applies exactly here.
    /// </remarks>
    public static string Reveal(Random random, string? previous)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (random.Next(RareOdds) == 0)
        {
            return Rare;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            string candidate = Secrets[random.Next(Secrets.Count)];
            if (candidate != previous)
            {
                return candidate;
            }
        }

        return Secrets[0];
    }
}
