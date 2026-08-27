using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.Xiangqi.Domain;
using ClassicGamePlugin.Features.Xiangqi.ViewModels;

namespace ClassicGamePlugin.Features.Xiangqi.Views;

/// <summary>
/// 绘制 9×10 中国象棋交叉点棋盘并在正向/翻转视图之间换算坐标。控件只读取 ViewModel 投影并转发点击，
/// 不判断棋子走法、将军或终局，避免 View 与领域规则产生两套真相。
/// </summary>
public sealed class XiangqiBoardControl : Control
{
    private const double BoardPadding = 38;
    private static readonly Typeface BoardTypeface =
        new("Microsoft YaHei UI", FontStyle.Normal, FontWeight.Bold);
    private static readonly Typeface SmallTypeface =
        new("Microsoft YaHei UI", FontStyle.Normal, FontWeight.SemiBold);
    private XiangqiViewModel? _subscribedGame;
    private XiangqiPosition? _hoverPosition;

    public static readonly StyledProperty<XiangqiViewModel?> GameProperty =
        AvaloniaProperty.Register<XiangqiBoardControl, XiangqiViewModel?>(nameof(Game));

    public XiangqiBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public XiangqiViewModel? Game
    {
        get => GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty)
        {
            SubscribeToGame(change.GetNewValue<XiangqiViewModel?>());
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var layout = GetLayout(Bounds.Size);
        var boardBrush = new SolidColorBrush(Color.Parse("#FFE2B86F"));
        var borderBrush = new SolidColorBrush(Color.Parse("#FF6C3F1D"));
        var gridBrush = new SolidColorBrush(Color.Parse("#FF573419"));
        context.DrawRectangle(boardBrush, new Pen(borderBrush, 3), layout.Board, 5, 5);
        DrawGrid(context, layout, gridBrush);
        DrawCoordinates(context, layout, gridBrush, Game?.IsBoardFlipped ?? false);

        if (Game is null)
        {
            return;
        }

        var snapshot = Game.CurrentSnapshot;
        DrawMoveHighlights(context, layout, Game, snapshot);
        for (var row = 0; row < XiangqiRules.RowCount; row++)
        {
            for (var column = 0; column < XiangqiRules.ColumnCount; column++)
            {
                var position = new XiangqiPosition(row, column);
                if (snapshot.GetPiece(position) is { } piece)
                {
                    DrawPiece(context, layout, position, piece, Game.IsBoardFlipped,
                        IsCheckedGeneral(snapshot, position, piece));
                }
            }
        }

        if (_hoverPosition is { } hover && Game.CanInteract)
        {
            context.DrawEllipse(
                new SolidColorBrush(Color.Parse("#33FFFFFF")),
                new Pen(new SolidColorBrush(Color.Parse("#AAFFFFFF")), 1.5),
                CenterOf(layout, hover, Game.IsBoardFlipped),
                layout.Spacing * 0.43,
                layout.Spacing * 0.43);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        var flipped = Game?.IsBoardFlipped ?? false;
        var next = TryHitTest(Bounds.Size, eventArgs.GetPosition(this), flipped, out var position)
            ? position
            : (XiangqiPosition?)null;
        if (_hoverPosition == next)
        {
            return;
        }

        _hoverPosition = next;
        ToolTip.SetTip(this, next is { } hover && Game is not null ? Game.DescribePosition(hover) : null);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs eventArgs)
    {
        base.OnPointerExited(eventArgs);
        _hoverPosition = null;
        ToolTip.SetTip(this, null);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (Game is null || !Game.CanInteract ||
            !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            !TryHitTest(Bounds.Size, eventArgs.GetPosition(this), Game.IsBoardFlipped, out var position))
        {
            return;
        }

        Focus();
        Game.PlayPosition(position);
        eventArgs.Handled = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        SubscribeToGame(null);
        base.OnDetachedFromVisualTree(eventArgs);
    }

    /// <summary>测试可直接验证正向、翻转、舍入和边界拒绝，无需构造平台原生指针事件。</summary>
    internal static bool TryHitTest(
        Size bounds,
        Point point,
        bool flipped,
        out XiangqiPosition position)
    {
        var layout = GetLayout(bounds);
        var visualColumn = (int)Math.Round((point.X - layout.Board.X - BoardPadding) / layout.Spacing);
        var visualRow = (int)Math.Round((point.Y - layout.Board.Y - BoardPadding) / layout.Spacing);
        var row = flipped ? XiangqiRules.RowCount - 1 - visualRow : visualRow;
        var column = flipped ? XiangqiRules.ColumnCount - 1 - visualColumn : visualColumn;
        position = new XiangqiPosition(row, column);
        if (!XiangqiRules.IsInside(position))
        {
            return false;
        }

        var center = CenterOf(layout, position, flipped);
        return Math.Abs(point.X - center.X) <= layout.Spacing * 0.45 &&
            Math.Abs(point.Y - center.Y) <= layout.Spacing * 0.45;
    }

    private void SubscribeToGame(XiangqiViewModel? game)
    {
        if (_subscribedGame is not null)
        {
            _subscribedGame.PropertyChanged -= OnGamePropertyChanged;
        }

        _subscribedGame = game;
        if (_subscribedGame is not null)
        {
            _subscribedGame.PropertyChanged += OnGamePropertyChanged;
        }
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => InvalidateVisual();

    private static void DrawGrid(DrawingContext context, BoardLayout layout, IBrush brush)
    {
        var pen = new Pen(brush, 1.2);
        for (var row = 0; row < XiangqiRules.RowCount; row++)
        {
            context.DrawLine(
                pen,
                GridPoint(layout, row, 0),
                GridPoint(layout, row, XiangqiRules.ColumnCount - 1));
        }

        for (var column = 0; column < XiangqiRules.ColumnCount; column++)
        {
            if (column is 0 or XiangqiRules.ColumnCount - 1)
            {
                context.DrawLine(pen, GridPoint(layout, 0, column), GridPoint(layout, 9, column));
            }
            else
            {
                context.DrawLine(pen, GridPoint(layout, 0, column), GridPoint(layout, 4, column));
                context.DrawLine(pen, GridPoint(layout, 5, column), GridPoint(layout, 9, column));
            }
        }

        context.DrawLine(pen, GridPoint(layout, 0, 3), GridPoint(layout, 2, 5));
        context.DrawLine(pen, GridPoint(layout, 0, 5), GridPoint(layout, 2, 3));
        context.DrawLine(pen, GridPoint(layout, 7, 3), GridPoint(layout, 9, 5));
        context.DrawLine(pen, GridPoint(layout, 7, 5), GridPoint(layout, 9, 3));
        DrawCenteredText(context, "楚 河", Midpoint(GridPoint(layout, 4, 1), GridPoint(layout, 5, 3)),
            SmallTypeface, layout.Spacing * 0.33, brush);
        DrawCenteredText(context, "汉 界", Midpoint(GridPoint(layout, 4, 5), GridPoint(layout, 5, 7)),
            SmallTypeface, layout.Spacing * 0.33, brush);
    }

    private static void DrawCoordinates(
        DrawingContext context,
        BoardLayout layout,
        IBrush brush,
        bool flipped)
    {
        for (var visualColumn = 0; visualColumn < XiangqiRules.ColumnCount; visualColumn++)
        {
            var domainColumn = flipped
                ? XiangqiRules.ColumnCount - 1 - visualColumn
                : visualColumn;
            var redNumber = domainColumn + 1;
            var blackNumber = XiangqiRules.ColumnCount - domainColumn;
            var topText = flipped ? ToChinese(redNumber) : blackNumber.ToString(CultureInfo.InvariantCulture);
            var bottomText = flipped ? blackNumber.ToString(CultureInfo.InvariantCulture) : ToChinese(redNumber);
            var x = layout.Board.X + BoardPadding + (visualColumn * layout.Spacing);
            DrawCenteredText(context, topText, new Point(x, layout.Board.Y + 15), SmallTypeface, 11, brush);
            DrawCenteredText(context, bottomText, new Point(x, layout.Board.Bottom - 15), SmallTypeface, 11, brush);
        }
    }

    private static void DrawMoveHighlights(
        DrawingContext context,
        BoardLayout layout,
        XiangqiViewModel game,
        XiangqiGameSnapshot snapshot)
    {
        if (snapshot.LastMove is { } last)
        {
            var pen = new Pen(new SolidColorBrush(Color.Parse("#FF1E88E5")), 3);
            context.DrawEllipse(null, pen, CenterOf(layout, last.From, game.IsBoardFlipped),
                layout.Spacing * 0.45, layout.Spacing * 0.45);
            context.DrawEllipse(null, pen, CenterOf(layout, last.To, game.IsBoardFlipped),
                layout.Spacing * 0.45, layout.Spacing * 0.45);
        }

        if (game.SelectedPosition is { } selected)
        {
            context.DrawEllipse(
                new SolidColorBrush(Color.Parse("#33FFF176")),
                new Pen(new SolidColorBrush(Color.Parse("#FFFFC107")), 4),
                CenterOf(layout, selected, game.IsBoardFlipped),
                layout.Spacing * 0.48,
                layout.Spacing * 0.48);
        }

        foreach (var target in game.LegalTargets)
        {
            var occupied = snapshot.GetPiece(target) is not null;
            context.DrawEllipse(
                occupied ? null : new SolidColorBrush(Color.Parse("#BB2E7D32")),
                occupied ? new Pen(new SolidColorBrush(Color.Parse("#FFD84343")), 4) : null,
                CenterOf(layout, target, game.IsBoardFlipped),
                occupied ? layout.Spacing * 0.47 : layout.Spacing * 0.11,
                occupied ? layout.Spacing * 0.47 : layout.Spacing * 0.11);
        }

        if (game.HintMove is { } hint)
        {
            var pen = new Pen(new SolidColorBrush(Color.Parse("#FFFFD54F")), 5, lineCap: PenLineCap.Round);
            var start = CenterOf(layout, hint.From, game.IsBoardFlipped);
            var end = CenterOf(layout, hint.To, game.IsBoardFlipped);
            context.DrawLine(pen, start, end);
            context.DrawEllipse(null, pen, start, layout.Spacing * 0.48, layout.Spacing * 0.48);
            context.DrawEllipse(null, pen, end, layout.Spacing * 0.48, layout.Spacing * 0.48);
        }
    }

    private static void DrawPiece(
        DrawingContext context,
        BoardLayout layout,
        XiangqiPosition position,
        XiangqiPiece piece,
        bool flipped,
        bool checkedGeneral)
    {
        var center = CenterOf(layout, position, flipped);
        var radius = layout.Spacing * 0.43;
        var fill = new SolidColorBrush(Color.Parse("#FFFFF4D8"));
        var color = piece.Side == XiangqiSide.Red ? "#FFC62828" : "#FF202124";
        var stroke = new SolidColorBrush(Color.Parse(color));
        if (checkedGeneral)
        {
            context.DrawEllipse(
                new SolidColorBrush(Color.Parse("#55FF1744")),
                new Pen(new SolidColorBrush(Color.Parse("#FFFF1744")), 5),
                center,
                radius * 1.13,
                radius * 1.13);
        }

        context.DrawEllipse(fill, new Pen(stroke, 2.2), center, radius, radius);
        context.DrawEllipse(null, new Pen(stroke, 1), center, radius * 0.82, radius * 0.82);
        DrawCenteredText(context, piece.DisplayName, center, BoardTypeface, layout.Spacing * 0.46, stroke);
    }

    private static bool IsCheckedGeneral(
        XiangqiGameSnapshot snapshot,
        XiangqiPosition position,
        XiangqiPiece piece) =>
        piece.Type == XiangqiPieceType.General && XiangqiRules.IsInCheck(snapshot, piece.Side);

    private static BoardLayout GetLayout(Size bounds)
    {
        var spacing = Math.Max(28, Math.Min(
            (bounds.Width - (BoardPadding * 2)) / (XiangqiRules.ColumnCount - 1),
            (bounds.Height - (BoardPadding * 2)) / (XiangqiRules.RowCount - 1)));
        var width = (spacing * (XiangqiRules.ColumnCount - 1)) + (BoardPadding * 2);
        var height = (spacing * (XiangqiRules.RowCount - 1)) + (BoardPadding * 2);
        return new BoardLayout(
            new Rect((bounds.Width - width) / 2, (bounds.Height - height) / 2, width, height),
            spacing);
    }

    private static Point CenterOf(BoardLayout layout, XiangqiPosition position, bool flipped)
    {
        var visualRow = flipped ? XiangqiRules.RowCount - 1 - position.Row : position.Row;
        var visualColumn = flipped ? XiangqiRules.ColumnCount - 1 - position.Column : position.Column;
        return GridPoint(layout, visualRow, visualColumn);
    }

    private static Point GridPoint(BoardLayout layout, int row, int column) =>
        new(
            layout.Board.X + BoardPadding + (column * layout.Spacing),
            layout.Board.Y + BoardPadding + (row * layout.Spacing));

    private static Point Midpoint(Point first, Point second) =>
        new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    private static void DrawCenteredText(
        DrawingContext context,
        string text,
        Point center,
        Typeface typeface,
        double size,
        IBrush brush)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush);
        context.DrawText(formatted, new Point(center.X - (formatted.Width / 2), center.Y - (formatted.Height / 2)));
    }

    private static string ToChinese(int number) => "零一二三四五六七八九"[number].ToString();
    private readonly record struct BoardLayout(Rect Board, double Spacing);
}
