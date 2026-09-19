//
//  OverlayAnchor.cs
//  Hangly
//

namespace Hangly.Core.Models;

/// <summary>Which edge of the display the rope hangs from.</summary>
/// <remarks>
/// Top only, and that is not an oversight: the rope hangs, so the anchor is always at
/// the top and what a person is choosing is which way along it.
/// </remarks>
public enum OverlayAnchor
{
    TopLeading,
    TopCenter,
    TopTrailing,
}

public static class OverlayAnchorTable
{
    public static string DisplayNameOf(OverlayAnchor anchor) => anchor switch
    {
        OverlayAnchor.TopLeading => "Top Left",
        OverlayAnchor.TopCenter => "Top Center",
        OverlayAnchor.TopTrailing => "Top Right",
        _ => "Top Center",
    };
}
