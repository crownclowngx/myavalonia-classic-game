using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.Match3.Domain;
using ClassicGamePlugin.Features.Match3.ViewModels;

namespace ClassicGamePlugin.Features.Match3.Views;

/// <summary>
/// 绘制六种棋子和特殊标记，并把点击、拖动转换为 ViewModel 意图。控件只消费领域 Transition 回放，
/// 不自行判断匹配、计分或特殊组合，避免形成第二套游戏规则。
/// </summary>
public sealed class Match3BoardControl : Control
{
    private Match3ViewModel? _subscribedGame;
    private Match3AnimationPlan? _animation;
    private DispatcherTimer? _animationTimer;
    private long _animationStarted;
    private TimeSpan _animationElapsed;
    private Match3Position? _pressedPosition;
    private Match3Position? _hoverPosition;
    private bool _isAttached;
    private bool _releasingCapture;

    public static readonly StyledProperty<Match3ViewModel?> GameProperty =
        AvaloniaProperty.Register<Match3BoardControl, Match3ViewModel?>(nameof(Game));

    public Match3BoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public Match3ViewModel? Game
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
                SubscribeToGame(change.GetNewValue<Match3ViewModel?>());
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
        CancelGesture();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var layout = GetLayout(Bounds.Size);
        context.DrawRectangle(
            Brush(dark ? "#FF202830" : "#FFF0E7D8"),
            new Pen(Brush(dark ? "#FF65717B" : "#FFB5A58E"), 1),
            layout.Board,
            12,
            12);
        DrawCells(context, layout, dark);
        if (Game is null)
        {
            return;
        }

        if (_animation is null)
        {
            DrawSnapshot(context, layout, dark, Game.Game.Board, 1, 0, 1);
            DrawSelection(context, layout);
            return;
        }

        DrawAnimation(context, layout, dark, _animation.GetFrame(_animationElapsed));
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

        Focus(NavigationMethod.Pointer);
        _pressedPosition = position;
        eventArgs.Pointer.Capture(this);
        eventArgs.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (_pressedPosition is not null)
        {
            eventArgs.Handled = true;
            return;
        }

        if (Game is null || !TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var position))
        {
            _hoverPosition = null;
            ToolTip.SetTip(this, null);
            return;
        }

        if (_hoverPosition == position)
        {
            return;
        }

        _hoverPosition = position;
        ToolTip.SetTip(this, Game.GetCellAccessibleText(position));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        var source = _pressedPosition;
        if (Game is not null && source is not null)
        {
            if (TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var target) &&
                target != source && Match3Rules.AreAdjacent(source.Value, target))
            {
                Game.HandleDragSwap(source.Value, target);
            }
            else
            {
                Game.HandleCellClick(source.Value);
            }
        }

        Release(eventArgs.Pointer);
        eventArgs.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs eventArgs)
    {
        base.OnPointerCaptureLost(eventArgs);
        if (!_releasingCapture)
        {
            CancelGesture();
        }
    }

    protected override void OnPointerExited(PointerEventArgs eventArgs)
    {
        base.OnPointerExited(eventArgs);
        _hoverPosition = null;
        ToolTip.SetTip(this, null);
    }

    internal static bool TryHitTest(Size bounds, Point point, out Match3Position position)
    {
        var layout = GetLayout(bounds);
        var column = (int)((point.X - layout.Board.X) / layout.CellSize);
        var row = (int)((point.Y - layout.Board.Y) / layout.CellSize);
        position = new Match3Position(row, column);
        return point.X >= layout.Board.X && point.X < layout.Board.Right &&
               point.Y >= layout.Board.Y && point.Y < layout.Board.Bottom &&
               Match3Rules.IsInside(position);
    }

    private void SubscribeToGame(Match3ViewModel? game)
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
        if (previous?.IsAnimationRunning == true)
        {
            previous.CompleteAnimation();
        }

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

    private void OnAnimationRequested(object? sender, Match3AnimationPlan plan)
    {
        StopAnimation();
        _animation = plan;
        _animationElapsed = TimeSpan.Zero;
        _animationStarted = Stopwatch.GetTimestamp();
        EnsureAnimationTimer().Start();
        InvalidateVisual();
    }

    private void OnAnimationCancellationRequested(object? sender, EventArgs eventArgs)
    {
        var game = _subscribedGame;
        StopAnimation();
        game?.CompleteAnimation();
    }

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

    private void DrawAnimation(
        DrawingContext context,
        BoardLayout layout,
        bool dark,
        Match3AnimationFrame frame)
    {
        var transition = _animation!.Transition;
        switch (frame.Phase)
        {
            case Match3AnimationPhaseKind.Swap:
                DrawSwap(context, layout, dark, transition.Before, transition.Source, transition.Target, frame.Progress);
                break;
            case Match3AnimationPhaseKind.SwapBack:
                DrawSwap(context, layout, dark, transition.Before, transition.Source, transition.Target, 1 - frame.Progress);
                break;
            case Match3AnimationPhaseKind.Clear:
                var clearStep = transition.Steps[frame.StepIndex];
                DrawSnapshot(context, layout, dark, clearStep.BeforeClear, 1, 0, 1,
                    clearStep.ClearedPositions.ToHashSet(), frame.Progress);
                break;
            case Match3AnimationPhaseKind.Fall:
                var fallStep = transition.Steps[frame.StepIndex];
                DrawSnapshot(context, layout, dark, fallStep.AfterRefill,
                    0.35 + (0.65 * frame.Progress),
                    -(1 - frame.Progress) * layout.CellSize * 0.45,
                    1);
                break;
            case Match3AnimationPhaseKind.Shuffle:
                DrawSnapshot(context, layout, dark, transition.After,
                    0.2 + (0.8 * frame.Progress),
                    -(1 - frame.Progress) * layout.CellSize * 0.2,
                    0.82 + (0.18 * frame.Progress));
                break;
            default:
                DrawSnapshot(context, layout, dark, transition.After, 1, 0, 1);
                break;
        }
    }

    private static void DrawSwap(
        DrawingContext context,
        BoardLayout layout,
        bool dark,
        IReadOnlyList<Match3Tile?> board,
        Match3Position source,
        Match3Position target,
        double progress)
    {
        var skipped = new HashSet<Match3Position> { source, target };
        DrawSnapshot(context, layout, dark, board, 1, 0, 1, skipped, skipAffected: true);
        var first = board[Match3Rules.ToIndex(source)];
        var second = board[Match3Rules.ToIndex(target)];
        if (first is not null)
        {
            DrawTile(context, layout, dark, Interpolate(source, target, progress), first.Value, 1, 1);
        }

        if (second is not null)
        {
            DrawTile(context, layout, dark, Interpolate(target, source, progress), second.Value, 1, 1);
        }
    }

    private static void DrawSnapshot(
        DrawingContext context,
        BoardLayout layout,
        bool dark,
        IReadOnlyList<Match3Tile?> board,
        double opacity,
        double verticalOffset,
        double scale,
        HashSet<Match3Position>? affected = null,
        double affectedProgress = 0,
        bool skipAffected = false)
    {
        for (var index = 0; index < board.Count; index++)
        {
            var tile = board[index];
            var position = Match3Rules.ToPosition(index);
            if (tile is null || skipAffected && affected?.Contains(position) == true)
            {
                continue;
            }

            var tileOpacity = opacity;
            var tileScale = scale;
            if (affected?.Contains(position) == true && affectedProgress > 0)
            {
                tileOpacity *= 1 - affectedProgress;
                tileScale *= 1 - (0.35 * affectedProgress);
            }

            DrawTile(context, layout, dark,
                new GridPoint(position.Row + (verticalOffset / layout.CellSize), position.Column),
                tile.Value, tileOpacity, tileScale);
        }
    }

    private void DrawSelection(DrawingContext context, BoardLayout layout)
    {
        if (Game is null)
        {
            return;
        }

        for (var index = 0; index < Match3Rules.CellCount; index++)
        {
            var position = Match3Rules.ToPosition(index);
            var selected = Game.SelectedPosition == position;
            var hinted = Game.IsHinted(position);
            if (!selected && !hinted)
            {
                continue;
            }

            var rect = CellRect(layout, position).Deflate(layout.CellSize * 0.06);
            var color = selected ? "#FFFFC857" : "#FF55D6BE";
            context.DrawRectangle(null, new Pen(Brush(color), Math.Max(2, layout.CellSize * 0.055)), rect, 9, 9);
        }
    }

    private static void DrawCells(DrawingContext context, BoardLayout layout, bool dark)
    {
        var fill = Brush(dark ? "#FF303B45" : "#FFFFF8ED");
        var line = new Pen(Brush(dark ? "#FF46535E" : "#FFDCCDB8"), 1);
        for (var row = 0; row < Match3Rules.BoardSize; row++)
        {
            for (var column = 0; column < Match3Rules.BoardSize; column++)
            {
                context.DrawRectangle(fill, line,
                    CellRect(layout, new Match3Position(row, column)).Deflate(2), 7, 7);
            }
        }
    }

    private static void DrawTile(
        DrawingContext context,
        BoardLayout layout,
        bool dark,
        GridPoint position,
        Match3Tile tile,
        double opacity,
        double scale)
    {
        var center = Center(layout, position);
        var radius = layout.CellSize * 0.34 * scale;
        if (tile.Special == Match3SpecialKind.Rainbow)
        {
            DrawRainbow(context, center, radius, opacity);
            return;
        }

        var (fillColor, strokeColor) = GetColors(tile.Kind!.Value, dark);
        var fill = Brush(fillColor, opacity);
        var stroke = new Pen(Brush(strokeColor, opacity), Math.Max(1.5, layout.CellSize * 0.035));
        switch (tile.Kind.Value)
        {
            case Match3GemKind.Ruby:
                context.DrawEllipse(fill, stroke, center, radius, radius);
                break;
            case Match3GemKind.Amber:
                context.DrawRectangle(fill, stroke,
                    new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), 8, 8);
                break;
            case Match3GemKind.Emerald:
                context.DrawGeometry(fill, stroke, Polygon(
                    new Point(center.X, center.Y - radius),
                    new Point(center.X + radius, center.Y),
                    new Point(center.X, center.Y + radius),
                    new Point(center.X - radius, center.Y)));
                break;
            case Match3GemKind.Sapphire:
                context.DrawGeometry(fill, stroke, Polygon(
                    new Point(center.X, center.Y - radius),
                    new Point(center.X + radius, center.Y + radius),
                    new Point(center.X - radius, center.Y + radius)));
                break;
            case Match3GemKind.Amethyst:
                context.DrawGeometry(fill, stroke, Polygon(
                    new Point(center.X - radius * 0.75, center.Y - radius),
                    new Point(center.X + radius * 0.75, center.Y - radius),
                    new Point(center.X + radius, center.Y),
                    new Point(center.X + radius * 0.75, center.Y + radius),
                    new Point(center.X - radius * 0.75, center.Y + radius),
                    new Point(center.X - radius, center.Y)));
                break;
            case Match3GemKind.Pearl:
                context.DrawEllipse(fill, stroke, center, radius, radius);
                context.DrawEllipse(null, new Pen(Brush("#FFFFFFFF", opacity * 0.8), Math.Max(2, radius * 0.18)),
                    center, radius * 0.55, radius * 0.55);
                break;
        }

        DrawSpecialMark(context, center, radius, tile.Special, opacity);
    }

    private static void DrawSpecialMark(
        DrawingContext context,
        Point center,
        double radius,
        Match3SpecialKind special,
        double opacity)
    {
        var pen = new Pen(Brush("#FFFFFFFF", opacity * 0.92), Math.Max(2, radius * 0.18));
        switch (special)
        {
            case Match3SpecialKind.RowClear:
                context.DrawLine(pen, center + new Vector(-radius * 0.72, 0), center + new Vector(radius * 0.72, 0));
                break;
            case Match3SpecialKind.ColumnClear:
                context.DrawLine(pen, center + new Vector(0, -radius * 0.72), center + new Vector(0, radius * 0.72));
                break;
            case Match3SpecialKind.AreaBomb:
                context.DrawEllipse(null, pen, center, radius * 0.48, radius * 0.48);
                context.DrawLine(pen, center + new Vector(-radius * 0.25, 0), center + new Vector(radius * 0.25, 0));
                context.DrawLine(pen, center + new Vector(0, -radius * 0.25), center + new Vector(0, radius * 0.25));
                break;
        }
    }

    private static void DrawRainbow(DrawingContext context, Point center, double radius, double opacity)
    {
        context.DrawEllipse(Brush("#FF20242A", opacity), null, center, radius, radius);
        var colors = new[] { "#FFFF5A5F", "#FFFFC857", "#FF43AA8B", "#FF4D96FF", "#FF9B5DE5" };
        for (var index = 0; index < colors.Length; index++)
        {
            var ring = radius * (0.82 - (index * 0.13));
            context.DrawEllipse(null, new Pen(Brush(colors[index], opacity), Math.Max(1.5, radius * 0.09)),
                center, ring, ring);
        }
    }

    private static (string Fill, string Stroke) GetColors(Match3GemKind kind, bool dark) => kind switch
    {
        Match3GemKind.Ruby => (dark ? "#FFE75C62" : "#FFE94B52", "#FF8B252A"),
        Match3GemKind.Amber => (dark ? "#FFFFB84D" : "#FFF2A52B", "#FF9A6114"),
        Match3GemKind.Emerald => (dark ? "#FF4FC58E" : "#FF36A873", "#FF1C6D4A"),
        Match3GemKind.Sapphire => (dark ? "#FF65A7FF" : "#FF438DE8", "#FF245894"),
        Match3GemKind.Amethyst => (dark ? "#FFB47AE8" : "#FF965BCB", "#FF5D3481"),
        Match3GemKind.Pearl => (dark ? "#FFE7EDF2" : "#FFF8F3E8", "#FF8A969F"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static StreamGeometry Polygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using var geometryContext = geometry.Open();
        geometryContext.BeginFigure(points[0], isFilled: true);
        foreach (var point in points.Skip(1))
        {
            geometryContext.LineTo(point);
        }

        geometryContext.EndFigure(isClosed: true);
        return geometry;
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
        if (Game is not null)
        {
            AutomationProperties.SetName(this, Game.AccessibleBoardText);
        }
    }

    private void Release(IPointer pointer)
    {
        _releasingCapture = true;
        pointer.Capture(null);
        _releasingCapture = false;
        CancelGesture();
    }

    private void CancelGesture()
    {
        _pressedPosition = null;
        InvalidateVisual();
    }

    private static BoardLayout GetLayout(Size bounds)
    {
        const double padding = 10;
        var cellSize = Math.Max(1,
            Math.Min((bounds.Width - padding * 2) / Match3Rules.BoardSize,
                (bounds.Height - padding * 2) / Match3Rules.BoardSize));
        var boardSize = cellSize * Match3Rules.BoardSize;
        return new BoardLayout(
            new Rect((bounds.Width - boardSize) / 2, (bounds.Height - boardSize) / 2, boardSize, boardSize),
            cellSize);
    }

    private static Rect CellRect(BoardLayout layout, Match3Position position) =>
        new(layout.Board.X + position.Column * layout.CellSize,
            layout.Board.Y + position.Row * layout.CellSize,
            layout.CellSize,
            layout.CellSize);

    private static Point Center(BoardLayout layout, GridPoint position) =>
        new(layout.Board.X + (position.Column + 0.5) * layout.CellSize,
            layout.Board.Y + (position.Row + 0.5) * layout.CellSize);

    private static GridPoint Interpolate(Match3Position from, Match3Position to, double progress) =>
        new(from.Row + ((to.Row - from.Row) * progress),
            from.Column + ((to.Column - from.Column) * progress));

    private static SolidColorBrush Brush(string color, double opacity = 1) =>
        new(Color.Parse(color), opacity);

    private readonly record struct BoardLayout(Rect Board, double CellSize);
    private readonly record struct GridPoint(double Row, double Column);
}
