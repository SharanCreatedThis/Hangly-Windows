//
//  CharmHooks.cs
//  Hangly
//
//  The charms whose artwork has a hook to tie the cord to.
//

namespace Hangly.Core.Models;

/// <summary>The charms drawn with a hook, ring or loop at their top for the cord to be tied to. macOS's <c>CharmHooks</c>.</summary>
/// <remarks>
/// Every charm here hangs from the rope by a metal end cap and a jump ring through its own ring (`HookConnector`), and
/// the rope runs in behind the top of every other charm, out of sight. Checked by eye against the artwork of every charm
/// in the library: a picture cannot tell a ring from the gap between a figure's arms reliably enough to decide it, and a
/// jump ring through somebody's elbow is worse than a rope run behind them.
///
/// <para>A charm that hangs from a bail — a solid tube above a jump ring, as the BTS figures, Superman and Green
/// Lantern do — is not here: a cord goes into a bail from the top, which is what it does on a charm without a hook.</para>
///
/// <para>The macOS build keeps the same list; the release gate checks the two agree.</para>
/// </remarks>
public static class CharmHooks
{
    public static IReadOnlySet<string> Ids { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // Classics
        "circle", "heart", "diamond", "horseshoe", "camera", "star",

        // Collection
        "ghanta", "scarab", "dreamCatcher", "templeBell", "vinayagarCoin", "omSymbol", "vel", "rudraksha", "allahPendant",
        "himmeli",
        // Hung by the cord loop at the top of their own drawn knot, the whole picture one charm (Sharan, 4 Oct).
        "daruma", "panchangJie",
        // The ring on the tip of Karuppu's sword, hung like the Vinayagar coin (Sharan, 4 Oct).
        "karuppuStatue",

        // Heroes
        "captainAmericaShield", "ironManHelmet", "hulkFist", "batmanSymbol", "wonderWomanEmblem", "shazamLightning",

        // Football
        "ronaldoJersey", "messiJersey", "neymarJersey", "realMadridCrest", "fcBarcelonaCrest",

        // Anime and sagas
        "surveyCorpsEmblem", "cadetCorpsEmblem", "akatsukiCloud", "sharingan", "houseStark", "houseTargaryen",
        "houseLannister",

        // Sneakers
        "airJordan1Chicago", "airJordan1Bred", "airJordan3", "airJordan4FireRed", "airJordan5", "airJordan6Carmine",
        "airJordan11Concord", "offWhiteJordan1",
    };
}
