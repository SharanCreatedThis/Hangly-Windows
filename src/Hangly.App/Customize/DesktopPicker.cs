//
//  DesktopPicker.cs
//  Hangly
//
//  Putting the charm where you want it by putting it there.
//

using Hangly.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

namespace Hangly.App.Customize;

/// <summary>A miniature of the display, with the charm on it, that you drag. The macOS <c>DesktopPositionPicker</c>.</summary>
/// <remarks>
/// A point on the picture is a point on the display (<see cref="PositionPicker"/>): the
/// picture has the display's own shape, across is the stored position and down is the
/// vertical offset. While a drag is in flight the charm follows the pointer directly and
/// each move is reported, so the charm on the real screen follows too. Drawn with plain
/// shapes and the charm's cached thumbnail — nothing here animates, so there is nothing to
/// reduce under reduced motion, and nothing costs anything once the window is closed.
///
/// <para>A drag needs a pointer; the Horizontal and Vertical sliders beside it set the
/// same two values from the keyboard and for screen readers, as the macOS picker's
/// accessibility representation does.</para>
/// </remarks>
public sealed class DesktopPicker : Grid
{
    private readonly Border screen;
    private readonly Canvas stage = new();
    private readonly Line cord = new() { Stroke = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(128, 255, 255, 255)), StrokeThickness = 1.4, StrokeStartLineCap = PenLineCap.Round };
    private readonly Image charm = new() { Stretch = Stretch.Uniform };
    private readonly Ellipse fallback = new() { Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(230, 212, 175, 55)) };
    private double position;
    private double offsetY;
    private double areaHeight = 1;
    private double aspect = 16.0 / 10.0;
    private double ropeLength = 1;
    private double charmSize = 1;
    private bool dragging;

    /// <summary>The rope, shortened against the real proportion so the charm stays on the picture. The macOS scale.</summary>
    private const double RopeScale = 0.30;

    /// <summary>The charm's radius as a fraction of the picture's height. The macOS scale.</summary>
    private const double CharmScale = 0.055;

    public DesktopPicker()
    {
        MaxWidth = 460;
        HorizontalAlignment = HorizontalAlignment.Left;

        var desktop = new Grid();
        desktop.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 66, 66, 72), Offset = 0 },
                    new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 34), Offset = 1 },
                },
            },
        });

        // The taskbar, because a picture of a Windows screen without one is a picture of a rectangle.
        desktop.Children.Add(new Border
        {
            Height = 9,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255)),
        });
        stage.Children.Add(cord);
        stage.Children.Add(fallback);
        stage.Children.Add(charm);
        desktop.Children.Add(stage);

        screen = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255)),
            Child = desktop,
        };
        Children.Add(screen);

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, "Where the charm hangs. Drag it on this picture of your screen, or use the sliders below.");
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => dragging = false;
        SizeChanged += (_, _) => Layout();
    }

    /// <summary>Raised during a drag with the position across and the offset down, in points.</summary>
    public event Action<double, double>? Moved;

    /// <summary>Shows the charm where the settings put it, on a picture of a work area this shape.</summary>
    public void Show(double position, double offsetY, double areaWidth, double areaHeight, double ropeLength, double charmSize, string? thumbnail)
    {
        if (dragging)
        {
            return;
        }

        this.position = position;
        this.offsetY = offsetY;
        this.areaHeight = Math.Max(1, areaHeight);
        aspect = areaHeight > 0 ? Math.Max(0.5, areaWidth / areaHeight) : aspect;
        this.ropeLength = ropeLength;
        this.charmSize = charmSize;
        charm.Source = thumbnail is null ? null : new BitmapImage(new Uri(thumbnail));
        fallback.Visibility = thumbnail is null ? Visibility.Visible : Visibility.Collapsed;
        charm.Visibility = thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
        Layout();
    }

    private void Layout()
    {
        double width = Math.Min(ActualWidth > 0 ? ActualWidth : MaxWidth, MaxWidth);
        double height = width / aspect;
        screen.Width = width;
        screen.Height = height;

        (double x, double y) = PositionPicker.UnitPoint(position, offsetY, areaHeight);
        double pinX = x * width;
        double pinY = y * height;
        double drop = height * Hangly.Core.Physics.RopeConfiguration.Layout.LengthFraction * ropeLength * RopeScale;
        double radius = Math.Max(7, height * CharmScale * charmSize);

        cord.X1 = pinX;
        cord.Y1 = pinY;
        cord.X2 = pinX;
        cord.Y2 = pinY + drop;
        foreach (FrameworkElement face in new FrameworkElement[] { charm, fallback })
        {
            face.Width = radius * 2;
            face.Height = radius * 2;
            Canvas.SetLeft(face, pinX - radius);
            Canvas.SetTop(face, pinY + drop);
        }
    }

    private void OnPressed(object sender, PointerRoutedEventArgs args)
    {
        dragging = CapturePointer(args.Pointer);
        Follow(args);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs args)
    {
        if (dragging)
        {
            Follow(args);
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs args)
    {
        dragging = false;
        ReleasePointerCapture(args.Pointer);
    }

    private void Follow(PointerRoutedEventArgs args)
    {
        Windows.Foundation.Point at = args.GetCurrentPoint(screen).Position;
        if (screen.Width <= 0 || screen.Height <= 0)
        {
            return;
        }

        (position, offsetY) = PositionPicker.FromUnit(at.X / screen.Width, at.Y / screen.Height, areaHeight);
        Layout();
        Moved?.Invoke(position, offsetY);
        args.Handled = true;
    }
}
