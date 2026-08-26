using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Reversi.Domain;

namespace ClassicGamePlugin.Features.Reversi.ViewModels;

/// <summary>
/// 承担黑白棋页面的命令、异步人机流程、计时和展示投影。棋盘规则全部委托给
/// <see cref="ReversiGame"/>，本类型不自行判断夹取方向或胜负。
/// </summary>
public sealed partial class ReversiViewModel : ObservableObject, IDisposable
{
    private readonly ReversiGame _game = new();
    private readonly ReversiGameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private readonly IReadOnlyDictionary<ReversiAiDifficulty, IReversiMoveStrategy> _computerStrategies;
    private readonly IReversiMoveStrategy _hintStrategy;
    private CancellationTokenSource? _computerCancellation;
    private Task? _pendingComputerTask;
    private ReversiPosition? _hintPosition;
    private int _gameVersion;
    private bool _disposed;

    [ObservableProperty]
    private ReversiGameModeOption _selectedMode;

    [ObservableProperty]
    private ReversiDifficultyOption _selectedDifficulty;

    [ObservableProperty]
    private ReversiColorOption _selectedHumanColor;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private bool _isComputerThinking;

    [ObservableProperty]
    private string _messageText = "黑方先手，请选择带有落子提示的位置";

    /// <summary>使用系统时间和生产 AI 策略创建页面 ViewModel。</summary>
    public ReversiViewModel()
        : this(
            TimeProvider.System,
            enableDisplayRefreshTimer: true,
            new Dictionary<ReversiAiDifficulty, IReversiMoveStrategy>
            {
                [ReversiAiDifficulty.Easy] = new RandomReversiMoveStrategy(Random.Shared),
                [ReversiAiDifficulty.Medium] = new StableReversiMoveStrategy(),
                [ReversiAiDifficulty.Hard] = new HardReversiMoveStrategy(),
            },
            new StableReversiMoveStrategy())
    {
    }

    /// <summary>使用可控时间和策略创建 ViewModel，保证测试不依赖墙钟或随机电脑。</summary>
    internal ReversiViewModel(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<ReversiAiDifficulty, IReversiMoveStrategy> computerStrategies,
        IReversiMoveStrategy? hintStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(computerStrategies);
        foreach (var difficulty in Enum.GetValues<ReversiAiDifficulty>())
        {
            if (!computerStrategies.ContainsKey(difficulty))
            {
                throw new ArgumentException("必须为三级电脑难度分别提供落子策略。", nameof(computerStrategies));
            }
        }

        _computerStrategies = computerStrategies;
        _hintStrategy = hintStrategy ?? new StableReversiMoveStrategy();
        _gameTimer = new ReversiGameTimer(timeProvider);
        ModeOptions =
        [
            new(ReversiGameMode.LocalTwoPlayer, "本地双人"),
            new(ReversiGameMode.HumanVsComputer, "人机对战"),
        ];
        DifficultyOptions =
        [
            new(ReversiAiDifficulty.Easy, "简单"),
            new(ReversiAiDifficulty.Medium, "中等"),
            new(ReversiAiDifficulty.Hard, "困难"),
        ];
        HumanColorOptions =
        [
            new(ReversiDiscColor.Black, "玩家执黑"),
            new(ReversiDiscColor.White, "玩家执白"),
        ];
        _selectedMode = ModeOptions[0];
        _selectedDifficulty = DifficultyOptions[1];
        _selectedHumanColor = HumanColorOptions[0];

        for (var row = 0; row < ReversiRules.BoardSize; row++)
        {
            for (var column = 0; column < ReversiRules.BoardSize; column++)
            {
                BoardCells.Add(new ReversiCellViewModel(new ReversiPosition(row, column)));
            }
        }

        if (enableDisplayRefreshTimer)
        {
            _displayRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            _displayRefreshTimer.Tick += OnDisplayRefreshTimerTick;
        }

        RefreshPresentation();
    }

    public IReadOnlyList<ReversiGameModeOption> ModeOptions { get; }
    public IReadOnlyList<ReversiDifficultyOption> DifficultyOptions { get; }
    public IReadOnlyList<ReversiColorOption> HumanColorOptions { get; }
    public ObservableCollection<ReversiCellViewModel> BoardCells { get; } = [];
    public ObservableCollection<ReversiHistoryItem> HistoryItems { get; } = [];
    public int BlackCount => _game.BlackCount;
    public int WhiteCount => _game.WhiteCount;
    public string BlackCountText => $"● 黑 {BlackCount}";
    public string WhiteCountText => $"○ 白 {WhiteCount}";
    public int MoveCount => _game.MoveCount;
    public bool IsHumanVsComputer => SelectedMode.Definition == ReversiGameMode.HumanVsComputer;
    public bool CanInteract => !_disposed && !IsComputerThinking &&
        _game.State != ReversiGameState.Finished &&
        (!IsHumanVsComputer || _game.CurrentPlayer == HumanColor);
    public bool CanUndo => !_disposed && !IsComputerThinking &&
        (IsHumanVsComputer ? _game.HasMoveBy(HumanColor) : _game.CanUndo);
    public bool CanHint => CanInteract && _game.GetLegalMoves().Count > 0;
    public string CurrentTurnText => _game.State == ReversiGameState.Finished
        ? "对局已结束"
        : $"{ColorName(_game.CurrentPlayer)}方回合";
    public string StatusText => _game.State switch
    {
        ReversiGameState.Finished when _game.Winner is { } winner => $"{ColorName(winner)}方获胜",
        ReversiGameState.Finished => "双方平局",
        _ when IsComputerThinking => "电脑思考中",
        ReversiGameState.Ready => "准备开始",
        _ => "进行中",
    };
    public string ResultText => _game.State == ReversiGameState.Finished
        ? $"最终比分 黑 {_game.BlackCount}：白 {_game.WhiteCount}"
        : string.Empty;

    internal ReversiGameState GameState => _game.State;
    internal ReversiDiscColor CurrentPlayer => _game.CurrentPlayer;
    internal bool IsTimerRunning => _gameTimer.IsRunning;
    internal Task WaitForComputerAsync() => _pendingComputerTask ?? Task.CompletedTask;

    /// <summary>接收 View 转译后的格子点击；不属于当前棋盘或当前不可操作时直接忽略。</summary>
    public void PlayCell(ReversiCellViewModel? cell)
    {
        if (cell is null || !BoardCells.Contains(cell) || !CanInteract)
        {
            return;
        }

        var result = _game.PlaceDisc(cell.Position);
        if (result is null)
        {
            MessageText = $"{cell.Coordinate} 不是当前回合的合法位置";
            return;
        }

        ApplyMoveResult(result, isComputer: false);
        QueueComputerTurnsIfNeeded();
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

        var wasFinished = _game.State == ReversiGameState.Finished;
        var undoneCount = 0;
        if (!IsHumanVsComputer)
        {
            undoneCount = _game.Undo() is null ? 0 : 1;
        }
        else
        {
            // 人机模式不是机械地退一手，而是一直恢复到上一个“轮到玩家选择”的快照。
            // 这同时覆盖电脑因玩家跳过而连续行动多次的情况。
            do
            {
                if (_game.Undo() is null)
                {
                    break;
                }

                undoneCount++;
            }
            while (_game.CurrentPlayer != HumanColor && _game.CanUndo);
        }

        if (undoneCount == 0)
        {
            return;
        }

        if (wasFinished && _game.State != ReversiGameState.Finished)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        _hintPosition = null;
        var text = IsHumanVsComputer
            ? $"撤销：已回退 {undoneCount} 手，返回玩家决策点"
            : "撤销：已回退上一手";
        HistoryItems.Add(new ReversiHistoryItem(text));
        MessageText = text;
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteHint))]
    private void Hint()
    {
        if (!CanHint)
        {
            return;
        }

        _hintPosition = _hintStrategy.SelectMove(
            _game.CreateSnapshot(),
            _game.CurrentPlayer,
            CancellationToken.None);
        MessageText = _hintPosition is { } hint
            ? $"提示：建议 {ColorName(_game.CurrentPlayer)}方落在 {hint.DisplayName}"
            : "当前没有可提示的合法位置";
        RefreshBoardCells();
    }

    partial void OnSelectedModeChanged(ReversiGameModeOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"已切换为{value.DisplayName}");
        }
    }

    partial void OnSelectedDifficultyChanged(ReversiDifficultyOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"电脑难度已切换为{value.DisplayName}");
        }
    }

    partial void OnSelectedHumanColorChanged(ReversiColorOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"已切换为{value.DisplayName}");
        }
    }

    /// <summary>停止后台电脑、UI 刷新与累计计时；释放后所有玩家输入均被忽略。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelComputerTurn();
        _displayRefreshTimer?.Stop();
        if (_displayRefreshTimer is not null)
        {
            _displayRefreshTimer.Tick -= OnDisplayRefreshTimerTick;
        }

        _gameTimer.Stop();
        RefreshElapsedTime();
        NotifyCommandStates();
    }

    internal void RefreshElapsedTime() => ElapsedSeconds = _gameTimer.ElapsedSeconds;

    private ReversiDiscColor HumanColor => SelectedHumanColor.Definition;

    private void StartNewGame(string message)
    {
        CancelComputerTurn();
        _game.StartNewGame();
        _gameTimer.Reset();
        _displayRefreshTimer?.Stop();
        ElapsedSeconds = 0;
        _hintPosition = null;
        HistoryItems.Clear();
        MessageText = message;
        RefreshPresentation();
        QueueComputerTurnsIfNeeded();
    }

    private void ApplyMoveResult(ReversiMoveResult result, bool isComputer)
    {
        if (!_gameTimer.IsRunning)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        _hintPosition = null;
        var actor = isComputer ? $"电脑（{ColorName(result.Player)}方）" : ColorName(result.Player) + "方";
        var moveText = $"{result.After.MoveCount}. {actor} {result.Position.DisplayName}，翻转 {result.FlippedPositions.Count} 枚";
        HistoryItems.Add(new ReversiHistoryItem(moveText));
        MessageText = moveText;
        if (result.SkippedPlayer is { } skipped)
        {
            var skipText = $"自动跳过：{ColorName(skipped)}方无合法位置，{ColorName(result.Player)}方继续";
            HistoryItems.Add(new ReversiHistoryItem(skipText));
            MessageText = skipText;
        }

        if (_game.State == ReversiGameState.Finished)
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
            var finishText = _game.Winner is { } winner
                ? $"对局结束：黑 {_game.BlackCount}：白 {_game.WhiteCount}，{ColorName(winner)}方获胜"
                : $"对局结束：黑 {_game.BlackCount}：白 {_game.WhiteCount}，双方平局";
            HistoryItems.Add(new ReversiHistoryItem(finishText));
            MessageText = finishText;
        }

        RefreshPresentation();
    }

    private void QueueComputerTurnsIfNeeded()
    {
        if (_disposed || !IsHumanVsComputer || _game.State == ReversiGameState.Finished ||
            _game.CurrentPlayer == HumanColor || IsComputerThinking)
        {
            return;
        }

        var version = _gameVersion;
        _computerCancellation = new CancellationTokenSource();
        IsComputerThinking = true;
        RefreshPresentation();
        _pendingComputerTask = RunComputerTurnsAsync(version, _computerCancellation.Token);
    }

    private async Task RunComputerTurnsAsync(int version, CancellationToken cancellationToken)
    {
        try
        {
            while (!_disposed && version == _gameVersion &&
                   _game.State != ReversiGameState.Finished &&
                   _game.CurrentPlayer != HumanColor)
            {
                var snapshot = _game.CreateSnapshot();
                var player = snapshot.CurrentPlayer;
                var strategy = _computerStrategies[SelectedDifficulty.Definition];
                var move = await Task.Run(
                    () => strategy.SelectMove(snapshot, player, cancellationToken),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // 配置切换、重开或释放都会增加版本号；旧搜索即使刚好完成，也不能提交到新棋局。
                if (_disposed || version != _gameVersion)
                {
                    return;
                }

                if (move is not { } selected || _game.PlaceDisc(selected) is not { } result)
                {
                    MessageText = "电脑策略未能返回合法位置，请重新开始对局";
                    return;
                }

                ApplyMoveResult(result, isComputer: true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed && version == _gameVersion)
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

    private void RefreshPresentation()
    {
        RefreshElapsedTime();
        OnPropertyChanged(nameof(BlackCount));
        OnPropertyChanged(nameof(WhiteCount));
        OnPropertyChanged(nameof(BlackCountText));
        OnPropertyChanged(nameof(WhiteCountText));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(IsHumanVsComputer));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanHint));
        OnPropertyChanged(nameof(CurrentTurnText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ResultText));
        RefreshBoardCells();
        NotifyCommandStates();
    }

    private void RefreshBoardCells()
    {
        var legalMoves = _game.GetLegalMoves().ToHashSet();
        foreach (var cell in BoardCells)
        {
            cell.Refresh(
                _game.GetDisc(cell.Position),
                legalMoves.Contains(cell.Position),
                _hintPosition == cell.Position,
                _game.LastMove == cell.Position,
                CanInteract);
        }
    }

    private void NotifyCommandStates()
    {
        UndoCommand.NotifyCanExecuteChanged();
        HintCommand.NotifyCanExecuteChanged();
    }

    private bool CanExecuteUndo() => CanUndo;
    private bool CanExecuteHint() => CanHint;
    private static string ColorName(ReversiDiscColor color) =>
        color == ReversiDiscColor.Black ? "黑" : "白";
    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();
}
