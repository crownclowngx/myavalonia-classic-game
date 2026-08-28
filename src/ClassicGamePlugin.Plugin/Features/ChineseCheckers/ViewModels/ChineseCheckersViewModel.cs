using System.Collections.ObjectModel;
using Avalonia.Threading;
using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassicGamePlugin.Features.ChineseCheckers.ViewModels;

/// <summary>
/// 编排中国跳棋的选择、命令、异步电脑、计时、记录和动画门闩。所有合法路径、强制撤营与胜负判断均委托给
/// <see cref="ChineseCheckersRules"/>；棋局版本保证已取消的五秒搜索不能写回新局。
/// </summary>
public sealed partial class ChineseCheckersViewModel : ObservableObject, IDisposable
{
    private ChineseCheckersGame _game = new();
    private readonly ChineseCheckersGameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private readonly IReadOnlyDictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy> _computerStrategies;
    private readonly IChineseCheckersMoveStrategy _hintStrategy;
    private CancellationTokenSource? _computerCancellation;
    private Task? _pendingComputerTask;
    private ChineseCheckersPosition? _selectedPosition;
    private IReadOnlyList<ChineseCheckersMove> _selectedMoves = [];
    private ChineseCheckersMove? _hintMove;
    private int _gameVersion;
    private bool _disposed;

    [ObservableProperty]
    private ChineseCheckersGameModeOption _selectedMode;

    [ObservableProperty]
    private ChineseCheckersDifficultyOption _selectedDifficulty;

    [ObservableProperty]
    private ChineseCheckersColorOption _selectedHumanColor;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private bool _isComputerThinking;

    [ObservableProperty]
    private bool _isAnimationRunning;

    [ObservableProperty]
    private bool _animationsEnabled = true;

    [ObservableProperty]
    private string _messageText = "蓝方先手，请选择一枚棋子";

    public ChineseCheckersViewModel()
        : this(
            TimeProvider.System,
            enableDisplayRefreshTimer: true,
            new Dictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy>
            {
                [ChineseCheckersAiDifficulty.Easy] = new RandomChineseCheckersMoveStrategy(Random.Shared),
                [ChineseCheckersAiDifficulty.Medium] = new StableChineseCheckersMoveStrategy(),
                [ChineseCheckersAiDifficulty.Hard] = new HardChineseCheckersMoveStrategy(),
            },
            new StableChineseCheckersMoveStrategy())
    {
    }

    internal ChineseCheckersViewModel(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy> computerStrategies,
        IChineseCheckersMoveStrategy? hintStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(computerStrategies);
        foreach (var difficulty in Enum.GetValues<ChineseCheckersAiDifficulty>())
        {
            if (!computerStrategies.ContainsKey(difficulty))
            {
                throw new ArgumentException("必须为三级中国跳棋电脑分别提供策略。", nameof(computerStrategies));
            }
        }

        _computerStrategies = computerStrategies;
        _hintStrategy = hintStrategy ?? new StableChineseCheckersMoveStrategy();
        _gameTimer = new ChineseCheckersGameTimer(timeProvider);
        ModeOptions =
        [
            new(ChineseCheckersGameMode.LocalTwoPlayer, "本地双人"),
            new(ChineseCheckersGameMode.HumanVsComputer, "人机对战"),
        ];
        DifficultyOptions =
        [
            new(ChineseCheckersAiDifficulty.Easy, "简单"),
            new(ChineseCheckersAiDifficulty.Medium, "中等"),
            new(ChineseCheckersAiDifficulty.Hard, "困难（最多 5 秒）"),
        ];
        HumanColorOptions =
        [
            new(ChineseCheckersSide.Blue, "玩家执蓝"),
            new(ChineseCheckersSide.Red, "玩家执红"),
        ];
        _selectedMode = ModeOptions[0];
        _selectedDifficulty = DifficultyOptions[1];
        _selectedHumanColor = HumanColorOptions[0];

        if (enableDisplayRefreshTimer)
        {
            _displayRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _displayRefreshTimer.Tick += OnDisplayRefreshTimerTick;
        }

        RefreshPresentation();
    }

    public IReadOnlyList<ChineseCheckersGameModeOption> ModeOptions { get; }
    public IReadOnlyList<ChineseCheckersDifficultyOption> DifficultyOptions { get; }
    public IReadOnlyList<ChineseCheckersColorOption> HumanColorOptions { get; }
    public ObservableCollection<ChineseCheckersHistoryItem> HistoryItems { get; } = [];
    public bool IsHumanVsComputer => SelectedMode.Definition == ChineseCheckersGameMode.HumanVsComputer;
    public bool IsBoardRotated => IsHumanVsComputer && HumanSide == ChineseCheckersSide.Red;
    public int MoveCount => _game.MoveCount;
    public int BlueGoalCount => ChineseCheckersRules.CountInGoal(_game.Snapshot, ChineseCheckersSide.Blue);
    public int RedGoalCount => ChineseCheckersRules.CountInGoal(_game.Snapshot, ChineseCheckersSide.Red);
    public string BlueProgressText => $"● 蓝方目标营 {BlueGoalCount}/10";
    public string RedProgressText => $"● 红方目标营 {RedGoalCount}/10";
    public string MoveCountText => $"共 {MoveCount} 手";
    public bool CanInteract => !_disposed && !IsComputerThinking && !IsAnimationRunning &&
        _game.State != ChineseCheckersGameState.Finished &&
        (!IsHumanVsComputer || _game.CurrentSide == HumanSide);
    public bool CanUndo => !_disposed && _game.CanUndo &&
        (!IsHumanVsComputer || _game.HasMoveBy(HumanSide));
    public bool CanHint => CanInteract;
    public string CurrentTurnText => _game.State == ChineseCheckersGameState.Finished
        ? "对局已结束"
        : $"{SideName(_game.CurrentSide)}方回合";
    public string StatusText => _game.State switch
    {
        ChineseCheckersGameState.Finished => $"{SideName(_game.Snapshot.Winner!.Value)}方获胜",
        _ when IsComputerThinking => "电脑思考中",
        _ when IsAnimationRunning => "棋子移动中",
        ChineseCheckersGameState.Ready => "准备开始",
        _ => "进行中",
    };
    public string ResultText => _game.Snapshot.TerminationReason switch
    {
        ChineseCheckersTerminationReason.GoalFilled => "十枚棋子已全部进入对角目标营",
        ChineseCheckersTerminationReason.BlockedHome => "对方起始营已无法完成强制撤营",
        _ => string.Empty,
    };

    internal ChineseCheckersGameSnapshot CurrentSnapshot => _game.Snapshot;
    internal ChineseCheckersPosition? SelectedPosition => _selectedPosition;
    internal IReadOnlyList<ChineseCheckersMove> SelectedMoves => _selectedMoves;
    internal ChineseCheckersMove? HintMove => _hintMove;
    internal ChineseCheckersGameState GameState => _game.State;
    internal ChineseCheckersSide CurrentSide => _game.CurrentSide;
    internal bool IsTimerRunning => _gameTimer.IsRunning;
    internal event EventHandler<ChineseCheckersAnimationPlan>? AnimationRequested;
    internal event EventHandler? AnimationCancellationRequested;
    internal Task WaitForComputerAsync() => _pendingComputerTask ?? Task.CompletedTask;

    /// <summary>接收自绘棋盘换算后的领域坐标；第一次选择棋子，第二次选择完整着法的最终落点。</summary>
    internal void SelectPosition(ChineseCheckersPosition position)
    {
        if (!CanInteract)
        {
            return;
        }

        if (_selectedPosition == position)
        {
            ClearSelection();
            MessageText = "已取消选择";
            RefreshPresentation();
            return;
        }

        if (_game.Snapshot.GetPiece(position) == _game.CurrentSide)
        {
            _selectedPosition = position;
            _selectedMoves = _game.GetLegalMoves().Where(move => move.From == position).ToArray();
            _hintMove = null;
            MessageText = _selectedMoves.Count == 0
                ? "该棋子当前没有合法落点"
                : $"已选择{SideName(_game.CurrentSide)}棋，可达 {_selectedMoves.Count} 个终点";
            RefreshPresentation();
            return;
        }

        var selectedMove = _selectedMoves.FirstOrDefault(move => move.To == position);
        if (selectedMove is null || _game.Move(selectedMove.From, selectedMove.To) is not { } result)
        {
            MessageText = "请选择高亮的合法终点";
            RefreshPresentation();
            return;
        }

        ClearSelection();
        ApplyMoveResult(result, isComputer: false);
    }

    internal string DescribePosition(ChineseCheckersPosition position)
    {
        var piece = _game.Snapshot.GetPiece(position);
        var camp = ChineseCheckersRules.BlueHome.Contains(position)
            ? "蓝方起始营"
            : ChineseCheckersRules.RedHome.Contains(position)
                ? "红方起始营"
                : "公共棋位";
        return piece is { } side ? $"{camp}，{SideName(side)}棋" : $"{camp}，空位";
    }

    [RelayCommand]
    private void Restart() => StartNewGame("已重新开始当前对局");

    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
    private void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        CancelComputerTurn();
        CancelAnimation();
        var undone = 0;
        if (_game.Undo() is not null)
        {
            undone++;
        }

        if (IsHumanVsComputer)
        {
            while (_game.CanUndo && _game.CurrentSide != HumanSide)
            {
                _game.Undo();
                undone++;
            }
        }

        ClearSelection();
        _hintMove = null;
        MessageText = IsHumanVsComputer
            ? $"撤销：已回退 {undone} 手，返回玩家决策点"
            : "撤销：已回退上一手";
        HistoryItems.Add(new ChineseCheckersHistoryItem(MessageText));
        SynchronizeTimerWithState();
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteHint))]
    private void Hint()
    {
        if (!CanHint)
        {
            return;
        }

        _hintMove = _hintStrategy.SelectMove(_game.Snapshot, _game.CurrentSide, CancellationToken.None);
        MessageText = _hintMove is { } move
            ? $"提示：建议从 {move.From.DisplayName} 移到 {move.To.DisplayName}"
            : "当前没有可提示的合法着法";
        RefreshPresentation();
    }

    partial void OnSelectedModeChanged(ChineseCheckersGameModeOption value) => StartNewGame("已切换对局模式");
    partial void OnSelectedDifficultyChanged(ChineseCheckersDifficultyOption value) => StartNewGame("已切换电脑难度");
    partial void OnSelectedHumanColorChanged(ChineseCheckersColorOption value) => StartNewGame("已切换玩家颜色");

    partial void OnAnimationsEnabledChanged(bool value)
    {
        if (!value)
        {
            CancelAnimation();
            QueueComputerTurnIfNeeded();
        }
    }

    /// <summary>由棋盘控件在动画自然完成或卸载时调用，随后才允许电脑开始下一回合。</summary>
    internal void CompleteAnimation()
    {
        if (!IsAnimationRunning)
        {
            return;
        }

        IsAnimationRunning = false;
        RefreshPresentation();
        QueueComputerTurnIfNeeded();
    }

    /// <summary>
    /// 棋盘离开视觉树或更换 DataContext 时取消视觉时间轴和后台搜索。已经提交的领域着法不会回滚；
    /// 再次激活时会依据当前回合重新排队电脑，避免旧 View 持有任务或新 View 卡在电脑回合。
    /// </summary>
    internal void DeactivateView()
    {
        CancelComputerTurn();
        CancelAnimation();
        RefreshPresentation();
    }

    internal void ActivateView() => QueueComputerTurnIfNeeded();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelComputerTurn();
        CancelAnimation();
        _displayRefreshTimer?.Stop();
        if (_displayRefreshTimer is not null)
        {
            _displayRefreshTimer.Tick -= OnDisplayRefreshTimerTick;
        }

        _gameTimer.Stop();
        RefreshElapsedTime();
    }

    internal void RefreshElapsedTime() => ElapsedSeconds = _gameTimer.ElapsedSeconds;

    private ChineseCheckersSide HumanSide => SelectedHumanColor.Definition;

    private void StartNewGame(string message)
    {
        if (_disposed)
        {
            return;
        }

        CancelComputerTurn();
        CancelAnimation();
        _game = new ChineseCheckersGame();
        _gameTimer.Reset();
        _displayRefreshTimer?.Stop();
        ElapsedSeconds = 0;
        HistoryItems.Clear();
        ClearSelection();
        _hintMove = null;
        MessageText = message;
        RefreshPresentation();
        QueueComputerTurnIfNeeded();
    }

    private void ApplyMoveResult(ChineseCheckersMoveResult result, bool isComputer)
    {
        StartTimerIfNeeded();
        _hintMove = null;
        var actor = isComputer ? $"电脑（{SideName(result.Side)}方）" : $"{SideName(result.Side)}方";
        var action = result.Move.Kind == ChineseCheckersMoveKind.Step
            ? "单步"
            : $"连续跳 {result.Move.Path.Count - 1} 段";
        MessageText = $"第 {_game.MoveCount} 手：{actor}{action}至 {result.Move.To.DisplayName}";
        HistoryItems.Add(new ChineseCheckersHistoryItem(MessageText));
        if (_game.State == ChineseCheckersGameState.Finished)
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
            var finish = $"对局结束：{SideName(_game.Snapshot.Winner!.Value)}方获胜，{ResultText}";
            HistoryItems.Add(new ChineseCheckersHistoryItem(finish));
            MessageText = finish;
        }

        PublishAnimation(result);
        RefreshPresentation();
        if (!IsAnimationRunning)
        {
            QueueComputerTurnIfNeeded();
        }
    }

    private void QueueComputerTurnIfNeeded()
    {
        if (_disposed || !IsHumanVsComputer || IsAnimationRunning || IsComputerThinking ||
            _game.State == ChineseCheckersGameState.Finished || _game.CurrentSide == HumanSide)
        {
            return;
        }

        var version = _gameVersion;
        _computerCancellation = new CancellationTokenSource();
        IsComputerThinking = true;
        RefreshPresentation();
        _pendingComputerTask = RunComputerTurnAsync(version, _computerCancellation.Token);
    }

    private async Task RunComputerTurnAsync(int version, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = _game.Snapshot;
            var side = snapshot.CurrentSide;
            var strategy = _computerStrategies[SelectedDifficulty.Definition];
            var move = await Task.Run(
                () => strategy.SelectMove(snapshot, side, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || version != _gameVersion)
            {
                return;
            }

            IsComputerThinking = false;
            if (move is null || _game.Move(move.From, move.To) is not { } result)
            {
                MessageText = "电脑未能返回合法着法，请重新开始对局";
                RefreshPresentation();
                return;
            }

            ApplyMoveResult(result, isComputer: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed && version == _gameVersion && IsComputerThinking)
            {
                IsComputerThinking = false;
                RefreshPresentation();
            }
        }
    }

    private void CancelComputerTurn()
    {
        _gameVersion++;
        _computerCancellation?.Cancel();
        _computerCancellation?.Dispose();
        _computerCancellation = null;
        _pendingComputerTask = null;
        IsComputerThinking = false;
    }

    private void PublishAnimation(ChineseCheckersMoveResult result)
    {
        if (!AnimationsEnabled || AnimationRequested is null)
        {
            IsAnimationRunning = false;
            return;
        }

        IsAnimationRunning = true;
        AnimationRequested.Invoke(this, new ChineseCheckersAnimationPlan(result));
    }

    private void CancelAnimation()
    {
        if (!IsAnimationRunning)
        {
            return;
        }

        AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
        IsAnimationRunning = false;
    }

    private void ClearSelection()
    {
        _selectedPosition = null;
        _selectedMoves = [];
    }

    private void StartTimerIfNeeded()
    {
        if (_gameTimer.IsRunning)
        {
            return;
        }

        _gameTimer.Start();
        _displayRefreshTimer?.Start();
    }

    private void SynchronizeTimerWithState()
    {
        if (_game.MoveCount > 0 && _game.State != ChineseCheckersGameState.Finished)
        {
            StartTimerIfNeeded();
        }
        else
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
        }

        RefreshElapsedTime();
    }

    private void RefreshPresentation()
    {
        RefreshElapsedTime();
        OnPropertyChanged(nameof(IsHumanVsComputer));
        OnPropertyChanged(nameof(IsBoardRotated));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(BlueGoalCount));
        OnPropertyChanged(nameof(RedGoalCount));
        OnPropertyChanged(nameof(BlueProgressText));
        OnPropertyChanged(nameof(RedProgressText));
        OnPropertyChanged(nameof(MoveCountText));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanHint));
        OnPropertyChanged(nameof(CurrentTurnText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(CurrentSnapshot));
        UndoCommand.NotifyCanExecuteChanged();
        HintCommand.NotifyCanExecuteChanged();
    }

    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();

    private bool CanExecuteUndo() => CanUndo;
    private bool CanExecuteHint() => CanHint;
    private static string SideName(ChineseCheckersSide side) =>
        side == ChineseCheckersSide.Blue ? "蓝" : "红";
}
