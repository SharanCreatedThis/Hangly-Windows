//
//  NotificationCardPainter.cs
//  Hangly
//
//  The card and the bell, laid out and drawn with Win2D.
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
    Bell,
}

/// <summary>One card, measured: where everything goes and what each part does when pressed.</summary>
internal sealed class CardLayout : IDisposable
{
    public required Rect Bounds { get; init; }

    public required float Radius { get; init; }

    public List<(Rect Bounds, CardCommand Command)> Targets { get; } = [];

    public List<(CanvasTextLayout Text, Vector2 At, TextRole Role)> Texts { get; } = [];

    public List<(Rect Bounds, CardCommand Command, bool Primary)> Buttons { get; } = [];

    public List<Vector2> Bullets { get; } = [];

    public Rect? Badge { get; set; }

    public string BadgeGlyph { get; set; } = string.Empty;

    public Rect? Progress { get; set; }

    public double? ProgressFraction { get; set; }

    public bool IsBell { get; init; }

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
internal sealed record CardTheme(Color Fill, Color Sheen, Color Stroke, Color Primary, Color Secondary, Color Accent, Color Track)
{
    public static readonly CardTheme Dark = new(
        Color.FromArgb(232, 30, 31, 36),
        Color.FromArgb(22, 255, 255, 255),
        Color.FromArgb(40, 255, 255, 255),
        Color.FromArgb(255, 245, 245, 247),
        Color.FromArgb(160, 255, 255, 255),
        Color.FromArgb(255, 0x8F, 0x7E, 0xFF),
        Color.FromArgb(40, 255, 255, 255));

    public static readonly CardTheme Light = new(
        Color.FromArgb(236, 250, 250, 252),
        Color.FromArgb(140, 255, 255, 255),
        Color.FromArgb(26, 0, 0, 0),
        Color.FromArgb(255, 28, 28, 30),
        Color.FromArgb(140, 0, 0, 0),
        Color.FromArgb(255, 0x6D, 0x5A, 0xE5),
        Color.FromArgb(30, 0, 0, 0));
}

/// <summary>
/// The card's look, macOS's <c>NotificationCardView</c> drawn by hand: a 296-point card with 20-point corners and
/// 16 points of padding; the title semibold at 14, the body at 12.5; the accent pill for the main button.
/// </summary>
/// <remarks>
/// <b>The glass.</b> Windows cannot blur what is behind a window whose pixels have their own alpha, so the material
/// is a deep tint — 91% in either theme — with a sheen falling from the top edge, a hairline highlight and two soft
/// shadows, near and far. The glyphs are Segoe MDL2 Assets, which every Windows 10 and 11 has.
/// </remarks>
internal static class NotificationCardPainter
{
    public const float Width = 296;
    public const float Padding = 16;
    private const string Font = "Segoe UI Variable Text";
    private const string Display = "Segoe UI Variable Display";
    private const string Glyphs = "Segoe MDL2 Assets";

    public static CardLayout Layout(ICanvasResourceCreator device, CardPresentation? card, int bellCount)
    {
        return card is null ? Bell(device, bellCount) : card.IsUpdate ? Update(device, card) : Broadcast(device, card.Broadcast!);
    }

    private static CardLayout Bell(ICanvasResourceCreator device, int count)
    {
        CanvasTextLayout glyph = Text(device, "", Glyphs, 11.5f, bold: false, 40);
        CanvasTextLayout number = Text(device, count.ToString(System.Globalization.CultureInfo.CurrentCulture), Display, 12, bold: true, 60);
        float width = 10 + (float)glyph.LayoutBounds.Width + 6 + (float)number.LayoutBounds.Width + 10;
        var layout = new CardLayout { Bounds = new Rect(0, 0, width, 26), Radius = 13, IsBell = true };
        layout.Texts.Add((glyph, new Vector2(10, 13 - ((float)glyph.LayoutBounds.Height / 2)), TextRole.Glyph));
        layout.Texts.Add((number, new Vector2(10 + (float)glyph.LayoutBounds.Width + 6, 13 - ((float)number.LayoutBounds.Height / 2)), TextRole.Title));
        layout.Targets.Add((layout.Bounds, CardCommand.Bell));
        return layout;
    }

    private static CardLayout Broadcast(ICanvasResourceCreator device, Announcement announcement)
    {
        float inner = Width - (2 * Padding);
        CanvasTextLayout title = Text(device, announcement.Title, Display, 14, bold: true, inner - 18);
        CanvasTextLayout message = Text(device, announcement.Message, Font, 12.5f, bold: false, inner);
        float y = Padding;
        var texts = new List<(CanvasTextLayout, Vector2, TextRole)> { (title, new Vector2(Padding, y), TextRole.Title) };
        y += (float)title.LayoutBounds.Height + 3;
        texts.Add((message, new Vector2(Padding, y), TextRole.Secondary));
        y += (float)message.LayoutBounds.Height;

        var buttons = new List<(Rect, CardCommand, bool)>();
        if (announcement.ButtonTitle is { } label)
        {
            y += 10;
            CanvasTextLayout text = Text(device, label, Font, 12.5f, bold: true, inner);
            float width = (float)text.LayoutBounds.Width + 28;
            var button = new Rect(Padding, y, width, 30);
            texts.Add((text, Centre(button, text), TextRole.ButtonPrimary));
            buttons.Add((button, CardCommand.Open, true));
            y += 30;
        }

        var layout = new CardLayout { Bounds = new Rect(0, 0, Width, y + Padding), Radius = 20 };
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
        var layout = new CardLayout { Bounds = default, Radius = 20 };
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
                y = PrimaryButton(device, "Update Now", CardCommand.UpdateNow, y, inner, texts, buttons);
                y += 8;
                CanvasTextLayout later = Text(device, "Later", Font, 11.5f, bold: true, inner);
                CanvasTextLayout dot = Text(device, "·", Font, 11.5f, bold: false, inner);
                CanvasTextLayout skip = Text(device, "Skip This Version", Font, 11.5f, bold: true, inner);
                float total = (float)(later.LayoutBounds.Width + 14 + dot.LayoutBounds.Width + 14 + skip.LayoutBounds.Width);
                float x = Padding + ((inner - total) / 2);
                float height = (float)later.LayoutBounds.Height;
                buttons.Add((new Rect(x - 4, y - 3, later.LayoutBounds.Width + 8, height + 6), CardCommand.Later, false));
                texts.Add((later, new Vector2(x, y), TextRole.ButtonQuiet));
                x += (float)later.LayoutBounds.Width + 14;
                texts.Add((dot, new Vector2(x, y), TextRole.Secondary));
                x += (float)dot.LayoutBounds.Width + 14;
                buttons.Add((new Rect(x - 4, y - 3, skip.LayoutBounds.Width + 8, height + 6), CardCommand.Skip, false));
                texts.Add((skip, new Vector2(x, y), TextRole.ButtonQuiet));
                y += height;
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

        var measured = new CardLayout { Bounds = new Rect(0, 0, Width, y + Padding), Radius = 20 };
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
        layout.Targets.Insert(0, (CloseBounds(layout), CardCommand.Close));
    }

    public static Rect CloseBounds(CardLayout layout) => new(layout.Bounds.Width - 28, 8, 20, 20);

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
        DrawShadow(session, width, height, radius, 18, 10, 0.20f);
        DrawShadow(session, width, height, radius, 1.5f, 1, 0.10f);

        session.FillRoundedRectangle(0, 0, width, height, radius, radius, theme.Fill);
        using (var sheen = new CanvasLinearGradientBrush(session, theme.Sheen, Color.FromArgb(0, theme.Sheen.R, theme.Sheen.G, theme.Sheen.B))
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(0, Math.Min(height, 70)),
        })
        {
            session.FillRoundedRectangle(0, 0, width, height, radius, radius, sheen);
        }

        session.DrawRoundedRectangle(0.5f, 0.5f, width - 1, height - 1, radius - 0.5f, radius - 0.5f, theme.Stroke, 1);

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

        if (layout.IsBell)
        {
            session.FillCircle(new Vector2(width - 3, 3), 3.5f, theme.Accent);
        }
        else if (cardHovered)
        {
            Rect close = CloseBounds(layout);
            var centre = new Vector2((float)(close.X + 10), (float)(close.Y + 10));
            session.FillCircle(centre, 10, hovered == CardCommand.Close ? theme.Track : Color.FromArgb(20, theme.Primary.R, theme.Primary.G, theme.Primary.B));
            session.DrawCircle(centre, 9.5f, theme.Stroke, 1);
            using CanvasTextLayout cross = new(session, "", new CanvasTextFormat { FontFamily = Glyphs, FontSize = 8 }, 20, 20);
            session.DrawTextLayout(cross, centre.X - (float)(cross.LayoutBounds.Width / 2) - (float)cross.LayoutBounds.X, centre.Y - (float)(cross.LayoutBounds.Height / 2) - (float)cross.LayoutBounds.Y, theme.Secondary);
        }
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
