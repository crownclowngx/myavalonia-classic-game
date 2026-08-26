using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.Gomoku.Domain;
using ClassicGamePlugin.Features.Gomoku.ViewModels;

namespace ClassicGamePlugin.Features.Gomoku.Views;

/// <summary>
/// 绘制 15×15 交叉点棋盘并把指针位置转译为领域坐标。控件只读取 ViewModel 的不可变投影，
/// 不判断胜负或禁手；所有点击仍交由 ViewModel/领域验证，避免 View 复制规则。
/// </summary>
public sealed class GomokuBoardControl : Control
{
    private const double BoardPadding = 30;
    private static readonly Typeface CoordinateTypeface =
        new("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
    private GomokuViewModel? _subscribedGame;
    private GomokuPosition? _hoverPosition;

    public static readonly StyledProperty<GomokuViewModel?> GameProperty =
        AvaloniaProperty.Register<GomokuBoardControl, GomokuViewModel?>(nameof(Game));

    public GomokuBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public GomokuViewModel? Game
    {
        get => GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty)
        {
            SubscribeToGame(change.GetNewValue<GomokuViewModel?>());
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var layout = GetLayout(Bounds.Size);
        var boardBrush = new SolidColorBrush(Color.Parse("#FFD8A85E"));
        var borderBrush = new SolidColorBrush(Color.Parse("#FF70451F"));
        var gridBrush = new SolidColorBrush(Color.Parse("#FF5A381B"));
        context.DrawRectangle(boardBrush, new Pen(borderBrush, 3), layout.Board, 4, 4);

        for (var index = 0; index < GomokuRules.BoardSize; index++)
        {
            var offset = BoardPadding + (index * layout.Spacing);
            context.DrawLine(
                new Pen(gridBrush, 1),
                new Point(layout.Board.X + BoardPadding, layout.Board.Y + offset),
                new Point(layout.Board.Right - BoardPadding, layout.Board.Y + offset));
            context.DrawLine(
                new Pen(gridBrush, 1),
                new Point(layout.Board.X + offset, layout.Board.Y + BoardPadding),
                new Point(layout.Board.X + offset, layout.Board.Bottom - BoardPadding));
            DrawCoordinate(context, ((char)('A' + index)).ToString(),
                new Point(layout.Board.X + offset - 4, layout.Board.Y + 6), gridBrush);
            DrawCoordinate(context, (index + 1).ToString(CultureInfo.InvariantCulture),
                new Point(layout.Board.X + 5, layout.Board.Y + offset - 7), gridBrush);
        }

        foreach (var star in new[]
        {
            new GomokuPosition(3, 3), new(3, 11), new(7, 7), new(11, 3), new(11, 11),
        })
        {
            context.DrawEllipse(gridBrush, null, CenterOf(layout, star), 4, 4);
        }

        if (Game is null)
        {
            return;
        }

        var snapshot = Game.CurrentSnapshot;
        DrawWinningLines(context, layout, snapshot.WinningLines);
        for (var row = 0; row < GomokuRules.BoardSize; row++)
        {
            for (var column = 0; column < GomokuRules.BoardSize; column++)
            {
                var position = new GomokuPosition(row, column);
                if (snapshot.GetStone(position) is { } stone)
                {
                    DrawStone(context, layout, position, stone, snapshot.LastMove == position);
                }
            }
        }

        foreach (var forbidden in Game.ForbiddenPoints)
        {
            DrawForbidden(context, layout, forbidden.Key);
        }

        if (Game.HintPosition is { } hint)
        {
            context.DrawEllipse(
                null,
                new Pen(new SolidColorBrush(Color.Parse("#FFFFC928")), 4),
                CenterOf(layout, hint),
                layout.Spacing * 0.34,
                layout.Spacing * 0.34);
        }

        if (_hoverPosition is { } hover && Game.CanInteract && snapshot.GetStone(hover) is null &&
            !Game.ForbiddenPoints.ContainsKey(hover))
        {
            var ghost = Game.CurrentPlayer == GomokuStone.Black
                ? new SolidColorBrush(Color.Parse("#770C0D0E"))
                : new SolidColorBrush(Color.Parse("#AAFFFFFF"));
            context.DrawEllipse(ghost, null, CenterOf(layout, hover), layout.Spacing * 0.4, layout.Spacing * 0.4);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        var next = TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var position)
            ? position
            : (GomokuPosition?)null;
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
            !TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var position))
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

    /// <summary>测试可直接验证交叉点舍入和边界拒绝，不需要构造平台原生指针事件。</summary>
    internal static bool TryHitTest(Size bounds, Point point, out GomokuPosition position)
    {
        var layout = GetLayout(bounds);
        var relativeX = point.X - layout.Board.X - BoardPadding;
        var relativeY = point.Y - layout.Board.Y - BoardPadding;
        var column = (int)Math.Round(relativeX / layout.Spacing);
        var row = (int)Math.Round(relativeY / layout.Spacing);
        position = new GomokuPosition(row, column);
        if (!GomokuRules.IsInside(row, column))
        {
            return false;
        }

        var center = CenterOf(layout, position);
        return Math.Abs(point.X - center.X) <= layout.Spacing * 0.45 &&
            Math.Abs(point.Y - center.Y) <= layout.Spacing * 0.45;
    }

    private void SubscribeToGame(GomokuViewModel? game)
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

    private static BoardLayout GetLayout(Size bounds)
    {
        var side = Math.Max(180, Math.Min(bounds.Width, bounds.Height));
        var board = new Rect((bounds.Width - side) / 2, (bounds.Height - side) / 2, side, side);
        return new BoardLayout(board, (side - (BoardPadding * 2)) / (GomokuRules.BoardSize - 1));
    }

    private static Point CenterOf(BoardLayout layout, GomokuPosition position) =>
        new(
            layout.Board.X + BoardPadding + (position.Column * layout.Spacing),
            layout.Board.Y + BoardPadding + (position.Row * layout.Spacing));

    private static void DrawStone(
        DrawingContext context,
        BoardLayout layout,
        GomokuPosition position,
        GomokuStone stone,
        bool isLastMove)
    {
        var center = CenterOf(layout, position);
        var radius = layout.Spacing * 0.41;
        var fill = stone == GomokuStone.Black
            ? new SolidColorBrush(Color.Parse("#FF17191C"))
            : new SolidColorBrush(Color.Parse("#FFF7F5EF"));
        var stroke = stone == GomokuStone.Black
            ? new SolidColorBrush(Color.Parse("#FF050607"))
            : new SolidColorBrush(Color.Parse("#FF8A8175"));
        context.DrawEllipse(fill, new Pen(stroke, 1), center, radius, radius);
        if (isLastMove)
        {
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#FFE63946")), null, center, 4, 4);
        }
    }

    private static void DrawForbidden(DrawingContext context, BoardLayout layout, GomokuPosition position)
    {
        var center = CenterOf(layout, position);
        var offset = layout.Spacing * 0.19;
        var pen = new Pen(new SolidColorBrush(Color.Parse("#FFD62828")), 2.5);
        context.DrawLine(pen, new Point(center.X - offset, center.Y - offset), new Point(center.X + offset, center.Y + offset));
        context.DrawLine(pen, new Point(center.X + offset, center.Y - offset), new Point(center.X - offset, center.Y + offset));
    }

    private static void DrawWinningLines(
        DrawingContext context,
        BoardLayout layout,
        IReadOnlyList<IReadOnlyList<GomokuPosition>> lines)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#DDFFB703")), 7, lineCap: PenLineCap.Round);
        foreach (var line in lines.Where(line => line.Count >= 2))
        {
            context.DrawLine(pen, CenterOf(layout, line[0]), CenterOf(layout, line[^1]));
        }
    }

    private static void DrawCoordinate(DrawingContext context, string text, Point origin, IBrush brush)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            CoordinateTypeface,
            11,
            brush);
        context.DrawText(formatted, origin);
    }

    private readonly record struct BoardLayout(Rect Board, double Spacing);
}
