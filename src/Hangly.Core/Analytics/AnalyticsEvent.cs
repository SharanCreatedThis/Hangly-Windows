//
//  AnalyticsEvent.cs
//  Hangly
//
//  The things the app is allowed to say about itself.
//

using Hangly.Core.Models;

namespace Hangly.Core.Analytics;

/// <summary>A property value, as a closed set of types.</summary>
/// <remarks>
/// Deliberately not <see cref="object"/>. An event built out of <c>object</c> cannot be
/// compared and cannot be asserted on in a test without casting — and the whole safety
/// argument for an analytics layer is that you can look at an event and see exactly what
/// leaves the machine. This is the list of things that may.
/// </remarks>
public abstract record AnalyticsValue
{
    private AnalyticsValue()
    {
    }

    public static AnalyticsValue Of(string value) => new Text(value);

    public static AnalyticsValue Of(int value) => new Integer(value);

    public static AnalyticsValue Of(double value) => new Number(value);

    public static AnalyticsValue Of(bool value) => new Flag(value);

    public static AnalyticsValue Of(IReadOnlyList<string> value) => new List(value);

    public sealed record Text(string Value) : AnalyticsValue;

    public sealed record Integer(int Value) : AnalyticsValue;

    public sealed record Number(double Value) : AnalyticsValue;

    public sealed record Flag(bool Value) : AnalyticsValue;

    /// <remarks>Compared by contents, because the generated equality would compare the list by reference.</remarks>
    public sealed record List(IReadOnlyList<string> Value) : AnalyticsValue
    {
        public bool Equals(List? other) =>
            other is not null && Value.SequenceEqual(other.Value, StringComparer.Ordinal);

        public override int GetHashCode()
        {
            var hash = default(HashCode);
            foreach (string item in Value)
            {
                hash.Add(item, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}

/// <summary>One thing that happened, and the few facts that go with it.</summary>
public sealed record AnalyticsEvent(string Name, IReadOnlyDictionary<string, AnalyticsValue> Properties)
{
    public AnalyticsEvent(string name)
        : this(name, new Dictionary<string, AnalyticsValue>(StringComparer.Ordinal))
    {
    }

    public bool Equals(AnalyticsEvent? other) =>
        other is not null
        && Name == other.Name
        && Properties.Count == other.Properties.Count
        && Properties.All(pair =>
            other.Properties.TryGetValue(pair.Key, out AnalyticsValue? value) && value == pair.Value);

    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
}

/// <summary>The whole vocabulary, in one place.</summary>
/// <remarks>
/// Nothing else in the app is allowed to invent an event name, which is what keeps the
/// list in <c>PRIVACY.md</c> true.
///
/// <para><b>The names are the macOS build's names, exactly.</b> Two platforms reporting
/// the same act under different names produce two datasets that cannot be added together,
/// and the whole value of counting anything here is being able to ask one question and
/// get one answer. That is also why the <c>airdrop_*</c> names survive onto a platform
/// with no AirDrop: on Windows the same act is a file dropped from Explorer, and calling
/// it something else would split the funnel in half.</para>
/// </remarks>
public static class Events
{
    public static AnalyticsEvent AppFirstLaunch { get; } = new("app_first_launch");

    public static AnalyticsEvent AppLaunch { get; } = new("app_launch");

    public static AnalyticsEvent AppQuit { get; } = new("app_quit");

    /// <summary>A charm was put on the rope.</summary>
    /// <param name="charmId">
    /// A built-in charm's catalogue id. A charm somebody made is reported as the word
    /// <c>custom</c> and nothing else — see <see cref="NameOf"/>.
    /// </param>
    public static AnalyticsEvent CharmAdded(string charmId) =>
        new("charm_added", Props(("charm", AnalyticsValue.Of(NameOf(charmId)))));

    public static AnalyticsEvent CharmRemoved(string charmId) =>
        new("charm_removed", Props(("charm", AnalyticsValue.Of(NameOf(charmId)))));

    public static AnalyticsEvent CharmReordered(int from, int to) =>
        new("charm_reordered", Props(("from", AnalyticsValue.Of(from)), ("to", AnalyticsValue.Of(to))));

    /// <summary>An image brought in from a file. The file is not named, measured or described.</summary>
    public static AnalyticsEvent CharmImported { get; } = new("charm_imported");

    /// <summary>A charm finished in Create and kept.</summary>
    public static AnalyticsEvent CharmSaved { get; } = new("charm_saved");

    public static AnalyticsEvent CharmSelected(string charmId) =>
        new("charm_selected", Props(("charm", AnalyticsValue.Of(NameOf(charmId)))));

    public static AnalyticsEvent RopeCountChanged(int count) =>
        new("rope_count_changed", Props(("count", AnalyticsValue.Of(count))));

    public static AnalyticsEvent RopeStyleChanged(RopeStyle style) =>
        new("rope_style_changed", Props(("style", AnalyticsValue.Of(style.ToString()))));

    /// <summary>
    /// A setting on the Appearance page moved. The name of the setting, never a value
    /// that could describe the person's screen.
    /// </summary>
    public static AnalyticsEvent AppearanceChanged(string setting) =>
        new("appearance_changed", Props(("setting", AnalyticsValue.Of(setting))));

    public static AnalyticsEvent CollectionOpened(string collection) =>
        new("collection_opened", Props(("collection", AnalyticsValue.Of(collection))));

    /// <summary>A charm from one of the collections was put on the rope.</summary>
    /// <remarks>
    /// Reported <i>in addition to</i> <see cref="CharmSelected"/>, not instead of it: that
    /// event is about the charm and this one is about the set it came from, and a query
    /// asking "how often is any charm chosen" should not have to know about collections
    /// to get the right answer.
    /// </remarks>
    public static AnalyticsEvent CollectionCharmSelected(string collection, string charmDisplayName) =>
        new("collection_charm_selected", Props(
            ("collection", AnalyticsValue.Of(collection)),
            ("charm", AnalyticsValue.Of(charmDisplayName))));

    public static AnalyticsEvent FollowPopupShown { get; } = new("follow_popup_shown");

    public static AnalyticsEvent FollowPopupFollowClicked { get; } = new("follow_popup_follow_clicked");

    public static AnalyticsEvent FollowPopupMaybeLater { get; } = new("follow_popup_maybe_later");

    public static AnalyticsEvent FollowPopupDismissed { get; } = new("follow_popup_dismissed");

    /// <summary>The Instagram link on the About page, a different place from the card.</summary>
    public static AnalyticsEvent FollowInstagramClicked { get; } = new("follow_instagram_clicked");

    /// <summary>A file entered the charm's hit area while file-drop mode is on.</summary>
    public static AnalyticsEvent AirdropDragEntered { get; } = new("airdrop_drag_entered");

    /// <summary>A file was released on the charm.</summary>
    /// <remarks>
    /// The extension and a coarse size bucket are the only facts that leave: no file
    /// name, no path, no destination, no user content.
    /// </remarks>
    public static AnalyticsEvent AirdropFileDropped(string extension, long sizeInBytes) =>
        new("airdrop_file_dropped", Props(
            ("fileType", AnalyticsValue.Of(extension.TrimStart('.').ToLowerInvariant())),
            ("fileSizeBucket", AnalyticsValue.Of(FileSizeBucket(sizeInBytes)))));

    public static AnalyticsEvent AirdropPickerOpened { get; } = new("airdrop_picker_opened");

    public static AnalyticsEvent CoffeeSheetOpened(string source) =>
        new("coffee_sheet_opened", Props(("coffee_button_source", AnalyticsValue.Of(source))));

    public static AnalyticsEvent CoffeeCopyUpi(string source) =>
        new("coffee_copy_upi", Props(("coffee_button_source", AnalyticsValue.Of(source))));

    public static AnalyticsEvent CoffeeQrViewed(string source) =>
        new("coffee_qr_viewed", Props(("coffee_button_source", AnalyticsValue.Of(source))));

    /// <summary>The coarse bucket a file's size falls in. Never the size itself.</summary>
    public static string FileSizeBucket(long bytes) => bytes switch
    {
        < 0 => "unknown",
        < 1_000_000 => "<1MB",
        < 10_000_000 => "1-10MB",
        < 100_000_000 => "10-100MB",
        < 1_000_000_000 => "100MB-1GB",
        _ => ">1GB",
    };

    /// <summary>
    /// How a charm is named in an event: built-ins by their catalogue id, anything
    /// else as the word <c>custom</c>.
    /// </summary>
    /// <remarks>
    /// An imported charm's identifier is unique to one person's file, and knowing that
    /// somebody imported <i>something</i> is the entire useful content of the fact.
    /// </remarks>
    public static string NameOf(string charmId) => CharmCatalog.Contains(charmId) ? charmId : "custom";

    private static Dictionary<string, AnalyticsValue> Props(params (string Key, AnalyticsValue Value)[] pairs)
    {
        var properties = new Dictionary<string, AnalyticsValue>(pairs.Length, StringComparer.Ordinal);
        foreach ((string key, AnalyticsValue value) in pairs)
        {
            properties[key] = value;
        }

        return properties;
    }
}
