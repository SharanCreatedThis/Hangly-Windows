//
//  AppSettings.cs
//  Hangly
//
//  The settings document, and the tolerant reading of it.
//

using System.Text.Json;
using System.Text.Json.Serialization;
using Hangly.Core.Models;

namespace Hangly.Core.Settings;

/// <summary>Everything about the overlay a person can change.</summary>
/// <remarks>
/// A plain immutable record: no change notification, nothing platform-shaped, nothing
/// that can be mutated from two places at once. The store owns the one write path.
/// </remarks>
public sealed record OverlaySettings
{
    public bool IsEnabled { get; init; } = true;

    /// <summary>How visible the charm is, 0.2 to 1.</summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>How large the charm is drawn, as a multiple of the shipped size.</summary>
    public double CharmSize { get; init; } = 1.0;

    /// <summary>How far the charm hangs, as a multiple of the shipped rope.</summary>
    public double RopeLength { get; init; } = 1.0;

    public OverlayAnchor Anchor { get; init; } = OverlayAnchor.TopCenter;

    /// <summary>User nudge from the anchor, in points.</summary>
    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public RopeStyle RopeStyle { get; init; } = RopeStyleTable.Shipped;

    /// <summary>Index of the display to hang on, in the order the system reports them.</summary>
    public int DisplayIndex { get; init; }

    public bool SoundEnabled { get; init; } = true;

    public double SoundVolume { get; init; } = 0.5;

    /// <summary>Clamps every field into the range the app supports.</summary>
    /// <remarks>
    /// Applied on read rather than trusted, because a settings file outlives the build
    /// that wrote it and a hand-edited one outlives good intentions.
    /// </remarks>
    public OverlaySettings Clamped() => this with
    {
        Opacity = Math.Clamp(Opacity, 0.2, 1.0),
        CharmSize = Math.Clamp(CharmSize, 0.5, 2.0),
        RopeLength = Math.Clamp(RopeLength, 0.5, 2.0),
        OffsetX = Math.Clamp(OffsetX, -4000, 4000),
        OffsetY = Math.Clamp(OffsetY, -2000, 2000),
        DisplayIndex = Math.Max(0, DisplayIndex),
        SoundVolume = Math.Clamp(SoundVolume, 0, 1),
    };
}

/// <summary>The whole settings document.</summary>
public sealed record AppSettings
{
    /// <summary>
    /// Written on every save so a future migration can be deliberate rather than
    /// archaeological.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    public bool LaunchAtLogin { get; init; }

    public bool HasSeenWelcome { get; init; }

    public OverlaySettings Overlay { get; init; } = new();

    public AppSettings Clamped() => this with { Overlay = Overlay.Clamped() };

    /// <summary>The reader and writer both sides of persistence use.</summary>
    /// <remarks>
    /// Enums are written as their names rather than their ordinals. A number would tie
    /// the file to the order the cases happen to be declared in, and inserting a rope
    /// style in the middle of that enum would silently re-point every existing user's
    /// cord at a different one.
    /// </remarks>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },

        // A document written by a newer build may name fields this one has never heard
        // of. Ignoring them is what lets a person move between versions without losing
        // everything they had set.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a document, falling back field by field rather than all at once.</summary>
    /// <remarks>
    /// Decoding is tolerant by design. Throwing on a missing key would mean that a new
    /// field in a future release discards every existing preference; throwing on a
    /// corrupt file would mean a bad write blocks launch. So an unreadable document
    /// yields the defaults, a partial one yields the defaults for what it omits, and an
    /// out-of-range one is clamped.
    /// </remarks>
    public static AppSettings FromJson(string json, out bool wasRecovered)
    {
        wasRecovered = false;

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        try
        {
            AppSettings? decoded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (decoded is null)
            {
                wasRecovered = true;
                return new AppSettings();
            }

            return decoded.Clamped();
        }
        catch (JsonException)
        {
            // Corrupt beyond a missing key. Logged by the caller and replaced with
            // defaults rather than blocking launch.
            wasRecovered = true;
            return new AppSettings();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
