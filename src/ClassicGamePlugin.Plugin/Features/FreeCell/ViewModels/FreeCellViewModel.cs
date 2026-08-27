using System.ComponentModel;
using System.Security.Cryptography;
using Avalonia.Threading;
using ClassicGamePlugin.Features.FreeCell.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassicGamePlugin.Features.FreeCell.ViewModels;

/// <summary>
/// 编排空当接龙页面的编号生成、求解提示、命令、计时、选择与动画意图。领域合法性始终委托给
/// <see cref="FreeCellRules"/> / <see cref="FreeCellGame"/>；棋局版本与取消令牌共同阻止旧后台结果写回新局。
/// </summary>
public sealed partial class FreeCellViewModel : ObservableObject, IDisposable
{
    internal const int HintNodeLimit = 1_000_000;
    private readonly IFreeCellDealProvider _dealProvider;
    private readonly IFreeCellSolver _solver;
    private readonly FreeCellGameTimer _timer;
    private readonly DispatcherTimer? _displayTimer;
    private FreeCellGame _game;
    private FreeCellDeal _acceptedDeal;
    private CancellationTokenSource? _generationCancellation;
    private CancellationTokenSource? _hintCancellation;
    private Task? _pendingGenerationTask;
    private Task? _pendingHintTask;
    private int _gameVersion;
    private bool _disposed;
    private (FreeCellLocation Source, int CardIndex)? _selection;
    private FreeCellMove? _currentHint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    [NotifyPropertyChangedFor(nameof(CanCancelGeneration))]
    private bool _isGenerating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isHintThinking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isAnimationRunning;

    [ObservableProperty]
    private bool _isAutoCollectEnabled = true;

    [ObservableProperty]
    private bool _areAnimationsEnabled = true;

    [ObservableProperty]
    private string _dealNumberText = "1";

    [ObservableProperty]
    private string _messageText = "正在准备空当接龙牌局…";

    [ObservableProperty]
    private int _elapsedSeconds;

    public FreeCellViewModel()
        : this(new FreeCellDealProvider(), new FreeCellSolver(), TimeProvider.System, true)
    {
    }

    internal FreeCellViewModel(
        IFreeCellDealProvider dealProvider,
        IFreeCellSolver solver,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
    {
        _dealProvider = dealProvider ?? throw new ArgumentNullException(nameof(dealProvider));
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        _timer = new FreeCellGameTimer(timeProvider);
        _acceptedDeal = FreeCellDealProvider.CreateCandidate(1, 0);
        _game = new FreeCellGame(_acceptedDeal, autoCollect: true);
        if (enableDisplayRefreshTimer)
        {
            _displayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _displayTimer.Tick += (_, _) => RefreshElapsedTime();
        }
    }

    internal event EventHandler<FreeCellAnimationPlan>? AnimationRequested;
    internal FreeCellSnapshot CurrentSnapshot => _game.Current;
    internal FreeCellMove? CurrentHint => _currentHint;
    internal (FreeCellLocation Source, int CardIndex)? Selection => _selection;
    internal Task? PendingGenerationTask => _pendingGenerationTask;
    internal Task? PendingHintTask => _pendingHintTask;
    public int DealNumber => _game.Current.DealNumber;
    public int MoveCount => _game.Current.MoveCount;
    public int FoundationCardCount => _game.Current.FoundationCardCount;
    public bool IsWon => _game.Current.State == FreeCellGameState.Won;
    public bool CanInteract => !_disposed && !IsGenerating && !IsAnimationRunning;
    public bool CanCancelGeneration => !_disposed && IsGenerating;
    public bool CanUndo => CanInteract && _game.CanUndo;
    public bool CanHint => CanInteract && !IsHintThinking && !IsWon;
    public string StatusText => IsWon
        ? "已完成"
        : IsGenerating ? "正在生成可解牌局" : IsHintThinking ? "正在求解提示" : "进行中";
    public double BoardHeight => Math.Max(650, 205 + (_game.Current.Tableaus.Max(column => column.Count) * 31) + 116);

    internal async Task InitializeAsync(int number, CancellationToken cancellationToken) =>
        await LoadDealAsync(number, cancellationToken).ConfigureAwait(true);

    internal bool CanMove(FreeCellMove move) => CanInteract && _game.CanMove(move);

    internal bool CanSelect(FreeCellLocation source, int cardIndex)
    {
        if (!CanInteract)
        {
            return false;
        }

        return source.Kind switch
        {
            FreeCellLocationKind.Tableau => source.Index is >= 0 and < 8 &&
                FreeCellRules.IsDescendingAlternating(_game.Current.Tableaus[source.Index], cardIndex),
            FreeCellLocationKind.FreeCell => source.Index is >= 0 and < 4 &&
                _game.Current.FreeCells[source.Index] is not null,
            _ => false,
        };
    }

    internal bool Move(FreeCellMove move)
    {
        if (!CanInteract || !_game.CanMove(move))
        {
            return false;
        }

        CancelHint();
        _gameVersion++;
        var transition = _game.Move(move, IsAutoCollectEnabled);
        if (transition is null)
        {
            return false;
        }

        ApplyTransition(transition, "已移动牌组");
        return true;
    }

    internal void HandleClick(FreeCellLocation location, int? cardIndex)
    {
        if (!CanInteract)
        {
            return;
        }

        if (_selection is { } selection)
        {
            var move = new FreeCellMove(selection.Source, selection.CardIndex, location);
            if (_game.CanMove(move))
            {
                Move(move);
                return;
            }
        }

        if (cardIndex is { } index && CanSelect(location, index))
        {
            _selection = _selection == (location, index) ? null : (location, index);
            _currentHint = null;
            MessageText = _selection is null ? "已取消选择" : "已选择牌组，请选择目标位置";
            RefreshPresentation();
        }
        else
        {
            ClearSelection();
        }
    }

    internal bool MoveToFoundation(FreeCellLocation source, int cardIndex)
    {
        var card = source.Kind switch
        {
            FreeCellLocationKind.Tableau when source.Index is >= 0 and < 8 &&
                cardIndex == _game.Current.Tableaus[source.Index].Count - 1 =>
                _game.Current.Tableaus[source.Index][cardIndex],
            FreeCellLocationKind.FreeCell when source.Index is >= 0 and < 4 =>
                _game.Current.FreeCells[source.Index],
            _ => null,
        };
        return card is { } value && Move(new FreeCellMove(source, cardIndex, FreeCellLocation.Foundation(value.Suit)));
    }

    internal void ClearSelection()
    {
        if (_selection is null)
        {
            return;
        }

        _selection = null;
        RefreshPresentation();
    }

    internal void ReportInvalidDrop() => MessageText = "该位置不接受当前牌组，牌已返回原位";

    internal void SetAnimationRunning(bool value)
    {
        if (_disposed || IsAnimationRunning == value)
        {
            return;
        }

        IsAnimationRunning = value;
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task LoadDeal()
    {
        if (!int.TryParse(DealNumberText, out var number) || number <= 0)
        {
            MessageText = "牌局编号必须是 1 到 2147483647 之间的整数";
            return;
        }

        await LoadDealAsync(number, CancellationToken.None).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task NewRandomDeal()
    {
        var number = RandomNumberGenerator.GetInt32(1, int.MaxValue);
        DealNumberText = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await LoadDealAsync(number, CancellationToken.None).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCancelGeneration))]
    private void CancelGeneration() => _generationCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private void ReplaySameDeal()
    {
        CancelBackgroundWork();
        _gameVersion++;
        _game.Start(_acceptedDeal, IsAutoCollectEnabled);
        ResetRound("已使用相同牌序重新开始");
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!CanInteract)
        {
            return;
        }

        CancelHint();
        _gameVersion++;
        var transition = _game.Undo();
        if (transition is null)
        {
            MessageText = "当前没有可撤销的步骤";
            return;
        }

        if (_game.Current.State == FreeCellGameState.Won)
        {
            _timer.Stop();
            _displayTimer?.Stop();
        }
        else if (_game.Current.MoveCount > 0)
        {
            _timer.Start();
            _displayTimer?.Start();
        }
        else
        {
            _timer.Stop();
        }

        ApplyTransition(transition, "已撤销上一步");
    }

    [RelayCommand(CanExecute = nameof(CanHint))]
    private void Hint()
    {
        if (!CanInteract || IsHintThinking || IsWon)
        {
            return;
        }

        CancelHint();
        var version = _gameVersion;
        var snapshot = _game.Current;
        _hintCancellation = new CancellationTokenSource();
        IsHintThinking = true;
        MessageText = "正在求解当前局面…";
        RefreshPresentation();
        _pendingHintTask = RunHintAsync(snapshot, version, _hintCancellation.Token);
    }

    partial void OnIsAutoCollectEnabledChanged(bool value)
    {
        if (_disposed || !value || !CanInteract)
        {
            return;
        }

        CancelHint();
        _gameVersion++;
        if (_game.CollectSafeCards() is { } transition)
        {
            ApplyTransition(transition, "已开启并执行安全自动收牌");
        }
        else
        {
            MessageText = "已开启安全自动收牌";
        }
    }

    partial void OnAreAnimationsEnabledChanged(bool value)
    {
        if (!value)
        {
            IsAnimationRunning = false;
        }
    }

    internal void RefreshElapsedTime() => ElapsedSeconds = _timer.ElapsedSeconds;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelBackgroundWork();
        _displayTimer?.Stop();
        _timer.Stop();
        RefreshElapsedTime();
        RefreshPresentation();
    }

    private async Task LoadDealAsync(int number, CancellationToken externalCancellation)
    {
        if (_disposed || number <= 0)
        {
            return;
        }

        CancelBackgroundWork();
        var version = ++_gameVersion;
        _generationCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
        IsGenerating = true;
        MessageText = $"正在生成并验证牌局 {number}…";
        RefreshPresentation();
        _pendingGenerationTask = RunGenerationAsync(number, version, _generationCancellation.Token);
        await _pendingGenerationTask.ConfigureAwait(true);
    }

    private async Task RunGenerationAsync(int number, int version, CancellationToken cancellationToken)
    {
        try
        {
            var deal = await _dealProvider.CreateSolvableDealAsync(number, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || version != _gameVersion)
            {
                return;
            }

            _acceptedDeal = deal;
            _game.Start(deal, IsAutoCollectEnabled);
            DealNumberText = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ResetRound(deal.CandidateIndex == 0
                ? $"已载入可解牌局 {number}"
                : $"已载入可解牌局 {number}（候选 {deal.CandidateIndex + 1}）");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!_disposed && version == _gameVersion)
            {
                MessageText = "已取消生成，当前牌局保持不变";
            }
        }
        catch (InvalidOperationException exception)
        {
            if (!_disposed && version == _gameVersion)
            {
                MessageText = exception.Message;
            }
        }
        finally
        {
            if (!_disposed && version == _gameVersion)
            {
                IsGenerating = false;
                RefreshPresentation();
            }
        }
    }

    private async Task RunHintAsync(
        FreeCellSnapshot snapshot,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await Task.Run(
                () => _solver.Solve(snapshot, HintNodeLimit, cancellationToken),
                cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || version != _gameVersion)
            {
                return;
            }

            _currentHint = result.Status == FreeCellSolveStatus.Solved && result.Moves.Count > 0
                ? result.Moves[0]
                : null;
            MessageText = result.Status switch
            {
                FreeCellSolveStatus.Solved when result.Moves.Count > 0 => "提示：已高亮一条可通向胜利的下一步",
                FreeCellSolveStatus.Solved => "当前局面已经完成",
                FreeCellSolveStatus.Unsolvable => "求解器已证明当前局面无解，可以撤销或重开",
                FreeCellSolveStatus.NodeLimitReached => "未能在搜索上限内找到解，可以继续操作或稍后重试",
                _ => "当前没有可用提示",
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed && version == _gameVersion)
            {
                IsHintThinking = false;
                RefreshPresentation();
            }
        }
    }

    private void ApplyTransition(FreeCellTransition transition, string message)
    {
        _selection = null;
        _currentHint = null;
        if (!_timer.IsRunning && transition.After.MoveCount > 0 && transition.After.State != FreeCellGameState.Won)
        {
            _timer.Start();
            _displayTimer?.Start();
        }

        if (transition.After.State == FreeCellGameState.Won)
        {
            _timer.Stop();
            _displayTimer?.Stop();
            MessageText = "恭喜完成空当接龙";
        }
        else
        {
            MessageText = transition.AutoCollectedCardIds.Count > 0
                ? $"{message}，并安全收取 {transition.AutoCollectedCardIds.Count} 张牌"
                : message;
        }

        RefreshElapsedTime();
        RefreshPresentation();
        if (AreAnimationsEnabled && AnimationRequested is not null)
        {
            AnimationRequested.Invoke(this, FreeCellAnimationPlan.Create(transition));
        }
    }

    private void ResetRound(string message)
    {
        _timer.Reset();
        _displayTimer?.Stop();
        ElapsedSeconds = 0;
        _selection = null;
        _currentHint = null;
        MessageText = message;
        RefreshPresentation();
    }

    private void CancelHint()
    {
        _hintCancellation?.Cancel();
        _hintCancellation?.Dispose();
        _hintCancellation = null;
        IsHintThinking = false;
        _currentHint = null;
    }

    private void CancelBackgroundWork()
    {
        _generationCancellation?.Cancel();
        _generationCancellation?.Dispose();
        _generationCancellation = null;
        CancelHint();
        IsGenerating = false;
    }

    private void RefreshPresentation()
    {
        OnPropertyChanged(nameof(CurrentSnapshot));
        OnPropertyChanged(nameof(CurrentHint));
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(DealNumber));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(FoundationCardCount));
        OnPropertyChanged(nameof(IsWon));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanCancelGeneration));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanHint));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(BoardHeight));
        LoadDealCommand.NotifyCanExecuteChanged();
        NewRandomDealCommand.NotifyCanExecuteChanged();
        CancelGenerationCommand.NotifyCanExecuteChanged();
        ReplaySameDealCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        HintCommand.NotifyCanExecuteChanged();
    }
}
