using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using ClassicGamePlugin.Features.ChineseCheckers.ViewModels;

namespace ClassicGamePlugin.Features.ChineseCheckers.Views;

/// <summary>
/// 自绘六角星棋盘并把指针命中转换为立方坐标。控件只消费快照、合法终点与动画计划，
/// 不自行计算连跳或胜负，避免 View 成为第二个规则引擎。
/// </summary>
public sealed class ChineseCheckersBoardControl : Control
{
    private const double Padding = 28;
    private ChineseCheckersViewModel? _subscribedGame;
    private ChineseCheckersAnimationPlan? _animation;
    private DispatcherTimer? _animationTimer;
    private long _animationStarted;
    private TimeSpan _animationElapsed;
    private ChineseCheckersPosition? _hoverPosition;
    private bool _isAttached;

    public static readonly StyledProperty<ChineseCheckersViewModel?> GameProperty =
        AvaloniaProperty.Register<ChineseCheckersBoardControl, ChineseCheckersViewModel?>(nameof(Game));

    public ChineseCheckersBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public ChineseCheckersViewModel? Game
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
                SubscribeToGame(change.GetNewValue<ChineseCheckersViewModel?>());
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
        context.DrawRectangle(Brush("#FFF2E2BE"), new Pen(Brush("#FF79552D"), 2), new Rect(Bounds.Size), 8, 8);
        var rotated = Game?.IsBoardRotated == true;
        DrawConnections(context, Bounds.Size, rotated);
        DrawHoles(context, Bounds.Size, rotated);
        if (Game is not { } game)
        {
            return;
        }

        DrawGuides(context, Bounds.Size, rotated, game);
        if (_animation is { } animation)
        {
            DrawSnapshot(context, Bounds.Size, rotated, animation.Move.Before, animation.Move.Move.From);
            DrawAnimatedPiece(context, Bounds.Size, rotated, animation);
        }
        else
        {
            DrawSnapshot(context, Bounds.Size, rotated, game.CurrentSnapshot, null);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        var next = TryHitTest(Bounds.Size, eventArgs.GetPosition(this), Game?.IsBoardRotated == true, out var position)
            ? position
            : (ChineseCheckersPosition?)null;
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
        if (Game is not { } game || !game.CanInteract ||
            !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            !TryHitTest(Bounds.Size, eventArgs.GetPosition(this), game.IsBoardRotated, out var position))
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        game.SelectPosition(position);
        eventArgs.Handled = true;
    }

    /// <summary>测试可直接验证正反棋盘坐标与边界拒绝，不需要平台原生指针事件。</summary>
    internal static bool TryHitTest(Size bounds, Point point, bool rotated, out ChineseCheckersPosition position)
    {
        var layout = CreateLayout(bounds);
        var bestDistance = double.MaxValue;
        position = default;
        foreach (var candidate in ChineseCheckersRules.AllPositions)
        {
            var center = CenterOf(layout, candidate, rotated);
            var distance = Math.Sqrt(Math.Pow(center.X - point.X, 2) + Math.Pow(center.Y - point.Y, 2));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                position = candidate;
            }
        }

        return bestDistance <= layout.Scale * 0.42;
    }

    internal static Point GetCenter(Size bounds, ChineseCheckersPosition position, bool rotated) =>
        CenterOf(CreateLayout(bounds), position, rotated);

    private void SubscribeToGame(ChineseCheckersViewModel? game)
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
        previous?.DeactivateView();
        if (game is not null)
        {
            game.PropertyChanged += OnGamePropertyChanged;
            game.AnimationRequested += OnAnimationRequested;
            game.AnimationCancellationRequested += OnAnimationCancellationRequested;
            game.ActivateView();
        }

        UpdateAutomationName();
        InvalidateVisual();
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        UpdateAutomationName();
        InvalidateVisual();
    }

    private void OnAnimationRequested(object? sender, ChineseCheckersAnimationPlan plan)
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

    private static void DrawConnections(DrawingContext context, Size size, bool rotated)
    {
        var layout = CreateLayout(size);
        var pen = new Pen(Brush("#907A613E"), 1.2);
        foreach (var position in ChineseCheckersRules.AllPositions)
        {
            foreach (var direction in ChineseCheckersRules.Directions.Take(3))
            {
                var neighbor = position.Add(direction);
                if (ChineseCheckersRules.TryGetIndex(neighbor, out _))
                {
                    context.DrawLine(pen, CenterOf(layout, position, rotated), CenterOf(layout, neighbor, rotated));
                }
            }
        }
    }

    private static void DrawHoles(DrawingContext context, Size size, bool rotated)
    {
        var layout = CreateLayout(size);
        foreach (var position in ChineseCheckersRules.AllPositions)
        {
            var fill = ChineseCheckersRules.BlueHome.Contains(position)
                ? Brush("#553D82D7")
                : ChineseCheckersRules.RedHome.Contains(position)
                    ? Brush("#55E05A5A")
                    : Brush("#55FFFFFF");
            context.DrawEllipse(fill, new Pen(Brush("#AA79552D"), 1), CenterOf(layout, position, rotated),
                layout.Scale * 0.27, layout.Scale * 0.27);
        }
    }

    private static void DrawGuides(
        DrawingContext context,
        Size size,
        bool rotated,
        ChineseCheckersViewModel game)
    {
        var layout = CreateLayout(size);
        if (game.HintMove is { } hint)
        {
            var pen = new Pen(Brush("#FF815AC0"), 4);
            for (var index = 1; index < hint.Path.Count; index++)
            {
                context.DrawLine(pen, CenterOf(layout, hint.Path[index - 1], rotated),
                    CenterOf(layout, hint.Path[index], rotated));
            }
        }

        if (game.SelectedPosition is { } selected)
        {
            context.DrawEllipse(null, new Pen(Brush("#FFFFC107"), 4), CenterOf(layout, selected, rotated),
                layout.Scale * 0.43, layout.Scale * 0.43);
        }

        foreach (var move in game.SelectedMoves)
        {
            var center = CenterOf(layout, move.To, rotated);
            if (move.Kind == ChineseCheckersMoveKind.Step)
            {
                context.DrawEllipse(Brush("#FF2EAD67"), null, center, layout.Scale * 0.13, layout.Scale * 0.13);
            }
            else
            {
                context.DrawEllipse(null, new Pen(Brush("#FFFFB300"), 3), center,
                    layout.Scale * 0.29, layout.Scale * 0.29);
            }
        }
    }

    private static void DrawSnapshot(
        DrawingContext context,
        Size size,
        bool rotated,
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersPosition? omitted)
    {
        var layout = CreateLayout(size);
        foreach (var position in ChineseCheckersRules.AllPositions)
        {
            if (position != omitted && snapshot.GetPiece(position) is { } side)
            {
                DrawPiece(context, CenterOf(layout, position, rotated), layout.Scale, side, 1,
                    snapshot.LastMove?.To == position);
            }
        }
    }

    private void DrawAnimatedPiece(
        DrawingContext context,
        Size size,
        bool rotated,
        ChineseCheckersAnimationPlan animation)
    {
        var layout = CreateLayout(size);
        var frame = animation.GetMovementFrame(_animationElapsed);
        var from = CenterOf(layout, frame.From, rotated);
        var to = CenterOf(layout, frame.To, rotated);
        var center = new Point(from.X + ((to.X - from.X) * frame.Progress), from.Y + ((to.Y - from.Y) * frame.Progress));
        DrawPiece(context, center, layout.Scale, animation.Move.Side,
            animation.GetArrivalScale(_animationElapsed), true);
    }

    private static void DrawPiece(
        DrawingContext context,
        Point center,
        double scale,
        ChineseCheckersSide side,
        double sizeScale,
        bool isLastMove)
    {
        var fill = side == ChineseCheckersSide.Blue ? Brush("#FF2878CC") : Brush("#FFD94C4C");
        var stroke = side == ChineseCheckersSide.Blue ? Brush("#FF124A86") : Brush("#FF8E2525");
        var radius = scale * 0.37 * sizeScale;
        context.DrawEllipse(fill, new Pen(stroke, 1.5), center, radius, radius);
        context.DrawEllipse(Brush("#55FFFFFF"), null,
            new Point(center.X - (radius * 0.28), center.Y - (radius * 0.28)), radius * 0.23, radius * 0.23);
        if (isLastMove)
        {
            context.DrawEllipse(null, new Pen(Brush("#FFFFFFFF"), 2), center, radius * 0.48, radius * 0.48);
        }
    }

    private void UpdateAutomationName()
    {
        if (Game is { } game)
        {
            AutomationProperties.SetName(this,
                $"中国跳棋棋盘，{game.CurrentTurnText}，蓝方目标营 {game.BlueGoalCount}，红方目标营 {game.RedGoalCount}");
        }
    }

    private static BoardLayout CreateLayout(Size size)
    {
        var raw = ChineseCheckersRules.AllPositions.Select(ToRawPoint).ToArray();
        var minX = raw.Min(point => point.X);
        var maxX = raw.Max(point => point.X);
        var minY = raw.Min(point => point.Y);
        var maxY = raw.Max(point => point.Y);
        var scale = Math.Min(
            Math.Max(1, size.Width - (Padding * 2)) / (maxX - minX),
            Math.Max(1, size.Height - (Padding * 2)) / (maxY - minY));
        return new BoardLayout(new Point(size.Width / 2, size.Height / 2), scale);
    }

    private static Point CenterOf(BoardLayout layout, ChineseCheckersPosition position, bool rotated)
    {
        var raw = ToRawPoint(position);
        var direction = rotated ? -1 : 1;
        return new Point(layout.Center.X + (raw.X * layout.Scale * direction),
            layout.Center.Y + (raw.Y * layout.Scale * direction));
    }

    private static Point ToRawPoint(ChineseCheckersPosition position) =>
        new(Math.Sqrt(3) * (position.X + (position.Z / 2.0)), 1.5 * position.Z);

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
    private readonly record struct BoardLayout(Point Center, double Scale);
}
