//
//  NotificationCardPainter.cs
//  Hangly
//
//  The pop-up, laid out and drawn with Win2D.
//

using System.Numerics;
using Hangly.Core.Notifications;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace Hangly.App.Notifications;

/// <summary>What a press on the card asks for.</summary>
public enum CardCommand
{
    UpdateNow,
    Later,
    Skip,
    Close,
    Open,
}

/// <summary>One card, measured: where everything goes and what each part does when pressed.</summary>
internal sealed class CardLayout : IDisposable
{
    public required Rect Bounds { get; init; }

    /// <summary>Whether the card has its ×: a broadcast can be sent away; the update card cannot (Sharan, 4 Oct).</summary>
    public bool Closable { get; init; } = true;

    public required float Radius { get; init; }

    public List<(Rect Bounds, CardCommand Command)> Targets { get; } = [];

    public List<(CanvasTextLayout Text, Vector2 At, TextRole Role)> Texts { get; } = [];

    public List<(Rect Bounds, CardCommand Command, bool Primary)> Buttons { get; } = [];

    public List<Vector2> Bullets { get; } = [];

    public Rect? Badge { get; set; }

    public string BadgeGlyph { get; set; } = string.Empty;

    public Rect? Progress { get; set; }

    public double? ProgressFraction { get; set; }

    /// <summary>Where Hangly's icon goes, on a broadcast: whose pop-up it is, as a system banner shows.</summary>
    public Rect? AppIcon { get; set; }

    public void Dispose()
    {
        foreach ((CanvasTextLayout text, _, _) in Texts)
        {
            text.Dispose();
        }
    }
}

internal enum TextRole
{
    Title,
    Body,
    Secondary,
    ButtonPrimary,
    ButtonQuiet,
    Glyph,
    GlyphOnAccent,
}

/// <summary>Light or dark, as Windows' own app theme is set.</summary>
/// <remarks>
/// <b>The fill is the glass's body:</b> 84% in dark, 87% in light — as translucent as it can be and still keep the words
/// clear on any wallpaper or page behind it. Over white, the dark card's fill comes out a mid grey with white text;
/// over black, the light card's comes out a pale grey with near-black text: both well past 7:1. The rim and the sheen do
/// the rest of what glass looks like: light caught along the top edge, and falling away down the card.
/// </remarks>
internal sealed record CardTheme(
    Color Fill, Color FillLow, Color Sheen, Color RimTop, Color RimBottom, Color Primary, Color Secondary, Color Accent, Color Track)
{
    public static readonly CardTheme Dark = new(
        Color.FromArgb(214, 36, 37, 44),
        Color.FromArgb(222, 26, 27, 33),
        Color.FromArgb(34, 255, 255, 255),
        Color.FromArgb(96, 255, 255, 255),
        Color.FromArgb(22, 255, 255, 255),
        Color.FromArgb(255, 246, 246, 248),
        Color.FromArgb(172, 255, 255, 255),
        Color.FromArgb(255, 0x8F, 0x7E, 0xFF),
        Color.FromArgb(40, 255, 255, 255));

    public static readonly CardTheme Light = new(
        Color.FromArgb(222, 252, 252, 254),
        Color.FromArgb(230, 242, 242, 247),
        Color.FromArgb(150, 255, 255, 255),
        Color.FromArgb(230, 255, 255, 255),
        Color.FromArgb(30, 0, 0, 0),
        Color.FromArgb(255, 24, 24, 27),
        Color.FromArgb(150, 0, 0, 0),
        Color.FromArgb(255, 0x6D, 0x5A, 0xE5),
        Color.FromArgb(30, 0, 0, 0));
}

/// <summary>
/// The card's look, macOS's <c>NotificationCardView</c> drawn by hand: a 296-point card with 22-point corners; the
/// title semibold at 13.5, the body at 12.5; Hangly's icon beside a broadcast; the accent pill for the main button.
/// </summary>
/// <remarks>
/// <b>The glass.</b> Windows cannot blur what is behind a window whose pixels have their own alpha, and its acrylic
/// backdrop turns solid on a window that is not active — which this one never is (tried on Windows 11, 3 Oct 2026).
/// So the glass is drawn: a translucent body (<see cref="CardTheme"/>), a sheen falling from the top edge, a rim bright
/// at the top and faint at the bottom, and two soft shadows. The glyphs are Segoe MDL2 Assets, which every Windows 10
/// and 11 has.
/// </remarks>
internal static class NotificationCardPainter
{
    public const float Width = 296;
    public const float Padding = 16;
    private const string Font = "Segoe UI Variable Text";
    private const string Display = "Segoe UI Variable Display";
    private const string Glyphs = "Segoe MDL2 Assets";

    public const float Radius = 22;
    private const float IconSide = 30;
    private const float IconGap = 11;

    public static CardLayout Layout(ICanvasResourceCreator device, CardPresentation card) =>
        card.IsUpdate ? Update(device, card) : Broadcast(device, card.Broadcast!);

    private static CardLayout Broadcast(ICanvasResourceCreator device, Announcement announcement)
    {
        const float top = 15;
        float left = Padding + IconSide + IconGap;
        float inner = Width - left - Padding;
        CanvasTextLayout title = Text(device, announcement.Title, Display, 13.5f, bold: true, inner - 20);
        CanvasTextLayout message = Text(device, announcement.Message, Font, 12.5f, bold: false, inner);
        float y = top;
        var texts = new List<(CanvasTextLayout, Vector2, TextRole)> { (title, new Vector2(left, y), TextRole.Title) };
        y += (float)title.LayoutBounds.Height + 2;
        texts.Add((message, new Vector2(left, y), TextRole.Secondary));
        y += (float)message.LayoutBounds.Height;

        var buttons = new List<(Rect, CardCommand, bool)>();
        if (announcement.ButtonTitle is { } label && NotificationAction.From(announcement.ActionType, announcement.ActionTarget) is not null)
        {
            y += 10;
            CanvasTextLayout text = Text(device, label, Font, 12.5f, bold: true, inner);
            float width = Math.Min((float)text.LayoutBounds.Width + 28, inner);
            var button = new Rect(left, y, width, 30);
            texts.Add((text, Centre(button, text), TextRole.ButtonPrimary));
            buttons.Add((button, CardCommand.Open, true));
            y += 30;
        }

        var layout = new CardLayout
        {
            Bounds = new Rect(0, 0, Width, Math.Max(y, top + IconSide) + top),
            Radius = Radius,
            AppIcon = new Rect(Padding, top, IconSide, IconSide),
        };
        layout.Texts.AddRange(texts);
        Finish(layout, buttons);
        return layout;
    }

    private static CardLayout Update(ICanvasResourceCreator device, CardPresentation card)
    {
        float inner = Width - (2 * Padding);
        string version = NotificationStore.ShortVersion(card.Version ?? string.Empty);
        CanvasTextLayout title = Text(device, $"Hangly {version} Available", Display, 14, bold: true, inner - 58);
        CanvasTextLayout subtitle = Text(
            device, card.Highlights.Count == 0 ? "New charms, fixes and refinements." : "What's new", Font, 11.5f, bold: false, inner - 58);
        var layout = new CardLayout { Bounds = default, Radius = Radius };
        var texts = new List<(CanvasTextLayout, Vector2, TextRole)>();
        var buttons = new List<(Rect, CardCommand, bool)>();

        float y = Padding;
        float headerHeight = Math.Max(30, (float)(title.LayoutBounds.Height + 1 + subtitle.LayoutBounds.Height));
        layout.Badge = new Rect(Padding, y + ((headerHeight - 30) / 2), 30, 30);
        layout.BadgeGlyph = "";
        float textTop = y + ((headerHeight - (float)(title.LayoutBounds.Height + 1 + subtitle.LayoutBounds.Height)) / 2);
        texts.Add((title, new Vector2(Padding + 40, textTop), TextRole.Title));
        texts.Add((subtitle, new Vector2(Padding + 40, textTop + (float)title.LayoutBounds.Height + 1), TextRole.Secondary));
        y += headerHeight + 12;

        foreach (string line in card.Highlights)
        {
            CanvasTextLayout bullet = Text(device, line, Font, 12.5f, bold: false, inner - 14);
            layout.Bullets.Add(new Vector2(Padding + 4, y + ((float)bullet.LayoutBounds.Height / 2)));
            texts.Add((bullet, new Vector2(Padding + 14, y), TextRole.Body));
            y += (float)bullet.LayoutBounds.Height + 5;
        }

        if (card.Highlights.Count > 0)
        {
            y += 7;
        }

        switch (card.Stage)
        {
            case UpdateNowStage.Idle:
                // One button (Sharan, 4 Oct): no Later, no Skip This Version.
                y = PrimaryButton(device, "Update in Background", CardCommand.UpdateNow, y, inner, texts, buttons);
                break;
            case UpdateNowStage.Failed:
                CanvasTextLayout failed = Text(device, "Couldn't update. Hangly is unchanged.", Font, 12, bold: false, inner);
                texts.Add((failed, new Vector2(Padding, y), TextRole.Secondary));
                y += (float)failed.LayoutBounds.Height + 8;
                y = PrimaryButton(device, "Try Again", CardCommand.UpdateNow, y, inner, texts, buttons);
                break;
            default:
                string label = card.Stage switch
                {
                    UpdateNowStage.Downloading => card.Percent is int percent ? $"Downloading {percent}%" : "Downloading…",
                    UpdateNowStage.Installing => "Installing…",
                    _ => "Restarting Hangly…",
                };
                CanvasTextLayout status = Text(device, label, Font, 12, bold: true, inner);
                texts.Add((status, new Vector2(Padding, y), TextRole.Secondary));
                y += (float)status.LayoutBounds.Height + 7;
                layout.Progress = new Rect(Padding, y, inner, 4);
                layout.ProgressFraction = card.Stage == UpdateNowStage.Downloading && card.Percent is int done ? done / 100.0 : null;
                y += 4;
                break;
        }

        var measured = new CardLayout { Bounds = new Rect(0, 0, Width, y + Padding), Radius = Radius, Closable = false };
        measured.Badge = layout.Badge;
        measured.BadgeGlyph = layout.BadgeGlyph;
        measured.Progress = layout.Progress;
        measured.ProgressFraction = layout.ProgressFraction;
        measured.Bullets.AddRange(layout.Bullets);
        measured.Texts.AddRange(texts);
        Finish(measured, buttons);
        return measured;
    }

    private static float PrimaryButton(
        ICanvasResourceCreator device,
        string label,
        CardCommand command,
        float y,
        float inner,
        List<(CanvasTextLayout, Vector2, TextRole)> texts,
        List<(Rect, CardCommand, bool)> buttons)
    {
        CanvasTextLayout text = Text(device, label, Font, 12.5f, bold: true, inner);
        var button = new Rect(Padding, y, inner, 30);
        texts.Add((text, Centre(button, text), TextRole.ButtonPrimary));
        buttons.Add((button, command, true));
        return y + 30;
    }

    /// <summary>The × in the corner, and every button, become things that can be pressed.</summary>
    private static void Finish(CardLayout layout, List<(Rect Bounds, CardCommand Command, bool Primary)> buttons)
    {
        layout.Buttons.AddRange(buttons);
        layout.Targets.AddRange(buttons.Select(button => (button.Bounds, button.Command)));
        if (layout.Closable)
        {
            layout.Targets.Insert(0, (CloseBounds(layout), CardCommand.Close));
        }
    }

    public static Rect CloseBounds(CardLayout layout) => new(layout.Bounds.Width - 29, 9, 20, 20);

    private static Vector2 Centre(Rect box, CanvasTextLayout text) => new(
        (float)(box.X + ((box.Width - text.LayoutBounds.Width) / 2) - text.LayoutBounds.X),
        (float)(box.Y + ((box.Height - text.LayoutBounds.Height) / 2) - text.LayoutBounds.Y));

    private static CanvasTextLayout Text(ICanvasResourceCreator device, string text, string font, float size, bool bold, float width)
    {
        using var format = new CanvasTextFormat
        {
            FontFamily = font,
            FontSize = size,
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            WordWrapping = CanvasWordWrapping.Wrap,
            Options = CanvasDrawTextOptions.EnableColorFont,
        };
        // Colour emoji ("⚽ Football Pack") is a property of the layout, not of its format.
        return new CanvasTextLayout(device, text, format, width, 400) { Options = CanvasDrawTextOptions.EnableColorFont };
    }

    // Drawing

    /// <summary>Draws <paramref name="layout"/> with its top-left at the origin of <paramref name="session"/>.</summary>
    public static void Draw(
        CanvasDrawingSession session,
        CardLayout layout,
        CardTheme theme,
        CardCommand? hovered,
        CardCommand? pressed,
        bool cardHovered,
        double time)
    {
        float width = (float)layout.Bounds.Width, height = (float)layout.Bounds.Height, radius = layout.Radius;
        // Soft and low: enough to lift the card off the desktop, never enough to read as a box.
        DrawShadow(session, width, height, radius, 14, 7, 0.14f);
        DrawShadow(session, width, height, radius, 1, 0.5f, 0.08f);

        // The body, a touch denser towards the bottom, as thick glass looks.
        using (var body = new CanvasLinearGradientBrush(session, theme.Fill, theme.FillLow)
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(0, height),
        })
        {
            session.FillRoundedRectangle(0, 0, width, height, radius, radius, body);
        }

        using (var sheen = new CanvasLinearGradientBrush(session, theme.Sheen, Color.FromArgb(0, theme.Sheen.R, theme.Sheen.G, theme.Sheen.B))
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(0, Math.Min(height, 56)),
        })
        {
            session.FillRoundedRectangle(0, 0, width, height, radius, radius, sheen);
        }

        // The rim: light caught along the top edge, fading down the sides.
        using (var rim = new CanvasLinearGradientBrush(session, theme.RimTop, theme.RimBottom)
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(0, height),
        })
        {
            session.DrawRoundedRectangle(0.5f, 0.5f, width - 1, height - 1, radius - 0.5f, radius - 0.5f, rim, 1);
        }

        if (layout.AppIcon is Rect iconBounds)
        {
            using CanvasBitmap? icon = AppIconImage.Create(session);
            if (icon is not null)
            {
                session.DrawImage(icon, iconBounds, icon.Bounds, 1, CanvasImageInterpolation.HighQualityCubic);
            }
        }

        if (layout.Badge is Rect badge)
        {
            var centre = new Vector2((float)(badge.X + 15), (float)(badge.Y + 15));
            using var fill = new CanvasLinearGradientBrush(session, theme.Accent, Color.FromArgb(200, theme.Accent.R, theme.Accent.G, theme.Accent.B))
            {
                StartPoint = new Vector2(centre.X, (float)badge.Y),
                EndPoint = new Vector2(centre.X, (float)badge.Bottom),
            };
            session.FillCircle(centre, 15, fill);
            session.DrawCircle(centre, 14.5f, Color.FromArgb(64, 255, 255, 255), 1);
            using CanvasTextLayout glyph = new(session, layout.BadgeGlyph, new CanvasTextFormat { FontFamily = Glyphs, FontSize = 13 }, 30, 30);
            session.DrawTextLayout(glyph, centre.X - (float)(glyph.LayoutBounds.Width / 2) - (float)glyph.LayoutBounds.X, centre.Y - (float)(glyph.LayoutBounds.Height / 2) - (float)glyph.LayoutBounds.Y, Colors.White);
        }

        foreach (Vector2 bullet in layout.Bullets)
        {
            session.FillCircle(bullet, 2, theme.Accent);
        }

        foreach ((Rect bounds, CardCommand command, bool primary) in layout.Buttons)
        {
            if (!primary)
            {
                continue;
            }

            float r = (float)bounds.Height / 2;
            byte alpha = pressed == command ? (byte)205 : hovered == command ? (byte)255 : (byte)235;
            session.FillRoundedRectangle(bounds, r, r, Color.FromArgb(alpha, theme.Accent.R, theme.Accent.G, theme.Accent.B));
            session.DrawRoundedRectangle(bounds, r, r, Color.FromArgb(52, 255, 255, 255), 1);
        }

        foreach ((CanvasTextLayout text, Vector2 at, TextRole role) in layout.Texts)
        {
            Color colour = role switch
            {
                TextRole.Secondary => theme.Secondary,
                TextRole.ButtonPrimary => Colors.White,
                TextRole.ButtonQuiet => theme.Secondary,
                TextRole.Body => Color.FromArgb(224, theme.Primary.R, theme.Primary.G, theme.Primary.B),
                _ => theme.Primary,
            };
            session.DrawTextLayout(text, at, colour);
        }

        if (layout.Progress is Rect track)
        {
            session.FillRoundedRectangle(track, 2, 2, theme.Track);
            double fraction = layout.ProgressFraction ?? 0.3;
            double start = layout.ProgressFraction is null ? ((time % 1.4) / 1.4 * 1.3) - 0.3 : 0;
            double left = Math.Max(0, start), right = Math.Min(1, start + fraction);
            if (right > left)
            {
                session.FillRoundedRectangle(
                    new Rect(track.X + (track.Width * left), track.Y, track.Width * (right - left), track.Height), 2, 2, theme.Accent);
            }
        }

        // The ×, on a broadcast: always there, quietly — a pop-up that leaves on its own must also be easy to send away —
        // and clearer while the pointer is on the card. The update card has none.
        if (!layout.Closable)
        {
            return;
        }

        Rect close = CloseBounds(layout);
        var closeCentre = new Vector2((float)(close.X + 10), (float)(close.Y + 10));
        byte closeFill = hovered == CardCommand.Close ? (byte)46 : cardHovered ? (byte)26 : (byte)15;
        session.FillCircle(closeCentre, 10, Color.FromArgb(closeFill, theme.Primary.R, theme.Primary.G, theme.Primary.B));
        using CanvasTextLayout cross = new(session, "\uE8BB", new CanvasTextFormat { FontFamily = Glyphs, FontSize = 8 }, 20, 20);
        Color crossColour = cardHovered ? theme.Primary : theme.Secondary;
        session.DrawTextLayout(
            cross,
            closeCentre.X - (float)(cross.LayoutBounds.Width / 2) - (float)cross.LayoutBounds.X,
            closeCentre.Y - (float)(cross.LayoutBounds.Height / 2) - (float)cross.LayoutBounds.Y,
            crossColour);
    }

    private static void DrawShadow(CanvasDrawingSession session, float width, float height, float radius, float blur, float offset, float opacity)
    {
        using var shape = new CanvasCommandList(session);
        using (CanvasDrawingSession inner = shape.CreateDrawingSession())
        {
            inner.FillRoundedRectangle(0, 0, width, height, radius, radius, Color.FromArgb((byte)(opacity * 255), 0, 0, 0));
        }

        using var effect = new GaussianBlurEffect { Source = shape, BlurAmount = blur / 2, BorderMode = EffectBorderMode.Soft };
        session.DrawImage(effect, 0, offset);
    }

    private static class Colors
    {
        public static readonly Color White = Color.FromArgb(255, 255, 255, 255);
    }
}
