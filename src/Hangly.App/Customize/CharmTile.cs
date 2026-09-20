//
//  CharmTile.cs
//  Hangly
//
//  One charm as the picker shows it.
//

using System.ComponentModel;
using Hangly.Core.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Hangly.App.Customize;

/// <summary>A charm in the grid: its picture, its name, and whether it is on the cord.</summary>
/// <remarks>
/// A class with properties rather than a record, because <c>x:Bind</c> reads properties
/// and <see cref="INotifyPropertyChanged"/> is how the chosen outline appears without
/// rebuilding the grid.
///
/// <para>The picture and the outline are exposed as the types XAML actually wants — an
/// <see cref="ImageSource"/> and a <see cref="Brush"/> — rather than as a path and a
/// bool with converters in the markup. Converters in a template are a layer to debug
/// through when a tile does not draw, and this window has enough surface already.</para>
/// </remarks>
public sealed class CharmTile : INotifyPropertyChanged
{
    private static readonly SolidColorBrush Unchosen = new(Microsoft.UI.Colors.Transparent);

    private bool isChosen;
    private bool isFavourite;

    public CharmTile(CharmCatalogEntry entry)
    {
        Entry = entry;

        string? path = CharmThumbnails.PathFor(entry);
        Image = path is null ? null : new BitmapImage(new Uri(path));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CharmCatalogEntry Entry { get; }

    public string Id => Entry.Id;

    public string DisplayName => Entry.DisplayName;

    public ImageSource? Image { get; }

    /// <summary>Whether this charm is currently hanging.</summary>
    public bool IsChosen
    {
        get => isChosen;
        set
        {
            if (isChosen == value)
            {
                return;
            }

            isChosen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChosen)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Outline)));
        }
    }

    /// <summary>The tile's border: the accent colour when chosen, nothing when not.</summary>
    public Brush Outline => isChosen
        ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"]
        : Unchosen;

    /// <summary>Whether this charm has been starred.</summary>
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavourite)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavouriteGlyph)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavouriteLabel)));
        }
    }

    /// <summary>A filled star when starred, an outline when not. Segoe Fluent Icons.</summary>
    public string FavouriteGlyph => isFavourite ? "\uE735" : "\uE734";

    /// <summary>
    /// What a screen reader says, and what the tooltip shows.
    /// </summary>
    /// <remarks>
    /// Names the charm as well as the action. A grid of eighty-one buttons all announcing
    /// "Add to favourites" tells somebody navigating by voice nothing about which one
    /// they are on.
    /// </remarks>
    public string FavouriteLabel => isFavourite
        ? $"Remove {DisplayName} from favourites"
        : $"Add {DisplayName} to favourites";
}

/// <summary>The charms of one pack, which is how the grid is divided.</summary>
public sealed class CharmGroup(string name, IReadOnlyList<CharmTile> charms)
{
    public string Name { get; } = name;

    public IReadOnlyList<CharmTile> Charms { get; } = charms;
}
