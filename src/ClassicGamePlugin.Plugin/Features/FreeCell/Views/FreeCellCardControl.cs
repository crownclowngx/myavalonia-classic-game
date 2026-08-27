using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using ClassicGamePlugin.Features.FreeCell.Domain;

namespace ClassicGamePlugin.Features.FreeCell.Views;

/// <summary>
/// 空当接龙专用的单牌控件，只把牌状态转换为可读视觉与辅助名称。它不知道牌列、移动容量、
/// 基础区规则或拖拽目标，避免单牌控件成为第二个规则引擎。
/// </summary>
internal sealed class FreeCellCardControl : Control
{
    private static readonly Typeface Typeface = new("Segoe UI", FontStyle.Normal, FontWeight.Bold);
    private FreeCellCard _card;

    internal FreeCellCard Card
    {
        get => _card;
        set
        {
            _card = value;
            AutomationProperties.SetName(this, GetAccessibleName(value));
            ToolTip.SetTip(this, GetAccessibleName(value));
            InvalidateVisual();
        }
    }

    internal bool IsSelected { get; set; }
    internal bool IsHinted { get; set; }
    internal bool IsDragged { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var bounds = new Rect(Bounds.Size);
        var shadow = new SolidColorBrush(Color.Parse(dark ? "#66000000" : "#40000000"));
        var face = new SolidColorBrush(Color.Parse(dark ? "#FFF0F2F4" : "#FFFFFFFF"));
        var border = new SolidColorBrush(Color.Parse(
            IsDragged ? "#FF4EA1F3" : IsSelected ? "#FF2878C8" : IsHinted ? "#FFE5A620" : dark ? "#FF697584" : "#FF7D8998"));
        var text = new SolidColorBrush(Color.Parse(Card.IsRed ? "#FFC62832" : "#FF26313D"));
        context.DrawRectangle(shadow, null, bounds.Translate(new Vector(2, 3)), 7, 7);
        context.DrawRectangle(face, new Pen(border, IsSelected || IsHinted || IsDragged ? 3 : 1), bounds, 7, 7);

        var rank = RankText(Card.Rank);
        var suit = SuitText(Card.Suit);
        DrawText(context, rank, new Point(7, 4), 20, text);
        DrawText(context, suit, new Point(8, 29), 18, text);
        DrawText(context, suit, new Point(Math.Max(12, bounds.Width / 2 - 17), Math.Max(30, bounds.Height / 2 - 20)), 38, text);
    }

    internal static string GetAccessibleName(FreeCellCard card) =>
        $"{SuitName(card.Suit)}{RankName(card.Rank)}";

    private static void DrawText(DrawingContext context, string value, Point origin, double size, IBrush brush) =>
        context.DrawText(new FormattedText(
            value,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            Typeface,
            size,
            brush), origin);

    private static string RankText(int rank) => rank switch
    {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => rank.ToString(CultureInfo.InvariantCulture),
    };

    private static string SuitText(FreeCellSuit suit) => suit switch
    {
        FreeCellSuit.Spades => "♠",
        FreeCellSuit.Hearts => "♥",
        FreeCellSuit.Clubs => "♣",
        FreeCellSuit.Diamonds => "♦",
        _ => throw new InvalidOperationException("遇到了未知花色。"),
    };

    private static string SuitName(FreeCellSuit suit) => suit switch
    {
        FreeCellSuit.Spades => "黑桃",
        FreeCellSuit.Hearts => "红桃",
        FreeCellSuit.Clubs => "梅花",
        FreeCellSuit.Diamonds => "方块",
        _ => "未知花色",
    };

    private static string RankName(int rank) => rank switch
    {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => rank.ToString(CultureInfo.InvariantCulture),
    };
}
