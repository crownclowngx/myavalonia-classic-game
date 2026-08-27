using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.Tetris.Domain;
using ClassicGamePlugin.Features.Tetris.ViewModels;

namespace ClassicGamePlugin.Features.Tetris.Views;

/// <summary>
/// 使用 DrawingContext 绘制 10×20 棋盘、暂存、5 枚预览、幽灵块和短时动画，并把获得焦点后的局部键盘输入转换为
/// ViewModel 意图。控件只维持按键与视觉时间，不判断碰撞、SRS、锁定或计分，领域状态始终以 ViewModel 中的游戏为准。
/// </summary>
public sealed class TetrisBoardControl : Control
{
    internal static readonly TimeSpan DasDelay = TimeSpan.FromMilliseconds(150);
    internal static readonly TimeSpan ArrInterval = TimeSpan.FromMilliseconds(40);

    private readonly HashSet<Key> _pressedKeys = [];
    private DispatcherTimer? _timer;
    private TetrisViewModel? _subscribedGame;
    private TetrisAnimationPlan? _animation;
    private TimeSpan _animationElapsed;
    private long _lastTickTimestamp;
    private int _horizontalDirection;
    private TimeSpan _horizontalUntilRepeat;
    private bool _softDropHeld;
    private bool _isAttached;
    private Window? _topLevelWindow;

    public static readonly StyledProperty<TetrisViewModel?> GameProperty =
        AvaloniaProperty.Register<TetrisBoardControl, TetrisViewModel?>(nameof(Game));

    public TetrisBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
        LostFocus += OnControlLostFocus;
    }

    public TetrisViewModel? Game
    {
        get => GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    internal bool HasActiveAnimation => _animation is not null;
    internal bool IsTimerRunning => _timer?.IsEnabled == true;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty)
        {
            if (_isAttached)
            {
                SubscribeToGame(change.GetNewValue<TetrisViewModel?>());
            }

            UpdateAutomationName();
            InvalidateVisual();
        }
        else if (change.Property == IsVisibleProperty && _isAttached && !change.GetNewValue<bool>())
        {
            ClearHeldInput();
            Game?.PauseForLifecycle();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        _isAttached = true;
        SubscribeToGame(Game);
        _topLevelWindow = TopLevel.GetTopLevel(this) as Window;
        if (_topLevelWindow is not null)
        {
            _topLevelWindow.Deactivated += OnTopLevelDeactivated;
        }

        _lastTickTimestamp = Stopwatch.GetTimestamp();
        EnsureTimer().Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        _isAttached = false;
        if (_topLevelWindow is not null)
        {
            _topLevelWindow.Deactivated -= OnTopLevelDeactivated;
            _topLevelWindow = null;
        }

        _timer?.Stop();
        ClearHeldInput();
        Game?.PauseForLifecycle();
        SubscribeToGame(null);
        base.OnDetachedFromVisualTree(eventArgs);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var palette = Palette.Create(dark);
        context.DrawRectangle(palette.OuterBackground, null, new Rect(Bounds.Size), 8, 8);
        if (Game is not { } viewModel)
        {
            return;
        }

        var layout = GetLayout(Bounds.Size);
        DrawSidePanels(context, layout, viewModel.Game, palette);
        DrawBoardBackground(context, layout, palette);
        if (_animation is null)
        {
            DrawSettledCells(context, layout, viewModel.Game.Cells, palette);
            if (viewModel.Game.State != TetrisGameState.GameOver)
            {
                DrawPiece(context, layout, viewModel.Game.GetGhostPiece(), palette, ghost: true);
                DrawPiece(context, layout, viewModel.Game.ActivePiece, palette, rowOffset: viewModel.Loop.FallProgress);
            }
        }
        else
        {
            DrawAnimation(context, layout, palette);
        }

        if (viewModel.IsPaused || viewModel.IsGameOver)
        {
            DrawStateOverlay(context, layout, viewModel.IsGameOver ? "游戏结束" : "已暂停", palette);
        }
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

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (!TryMapInput(eventArgs.Key, eventArgs.KeyModifiers, out var action))
        {
            return;
        }

        eventArgs.Handled = true;
        if (!_pressedKeys.Add(eventArgs.Key))
        {
            return;
        }

        HandlePressedAction(action);
    }

    protected override void OnKeyUp(KeyEventArgs eventArgs)
    {
        base.OnKeyUp(eventArgs);
        if (!TryMapInput(eventArgs.Key, eventArgs.KeyModifiers, out var action))
        {
            return;
        }

        eventArgs.Handled = true;
        _pressedKeys.Remove(eventArgs.Key);
        if (action == TetrisInputAction.SoftDrop)
        {
            _softDropHeld = HasPressedSoftDropKey();
        }
        else if (action is TetrisInputAction.MoveLeft or TetrisInputAction.MoveRight)
        {
            RecalculateHorizontalDirection();
        }
    }

    private void OnControlLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs) =>
        ClearHeldInput();

    /// <summary>
    /// 纯键位映射只接受无修饰游戏键。Shift 本身作为暂存键是例外；其余组合键交还 Host，防止吞掉应用级快捷键。
    /// </summary>
    internal static bool TryMapInput(Key key, KeyModifiers modifiers, out TetrisInputAction action)
    {
        if (key is Key.LeftShift or Key.RightShift)
        {
            action = TetrisInputAction.Hold;
            return true;
        }

        if (modifiers != KeyModifiers.None)
        {
            action = TetrisInputAction.None;
            return false;
        }

        action = key switch
        {
            Key.Left or Key.A => TetrisInputAction.MoveLeft,
            Key.Right or Key.D => TetrisInputAction.MoveRight,
            Key.Down or Key.S => TetrisInputAction.SoftDrop,
            Key.Up or Key.X => TetrisInputAction.RotateClockwise,
            Key.Z => TetrisInputAction.RotateCounterClockwise,
            Key.Space => TetrisInputAction.HardDrop,
            Key.C => TetrisInputAction.Hold,
            Key.P => TetrisInputAction.TogglePause,
            _ => TetrisInputAction.None,
        };
        return action != TetrisInputAction.None;
    }

    internal static BoardLayout GetLayout(Size bounds)
    {
        const double padding = 12;
        const double sideWidth = 118;
        const double gap = 14;
        var availableBoardWidth = Math.Max(10, bounds.Width - (padding * 2) - (sideWidth * 2) - (gap * 2));
        var cell = Math.Max(1, Math.Min(
            availableBoardWidth / TetrisRules.BoardWidth,
            Math.Max(20, bounds.Height - (padding * 2)) / TetrisRules.VisibleHeight));
        var board = new Rect(
            (bounds.Width - (cell * TetrisRules.BoardWidth)) / 2,
            (bounds.Height - (cell * TetrisRules.VisibleHeight)) / 2,
            cell * TetrisRules.BoardWidth,
            cell * TetrisRules.VisibleHeight);
        return new BoardLayout(
            board,
            new Rect(board.X - gap - sideWidth, board.Y, sideWidth, Math.Min(150, board.Height)),
            new Rect(board.Right + gap, board.Y, sideWidth, board.Height),
            cell);
    }

    private void HandlePressedAction(TetrisInputAction action)
    {
        if (Game is not { } viewModel)
        {
            return;
        }

        switch (action)
        {
            case TetrisInputAction.MoveLeft:
                StartHorizontal(-1);
                viewModel.MoveLeft();
                break;
            case TetrisInputAction.MoveRight:
                StartHorizontal(1);
                viewModel.MoveRight();
                break;
            case TetrisInputAction.SoftDrop:
                _softDropHeld = true;
                viewModel.SoftDrop();
                break;
            case TetrisInputAction.RotateClockwise:
                viewModel.RotateClockwise();
                break;
            case TetrisInputAction.RotateCounterClockwise:
                viewModel.RotateCounterClockwise();
                break;
            case TetrisInputAction.HardDrop:
                viewModel.HardDrop();
                break;
            case TetrisInputAction.Hold:
                viewModel.Hold();
                break;
            case TetrisInputAction.TogglePause:
                ClearHeldInput();
                viewModel.TogglePause();
                break;
        }
    }

    private void OnTimerTick(object? sender, EventArgs eventArgs)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastTickTimestamp, now);
        _lastTickTimestamp = now;
        if (_animation is not null)
        {
            _animationElapsed += elapsed;
            if (_animation.IsComplete(_animationElapsed))
            {
                FinishAnimation();
            }
            else
            {
                InvalidateVisual();
            }

            return;
        }

        AdvanceHorizontalRepeat(elapsed);
        Game?.Advance(elapsed, _softDropHeld);
        InvalidateVisual();
    }

    private void AdvanceHorizontalRepeat(TimeSpan elapsed)
    {
        if (_horizontalDirection == 0 || Game is not { CanPlay: true } viewModel)
        {
            return;
        }

        _horizontalUntilRepeat -= elapsed;
        while (_horizontalUntilRepeat <= TimeSpan.Zero)
        {
            if (_horizontalDirection < 0)
            {
                viewModel.MoveLeft();
            }
            else
            {
                viewModel.MoveRight();
            }

            _horizontalUntilRepeat += ArrInterval;
        }
    }

    private void StartHorizontal(int direction)
    {
        _horizontalDirection = direction;
        _horizontalUntilRepeat = DasDelay;
    }

    private void RecalculateHorizontalDirection()
    {
        var left = _pressedKeys.Contains(Key.Left) || _pressedKeys.Contains(Key.A);
        var right = _pressedKeys.Contains(Key.Right) || _pressedKeys.Contains(Key.D);
        if (left == right)
        {
            _horizontalDirection = 0;
        }
        else
        {
            StartHorizontal(left ? -1 : 1);
        }
    }

    private bool HasPressedSoftDropKey() =>
        _pressedKeys.Contains(Key.Down) || _pressedKeys.Contains(Key.S);

    private void ClearHeldInput()
    {
        _pressedKeys.Clear();
        _horizontalDirection = 0;
        _horizontalUntilRepeat = TimeSpan.Zero;
        _softDropHeld = false;
    }

    private void SubscribeToGame(TetrisViewModel? game)
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

        _subscribedGame = game;
        _animation = null;
        _animationElapsed = TimeSpan.Zero;
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

    private void OnAnimationRequested(object? sender, TetrisAnimationPlan plan)
    {
        _animation = plan;
        _animationElapsed = TimeSpan.Zero;
        ClearHeldInput();
        InvalidateVisual();
    }

    private void OnAnimationCancellationRequested(object? sender, EventArgs eventArgs) => FinishAnimation();

    private void FinishAnimation()
    {
        var viewModel = _subscribedGame;
        _animation = null;
        _animationElapsed = TimeSpan.Zero;
        viewModel?.CompleteAnimation();
        InvalidateVisual();
    }

    private void OnTopLevelDeactivated(object? sender, EventArgs eventArgs)
    {
        ClearHeldInput();
        Game?.PauseForLifecycle();
    }

    private DispatcherTimer EnsureTimer()
    {
        if (_timer is not null)
        {
            return _timer;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTimerTick;
        return _timer;
    }

    private void UpdateAutomationName()
    {
        if (Game is { } game)
        {
            AutomationProperties.SetName(this, game.AccessibleBoardText);
        }
    }

    private void DrawBoardBackground(DrawingContext context, BoardLayout layout, Palette palette)
    {
        context.DrawRectangle(palette.BoardBackground, new Pen(palette.Border, 2), layout.Board, 3, 3);
        var gridPen = new Pen(palette.Grid, 1);
        for (var column = 1; column < TetrisRules.BoardWidth; column++)
        {
            var x = layout.Board.X + (column * layout.CellSize);
            context.DrawLine(gridPen, new Point(x, layout.Board.Y), new Point(x, layout.Board.Bottom));
        }

        for (var row = 1; row < TetrisRules.VisibleHeight; row++)
        {
            var y = layout.Board.Y + (row * layout.CellSize);
            context.DrawLine(gridPen, new Point(layout.Board.X, y), new Point(layout.Board.Right, y));
        }
    }

    private void DrawSettledCells(
        DrawingContext context,
        BoardLayout layout,
        IReadOnlyList<TetrominoType?> cells,
        Palette palette)
    {
        for (var row = TetrisRules.HiddenRows; row < TetrisRules.BoardHeight; row++)
        {
            for (var column = 0; column < TetrisRules.BoardWidth; column++)
            {
                if (cells[TetrisRules.ToIndex(row, column)] is { } type)
                {
                    DrawBlock(context, layout, row - TetrisRules.HiddenRows, column, type, palette);
                }
            }
        }
    }

    private void DrawAnimation(DrawingContext context, BoardLayout layout, Palette palette)
    {
        var plan = _animation!;
        var transition = plan.Transition;
        if (plan.HasHardDrop && _animationElapsed < TetrisAnimationPlan.HardDropDuration)
        {
            DrawSettledCells(context, layout, transition.BeforeCells, palette);
            var progress = plan.GetDropProgress(_animationElapsed);
            var row = transition.DropStartRow +
                      ((transition.LockedPiece.Row - transition.DropStartRow) * progress);
            DrawPiece(context, layout, transition.LockedPiece, palette, absoluteRow: row);
            return;
        }

        var locked = (TetrominoType?[])transition.BeforeCells.Clone();
        foreach (var position in TetrisRules.GetCells(transition.LockedPiece))
        {
            locked[TetrisRules.ToIndex(position.Row, position.Column)] = transition.LockedPiece.Type;
        }

        if (!plan.HasLineClear)
        {
            DrawSettledCells(context, layout, locked, palette);
            return;
        }

        var clearProgress = plan.GetClearProgress(_animationElapsed);
        var cleared = transition.ClearedRows.ToHashSet();
        for (var row = TetrisRules.HiddenRows; row < TetrisRules.BoardHeight; row++)
        {
            var clearedBelow = transition.ClearedRows.Count(clearedRow => clearedRow > row);
            var visualRow = row - TetrisRules.HiddenRows + (clearedBelow * clearProgress);
            for (var column = 0; column < TetrisRules.BoardWidth; column++)
            {
                if (locked[TetrisRules.ToIndex(row, column)] is not { } type)
                {
                    continue;
                }

                if (cleared.Contains(row) && clearProgress > 0.55)
                {
                    continue;
                }

                DrawBlock(context, layout, visualRow, column, type, palette);
            }
        }

        var flash = plan.GetClearFlash(_animationElapsed);
        foreach (var row in transition.ClearedRows.Where(row => row >= TetrisRules.HiddenRows))
        {
            var rect = new Rect(
                layout.Board.X,
                layout.Board.Y + ((row - TetrisRules.HiddenRows) * layout.CellSize),
                layout.Board.Width,
                layout.CellSize);
            context.DrawRectangle(new SolidColorBrush(Colors.White, flash * 0.75), null, rect);
        }
    }

    private void DrawPiece(
        DrawingContext context,
        BoardLayout layout,
        TetrisPiece piece,
        Palette palette,
        bool ghost = false,
        double rowOffset = 0,
        double? absoluteRow = null)
    {
        var baseRow = absoluteRow ?? piece.Row + rowOffset;
        foreach (var offset in TetrisRules.GetCells(piece with { Row = 0, Column = 0 }))
        {
            var visibleRow = baseRow + offset.Row - TetrisRules.HiddenRows;
            if (visibleRow <= -1 || visibleRow >= TetrisRules.VisibleHeight)
            {
                continue;
            }

            DrawBlock(context, layout, visibleRow, piece.Column + offset.Column, piece.Type, palette, ghost);
        }
    }

    private static void DrawBlock(
        DrawingContext context,
        BoardLayout layout,
        double visibleRow,
        int column,
        TetrominoType type,
        Palette palette,
        bool ghost = false)
    {
        var rect = new Rect(
            layout.Board.X + (column * layout.CellSize),
            layout.Board.Y + (visibleRow * layout.CellSize),
            layout.CellSize,
            layout.CellSize).Deflate(Math.Max(1, layout.CellSize * 0.06));
        var color = GetColor(type);
        var fill = new SolidColorBrush(color, ghost ? 0.17 : 1);
        var stroke = new SolidColorBrush(ghost ? color : palette.BlockStroke.Color, ghost ? 0.7 : 1);
        context.DrawRectangle(fill, new Pen(stroke, Math.Max(1, layout.CellSize * 0.045)), rect, 3, 3);
        if (!ghost)
        {
            var shine = new Pen(new SolidColorBrush(Colors.White, 0.35), Math.Max(1, layout.CellSize * 0.04));
            context.DrawLine(shine, rect.TopLeft + new Vector(2, 2), rect.TopRight + new Vector(-2, 2));
        }
    }

    private void DrawSidePanels(DrawingContext context, BoardLayout layout, TetrisGame game, Palette palette)
    {
        context.DrawRectangle(palette.Panel, new Pen(palette.Border, 1), layout.HoldPanel, 5, 5);
        context.DrawRectangle(palette.Panel, new Pen(palette.Border, 1), layout.NextPanel, 5, 5);
        DrawText(context, "暂存", layout.HoldPanel.TopLeft + new Vector(10, 8), 14, palette.Text);
        DrawText(context, "下一个", layout.NextPanel.TopLeft + new Vector(10, 8), 14, palette.Text);
        if (game.HeldPiece is { } held)
        {
            DrawPreviewPiece(context, held, new Point(layout.HoldPanel.X + 18, layout.HoldPanel.Y + 48), palette);
        }

        for (var index = 0; index < game.NextPieces.Count; index++)
        {
            DrawPreviewPiece(
                context,
                game.NextPieces[index],
                new Point(layout.NextPanel.X + 18, layout.NextPanel.Y + 43 + (index * 75)),
                palette);
        }
    }

    private static void DrawPreviewPiece(
        DrawingContext context,
        TetrominoType type,
        Point origin,
        Palette palette)
    {
        const double cell = 16;
        var cells = TetrisRules.GetCells(TetrisRules.CreateSpawnPiece(type));
        var minRow = cells.Min(position => position.Row);
        var minColumn = cells.Min(position => position.Column);
        foreach (var position in cells)
        {
            var rect = new Rect(
                origin.X + ((position.Column - minColumn) * cell),
                origin.Y + ((position.Row - minRow) * cell),
                cell,
                cell).Deflate(1);
            context.DrawRectangle(
                new SolidColorBrush(GetColor(type)),
                new Pen(palette.BlockStroke, 1),
                rect,
                2,
                2);
        }
    }

    private static void DrawStateOverlay(
        DrawingContext context,
        BoardLayout layout,
        string text,
        Palette palette)
    {
        context.DrawRectangle(new SolidColorBrush(Colors.Black, 0.62), null, layout.Board);
        var formatted = CreateText(text, 28, Brushes.White);
        context.DrawText(
            formatted,
            new Point(layout.Board.Center.X - (formatted.Width / 2), layout.Board.Center.Y - (formatted.Height / 2)));
    }

    private static void DrawText(DrawingContext context, string text, Point origin, double size, IBrush brush) =>
        context.DrawText(CreateText(text, size, brush), origin);

    private static FormattedText CreateText(string text, double size, IBrush brush) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush);

    private static Color GetColor(TetrominoType type) => type switch
    {
        TetrominoType.I => Color.Parse("#FF31C7E8"),
        TetrominoType.J => Color.Parse("#FF4169E1"),
        TetrominoType.L => Color.Parse("#FFF39A32"),
        TetrominoType.O => Color.Parse("#FFF0D83A"),
        TetrominoType.S => Color.Parse("#FF58B957"),
        TetrominoType.T => Color.Parse("#FF9B59C7"),
        TetrominoType.Z => Color.Parse("#FFE05252"),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    internal readonly record struct BoardLayout(Rect Board, Rect HoldPanel, Rect NextPanel, double CellSize);

    private readonly record struct Palette(
        IBrush OuterBackground,
        IBrush BoardBackground,
        IBrush Panel,
        IBrush Border,
        IBrush Grid,
        IBrush Text,
        SolidColorBrush BlockStroke)
    {
        internal static Palette Create(bool dark) => new(
            Brush(dark ? "#FF171E29" : "#FFE8EDF3"),
            Brush(dark ? "#FF0B1018" : "#FF18202B"),
            Brush(dark ? "#FF252E3C" : "#FFF8FAFC"),
            Brush(dark ? "#FF56667A" : "#FF9DAABC"),
            Brush(dark ? "#FF263141" : "#FF303C4B"),
            Brush(dark ? "#FFF0F4F8" : "#FF253244"),
            new SolidColorBrush(Color.Parse("#FF27313F")));

        private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
    }
}

internal enum TetrisInputAction
{
    None,
    MoveLeft,
    MoveRight,
    SoftDrop,
    RotateClockwise,
    RotateCounterClockwise,
    HardDrop,
    Hold,
    TogglePause,
}
