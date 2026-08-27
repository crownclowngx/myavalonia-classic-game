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
using ClassicGamePlugin.Features.Sokoban.Domain;
using ClassicGamePlugin.Features.Sokoban.ViewModels;

namespace ClassicGamePlugin.Features.Sokoban.Views;

/// <summary>
/// 使用 DrawingContext 绘制任意矩形推箱子地图，并把棋盘获得焦点后的局部键盘输入转换为 ViewModel 意图。
/// 控件不判断墙、箱子或完成规则；绘制动画也只消费已经提交的移动结果，避免 UI 形成第二套游戏状态。
/// </summary>
public sealed class SokobanBoardControl : Control
{
    private SokobanViewModel? _subscribedGame;
    private SokobanAnimationPlan? _animation;
    private DispatcherTimer? _animationTimer;
    private long _animationStarted;
    private TimeSpan _animationElapsed;
    private bool _isAttached;
    private SokobanPosition? _hoverPosition;

    public static readonly StyledProperty<SokobanViewModel?> GameProperty =
        AvaloniaProperty.Register<SokobanBoardControl, SokobanViewModel?>(nameof(Game));

    public SokobanBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public SokobanViewModel? Game
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
                SubscribeToGame(change.GetNewValue<SokobanViewModel?>());
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
        context.DrawRectangle(
            new SolidColorBrush(Color.Parse(ActualThemeVariant == ThemeVariant.Dark ? "#FF242A30" : "#FFE8ECEF")),
            null,
            new Rect(Bounds.Size),
            8,
            8);

        if (Game is not { } game)
        {
            return;
        }

        var layout = GetLayout(
            Bounds.Size,
            game.Game.Level.Width,
            game.Game.Level.Height,
            ActualThemeVariant == ThemeVariant.Dark);
        var snapshot = _animation is not null && _animationElapsed < SokobanAnimationPlan.MoveDuration
            ? _animation.Move.Before
            : game.Game.CreateSnapshot();
        DrawTerrain(context, layout, game.Game.Level);
        DrawDynamicObjects(context, layout, snapshot);
        DrawAnimation(context, layout);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Focus(NavigationMethod.Pointer);
            eventArgs.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (Game is not { } game ||
            !TryHitTest(Bounds.Size, game.Game.Level.Width, game.Game.Level.Height, eventArgs.GetPosition(this), out var position))
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
        ToolTip.SetTip(this, game.GetCellAccessibleText(position.Row, position.Column));
    }

    protected override void OnPointerExited(PointerEventArgs eventArgs)
    {
        base.OnPointerExited(eventArgs);
        _hoverPosition = null;
        ToolTip.SetTip(this, null);
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (Game is not { } game || !TryMapInput(eventArgs.Key, eventArgs.KeyModifiers, out var action))
        {
            return;
        }

        switch (action.Kind)
        {
            case SokobanInputActionKind.Move:
                game.Move(action.Direction!.Value);
                break;
            case SokobanInputActionKind.Undo:
                game.UndoCommand.Execute(null);
                break;
            case SokobanInputActionKind.Restart:
                game.RestartCommand.Execute(null);
                break;
            default:
                throw new InvalidOperationException("遇到了未知的推箱子键盘操作。");
        }

        eventArgs.Handled = true;
    }

    /// <summary>纯键位映射；只有无修饰方向键/WASD/U/R 和 Ctrl+Z 属于游戏，其余输入继续交给 Host。</summary>
    internal static bool TryMapInput(Key key, KeyModifiers modifiers, out SokobanInputAction action)
    {
        if (modifiers == KeyModifiers.Control && key == Key.Z)
        {
            action = new SokobanInputAction(SokobanInputActionKind.Undo, null);
            return true;
        }

        if (modifiers != KeyModifiers.None)
        {
            action = default;
            return false;
        }

        action = key switch
        {
            Key.Up or Key.W => new(SokobanInputActionKind.Move, SokobanDirection.Up),
            Key.Down or Key.S => new(SokobanInputActionKind.Move, SokobanDirection.Down),
            Key.Left or Key.A => new(SokobanInputActionKind.Move, SokobanDirection.Left),
            Key.Right or Key.D => new(SokobanInputActionKind.Move, SokobanDirection.Right),
            Key.U => new(SokobanInputActionKind.Undo, null),
            Key.R => new(SokobanInputActionKind.Restart, null),
            _ => default,
        };
        return action.Kind != SokobanInputActionKind.None;
    }

    internal static bool TryHitTest(
        Size bounds,
        int width,
        int height,
        Point point,
        out SokobanPosition position)
    {
        var layout = GetLayout(bounds, width, height);
        var column = (int)((point.X - layout.Board.X) / layout.CellSize);
        var row = (int)((point.Y - layout.Board.Y) / layout.CellSize);
        position = new SokobanPosition(row, column);
        return point.X >= layout.Board.X && point.X < layout.Board.Right &&
               point.Y >= layout.Board.Y && point.Y < layout.Board.Bottom &&
               row >= 0 && row < height && column >= 0 && column < width;
    }

    private void SubscribeToGame(SokobanViewModel? game)
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

    private void OnAnimationRequested(object? sender, SokobanAnimationPlan plan)
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

    private static void DrawTerrain(DrawingContext context, BoardLayout layout, SokobanLevelDefinition level)
    {
        var dark = layout.IsDark;
        var floor = Brush(dark ? "#FF39434A" : "#FFF3E6CF");
        var floorLine = Brush(dark ? "#FF4A555D" : "#FFE1D0B4");
        var wall = Brush(dark ? "#FF65747F" : "#FF788B98");
        var wallLine = Brush(dark ? "#FF9CABB4" : "#FF50636F");
        var goal = Brush(dark ? "#FFFFC857" : "#FFE09F24");

        for (var row = 0; row < level.Height; row++)
        {
            for (var column = 0; column < level.Width; column++)
            {
                var position = new SokobanPosition(row, column);
                var rect = CellRect(layout, position).Deflate(0.5);
                var terrain = level.TerrainAt(position);
                context.DrawRectangle(terrain == SokobanTerrain.Wall ? wall : floor,
                    new Pen(terrain == SokobanTerrain.Wall ? wallLine : floorLine, 1), rect, 3, 3);
                if (terrain == SokobanTerrain.Goal)
                {
                    var center = rect.Center;
                    var radius = layout.CellSize * 0.15;
                    context.DrawEllipse(null, new Pen(goal, Math.Max(2, layout.CellSize * 0.045)), center, radius, radius);
                }
            }
        }
    }

    private void DrawDynamicObjects(DrawingContext context, BoardLayout layout, SokobanGameSnapshot snapshot)
    {
        var moving = _animation is not null && _animationElapsed < SokobanAnimationPlan.MoveDuration;
        foreach (var box in snapshot.Boxes)
        {
            if (moving && _animation!.Move.BoxFrom == box)
            {
                continue;
            }

            DrawBox(context, layout, box, Game!.Game.Level.IsGoal(box));
        }

        if (!moving)
        {
            DrawPlayer(context, layout, snapshot.Player);
        }
    }

    private void DrawAnimation(DrawingContext context, BoardLayout layout)
    {
        if (_animation is null)
        {
            return;
        }

        if (_animationElapsed < SokobanAnimationPlan.MoveDuration)
        {
            var progress = _animation.GetMoveProgress(_animationElapsed);
            DrawPlayer(context, layout, Interpolate(_animation.Move.Before.Player, _animation.Move.After.Player, progress));
            if (_animation.Move.BoxFrom is { } from && _animation.Move.BoxTo is { } to)
            {
                DrawBox(context, layout, Interpolate(from, to, progress), isOnGoal: false);
            }

            return;
        }

        var pulse = _animation.GetCompletionPulse(_animationElapsed);
        if (pulse <= 0 || Game is null)
        {
            return;
        }

        var pen = new Pen(Brush("#FFFFD166", 0.3 + (pulse * 0.6)), Math.Max(2, layout.CellSize * 0.06));
        foreach (var box in Game.Game.Boxes.Where(Game.Game.Level.IsGoal))
        {
            var rect = CellRect(layout, box).Deflate(layout.CellSize * (0.12 - (pulse * 0.05)));
            context.DrawRectangle(null, pen, rect, 8, 8);
        }
    }

    private static void DrawBox(DrawingContext context, BoardLayout layout, SokobanPosition position, bool isOnGoal) =>
        DrawBox(context, layout, new GridPoint(position.Row, position.Column), isOnGoal);

    private static void DrawBox(DrawingContext context, BoardLayout layout, GridPoint position, bool isOnGoal)
    {
        var rect = CellRect(layout, position).Deflate(layout.CellSize * 0.12);
        var fill = Brush(isOnGoal ? "#FF43AA8B" : "#FFC9843C");
        var stroke = Brush(isOnGoal ? "#FF276B57" : "#FF7A451D");
        context.DrawRectangle(fill, new Pen(stroke, Math.Max(1.5, layout.CellSize * 0.04)), rect, 6, 6);
        var inset = rect.Deflate(layout.CellSize * 0.17);
        context.DrawLine(new Pen(stroke, Math.Max(1, layout.CellSize * 0.035)), inset.TopLeft, inset.BottomRight);
        context.DrawLine(new Pen(stroke, Math.Max(1, layout.CellSize * 0.035)), inset.TopRight, inset.BottomLeft);
    }

    private static void DrawPlayer(DrawingContext context, BoardLayout layout, SokobanPosition position) =>
        DrawPlayer(context, layout, new GridPoint(position.Row, position.Column));

    private static void DrawPlayer(DrawingContext context, BoardLayout layout, GridPoint position)
    {
        var center = Center(layout, position);
        var radius = layout.CellSize * 0.27;
        var fill = Brush("#FF4D96FF");
        var stroke = new Pen(Brush("#FF245AA5"), Math.Max(1.5, layout.CellSize * 0.04));
        context.DrawEllipse(fill, stroke, center, radius, radius);
        var eye = Brush("#FFFFFFFF");
        context.DrawEllipse(eye, null, center + new Vector(-radius * 0.35, -radius * 0.18), radius * 0.13, radius * 0.13);
        context.DrawEllipse(eye, null, center + new Vector(radius * 0.35, -radius * 0.18), radius * 0.13, radius * 0.13);
    }

    private static BoardLayout GetLayout(Size bounds, int width, int height, bool isDark = false)
    {
        const double padding = 12;
        var cellSize = Math.Max(1, Math.Min((bounds.Width - padding * 2) / width, (bounds.Height - padding * 2) / height));
        var board = new Rect(
            (bounds.Width - (cellSize * width)) / 2,
            (bounds.Height - (cellSize * height)) / 2,
            cellSize * width,
            cellSize * height);
        return new BoardLayout(board, cellSize, isDark);
    }

    private static Rect CellRect(BoardLayout layout, SokobanPosition position) =>
        new(layout.Board.X + (position.Column * layout.CellSize),
            layout.Board.Y + (position.Row * layout.CellSize), layout.CellSize, layout.CellSize);

    private static Rect CellRect(BoardLayout layout, GridPoint position) =>
        new(layout.Board.X + (position.Column * layout.CellSize),
            layout.Board.Y + (position.Row * layout.CellSize), layout.CellSize, layout.CellSize);

    private static Point Center(BoardLayout layout, GridPoint position) => CellRect(layout, position).Center;

    private static GridPoint Interpolate(SokobanPosition from, SokobanPosition to, double progress) =>
        new(from.Row + ((to.Row - from.Row) * progress),
            from.Column + ((to.Column - from.Column) * progress));

    private static SolidColorBrush Brush(string color, double opacity = 1) =>
        new(Color.Parse(color), opacity);

    private readonly record struct BoardLayout(Rect Board, double CellSize, bool IsDark);
    private readonly record struct GridPoint(double Row, double Column);
}

internal enum SokobanInputActionKind
{
    None,
    Move,
    Undo,
    Restart,
}

internal readonly record struct SokobanInputAction(SokobanInputActionKind Kind, SokobanDirection? Direction);
