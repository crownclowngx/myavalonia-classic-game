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
using ClassicGamePlugin.Features.SpiderSolitaire.Domain;
using ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;

namespace ClassicGamePlugin.Features.SpiderSolitaire.Views;

/// <summary>
/// 蜘蛛纸牌的纯代码绘制与设备输入控件。它把领域快照布局成牌桌，负责点击、拖拽、命中测试和动画播放，
/// 但所有合法性判断与状态修改仍委托给 ViewModel/领域层，避免 View 复制规则。
/// </summary>
public sealed class SpiderBoardControl : Control
{
    private const double CardWidth = 68;
    private const double CardHeight = 94;
    private const double ColumnGap = 12;
    private const double TableMargin = 20;
    private const double TableauTop = 145;
    private const double DragThreshold = 6;
    private static readonly Typeface CardTypeface = new("Segoe UI", FontStyle.Normal, FontWeight.Bold);
    private readonly DispatcherTimer _animationTimer;
    private readonly DispatcherTimer _dragReturnTimer;
    private SpiderSolitaireViewModel? _subscribedGame;
    private SpiderAnimationPlan? _animation;
    private long _animationStarted;
    private (int Column, int? CardIndex)? _pressedHit;
    private Point _pressedPosition;
    private Point _dragPosition;
    private bool _isDragging;
    private bool _releasingCapture;
    private DragReturnState? _dragReturn;

    public static readonly StyledProperty<SpiderSolitaireViewModel?> GameProperty =
        AvaloniaProperty.Register<SpiderBoardControl, SpiderSolitaireViewModel?>(nameof(Game));

    public SpiderBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _animationTimer.Tick += OnAnimationTick;
        _dragReturnTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _dragReturnTimer.Tick += OnDragReturnTick;
    }

    public SpiderSolitaireViewModel? Game
    {
        get => GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != GameProperty)
        {
            return;
        }

        SubscribeToGame(change.GetNewValue<SpiderSolitaireViewModel?>());
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Game is null)
        {
            return;
        }

        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var palette = BoardPalette.Create(dark);
        context.DrawRectangle(palette.Table, null, new Rect(Bounds.Size));
        DrawHeader(context, _animation?.Transition.Before ?? Game.CurrentSnapshot, palette);

        if (_animation is not null)
        {
            DrawAnimatedSnapshot(context, palette);
        }
        else
        {
            DrawSnapshot(context, Game.CurrentSnapshot, palette);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (Game is null || Game.IsAnimationRunning ||
            !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        var position = eventArgs.GetPosition(this);
        if (GetStockRect().Contains(position))
        {
            Game.DealCommand.Execute(null);
            eventArgs.Handled = true;
            return;
        }

        _pressedHit = HitTestTableau(Game.CurrentSnapshot, position);
        if (_pressedHit is null)
        {
            return;
        }

        _pressedPosition = position;
        _dragPosition = position;
        eventArgs.Pointer.Capture(this);
        eventArgs.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (Game is null || _pressedHit is not { CardIndex: { } cardIndex } hit)
        {
            return;
        }

        var position = eventArgs.GetPosition(this);
        if (!_isDragging && Distance(position, _pressedPosition) >= DragThreshold &&
            Game.CanSelectSequence(hit.Column, cardIndex))
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            _dragPosition = position;
            InvalidateVisual();
            eventArgs.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (Game is null || _pressedHit is not { } hit)
        {
            ReleasePointer(eventArgs.Pointer);
            return;
        }

        if (_isDragging && hit.CardIndex is { } sourceIndex)
        {
            var destination = GetColumnAt(eventArgs.GetPosition(this));
            var moved = destination is { } destinationColumn &&
                Game.Move(hit.Column, sourceIndex, destinationColumn);
            if (!moved)
            {
                StartDragReturn(hit.Column, sourceIndex, eventArgs.GetPosition(this));
                Game.ReportInvalidDrop();
            }
        }
        else
        {
            Game.HandleColumnClick(hit.Column, hit.CardIndex);
        }

        ReleasePointer(eventArgs.Pointer);
        eventArgs.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs eventArgs)
    {
        base.OnPointerCaptureLost(eventArgs);
        if (!_releasingCapture)
        {
            CancelPointerGesture();
        }
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (Game is null)
        {
            return;
        }

        if (eventArgs.Key == Key.Escape)
        {
            CancelPointerGesture();
            Game.ClearSelection();
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.H)
        {
            Game.HintCommand.Execute(null);
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Z && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Game.UndoCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        StopAnimation();
        StopDragReturn();
        CancelPointerGesture();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    /// <summary>测试可直接验证拖拽阈值，不需要构造平台原生指针事件。</summary>
    internal static bool IsDragDistance(Point origin, Point current) =>
        Distance(origin, current) >= DragThreshold;

    private void SubscribeToGame(SpiderSolitaireViewModel? game)
    {
        if (_subscribedGame is not null)
        {
            _subscribedGame.PropertyChanged -= OnGamePropertyChanged;
            _subscribedGame.AnimationRequested -= OnAnimationRequested;
        }

        _subscribedGame = game;
        if (_subscribedGame is not null)
        {
            _subscribedGame.PropertyChanged += OnGamePropertyChanged;
            _subscribedGame.AnimationRequested += OnAnimationRequested;
        }
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs) =>
        InvalidateVisual();

    private void OnAnimationRequested(object? sender, SpiderAnimationPlan plan)
    {
        StopAnimation();
        _animation = plan;
        _animationStarted = Stopwatch.GetTimestamp();
        Game?.SetAnimationRunning(true);
        _animationTimer.Start();
        InvalidateVisual();
    }

    private void OnAnimationTick(object? sender, EventArgs eventArgs)
    {
        if (_animation is null ||
            Stopwatch.GetElapsedTime(_animationStarted) < _animation.TotalDuration)
        {
            InvalidateVisual();
            return;
        }

        StopAnimation();
        InvalidateVisual();
    }

    private void StopAnimation()
    {
        _animationTimer.Stop();
        _animation = null;
        Game?.SetAnimationRunning(false);
    }

    private void DrawHeader(
        DrawingContext context,
        SpiderGameSnapshot snapshot,
        BoardPalette palette)
    {
        var stockRect = GetStockRect();
        DrawSlot(context, stockRect, palette);
        if (snapshot.Stock.Count > 0)
        {
            DrawCardBack(context, stockRect, palette);
            DrawText(context, snapshot.Stock.Count.ToString(CultureInfo.InvariantCulture),
                new Point(stockRect.Right + 8, stockRect.Center.Y - 9), 14, palette.MutedText);
        }
        else
        {
            DrawText(context, "库存", new Point(stockRect.X + 18, stockRect.Y + 36), 13, palette.MutedText);
        }

        DrawText(context, "完成牌组", new Point(420, 20), 14, palette.MutedText);
        for (var index = 0; index < 8; index++)
        {
            var rect = GetFoundationRect(index);
            DrawSlot(context, rect, palette);
            if (index < snapshot.CompletedRuns.Count)
            {
                var king = snapshot.CompletedRuns[index][0];
                DrawCardFace(context, king, rect, palette, selected: false, hinted: false);
            }
        }
    }

    private void DrawSnapshot(
        DrawingContext context,
        SpiderGameSnapshot snapshot,
        BoardPalette palette)
    {
        var draggedIds = GetDraggedCardIds(snapshot);
        for (var columnIndex = 0; columnIndex < snapshot.Columns.Count; columnIndex++)
        {
            DrawColumnSlot(context, columnIndex, palette, IsDestinationHighlighted(columnIndex));
            var column = snapshot.Columns[columnIndex];
            var positions = GetColumnRects(columnIndex, column);
            for (var cardIndex = 0; cardIndex < column.Count; cardIndex++)
            {
                if (draggedIds.Contains(column[cardIndex].Id))
                {
                    continue;
                }

                DrawCard(context, column[cardIndex], positions[cardIndex], palette,
                    IsSelected(columnIndex, cardIndex), IsHinted(columnIndex, cardIndex));
            }
        }

        if (_isDragging && _pressedHit is { CardIndex: { } sourceIndex } hit)
        {
            var cards = snapshot.Columns[hit.Column].Skip(sourceIndex).ToArray();
            var y = _dragPosition.Y - 18;
            foreach (var card in cards)
            {
                var rect = new Rect(_dragPosition.X - (CardWidth / 2), y, CardWidth, CardHeight);
                DrawCard(context, card, rect, palette, selected: true, hinted: false);
                y += 26;
            }
        }
        else if (_dragReturn is { } returning)
        {
            DrawReturningCards(context, snapshot, palette, returning);
        }
    }

    private void DrawAnimatedSnapshot(DrawingContext context, BoardPalette palette)
    {
        var plan = _animation!;
        var elapsed = Stopwatch.GetElapsedTime(_animationStarted).TotalMilliseconds;
        var actionProgress = GetStageProgress(plan, plan.Stages[0].Kind, elapsed);
        var flipProgress = GetStageProgress(plan, SpiderAnimationStageKind.Flip, elapsed);
        var completeProgress = GetStageProgress(plan, SpiderAnimationStageKind.CompleteRun, elapsed);
        // 三次缓动让每个阶段末端自然减速，同时不引入独立动画框架。
        var easedAction = EaseOut(actionProgress);
        var easedComplete = EaseOut(completeProgress);
        var beforeRects = GetAllCardRects(plan.Transition.Before);
        var afterRects = GetAllCardRects(plan.Transition.After);

        for (var columnIndex = 0; columnIndex < 10; columnIndex++)
        {
            DrawColumnSlot(context, columnIndex, palette, highlighted: false);
            var column = plan.Transition.After.Columns[columnIndex];
            foreach (var card in column)
            {
                var target = afterRects[card.Id];
                var origin = beforeRects.GetValueOrDefault(card.Id, GetStockRect());
                var rect = Interpolate(origin, target, easedAction);
                var displayedCard = card;
                if (plan.Transition.FlippedCardIds.Contains(card.Id))
                {
                    var scale = Math.Abs((flipProgress * 2) - 1);
                    rect = new Rect(
                        rect.Center.X - ((rect.Width * scale) / 2),
                        rect.Y,
                        rect.Width * scale,
                        rect.Height);
                    displayedCard = card with { IsFaceUp = flipProgress >= 0.5 };
                }

                DrawCard(context, displayedCard, rect, palette, selected: false, hinted: false);
            }
        }

        // 完成组在动画末段汇入对应完成槽；最终静态画面只显示每组的 K。
        for (var runIndex = 0; runIndex < plan.Transition.After.CompletedRuns.Count; runIndex++)
        {
            var run = plan.Transition.After.CompletedRuns[runIndex];
            var target = GetFoundationRect(runIndex);
            if (run.Any(card => plan.Transition.CompletedCardIds.Contains(card.Id)) && completeProgress < 1)
            {
                foreach (var card in run)
                {
                    var origin = beforeRects.GetValueOrDefault(card.Id, target);
                    DrawCard(context, card, Interpolate(origin, target, easedComplete), palette, false, false);
                }
            }
            else
            {
                DrawCardFace(context, run[0], target, palette, false, false);
            }
        }
    }

    private void DrawColumnSlot(DrawingContext context, int column, BoardPalette palette, bool highlighted)
    {
        var rect = new Rect(GetColumnX(column), TableauTop, CardWidth, CardHeight);
        context.DrawRectangle(
            highlighted ? palette.ValidTarget : palette.EmptySlot,
            new Pen(highlighted ? palette.Selection : palette.SlotBorder, highlighted ? 3 : 1),
            rect,
            6,
            6);
    }

    private static void DrawSlot(DrawingContext context, Rect rect, BoardPalette palette) =>
        context.DrawRectangle(palette.EmptySlot, new Pen(palette.SlotBorder, 1), rect, 6, 6);

    private static void DrawCard(
        DrawingContext context,
        SpiderCardState card,
        Rect rect,
        BoardPalette palette,
        bool selected,
        bool hinted)
    {
        if (card.IsFaceUp)
        {
            DrawCardFace(context, card, rect, palette, selected, hinted);
        }
        else
        {
            DrawCardBack(context, rect, palette);
        }
    }

    private static void DrawCardFace(
        DrawingContext context,
        SpiderCardState card,
        Rect rect,
        BoardPalette palette,
        bool selected,
        bool hinted)
    {
        context.DrawRectangle(palette.Shadow, null, rect.Translate(new Vector(2, 3)), 6, 6);
        var border = selected ? palette.Selection : hinted ? palette.Hint : palette.CardBorder;
        context.DrawRectangle(palette.CardFace, new Pen(border, selected || hinted ? 3 : 1), rect, 6, 6);

        var suitBrush = card.Suit is SpiderCardSuit.Hearts or SpiderCardSuit.Diamonds
            ? palette.RedSuit
            : palette.BlackSuit;
        var rank = RankText(card.Rank);
        var suit = SuitText(card.Suit);
        DrawText(context, rank, new Point(rect.X + 6, rect.Y + 3), 17, suitBrush);
        DrawText(context, suit, new Point(rect.X + 7, rect.Y + 22), 16, suitBrush);
        DrawText(context, suit, new Point(rect.Center.X - 12, rect.Center.Y - 17), 30, suitBrush);
    }

    private static void DrawCardBack(DrawingContext context, Rect rect, BoardPalette palette)
    {
        context.DrawRectangle(palette.Shadow, null, rect.Translate(new Vector(2, 3)), 6, 6);
        context.DrawRectangle(palette.CardBackBorder, new Pen(palette.CardBorder, 1), rect, 6, 6);
        var inner = rect.Deflate(5);
        context.DrawRectangle(palette.CardBack, new Pen(palette.CardBackLine, 1), inner, 4, 4);
        for (var offset = 8d; offset < inner.Width + inner.Height; offset += 10)
        {
            var start = new Point(inner.X + Math.Max(0, offset - inner.Height), inner.Y + Math.Min(offset, inner.Height));
            var end = new Point(inner.X + Math.Min(offset, inner.Width), inner.Y + Math.Max(0, offset - inner.Width));
            context.DrawLine(new Pen(palette.CardBackLine, 1), start, end);
        }
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        Point origin,
        double fontSize,
        IBrush brush)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            CardTypeface,
            fontSize,
            brush);
        context.DrawText(formatted, origin);
    }

    private Dictionary<int, Rect> GetAllCardRects(SpiderGameSnapshot snapshot)
    {
        var result = new Dictionary<int, Rect>();
        foreach (var card in snapshot.Stock)
        {
            result[card.Id] = GetStockRect();
        }

        for (var column = 0; column < snapshot.Columns.Count; column++)
        {
            var cards = snapshot.Columns[column];
            var rects = GetColumnRects(column, cards);
            for (var index = 0; index < cards.Count; index++)
            {
                result[cards[index].Id] = rects[index];
            }
        }

        for (var run = 0; run < snapshot.CompletedRuns.Count; run++)
        {
            foreach (var card in snapshot.CompletedRuns[run])
            {
                result[card.Id] = GetFoundationRect(run);
            }
        }

        return result;
    }

    private static IReadOnlyList<Rect> GetColumnRects(
        int columnIndex,
        IReadOnlyList<SpiderCardState> cards)
    {
        var result = new List<Rect>(cards.Count);
        var y = TableauTop;
        for (var index = 0; index < cards.Count; index++)
        {
            result.Add(new Rect(GetColumnX(columnIndex), y, CardWidth, CardHeight));
            if (index < cards.Count - 1)
            {
                y += cards[index].IsFaceUp ? 26 : 13;
            }
        }

        return result;
    }

    private (int Column, int? CardIndex)? HitTestTableau(
        SpiderGameSnapshot snapshot,
        Point position)
    {
        var column = GetColumnAt(position);
        if (column is null || position.Y < TableauTop)
        {
            return null;
        }

        var cards = snapshot.Columns[column.Value];
        var rects = GetColumnRects(column.Value, cards);
        for (var index = cards.Count - 1; index >= 0; index--)
        {
            if (rects[index].Contains(position))
            {
                return (column.Value, index);
            }
        }

        return (column.Value, null);
    }

    private int? GetColumnAt(Point position)
    {
        if (position.X < TableMargin - (ColumnGap / 2) || position.Y < TableauTop)
        {
            return null;
        }

        var stride = CardWidth + ColumnGap;
        var column = (int)Math.Floor((position.X - TableMargin + (ColumnGap / 2)) / stride);
        return column is >= 0 and < 10 ? column : null;
    }

    private bool IsSelected(int column, int cardIndex) =>
        Game?.Selection is { } selection &&
        selection.Column == column && cardIndex >= selection.CardIndex;

    private bool IsHinted(int column, int cardIndex) =>
        Game?.CurrentHint is { Kind: SpiderHintKind.Move } hint &&
        ((hint.SourceColumn == column && cardIndex >= hint.SourceIndex) ||
         (hint.DestinationColumn == column && cardIndex == Game.CurrentSnapshot.Columns[column].Count - 1));

    private bool IsDestinationHighlighted(int column) =>
        _isDragging && _pressedHit is { CardIndex: { } cardIndex } hit
            ? Game?.CanMove(hit.Column, cardIndex, column) == true
            : Game?.Selection is not null && Game.IsLegalDestination(column);

    private HashSet<int> GetDraggedCardIds(SpiderGameSnapshot snapshot)
    {
        if (_dragReturn is { } returning)
        {
            return snapshot.Columns[returning.SourceColumn]
                .Skip(returning.SourceIndex)
                .Select(card => card.Id)
                .ToHashSet();
        }

        if (!_isDragging || _pressedHit is not { CardIndex: { } sourceIndex } hit)
        {
            return [];
        }

        return snapshot.Columns[hit.Column].Skip(sourceIndex).Select(card => card.Id).ToHashSet();
    }

    private void ReleasePointer(IPointer pointer)
    {
        _releasingCapture = true;
        pointer.Capture(null);
        _releasingCapture = false;
        CancelPointerGesture();
    }

    private void CancelPointerGesture()
    {
        _pressedHit = null;
        _isDragging = false;
        InvalidateVisual();
    }

    private void StartDragReturn(int sourceColumn, int sourceIndex, Point releasePosition)
    {
        StopDragReturn();
        _dragReturn = new DragReturnState(
            sourceColumn,
            sourceIndex,
            releasePosition,
            Stopwatch.GetTimestamp());
        _dragReturnTimer.Start();
    }

    private void OnDragReturnTick(object? sender, EventArgs eventArgs)
    {
        if (_dragReturn is not { } returning ||
            Stopwatch.GetElapsedTime(returning.StartedTimestamp) >= TimeSpan.FromMilliseconds(140))
        {
            StopDragReturn();
        }

        InvalidateVisual();
    }

    private void StopDragReturn()
    {
        _dragReturnTimer.Stop();
        _dragReturn = null;
    }

    private void DrawReturningCards(
        DrawingContext context,
        SpiderGameSnapshot snapshot,
        BoardPalette palette,
        DragReturnState returning)
    {
        var cards = snapshot.Columns[returning.SourceColumn].Skip(returning.SourceIndex).ToArray();
        var targetRects = GetColumnRects(
            returning.SourceColumn,
            snapshot.Columns[returning.SourceColumn]);
        var elapsed = Stopwatch.GetElapsedTime(returning.StartedTimestamp).TotalMilliseconds;
        var progress = Math.Clamp(elapsed / 140, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        for (var index = 0; index < cards.Length; index++)
        {
            var origin = new Rect(
                returning.ReleasePosition.X - (CardWidth / 2),
                returning.ReleasePosition.Y - 18 + (index * 26),
                CardWidth,
                CardHeight);
            var target = targetRects[returning.SourceIndex + index];
            DrawCard(context, cards[index], Interpolate(origin, target, eased), palette, true, false);
        }
    }

    private static Rect GetStockRect() => new(TableMargin, 20, CardWidth, CardHeight);
    private static Rect GetFoundationRect(int index) => new(420 + (index * 48), 48, 40, 56);
    private static double GetColumnX(int column) => TableMargin + (column * (CardWidth + ColumnGap));
    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));

    private static Rect Interpolate(Rect from, Rect to, double progress) =>
        new(
            from.X + ((to.X - from.X) * progress),
            from.Y + ((to.Y - from.Y) * progress),
            from.Width + ((to.Width - from.Width) * progress),
            from.Height + ((to.Height - from.Height) * progress));

    private static double GetStageProgress(
        SpiderAnimationPlan plan,
        SpiderAnimationStageKind requestedKind,
        double elapsedMilliseconds)
    {
        var stageStart = 0d;
        foreach (var stage in plan.Stages)
        {
            var stageEnd = stageStart + stage.Duration.TotalMilliseconds;
            if (stage.Kind == requestedKind)
            {
                return Math.Clamp(
                    (elapsedMilliseconds - stageStart) / Math.Max(1, stage.Duration.TotalMilliseconds),
                    0,
                    1);
            }

            stageStart = stageEnd;
        }

        // 没有该阶段表示不需要等待该效果，按已完成处理。
        return 1;
    }

    private static double EaseOut(double progress) => 1 - Math.Pow(1 - progress, 3);

    private static string RankText(int rank) => rank switch
    {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => rank.ToString(CultureInfo.InvariantCulture),
    };

    private static string SuitText(SpiderCardSuit suit) => suit switch
    {
        SpiderCardSuit.Spades => "♠",
        SpiderCardSuit.Hearts => "♥",
        SpiderCardSuit.Clubs => "♣",
        SpiderCardSuit.Diamonds => "♦",
        _ => throw new InvalidOperationException("遇到了未知花色。"),
    };

    private sealed record BoardPalette(
        IBrush Table,
        IBrush CardFace,
        IBrush CardBorder,
        IBrush Shadow,
        IBrush BlackSuit,
        IBrush RedSuit,
        IBrush CardBack,
        IBrush CardBackBorder,
        IBrush CardBackLine,
        IBrush EmptySlot,
        IBrush SlotBorder,
        IBrush Selection,
        IBrush Hint,
        IBrush ValidTarget,
        IBrush MutedText)
    {
        internal static BoardPalette Create(bool dark) => dark
            ? new(
                Brush.Parse("#303945"), Brush.Parse("#E8ECF1"), Brush.Parse("#667383"),
                Brush.Parse("#50000000"), Brush.Parse("#202733"), Brush.Parse("#B4232C"),
                Brush.Parse("#52677E"), Brush.Parse("#394B5F"), Brush.Parse("#8EA2B8"),
                Brush.Parse("#26313D"), Brush.Parse("#667383"), Brush.Parse("#69A7E8"),
                Brush.Parse("#F3C969"), Brush.Parse("#405A73"), Brush.Parse("#D0D8E2"))
            : new(
                Brush.Parse("#B7C0CB"), Brush.Parse("#FFFFFF"), Brush.Parse("#7D8998"),
                Brush.Parse("#38000000"), Brush.Parse("#253142"), Brush.Parse("#C62832"),
                Brush.Parse("#607A96"), Brush.Parse("#E7ECF1"), Brush.Parse("#B9C7D5"),
                Brush.Parse("#9EABB9"), Brush.Parse("#7D8998"), Brush.Parse("#246FB5"),
                Brush.Parse("#B7791F"), Brush.Parse("#AFC8DF"), Brush.Parse("#4B596A"));
    }

    private sealed record DragReturnState(
        int SourceColumn,
        int SourceIndex,
        Point ReleasePosition,
        long StartedTimestamp);
}
