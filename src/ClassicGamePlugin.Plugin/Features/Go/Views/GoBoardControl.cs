using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.Go.Domain;
using ClassicGamePlugin.Features.Go.ViewModels;

namespace ClassicGamePlugin.Features.Go.Views;

/// <summary>
/// 绘制固定 19 路棋盘并把指针位置转译为领域行列。控件只消费不可变快照与动画计划，
/// 不自行判断提子、自杀、全局同形或数子归属，避免 View 形成第二套规则实现。
/// </summary>
public sealed class GoBoardControl : Control
{
    private const double BoardPadding = 34;
    private const string ColumnNames = "ABCDEFGHJKLMNOPQRST";
    private static readonly Typeface CoordinateTypeface =
        new("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

    private GoViewModel? _subscribedGame;
    private GoAnimationPlan? _animation;
    private DispatcherTimer? _animationTimer;
    private long _animationStarted;
    private TimeSpan _animationElapsed;
    private GoPosition? _hoverPosition;
    private bool _isAttached;

    public static readonly StyledProperty<GoViewModel?> GameProperty =
        AvaloniaProperty.Register<GoBoardControl, GoViewModel?>(nameof(Game));

    public GoBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public GoViewModel? Game
    {
        get => GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    internal bool HasActiveAnimation => _animation is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty)
        {
            if (_isAttached)
            {
                SubscribeToGame(change.GetNewValue<GoViewModel?>());
            }

            UpdateAutomationName();
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        _isAttached = true;
        SubscribeToGame(Game);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        _isAttached = false;
        SubscribeToGame(null);
        base.OnDetachedFromVisualTree(eventArgs);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var layout = GetLayout(Bounds.Size);
        var boardBrush = Brush("#FFD8A85E");
        var borderBrush = Brush("#FF70451F");
        var gridBrush = Brush("#FF523415");
        context.DrawRectangle(boardBrush, new Pen(borderBrush, 3), layout.Board, 5, 5);
        DrawGrid(context, layout, gridBrush);

        if (Game is not { } game)
        {
            return;
        }

        var snapshot = game.CurrentSnapshot;
        if (_animation is { } animation)
        {
            DrawAnimatedPosition(context, layout, animation);
        }
        else
        {
            DrawSnapshot(context, layout, snapshot);
        }

        DrawTerritory(context, layout, snapshot);
        if (_hoverPosition is { } hover && game.CanPlay && snapshot.GetStone(hover) is null)
        {
            var ghost = snapshot.CurrentPlayer == GoStone.Black
                ? Brush("#6613171C")
                : Brush("#AAFFFFFF");
            context.DrawEllipse(ghost, null, CenterOf(layout, hover), layout.Spacing * 0.42, layout.Spacing * 0.42);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        var next = TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var position)
            ? position
            : (GoPosition?)null;
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
        if (Game is not { } game || !game.CanBoardInteract ||
            !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            !TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var position))
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        game.PlayPosition(position.Row, position.Column);
        eventArgs.Handled = true;
    }

    /// <summary>测试可直接验证交叉点舍入和边界拒绝，无需构造平台原生指针事件。</summary>
    internal static bool TryHitTest(Size bounds, Point point, out GoPosition position)
    {
        var layout = GetLayout(bounds);
        var relativeX = point.X - layout.Board.X - BoardPadding;
        var relativeY = point.Y - layout.Board.Y - BoardPadding;
        var column = (int)Math.Round(relativeX / layout.Spacing);
        var row = (int)Math.Round(relativeY / layout.Spacing);
        position = new GoPosition(row, column);
        if (!GoRules.IsInside(row, column))
        {
            return false;
        }

        var center = CenterOf(layout, position);
        return Math.Abs(point.X - center.X) <= layout.Spacing * 0.45 &&
            Math.Abs(point.Y - center.Y) <= layout.Spacing * 0.45;
    }

    internal static string GetColumnLabel(int column)
    {
        if (column < 0 || column >= GoRules.BoardSize)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        return ColumnNames[column].ToString();
    }

    private void SubscribeToGame(GoViewModel? game)
    {
        if (ReferenceEquals(_subscribedGame, game))
        {
            return;
        }

        var previous = _subscribedGame;
        if (previous is not null)
        {
            previous.PropertyChanged -= OnGamePropertyChanged;
            previous.AnimationRequested -= OnAnimationRequested;
            previous.AnimationCancellationRequested -= OnAnimationCancellationRequested;
        }

        StopAnimation();
        _subscribedGame = game;
        previous?.CompleteAnimation();
        if (game is not null)
        {
            game.PropertyChanged += OnGamePropertyChanged;
            game.AnimationRequested += OnAnimationRequested;
            game.AnimationCancellationRequested += OnAnimationCancellationRequested;
        }

        UpdateAutomationName();
        InvalidateVisual();
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        UpdateAutomationName();
        InvalidateVisual();
    }

    private void OnAnimationRequested(object? sender, GoAnimationPlan plan)
    {
        StopAnimation();
        _animation = plan;
        _animationElapsed = TimeSpan.Zero;
        _animationStarted = Stopwatch.GetTimestamp();
        EnsureAnimationTimer().Start();
        InvalidateVisual();
    }

    private void OnAnimationCancellationRequested(object? sender, EventArgs eventArgs) => StopAnimation();

    private void OnAnimationTick(object? sender, EventArgs eventArgs)
    {
        if (_animation is null)
        {
            _animationTimer?.Stop();
            return;
        }

        _animationElapsed = Stopwatch.GetElapsedTime(_animationStarted);
        if (_animation.IsComplete(_animationElapsed))
        {
            var game = _subscribedGame;
            StopAnimation();
            game?.CompleteAnimation();
            return;
        }

        InvalidateVisual();
    }

    private DispatcherTimer EnsureAnimationTimer()
    {
        if (_animationTimer is not null)
        {
            return _animationTimer;
        }

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += OnAnimationTick;
        return _animationTimer;
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animation = null;
        _animationElapsed = TimeSpan.Zero;
        InvalidateVisual();
    }

    private void UpdateAutomationName()
    {
        if (Game is { } game)
        {
            AutomationProperties.SetName(this, game.AccessibleBoardText);
        }
    }

    private static void DrawGrid(DrawingContext context, BoardLayout layout, IBrush gridBrush)
    {
        var pen = new Pen(gridBrush, 1);
        for (var index = 0; index < GoRules.BoardSize; index++)
        {
            var offset = BoardPadding + (index * layout.Spacing);
            context.DrawLine(
                pen,
                new Point(layout.Board.X + BoardPadding, layout.Board.Y + offset),
                new Point(layout.Board.Right - BoardPadding, layout.Board.Y + offset));
            context.DrawLine(
                pen,
                new Point(layout.Board.X + offset, layout.Board.Y + BoardPadding),
                new Point(layout.Board.X + offset, layout.Board.Bottom - BoardPadding));
            DrawCoordinate(
                context,
                GetColumnLabel(index),
                new Point(layout.Board.X + offset - 4, layout.Board.Bottom - 25),
                gridBrush);
            DrawCoordinate(
                context,
                (GoRules.BoardSize - index).ToString(CultureInfo.InvariantCulture),
                new Point(layout.Board.X + 7, layout.Board.Y + offset - 7),
                gridBrush);
        }

        foreach (var row in new[] { 3, 9, 15 })
        {
            foreach (var column in new[] { 3, 9, 15 })
            {
                context.DrawEllipse(gridBrush, null, CenterOf(layout, new GoPosition(row, column)), 3.5, 3.5);
            }
        }
    }

    private static void DrawSnapshot(DrawingContext context, BoardLayout layout, GoGameSnapshot snapshot)
    {
        for (var row = 0; row < GoRules.BoardSize; row++)
        {
            for (var column = 0; column < GoRules.BoardSize; column++)
            {
                var position = new GoPosition(row, column);
                if (snapshot.GetStone(position) is { } stone)
                {
                    DrawStone(
                        context,
                        layout,
                        position,
                        stone,
                        scale: 1,
                        opacity: snapshot.IsMarkedDead(position) ? 0.42 : 1,
                        isLastMove: snapshot.LastMove == position,
                        isMarkedDead: snapshot.IsMarkedDead(position));
                }
            }
        }
    }

    private void DrawAnimatedPosition(DrawingContext context, BoardLayout layout, GoAnimationPlan plan)
    {
        var after = plan.Move.After;
        var captured = plan.Move.CapturedPositions.ToHashSet();
        for (var row = 0; row < GoRules.BoardSize; row++)
        {
            for (var column = 0; column < GoRules.BoardSize; column++)
            {
                var position = new GoPosition(row, column);
                if (position != plan.Move.Position && after.GetStone(position) is { } stone)
                {
                    DrawStone(context, layout, position, stone, 1, 1, false, false);
                }
            }
        }

        foreach (var position in captured)
        {
            if (plan.Move.Before.GetStone(position) is { } stone)
            {
                DrawStone(
                    context,
                    layout,
                    position,
                    stone,
                    plan.GetCaptureScale(_animationElapsed),
                    plan.GetCaptureOpacity(_animationElapsed),
                    false,
                    false);
            }
        }

        DrawStone(
            context,
            layout,
            plan.Move.Position,
            plan.Move.Player,
            plan.GetPlacementScale(_animationElapsed),
            plan.GetPlacementOpacity(_animationElapsed),
            true,
            false);
    }

    private static void DrawTerritory(DrawingContext context, BoardLayout layout, GoGameSnapshot snapshot)
    {
        if (snapshot.Score is not { } score)
        {
            return;
        }

        foreach (var territory in score.TerritoryOwners)
        {
            var center = CenterOf(layout, territory.Key);
            var color = territory.Value == GoStone.Black ? "#CC17191C" : "#DDF8F6EF";
            var size = layout.Spacing * 0.24;
            context.DrawRectangle(
                Brush(color),
                new Pen(Brush("#99704A25"), 0.8),
                new Rect(center.X - size, center.Y - size, size * 2, size * 2),
                1,
                1);
        }
    }

    private static void DrawStone(
        DrawingContext context,
        BoardLayout layout,
        GoPosition position,
        GoStone stone,
        double scale,
        double opacity,
        bool isLastMove,
        bool isMarkedDead)
    {
        if (opacity <= 0)
        {
            return;
        }

        var center = CenterOf(layout, position);
        var radius = layout.Spacing * 0.46 * scale;
        var baseFill = Color.Parse(stone == GoStone.Black ? "#FF17191C" : "#FFF7F5EF");
        var baseStroke = Color.Parse(stone == GoStone.Black ? "#FF050607" : "#FF80796F");
        context.DrawEllipse(
            new SolidColorBrush(WithOpacity(baseFill, opacity)),
            new Pen(new SolidColorBrush(WithOpacity(baseStroke, opacity)), 1),
            center,
            radius,
            radius);

        if (isLastMove)
        {
            context.DrawEllipse(Brush("#FFE63946"), null, center, 3.5, 3.5);
        }

        if (isMarkedDead)
        {
            var offset = layout.Spacing * 0.19;
            var pen = new Pen(Brush("#FFE63946"), 2.2);
            context.DrawLine(pen, new Point(center.X - offset, center.Y - offset), new Point(center.X + offset, center.Y + offset));
            context.DrawLine(pen, new Point(center.X + offset, center.Y - offset), new Point(center.X - offset, center.Y + offset));
        }
    }

    private static BoardLayout GetLayout(Size bounds)
    {
        var side = Math.Max(220, Math.Min(bounds.Width, bounds.Height));
        var board = new Rect((bounds.Width - side) / 2, (bounds.Height - side) / 2, side, side);
        return new BoardLayout(board, (side - (BoardPadding * 2)) / (GoRules.BoardSize - 1));
    }

    private static Point CenterOf(BoardLayout layout, GoPosition position) =>
        new(
            layout.Board.X + BoardPadding + (position.Column * layout.Spacing),
            layout.Board.Y + BoardPadding + (position.Row * layout.Spacing));

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

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    private readonly record struct BoardLayout(Rect Board, double Spacing);
}
