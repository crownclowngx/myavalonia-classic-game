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
using ClassicGamePlugin.Features.FreeCell.Domain;
using ClassicGamePlugin.Features.FreeCell.ViewModels;

namespace ClassicGamePlugin.Features.FreeCell.Views;

/// <summary>
/// 八列牌桌的布局和输入协调控件。它保留 52 个稳定 <see cref="FreeCellCardControl"/> 子控件，
/// 把点击、双击、拖放和快捷键翻译为领域位置；目标是否合法仍由 ViewModel/领域层判断。
/// </summary>
public sealed class FreeCellBoardControl : Panel
{
    internal const double DragThreshold = 6;
    private const double CardWidth = 84;
    private const double CardHeight = 116;
    private const double HorizontalStride = 108;
    private const double TableMargin = 24;
    private const double HeaderTop = 24;
    private const double TableauTop = 165;
    private const double FaceUpOffset = 31;
    private readonly Dictionary<int, FreeCellCardControl> _cards = [];
    private readonly Border[] _freeCellSlots;
    private readonly Border[] _foundationSlots;
    private readonly Border[] _tableauSlots;
    private readonly DispatcherTimer _visualTimer;
    private FreeCellViewModel? _subscribedGame;
    private FreeCellAnimationPlan? _animation;
    private long _animationStarted;
    private PressedCard? _pressed;
    private Point _pressedPosition;
    private Point _dragPosition;
    private bool _isDragging;
    private bool _releasingCapture;
    private ReturnAnimation? _returnAnimation;

    public static readonly StyledProperty<FreeCellViewModel?> GameProperty =
        AvaloniaProperty.Register<FreeCellBoardControl, FreeCellViewModel?>(nameof(Game));

    public FreeCellBoardControl()
    {
        Focusable = true;
        ClipToBounds = true;
        AutomationProperties.SetName(this, "空当接龙牌桌，顶部为四个空闲单元和四个基础区，下方为八个牌列");
        _freeCellSlots = Enumerable.Range(0, 4)
            .Select(index => CreateSlot($"空闲单元 {index + 1}", "空闲"))
            .ToArray();
        _foundationSlots = Enum.GetValues<FreeCellSuit>()
            .Select(suit => CreateSlot($"{SuitSymbol(suit)}基础区", SuitSymbol(suit)))
            .ToArray();
        _tableauSlots = Enumerable.Range(0, 8)
            .Select(index => CreateSlot($"牌列 {index + 1}", (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        foreach (var slot in _freeCellSlots.Concat(_foundationSlots).Concat(_tableauSlots))
        {
            Children.Add(slot);
        }

        _visualTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _visualTimer.Tick += OnVisualTick;
    }

    public FreeCellViewModel? Game
    {
        get => GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    internal bool HasActiveAnimation => _animation is not null || _returnAnimation is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameProperty)
        {
            Subscribe(change.GetNewValue<FreeCellViewModel?>());
            SynchronizeCards();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(CardWidth, CardHeight));
        }

        return new Size(912, Math.Max(650, double.IsInfinity(availableSize.Height) ? 650 : availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Game is null)
        {
            return finalSize;
        }

        var snapshot = Game.CurrentSnapshot;
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        Background = new SolidColorBrush(Color.Parse(dark ? "#FF26313B" : "#FFB8C2CC"));
        var slotFill = new SolidColorBrush(Color.Parse(dark ? "#FF313E4B" : "#FFA4B0BC"));
        var slotBorder = new SolidColorBrush(Color.Parse(dark ? "#FF718092" : "#FF738292"));
        for (var index = 0; index < 4; index++)
        {
            ConfigureSlot(_freeCellSlots[index], slotFill, slotBorder, highlighted: false);
            ConfigureSlot(_foundationSlots[index], slotFill, slotBorder,
                IsLegalDrop(FreeCellLocation.Foundation((FreeCellSuit)index)));
            _freeCellSlots[index].Arrange(GetFreeCellRect(index));
            _foundationSlots[index].Arrange(GetFoundationRect(index));
        }

        for (var index = 0; index < 8; index++)
        {
            ConfigureSlot(_tableauSlots[index], slotFill, slotBorder,
                IsLegalDrop(FreeCellLocation.Tableau(index)));
            _tableauSlots[index].Arrange(GetTableauSlotRect(index));
        }

        var targetRects = GetCardRects(snapshot);
        var beforeRects = _animation is null ? null : GetCardRects(_animation.Transition.Before);
        var progress = _animation is null
            ? 1
            : EaseOut(Math.Clamp(
                Stopwatch.GetElapsedTime(_animationStarted).TotalMilliseconds /
                Math.Max(1, _animation.TotalDuration.TotalMilliseconds), 0, 1));
        var draggedIds = GetDraggedIds(snapshot);
        var returnProgress = _returnAnimation is null
            ? 1
            : EaseOut(Math.Clamp(
                Stopwatch.GetElapsedTime(_returnAnimation.Started).TotalMilliseconds / 120, 0, 1));

        foreach (var (id, control) in _cards)
        {
            if (!targetRects.TryGetValue(id, out var target))
            {
                control.Arrange(default);
                continue;
            }

            var rect = target;
            if (beforeRects is not null && beforeRects.TryGetValue(id, out var before))
            {
                rect = Interpolate(before, target, progress);
            }

            if (_isDragging && draggedIds.TryGetValue(id, out var dragOffset))
            {
                rect = new Rect(
                    _dragPosition.X - (CardWidth / 2),
                    _dragPosition.Y - 18 + dragOffset,
                    CardWidth,
                    CardHeight);
                control.ZIndex = 1000 + (int)dragOffset;
            }
            else if (_returnAnimation is { } returning && returning.CardOffsets.TryGetValue(id, out var offset))
            {
                var origin = new Rect(
                    returning.ReleasePosition.X - (CardWidth / 2),
                    returning.ReleasePosition.Y - 18 + offset,
                    CardWidth,
                    CardHeight);
                rect = Interpolate(origin, target, returnProgress);
                control.ZIndex = 900 + (int)offset;
            }

            control.Arrange(rect);
        }

        UpdateVisualStates();
        return finalSize;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (Game is null || !Game.CanInteract ||
            !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        var hit = HitTest(Game.CurrentSnapshot, eventArgs.GetPosition(this));
        if (hit is null)
        {
            return;
        }

        if (eventArgs.ClickCount >= 2 && hit.Value.CardIndex is { } doubleClickIndex &&
            Game.MoveToFoundation(hit.Value.Location, doubleClickIndex))
        {
            eventArgs.Handled = true;
            return;
        }

        _pressed = hit.Value.CardIndex is { } index
            ? new PressedCard(hit.Value.Location, index)
            : null;
        _pressedPosition = eventArgs.GetPosition(this);
        _dragPosition = _pressedPosition;
        eventArgs.Pointer.Capture(this);
        eventArgs.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (Game is null || _pressed is not { } pressed)
        {
            return;
        }

        var position = eventArgs.GetPosition(this);
        if (!_isDragging && IsDragDistance(_pressedPosition, position) &&
            Game.CanSelect(pressed.Location, pressed.CardIndex))
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            _dragPosition = position;
            InvalidateArrange();
            InvalidateVisual();
            eventArgs.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (Game is null)
        {
            Release(eventArgs.Pointer);
            return;
        }

        if (_pressed is { } pressed)
        {
            if (_isDragging)
            {
                var destination = GetDropLocation(eventArgs.GetPosition(this));
                var moved = destination is { } target &&
                    Game.Move(new FreeCellMove(pressed.Location, pressed.CardIndex, target));
                if (!moved)
                {
                    StartReturnAnimation(pressed, eventArgs.GetPosition(this));
                    Game.ReportInvalidDrop();
                }
            }
            else
            {
                Game.HandleClick(pressed.Location, pressed.CardIndex);
            }
        }
        else if (!_isDragging && HitTest(Game.CurrentSnapshot, eventArgs.GetPosition(this)) is { } target)
        {
            Game.HandleClick(target.Location, target.CardIndex);
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

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (Game is null)
        {
            return;
        }

        if (eventArgs.Key == Key.Escape)
        {
            CancelGesture();
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
        StopVisuals();
        Subscribe(null);
        base.OnDetachedFromVisualTree(eventArgs);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        base.OnAttachedToVisualTree(eventArgs);
        Subscribe(Game);
        SynchronizeCards();
    }

    internal static bool IsDragDistance(Point origin, Point current) =>
        Math.Sqrt(Math.Pow(current.X - origin.X, 2) + Math.Pow(current.Y - origin.Y, 2)) >= DragThreshold;

    internal static int? GetTableauColumnAt(Point point)
    {
        if (point.Y < TableauTop || point.X < TableMargin - 12)
        {
            return null;
        }

        var index = (int)Math.Floor((point.X - TableMargin + 12) / HorizontalStride);
        return index is >= 0 and < 8 ? index : null;
    }

    private void Subscribe(FreeCellViewModel? game)
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

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(FreeCellViewModel.AreAnimationsEnabled) &&
            Game?.AreAnimationsEnabled == false)
        {
            StopVisuals();
        }

        SynchronizeCards();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void OnAnimationRequested(object? sender, FreeCellAnimationPlan plan)
    {
        StopVisuals();
        _animation = plan;
        _animationStarted = Stopwatch.GetTimestamp();
        Game?.SetAnimationRunning(true);
        _visualTimer.Start();
        InvalidateArrange();
    }

    private void SynchronizeCards()
    {
        if (Game is null)
        {
            foreach (var control in _cards.Values)
            {
                Children.Remove(control);
            }
            _cards.Clear();
            return;
        }

        foreach (var card in EnumerateCards(Game.CurrentSnapshot))
        {
            if (!_cards.TryGetValue(card.Id, out var control))
            {
                control = new FreeCellCardControl();
                _cards.Add(card.Id, control);
                Children.Add(control);
            }

            control.Card = card;
        }

        InvalidateMeasure();
        InvalidateArrange();
    }

    private void UpdateVisualStates()
    {
        if (Game is null)
        {
            return;
        }

        var selectedIds = Game.Selection is { } selection
            ? GetSourceCardIds(Game.CurrentSnapshot, selection.Source, selection.CardIndex)
            : new HashSet<int>();
        var hintedIds = Game.CurrentHint is { } hint
            ? GetSourceCardIds(Game.CurrentSnapshot, hint.Source, hint.SourceCardIndex)
            : new HashSet<int>();
        var draggedIds = GetDraggedIds(Game.CurrentSnapshot).Keys.ToHashSet();
        foreach (var (id, card) in _cards)
        {
            card.IsSelected = selectedIds.Contains(id);
            card.IsHinted = hintedIds.Contains(id);
            card.IsDragged = draggedIds.Contains(id);
            card.InvalidateVisual();
        }
    }

    private Dictionary<int, Rect> GetCardRects(FreeCellSnapshot snapshot)
    {
        var result = new Dictionary<int, Rect>();
        for (var index = 0; index < snapshot.FreeCells.Count; index++)
        {
            if (snapshot.FreeCells[index] is { } card)
            {
                result[card.Id] = GetFreeCellRect(index);
            }
        }

        foreach (var suit in Enum.GetValues<FreeCellSuit>())
        {
            var top = snapshot.Foundations[(int)suit];
            for (var rank = 1; rank <= top; rank++)
            {
                var card = EnumerateCards(snapshot).First(value => value.Suit == suit && value.Rank == rank);
                result[card.Id] = GetFoundationRect((int)suit);
            }
        }

        for (var column = 0; column < snapshot.Tableaus.Count; column++)
        {
            for (var index = 0; index < snapshot.Tableaus[column].Count; index++)
            {
                result[snapshot.Tableaus[column][index].Id] = new Rect(
                    TableMargin + (column * HorizontalStride),
                    TableauTop + (index * FaceUpOffset),
                    CardWidth,
                    CardHeight);
            }
        }

        SetStableZOrder(snapshot);
        return result;
    }

    private void SetStableZOrder(FreeCellSnapshot snapshot)
    {
        foreach (var card in _cards.Values)
        {
            card.ZIndex = 0;
        }

        for (var column = 0; column < snapshot.Tableaus.Count; column++)
        {
            for (var index = 0; index < snapshot.Tableaus[column].Count; index++)
            {
                _cards[snapshot.Tableaus[column][index].Id].ZIndex = 10 + index;
            }
        }

        foreach (var suit in Enum.GetValues<FreeCellSuit>())
        {
            for (var rank = 1; rank <= snapshot.Foundations[(int)suit]; rank++)
            {
                var card = EnumerateCards(snapshot).First(value => value.Suit == suit && value.Rank == rank);
                _cards[card.Id].ZIndex = 10 + rank;
            }
        }
    }

    private HitResult? HitTest(FreeCellSnapshot snapshot, Point point)
    {
        for (var index = 0; index < 4; index++)
        {
            if (GetFreeCellRect(index).Contains(point))
            {
                return new HitResult(FreeCellLocation.Cell(index), snapshot.FreeCells[index] is null ? null : 0);
            }

            if (GetFoundationRect(index).Contains(point))
            {
                return new HitResult(FreeCellLocation.Foundation((FreeCellSuit)index), null);
            }
        }

        var column = GetTableauColumnAt(point);
        if (column is null)
        {
            return null;
        }

        var cards = snapshot.Tableaus[column.Value];
        for (var index = cards.Count - 1; index >= 0; index--)
        {
            var rect = new Rect(
                TableMargin + (column.Value * HorizontalStride),
                TableauTop + (index * FaceUpOffset),
                CardWidth,
                CardHeight);
            if (rect.Contains(point))
            {
                return new HitResult(FreeCellLocation.Tableau(column.Value), index);
            }
        }

        return new HitResult(FreeCellLocation.Tableau(column.Value), null);
    }

    private FreeCellLocation? GetDropLocation(Point point) =>
        HitTest(Game!.CurrentSnapshot, point)?.Location;

    private bool IsLegalDrop(FreeCellLocation destination) =>
        Game is not null && _isDragging && _pressed is { } pressed &&
        Game.CanMove(new FreeCellMove(pressed.Location, pressed.CardIndex, destination));

    private Dictionary<int, double> GetDraggedIds(FreeCellSnapshot snapshot)
    {
        if (!_isDragging || _pressed is not { } pressed)
        {
            return [];
        }

        return GetSourceCardIds(snapshot, pressed.Location, pressed.CardIndex)
            .Select((id, index) => (id, offset: index * FaceUpOffset))
            .ToDictionary(value => value.id, value => value.offset);
    }

    private static HashSet<int> GetSourceCardIds(
        FreeCellSnapshot snapshot,
        FreeCellLocation source,
        int cardIndex)
    {
        return source.Kind switch
        {
            FreeCellLocationKind.Tableau when source.Index is >= 0 and < 8 =>
                snapshot.Tableaus[source.Index].Skip(cardIndex).Select(card => card.Id).ToHashSet(),
            FreeCellLocationKind.FreeCell when source.Index is >= 0 and < 4 &&
                snapshot.FreeCells[source.Index] is { } card => [card.Id],
            _ => [],
        };
    }

    private void StartReturnAnimation(PressedCard pressed, Point releasePosition)
    {
        var offsets = GetSourceCardIds(Game!.CurrentSnapshot, pressed.Location, pressed.CardIndex)
            .Select((id, index) => (id, offset: index * FaceUpOffset))
            .ToDictionary(value => value.id, value => value.offset);
        _returnAnimation = new ReturnAnimation(releasePosition, Stopwatch.GetTimestamp(), offsets);
        _visualTimer.Start();
    }

    private void OnVisualTick(object? sender, EventArgs eventArgs)
    {
        if (_animation is not null && Stopwatch.GetElapsedTime(_animationStarted) >= _animation.TotalDuration)
        {
            _animation = null;
            Game?.SetAnimationRunning(false);
        }

        if (_returnAnimation is not null &&
            Stopwatch.GetElapsedTime(_returnAnimation.Started) >= TimeSpan.FromMilliseconds(120))
        {
            _returnAnimation = null;
        }

        if (_animation is null && _returnAnimation is null)
        {
            _visualTimer.Stop();
        }

        InvalidateArrange();
        InvalidateVisual();
    }

    private void StopVisuals()
    {
        _visualTimer.Stop();
        _animation = null;
        _returnAnimation = null;
        Game?.SetAnimationRunning(false);
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
        _pressed = null;
        _isDragging = false;
        InvalidateArrange();
        InvalidateVisual();
    }

    private static IEnumerable<FreeCellCard> EnumerateCards(FreeCellSnapshot snapshot) =>
        snapshot.Tableaus.SelectMany(column => column)
            .Concat(snapshot.FreeCells.Where(card => card is not null).Select(card => card!.Value))
            .Concat(Enum.GetValues<FreeCellSuit>().SelectMany(suit =>
                Enumerable.Range(1, snapshot.Foundations[(int)suit])
                    .Select(rank => new FreeCellCard(((int)suit * 13) + rank - 1, suit, rank))))
            .DistinctBy(card => card.Id);

    private static Rect GetFreeCellRect(int index) =>
        new(TableMargin + (index * 100), HeaderTop, CardWidth, CardHeight);

    private static Rect GetFoundationRect(int index) =>
        new(504 + (index * 100), HeaderTop, CardWidth, CardHeight);

    private static Rect GetTableauSlotRect(int index) =>
        new(TableMargin + (index * HorizontalStride), TableauTop, CardWidth, CardHeight);

    private static string SuitSymbol(FreeCellSuit suit) => suit switch
    {
        FreeCellSuit.Spades => "♠ 基础",
        FreeCellSuit.Hearts => "♥ 基础",
        FreeCellSuit.Clubs => "♣ 基础",
        FreeCellSuit.Diamonds => "♦ 基础",
        _ => string.Empty,
    };

    private static Rect Interpolate(Rect from, Rect to, double progress) => new(
        from.X + ((to.X - from.X) * progress),
        from.Y + ((to.Y - from.Y) * progress),
        CardWidth,
        CardHeight);

    private static double EaseOut(double value) => 1 - Math.Pow(1 - value, 3);

    private static Border CreateSlot(string accessibleName, string displayText)
    {
        var slot = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = 14,
                Text = displayText,
            },
        };
        AutomationProperties.SetName(slot, accessibleName);
        ToolTip.SetTip(slot, accessibleName);
        slot.ZIndex = -10;
        return slot;
    }

    private static void ConfigureSlot(Border slot, IBrush fill, IBrush border, bool highlighted)
    {
        slot.Background = highlighted ? new SolidColorBrush(Color.Parse("#334EA1F3")) : fill;
        slot.BorderBrush = highlighted ? new SolidColorBrush(Color.Parse("#FF4EA1F3")) : border;
        slot.BorderThickness = new Thickness(highlighted ? 3 : 1);
        if (slot.Child is TextBlock text)
        {
            text.Foreground = border;
        }
    }

    private readonly record struct HitResult(FreeCellLocation Location, int? CardIndex);
    private readonly record struct PressedCard(FreeCellLocation Location, int CardIndex);
    private sealed record ReturnAnimation(
        Point ReleasePosition,
        long Started,
        IReadOnlyDictionary<int, double> CardOffsets);
}
