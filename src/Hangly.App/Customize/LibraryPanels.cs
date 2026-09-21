//
//  LibraryPanels.cs
//  Hangly
//
//  The detail panel and the collection cards: what the Library shows around the grid.
//

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hangly.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Hangly.App.Customize;

/// <summary>Everything the detail panel says about the charm in hand.</summary>
/// <remarks>
/// The macOS counterpart is <c>LibraryDetailPanel</c>, and the documentation is explicit
/// about how it is driven: "the detail pane, which always shows the selection". So this
/// follows the selection and never drives it — picking a charm changes the rope, and the
/// panel reports what changed rather than offering a second way to do it.
///
/// <para>Every field it shows was already in the catalogue: region, description and tags
/// are populated for all seventy charms and were simply never displayed.</para>
/// </remarks>
public sealed class CharmDetail : INotifyPropertyChanged
{
    private bool isOnRope;
    private bool isFavourite;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Where the charm is from, shown in small capitals above the name.</summary>
    public string Region { get; private set; } = string.Empty;

    /// <summary>
    /// The collection this charm belongs to, shown beside its region.
    /// </summary>
    /// <remarks>
    /// Which collection a charm came from is the one fact the detail panel was missing:
    /// somebody looking at a charm they found through search had no way of knowing it was
    /// a Marvel charm or a Tamil Spiritual one without going back and filtering.
    /// </remarks>
    public string Category { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; private set; } = [];

    public ImageSource? Image { get; private set; }

    /// <summary>Whether there is a charm to describe at all.</summary>
    public bool HasCharm { get; private set; }

    public Visibility PanelVisibility => HasCharm ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PlaceholderVisibility => HasCharm ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CategoryVisibility =>
        Category.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RegionVisibility =>
        string.IsNullOrWhiteSpace(Region) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsOnRope
    {
        get => isOnRope;
        set
        {
            if (isOnRope == value)
            {
                return;
            }

            isOnRope = value;
            Notify();
            Notify(nameof(RopeGlyph));
            Notify(nameof(RopeLabel));
        }
    }

    /// <summary>A filled check when it is hanging, an outline when it is not.</summary>
    public string RopeGlyph => isOnRope ? "" : "";

    public string RopeLabel => isOnRope ? "On the rope" : "Not on the rope";

    public bool IsFavourite
    {
        get => isFavourite;
        set
        {
            if (isFavourite == value)
            {
                return;
            }

            isFavourite = value;
            Notify();
            Notify(nameof(FavouriteGlyph));
        }
    }

    public string FavouriteGlyph => isFavourite ? "" : "";

    /// <summary>Points the panel at a charm, or at nothing.</summary>
    public void Show(CharmCatalogEntry? entry, ImageSource? image)
    {
        HasCharm = entry is not null;
        DisplayName = entry?.DisplayName ?? string.Empty;
        Region = entry?.Region ?? string.Empty;
        Category = entry is null ? string.Empty : CharmCatalog.CollectionNameOf(entry);
        Description = entry?.Description ?? string.Empty;
        Tags = entry?.Tags ?? [];
        Image = image;

        Notify(nameof(DisplayName));
        Notify(nameof(Region));
        Notify(nameof(Category));
        Notify(nameof(CategoryVisibility));
        Notify(nameof(Description));
        Notify(nameof(Tags));
        Notify(nameof(Image));
        Notify(nameof(HasCharm));
        Notify(nameof(PanelVisibility));
        Notify(nameof(PlaceholderVisibility));
        Notify(nameof(RegionVisibility));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One collection, as a card above the grid.</summary>
/// <remarks>
/// The macOS counterpart is <c>CollectionHeroCard</c>. Its artwork is a small cluster of
/// the collection's own charms rather than a separate illustration, which is what lets a
/// collection be added without anyone drawing a cover for it.
/// </remarks>
public sealed class CollectionCard
{
    public CollectionCard(CharmCollection collection, IReadOnlyList<CharmTile> members)
    {
        Id = collection.Id;
        Name = collection.Name;
        Description = collection.Description;
        Count = members.Count;

        // Three at most, and the first three: enough to say what the collection looks
        // like without the card becoming a second grid.
        First = members.ElementAtOrDefault(0)?.Image;
        Second = members.ElementAtOrDefault(1)?.Image;
        Third = members.ElementAtOrDefault(2)?.Image;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public int Count { get; }

    public string CountLabel => Count == 1 ? "1 charm" : $"{Count} charms";

    public ImageSource? First { get; }

    public ImageSource? Second { get; }

    public ImageSource? Third { get; }

    public Visibility SecondVisibility => Second is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ThirdVisibility => Third is null ? Visibility.Collapsed : Visibility.Visible;
}

/// <summary>One place on the rope, as the reorder strip shows it.</summary>
/// <remarks>
/// The macOS counterpart is <c>RopeSlotTile</c>, and its strip reorders by dragging —
/// <c>RopeSlotDropDelegate</c> is the other half. A WinUI <c>ListView</c> with
/// <c>CanReorderItems</c> does the same job without the drop logic being written here,
/// which is why the places are a list rather than the row of buttons they used to be.
/// </remarks>
public sealed class SlotTile
{
    public SlotTile(int index, string name, Microsoft.UI.Xaml.Media.ImageSource? image)
    {
        Index = index;
        Name = name;
        Image = image;
    }

    /// <summary>Where this place sat when the strip was built, from the anchor down.</summary>
    public int Index { get; }

    public string Name { get; }

    public Microsoft.UI.Xaml.Media.ImageSource? Image { get; }
}

/// <summary>One rope in the Library's rope list.</summary>
/// <remarks>
/// A name and a sentence, and no picture. The five ropes differ in how they move rather
/// than in how they are drawn — each is a different set of solver values — so a thumbnail
/// of a cord would show five near-identical lines and tell somebody nothing.
/// </remarks>
public sealed record RopeChoiceItem(string Name, string Description);
