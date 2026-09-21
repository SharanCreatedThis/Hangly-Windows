//
//  CharmCatalog.cs
//  Hangly
//
//  The shipped charm set, and how a charm's artwork becomes physics.
//

using Hangly.Core.Geometry;

namespace Hangly.Core.Models;

/// <summary>Every built-in charm, and the rules that turn one into something the rope can carry.</summary>
/// <remarks>
/// The eighty-one entries are in <c>CharmCatalog.Generated.cs</c>, written from the Swift
/// by <c>tools/generate-catalogue.py</c>. This half is the part that is reasoning rather
/// than data, and it is hand-written for the same reason the other half is not.
///
/// <para><b>Why the knot and the beads are not in the table.</b> Both are measured from
/// the artwork by <see cref="CharmArtworkSplitter"/>. The designer drew the beads where
/// they are; a number typed beside the file is a second opinion that can disagree with
/// the picture, and the picture is the one the user sees. A rope at rest is therefore
/// laid out exactly as drawn, and the simulation only lets the beads slide from there.
/// </para>
/// </remarks>
public static partial class CharmCatalog
{
    /// <summary>
    /// Used only when an asset cannot be measured, which means it is missing: the knot
    /// then sits on the placeholder bead's bounding circle.
    /// </summary>
    public const double FallbackKnotInset = 0.96;

    /// <summary>
    /// How much heavier a bead is than the charm for its size. Beads are solid glass or
    /// metal; a charm is mostly hollow.
    /// </summary>
    public const double BeadDensity = 6.0;

    /// <summary>Floor on a bead's weight, so the smallest still pulls on the cord.</summary>
    public const double MinimumBeadMass = 0.05;

    /// <summary>
    /// What hangs on the rope when nothing has been chosen — the charm Hangly opens with.
    /// </summary>
    /// <remarks>
    /// The plain bead, which is <c>circle</c> in the catalogue and <c>Bead.svg</c> on
    /// disk. It is also what <see cref="Find"/> falls back to, so an unknown id from a
    /// settings file written by a newer build can never leave the rope bare.
    /// </remarks>
    public const string DefaultId = "circle";

    /// <summary>The charm a brand-new install hangs.</summary>
    /// <remarks>
    /// Deliberately not <see cref="DefaultId"/>, which is the <em>fallback</em> — what a
    /// rope falls back to when the charm it names has been deleted or was written by a
    /// build that had one this one does not. That has to stay the plain bead: it is the
    /// one charm that is always present and always correct, and swapping it would mean a
    /// deleted import silently becoming somebody else's charm.
    ///
    /// <para>This is a different question — what somebody should meet on their first
    /// launch — and the answer is the nazar. It reads as a charm at a glance, it is the
    /// one most people recognise, and a plain grey bead is a poor first impression of a
    /// catalogue of seventy.</para>
    /// </remarks>
    public const string FirstRunId = "nazar";

    /// <summary>The name of the collection a charm belongs to, or empty if it has none.</summary>
    /// <remarks>
    /// Imported charms have no collection, which is why this can be empty rather than
    /// falling back to something that sounds like one.
    /// </remarks>
    public static string CollectionNameOf(CharmCatalogEntry entry)
    {
        foreach (CharmCollection collection in Collections)
        {
            if (string.Equals(collection.Id, entry.CategoryId, StringComparison.Ordinal))
            {
                return collection.Name;
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, CharmCatalogEntry>? index;

    /// <summary>
    /// Built on first use rather than in a field initialiser. <see cref="All"/> lives in
    /// the generated half of this partial class, and C# does not order static field
    /// initialisers across partial files: initialised eagerly, this ran first and found
    /// <see cref="All"/> still null, so every call threw TypeInitializationException.
    /// </summary>
    private static Dictionary<string, CharmCatalogEntry> ById =>
        index ??= All.ToDictionary(entry => entry.Id, StringComparer.Ordinal);

    /// <summary>The charm with this id, or the plain bead when there is no such charm.</summary>
    public static CharmCatalogEntry Find(string id) =>
        ById.TryGetValue(id, out CharmCatalogEntry? entry) ? entry : ById[DefaultId];

    /// <summary>Whether the catalogue knows this id.</summary>
    public static bool Contains(string id) => ById.ContainsKey(id);

    /// <summary>
    /// What the rope has to carry, with the knot where the artwork says it is.
    /// </summary>
    /// <param name="regions">
    /// The measured split, or <see langword="null"/> when the artwork could not be read.
    /// </param>
    public static CharmMetrics MetricsFor(CharmCatalogEntry entry, CharmArtworkRegions? regions) => new(
        entry.Mass,
        entry.RadiusRatio,
        regions?.KnotInset ?? FallbackKnotInset);

    /// <summary>
    /// The beads the artwork draws above the charm, in proportions of the charm's radius
    /// so they survive a rescale.
    /// </summary>
    /// <remarks>
    /// A charm that draws none gets none. There used to be a set of three invented here
    /// for that case, on the reasoning that a charm created from a photograph would
    /// otherwise hang on a bare string beside seventy that do not — but fifty-five of the
    /// built-ins draw no beads either, and they all got them.
    /// </remarks>
    public static IReadOnlyList<CharmBead> BeadsFor(CharmCatalogEntry entry, CharmArtworkRegions? regions)
    {
        if (regions is not CharmArtworkRegions split)
        {
            return [];
        }

        double longest = Math.Max(split.Body.Width, split.Body.Height);
        if (longest <= 0)
        {
            return [];
        }

        // Unit-square distances become multiples of the charm's radius.
        double scale = 2 / longest;

        var beads = new List<CharmBead>(split.Beads.Count);
        foreach (Rect rect in split.Beads)
        {
            var size = new Size(rect.Width * scale, rect.Height * scale);

            // The mean of the two sides, so a long bead is not weighed as a sphere of
            // its length.
            double radius = Math.Sqrt(size.Width * size.Height) / 2;

            beads.Add(new CharmBead(
                size,
                Offset: (split.Body.Top - rect.MidY) * scale,

                // Weight for a solid bead of this size beside the charm's own, floored
                // so the lightest still registers on the rope.
                Mass: Math.Max(MinimumBeadMass, entry.Mass * Math.Pow(radius, 3) * BeadDensity)));
        }

        return beads;
    }
}
