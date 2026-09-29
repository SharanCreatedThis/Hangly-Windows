//
//  HanglyButtons.cs
//  Hangly
//
//  The call-to-action buttons, in Hangly's own colour.
//

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hangly.App.Branding;

/// <summary>Hangly's primary and secondary buttons: macOS's <c>HanglyPrimaryButtonStyle</c> and <c>HanglySecondaryButtonStyle</c>.</summary>
/// <remarks>
/// <b>Hangly's indigo, not the system accent.</b> The same two colours as the macOS asset
/// catalog's <c>AccentColor</c> — #6D5AE5, and #8F7EFF in dark mode — so the one button a
/// card most wants pressed looks like Hangly on both platforms rather than like whichever
/// accent Windows is set to. 44 points tall (48 for the Enjoying Hangly card's hero) with a
/// 12-point corner, and no glow: it brightens a little under the pointer and darkens a
/// little when pressed, as on macOS.
/// </remarks>
internal static class HanglyButtons
{
    private static readonly Windows.UI.Color Light = Windows.UI.Color.FromArgb(255, 0x6D, 0x5A, 0xE5);
    private static readonly Windows.UI.Color Dark = Windows.UI.Color.FromArgb(255, 0x8F, 0x7E, 0xFF);

    /// <summary>Solid Hangly indigo, white semibold type.</summary>
    public static Button Primary(object content, double height = 44)
    {
        var button = Sized(new Button
        {
            Content = content,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        }, height);
        button.FontSize = height >= 48 ? 16 : 15;
        Brand(button);
        button.ActualThemeChanged += (_, _) => Brand(button);
        return button;
    }

    /// <summary>The second choice beside a primary one: the same size, the standard fill.</summary>
    public static Button Secondary(object content, double height = 44) => Sized(new Button { Content = content }, height);

    /// <summary>The corner every card button and field shares, on both platforms.</summary>
    public static CornerRadius Corner => new(12);

    /// <summary>Gives the accent buttons in <paramref name="element"/> Hangly's colour: a button written in markup, or a dialog's primary button.</summary>
    public static void Brand(FrameworkElement element)
    {
        Windows.UI.Color colour = element.ActualTheme == ElementTheme.Dark ? Dark : Light;
        var rest = new SolidColorBrush(colour);
        var over = new SolidColorBrush(Shade(colour, 1.06));
        var pressed = new SolidColorBrush(Shade(colour, 0.92));
        var white = new SolidColorBrush(Microsoft.UI.Colors.White);

        // As the button's own resources rather than its Background, so hover and pressed
        // follow it: assigning Background alone leaves a button that changes colour under
        // the pointer.
        element.Resources["AccentButtonBackground"] = rest;
        element.Resources["AccentButtonBackgroundPointerOver"] = over;
        element.Resources["AccentButtonBackgroundPressed"] = pressed;
        element.Resources["AccentButtonBorderBrush"] = rest;
        element.Resources["AccentButtonBorderBrushPointerOver"] = over;
        element.Resources["AccentButtonBorderBrushPressed"] = pressed;
        element.Resources["AccentButtonForeground"] = white;
        element.Resources["AccentButtonForegroundPointerOver"] = white;
        element.Resources["AccentButtonForegroundPressed"] = white;
    }

    private static Button Sized(Button button, double height)
    {
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.Height = height;
        button.FontSize = 15;
        button.CornerRadius = Corner;
        return button;
    }

    private static Windows.UI.Color Shade(Windows.UI.Color colour, double factor) => Windows.UI.Color.FromArgb(
        colour.A,
        (byte)Math.Clamp(colour.R * factor, 0, 255),
        (byte)Math.Clamp(colour.G * factor, 0, 255),
        (byte)Math.Clamp(colour.B * factor, 0, 255));
}
