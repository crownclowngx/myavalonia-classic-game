using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassicGamePlugin.Features.Sudoku.Domain;
using ClassicGamePlugin.Features.Sudoku.ViewModels;

namespace ClassicGamePlugin.Features.Sudoku.Views;

/// <summary>
/// 绘制数独棋盘并把局部指针、方向键和数字键转换为 ViewModel 意图。控件不判断冲突、答案或是否可编辑，
/// 因而不会形成第二套数独规则；所有输入仍由 ViewModel 和领域对局作最终验证。
/// </summary>
public sealed class SudokuBoardControl : Control
{
    private static readonly Typeface ValueTypeface =
        new("Microsoft YaHei UI", FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface GivenTypeface =
        new("Microsoft YaHei UI", FontStyle.Normal, FontWeight.Bold);
    private SudokuViewModel? _subscribedGame;
    private SudokuAnimationPlan? _animation;
    private DispatcherTimer? _animationTimer;
    private long _animationStarted;
    private SudokuPosition? _hoverPosition;
    private bool _isAttached;

    public static readonly StyledProperty<SudokuViewModel?> GameProperty =
        AvaloniaProperty.Register<SudokuBoardControl, SudokuViewModel?>(nameof(Game));

    public SudokuBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public SudokuViewModel? Game
    {
        get => GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty)
        {
            if (_isAttached)
            {
                SubscribeToGame(change.GetNewValue<SudokuViewModel?>());
            }

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
        StopAnimation();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var layout = GetLayout(Bounds.Size);
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var boardBrush = Brush(isDark ? "#FF202833" : "#FFF9FBFD");
        var lineBrush = Brush(isDark ? "#FF9AA8BA" : "#FF465568");
        context.DrawRectangle(boardBrush, new Pen(lineBrush, 3), layout.Board, 5, 5);

        if (Game is { } game)
        {
            DrawCells(context, layout, game, isDark);
            DrawCompletionWave(context, layout);
        }

        DrawGrid(context, layout, lineBrush);
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        var next = TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var position)
            ? position
            : (SudokuPosition?)null;
        if (_hoverPosition == next)
        {
            return;
        }

        _hoverPosition = next;
        ToolTip.SetTip(this, next is { } hover && Game is { } game
            ? game.BoardCells[SudokuRules.ToIndex(hover)].AccessibleText
            : null);
    }

    protected override void OnPointerExited(PointerEventArgs eventArgs)
    {
        base.OnPointerExited(eventArgs);
        _hoverPosition = null;
        ToolTip.SetTip(this, null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (Game is null || !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            !TryHitTest(Bounds.Size, eventArgs.GetPosition(this), out var position))
        {
            return;
        }

        // 明确声明这是指针导航取得的焦点，避免外层 ScrollViewer 在自动滚动后继续成为键盘焦点目标。
        Focus(NavigationMethod.Pointer);
        Game.SelectCell(position);
        eventArgs.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (Game is not { } game || !TryMapKey(eventArgs.Key, out var action))
        {
            return;
        }

        switch (action.Kind)
        {
            case SudokuKeyActionKind.Number:
                game.EnterNumberCommand.Execute(action.Number);
                break;
            case SudokuKeyActionKind.Clear:
                game.ClearSelectedCommand.Execute(null);
                break;
            case SudokuKeyActionKind.ToggleNotes:
                game.ToggleNotesCommand.Execute(null);
                break;
            case SudokuKeyActionKind.MoveSelection:
                game.MoveSelection(action.RowDelta, action.ColumnDelta);
                break;
            default:
                throw new InvalidOperationException("遇到了未知的数独键盘操作。");
        }

        eventArgs.Handled = true;
    }

    /// <summary>把控件坐标换算为行列；边界外坐标拒绝，不需要构造平台指针事件即可测试。</summary>
    internal static bool TryHitTest(Size bounds, Point point, out SudokuPosition position)
    {
        var layout = GetLayout(bounds);
        var relativeX = point.X - layout.Board.X;
        var relativeY = point.Y - layout.Board.Y;
        var column = (int)(relativeX / layout.CellSize);
        var row = (int)(relativeY / layout.CellSize);
        position = new SudokuPosition(row, column);
        return relativeX >= 0 && relativeY >= 0 &&
               relativeX < layout.Board.Width && relativeY < layout.Board.Height &&
               SudokuRules.IsInside(position);
    }

    /// <summary>纯键位映射；返回 false 的按键必须继续交给 Host 处理。</summary>
    internal static bool TryMapKey(Key key, out SudokuKeyAction action)
    {
        action = key switch
        {
            Key.D1 or Key.NumPad1 => SudokuKeyAction.NumberValue(1),
            Key.D2 or Key.NumPad2 => SudokuKeyAction.NumberValue(2),
            Key.D3 or Key.NumPad3 => SudokuKeyAction.NumberValue(3),
            Key.D4 or Key.NumPad4 => SudokuKeyAction.NumberValue(4),
            Key.D5 or Key.NumPad5 => SudokuKeyAction.NumberValue(5),
            Key.D6 or Key.NumPad6 => SudokuKeyAction.NumberValue(6),
            Key.D7 or Key.NumPad7 => SudokuKeyAction.NumberValue(7),
            Key.D8 or Key.NumPad8 => SudokuKeyAction.NumberValue(8),
            Key.D9 or Key.NumPad9 => SudokuKeyAction.NumberValue(9),
            Key.D0 or Key.NumPad0 or Key.Back or Key.Delete => SudokuKeyAction.Clear(),
            Key.N => SudokuKeyAction.ToggleNotes(),
            Key.Up => SudokuKeyAction.Move(-1, 0),
            Key.Down => SudokuKeyAction.Move(1, 0),
            Key.Left => SudokuKeyAction.Move(0, -1),
            Key.Right => SudokuKeyAction.Move(0, 1),
            _ => default,
        };
        return key is Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4 or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9 or
            Key.NumPad0 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3 or Key.NumPad4 or Key.NumPad5 or Key.NumPad6 or Key.NumPad7 or Key.NumPad8 or Key.NumPad9 or
            Key.Back or Key.Delete or Key.N or Key.Up or Key.Down or Key.Left or Key.Right;
    }

    internal void FinishAnimationImmediately() => StopAnimation();

    private void DrawCells(DrawingContext context, BoardLayout layout, SudokuViewModel game, bool isDark)
    {
        var elapsed = _animation is null
            ? TimeSpan.Zero
            : Stopwatch.GetElapsedTime(_animationStarted);
        foreach (var cell in game.BoardCells)
        {
            var position = new SudokuPosition(cell.Row, cell.Column);
            var bounds = CellBounds(layout, position);
            var fill = cell.IsConflict
                ? Brush(isDark ? "#FF713D45" : "#FFFFD8DC")
                : cell.IsSelected
                    ? Brush(isDark ? "#FF315D78" : "#FFCAE9FA")
                    : cell.HasSameValue
                        ? Brush(isDark ? "#FF344E61" : "#FFDCEFF9")
                        : cell.IsRelated
                            ? Brush(isDark ? "#FF2B3745" : "#FFEEF4F8")
                            : Brush(isDark ? "#FF202833" : "#FFF9FBFD");
            context.FillRectangle(fill, bounds);

            var offset = _animation is not null && _animation.Conflicts.Contains(position)
                ? _animation.GetConflictOffset(elapsed)
                : 0;
            var center = new Point(bounds.Center.X + offset, bounds.Center.Y);
            if (cell.HasValue)
            {
                var scale = _animation?.Target == position
                    ? _animation.GetPlacementScale(elapsed)
                    : 1;
                var valueBrush = cell.IsConflict
                    ? Brush("#FFE33D4E")
                    : cell.IsGiven
                        ? Brush(isDark ? "#FFF5F7FA" : "#FF1D2733")
                        : cell.IsHint
                            ? Brush(isDark ? "#FF6ED5A8" : "#FF16835D")
                            : Brush(isDark ? "#FF79BFF2" : "#FF176EA6");
                DrawCenteredText(
                    context,
                    cell.DisplayText,
                    center,
                    cell.IsGiven ? GivenTypeface : ValueTypeface,
                    layout.CellSize * 0.49 * scale,
                    valueBrush);
            }
            else
            {
                DrawNotes(context, bounds, cell, isDark, offset);
            }
        }
    }

    private static void DrawNotes(
        DrawingContext context,
        Rect bounds,
        SudokuCellViewModel cell,
        bool isDark,
        double horizontalOffset)
    {
        var noteBrush = Brush(isDark ? "#FFB7C1CF" : "#FF667487");
        var noteSize = bounds.Width * 0.18;
        for (var number = 1; number <= SudokuRules.BoardSize; number++)
        {
            if (!cell.HasNote(number))
            {
                continue;
            }

            var noteRow = (number - 1) / SudokuRules.BoxSize;
            var noteColumn = (number - 1) % SudokuRules.BoxSize;
            var center = new Point(
                bounds.X + horizontalOffset + ((noteColumn + 0.5) * bounds.Width / SudokuRules.BoxSize),
                bounds.Y + ((noteRow + 0.5) * bounds.Height / SudokuRules.BoxSize));
            DrawCenteredText(context, number.ToString(CultureInfo.InvariantCulture), center, ValueTypeface, noteSize, noteBrush);
        }
    }

    private void DrawCompletionWave(DrawingContext context, BoardLayout layout)
    {
        if (_animation is null)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(_animationStarted);
        for (var box = 0; box < 9; box++)
        {
            var intensity = _animation.GetCompletionIntensity(box, elapsed);
            if (intensity <= 0)
            {
                continue;
            }

            var boxRow = box / SudokuRules.BoxSize;
            var boxColumn = box % SudokuRules.BoxSize;
            var rect = new Rect(
                layout.Board.X + (boxColumn * layout.CellSize * SudokuRules.BoxSize),
                layout.Board.Y + (boxRow * layout.CellSize * SudokuRules.BoxSize),
                layout.CellSize * SudokuRules.BoxSize,
                layout.CellSize * SudokuRules.BoxSize);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb((byte)(80 * intensity), 55, 205, 135)), rect);
        }
    }

    private static void DrawGrid(DrawingContext context, BoardLayout layout, IBrush brush)
    {
        for (var index = 0; index <= SudokuRules.BoardSize; index++)
        {
            var thickness = index % SudokuRules.BoxSize == 0 ? 2.6 : 0.8;
            var offset = index * layout.CellSize;
            var pen = new Pen(brush, thickness);
            context.DrawLine(
                pen,
                new Point(layout.Board.X + offset, layout.Board.Y),
                new Point(layout.Board.X + offset, layout.Board.Bottom));
            context.DrawLine(
                pen,
                new Point(layout.Board.X, layout.Board.Y + offset),
                new Point(layout.Board.Right, layout.Board.Y + offset));
        }
    }

    private void SubscribeToGame(SudokuViewModel? game)
    {
        if (ReferenceEquals(_subscribedGame, game))
        {
            return;
        }

        if (_subscribedGame is not null)
        {
            _subscribedGame.PropertyChanged -= OnGamePropertyChanged;
            _subscribedGame.AnimationRequested -= OnAnimationRequested;
            _subscribedGame.AnimationCancellationRequested -= OnAnimationCancellationRequested;
        }

        StopAnimation();
        _subscribedGame = game;
        if (_subscribedGame is not null)
        {
            _subscribedGame.PropertyChanged += OnGamePropertyChanged;
            _subscribedGame.AnimationRequested += OnAnimationRequested;
            _subscribedGame.AnimationCancellationRequested += OnAnimationCancellationRequested;
        }
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) => InvalidateVisual();

    private void OnAnimationRequested(object? sender, SudokuAnimationPlan plan)
    {
        StopAnimation();
        _animation = plan;
        _animationStarted = Stopwatch.GetTimestamp();
        EnsureAnimationTimer().Start();
        InvalidateVisual();
    }

    private void OnAnimationCancellationRequested(object? sender, EventArgs eventArgs) => StopAnimation();

    private void OnAnimationTick(object? sender, EventArgs eventArgs)
    {
        if (_animation is null || _animation.IsComplete(Stopwatch.GetElapsedTime(_animationStarted)))
        {
            StopAnimation();
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
        InvalidateVisual();
    }

    private static BoardLayout GetLayout(Size bounds)
    {
        var side = Math.Max(270, Math.Min(bounds.Width, bounds.Height));
        return new BoardLayout(
            new Rect((bounds.Width - side) / 2, (bounds.Height - side) / 2, side, side),
            side / SudokuRules.BoardSize);
    }

    private static Rect CellBounds(BoardLayout layout, SudokuPosition position) =>
        new(
            layout.Board.X + (position.Column * layout.CellSize),
            layout.Board.Y + (position.Row * layout.CellSize),
            layout.CellSize,
            layout.CellSize);

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

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

    private readonly record struct BoardLayout(Rect Board, double CellSize);
}

internal enum SudokuKeyActionKind
{
    Number,
    Clear,
    ToggleNotes,
    MoveSelection,
}

internal readonly record struct SudokuKeyAction(
    SudokuKeyActionKind Kind,
    int Number,
    int RowDelta,
    int ColumnDelta)
{
    internal static SudokuKeyAction NumberValue(int number) => new(SudokuKeyActionKind.Number, number, 0, 0);
    internal static SudokuKeyAction Clear() => new(SudokuKeyActionKind.Clear, 0, 0, 0);
    internal static SudokuKeyAction ToggleNotes() => new(SudokuKeyActionKind.ToggleNotes, 0, 0, 0);
    internal static SudokuKeyAction Move(int rowDelta, int columnDelta) =>
        new(SudokuKeyActionKind.MoveSelection, 0, rowDelta, columnDelta);
}
