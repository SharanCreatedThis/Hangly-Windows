//
//  CharmAnchors.cs
//  Hangly
//
//  Where the rope meets and leaves each built-in charm, read from the table both apps share. macOS's CharmAnchors.
//

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hangly.Core.Models;

/// <summary>The rope anchors of every built-in charm, read from <c>CharmAnchors.json</c>.</summary>
/// <remarks>
/// Generated on macOS from the artwork by <c>Scripts/generate-charm-anchors.sh</c> and copied here byte for byte; never
/// edited by hand. It is the one answer for the built-in charms on both platforms: where the cord meets a charm, where it
/// comes out to the charm below, and the hook it hangs by. A charm with no entry — anything imported — is measured at run
/// time. Everything is in the artwork's fitted unit square, (0, 0) at the top left.
/// </remarks>
public sealed record CharmAnchors(int Version, double Tolerance, IReadOnlyDictionary<string, CharmAnchors.Entry> Charms)
{
    public sealed record Point(double X, double Y);

    public sealed record Box(double X, double Y, double Width, double Height);

    public sealed record Hook(double BarTop, double EyeTop, double EyeBottom, double CentreX, double SlotWidth);

    /// <param name="Attachment">"ring", "top" or "ownRope".</param>
    /// <param name="MeasurementMode">"auto", or "override" with <paramref name="OverriddenFields"/> set by hand, and why.</param>
    /// <param name="ReviewNote">Measured, looked at, and kept as measured: why.</param>
    public sealed record Entry(
        string Attachment,
        bool HasHook,
        Box? Body = null,
        Point? TopAnchor = null,
        double? TopDepth = null,
        Point? BottomExit = null,
        double? BottomDepth = null,
        Point? ConnectorAnchor = null,
        Hook? Hook = null,
        string MeasurementMode = "auto",
        IReadOnlyList<string>? OverriddenFields = null,
        string? OverrideReason = null,
        string? ReviewNote = null);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The table at <paramref name="path"/>, or null if it is missing or unreadable.</summary>
    public static CharmAnchors? Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<CharmAnchors>(File.ReadAllText(path), Options) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The measured regions with this entry's anchors in place of the measured ones.</summary>
    public static CharmArtworkRegions Apply(CharmArtworkRegions regions, Entry entry) => regions with
    {
        Hook = entry.Hook is Hook hook
            ? new CharmArtworkHook(hook.BarTop, hook.EyeTop, hook.EyeBottom, hook.CentreX, hook.SlotWidth)
            : null,
        AxisY = entry.TopAnchor?.Y,
        AxisBottom = entry.TopAnchor is Point top && entry.TopDepth is double depth ? top.Y + depth : null,
        AxisLastBottom = entry.BottomExit?.Y,
        AxisLastTop = entry.BottomExit is Point exit && entry.BottomDepth is double exitDepth ? exit.Y - exitDepth : null,
        ExitX = entry.BottomExit?.X,
    };

    /// <summary>The largest distance between two entries' anchors, in the unit square. macOS's <c>drift</c>.</summary>
    public static double Drift(Entry measured, Entry stored)
    {
        static double Gap(Point? left, Point? right) => (left, right) switch
        {
            (Point a, Point b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y))),
            (null, null) => 0,
            _ => double.PositiveInfinity,
        };

        // A field set by hand is not the artwork's to agree with.
        bool Manual(string field) => stored.OverriddenFields?.Contains(field) == true;
        double top = Manual("topAnchor") ? 0 : Gap(measured.TopAnchor, stored.TopAnchor);
        double exit = Manual("bottomExit") ? 0 : Gap(measured.BottomExit, stored.BottomExit);
        double worst = Math.Max(top, Math.Max(exit, Gap(measured.ConnectorAnchor, stored.ConnectorAnchor)));
        if (measured.HasHook != stored.HasHook)
        {
            return double.PositiveInfinity;
        }

        if (measured.Hook is Hook left && stored.Hook is Hook right)
        {
            worst = Math.Max(worst, Math.Max(Math.Abs(left.BarTop - right.BarTop), Math.Max(Math.Abs(left.EyeBottom - right.EyeBottom), Math.Abs(left.SlotWidth - right.SlotWidth))));
        }

        return worst;
    }
}
