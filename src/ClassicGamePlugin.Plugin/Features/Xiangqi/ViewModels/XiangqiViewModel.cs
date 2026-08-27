using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Xiangqi.Domain;

namespace ClassicGamePlugin.Features.Xiangqi.ViewModels;

/// <summary>
/// 编排中国象棋页面的选择交互、三级电脑、决策点撤销、计时和展示投影。所有合法性和终局判断均委托
/// 给领域层；棋局版本与取消令牌共同保证已经过期的后台结果不能写回新局或撤销后的棋盘。
/// </summary>
public sealed partial class XiangqiViewModel : ObservableObject, IDisposable
{
    private readonly XiangqiGame _game = new();
    private readonly XiangqiGameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private readonly IReadOnlyDictionary<XiangqiAiDifficulty, IXiangqiMoveStrategy> _computerStrategies;
    private readonly IXiangqiMoveStrategy _hintStrategy;
    private CancellationTokenSource? _computerCancellation;
    private CancellationTokenSource? _hintCancellation;
    private Task? _pendingComputerTask;
    private Task? _pendingHintTask;
    private XiangqiPosition? _selectedPosition;
    private IReadOnlySet<XiangqiPosition> _legalTargets = new HashSet<XiangqiPosition>();
    private XiangqiMove? _hintMove;
    private int _gameVersion;
    private bool _disposed;

    [ObservableProperty]
    private XiangqiGameModeOption _selectedMode;

    [ObservableProperty]
    private XiangqiDifficultyOption _selectedDifficulty;

    [ObservableProperty]
    private XiangqiSideOption _selectedHumanSide;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private bool _isComputerThinking;

    [ObservableProperty]
    private bool _isHintThinking;

    [ObservableProperty]
    private bool _isResignConfirmationPending;

    [ObservableProperty]
    private string _messageText = "红方先行，请选择棋子";

    public XiangqiViewModel()
        : this(
            TimeProvider.System,
            enableDisplayRefreshTimer: true,
            new Dictionary<XiangqiAiDifficulty, IXiangqiMoveStrategy>
            {
                [XiangqiAiDifficulty.Easy] = new EasyXiangqiMoveStrategy(Random.Shared),
                [XiangqiAiDifficulty.Medium] = SearchXiangqiMoveStrategy.CreateMedium(),
                [XiangqiAiDifficulty.Hard] = SearchXiangqiMoveStrategy.CreateHard(),
            },
            SearchXiangqiMoveStrategy.CreateMedium())
    {
    }

    /// <summary>注入可控时间和策略，使 ViewModel 测试不依赖随机数、真实墙钟或生产搜索时长。</summary>
    internal XiangqiViewModel(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<XiangqiAiDifficulty, IXiangqiMoveStrategy> computerStrategies,
        IXiangqiMoveStrategy? hintStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(computerStrategies);
        foreach (var difficulty in Enum.GetValues<XiangqiAiDifficulty>())
        {
            if (!computerStrategies.ContainsKey(difficulty))
            {
                throw new ArgumentException("必须为三级中国象棋电脑分别提供策略。", nameof(computerStrategies));
            }
        }

        _computerStrategies = computerStrategies;
        _hintStrategy = hintStrategy ?? computerStrategies[XiangqiAiDifficulty.Medium];
        _gameTimer = new XiangqiGameTimer(timeProvider);
        ModeOptions =
        [
            new(XiangqiGameMode.LocalTwoPlayer, "本地双人"),
            new(XiangqiGameMode.HumanVsComputer, "人机对战"),
        ];
        DifficultyOptions =
        [
            new(XiangqiAiDifficulty.Easy, "简单"),
            new(XiangqiAiDifficulty.Medium, "中等"),
            new(XiangqiAiDifficulty.Hard, "困难"),
        ];
        HumanSideOptions =
        [
            new(XiangqiSide.Red, "玩家执红"),
            new(XiangqiSide.Black, "玩家执黑"),
        ];
        _selectedMode = ModeOptions[0];
        _selectedDifficulty = DifficultyOptions[1];
        _selectedHumanSide = HumanSideOptions[0];

        if (enableDisplayRefreshTimer)
        {
            _displayRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _displayRefreshTimer.Tick += OnDisplayRefreshTimerTick;
        }

        RefreshPresentation();
    }

    public IReadOnlyList<XiangqiGameModeOption> ModeOptions { get; }
    public IReadOnlyList<XiangqiDifficultyOption> DifficultyOptions { get; }
    public IReadOnlyList<XiangqiSideOption> HumanSideOptions { get; }
    public ObservableCollection<XiangqiHistoryItem> HistoryItems { get; } = [];
    public int RedCount => XiangqiRules.CountPieces(_game.CreateSnapshot(), XiangqiSide.Red);
    public int BlackCount => XiangqiRules.CountPieces(_game.CreateSnapshot(), XiangqiSide.Black);
    public int MoveCount => _game.MoveCount;
    public string RedCountText => $"红方 {RedCount} 子";
    public string BlackCountText => $"黑方 {BlackCount} 子";
    public string MoveCountText => $"共 {MoveCount} 手";
    public bool IsHumanVsComputer => SelectedMode.Definition == XiangqiGameMode.HumanVsComputer;
    public bool IsBoardFlipped => IsHumanVsComputer && HumanSide == XiangqiSide.Black;
    public bool CanInteract => !_disposed && !IsComputerThinking && !IsHintThinking &&
        _game.State != XiangqiGameState.Finished &&
        (!IsHumanVsComputer || _game.CurrentSide == HumanSide);
    public bool CanUndo => !_disposed && _game.TerminationReason != XiangqiTerminationReason.Resignation && _game.CanUndo &&
        (!IsHumanVsComputer || HasHumanMoveToUndo());
    public bool CanHint => CanInteract;
    public bool CanResign => !_disposed && _game.State != XiangqiGameState.Finished;
    public string ResignButtonText => IsResignConfirmationPending ? "确认认输" : "认输";
    public string CurrentTurnText => _game.State == XiangqiGameState.Finished
        ? "对局已结束"
        : $"{SideName(_game.CurrentSide)}方回合";
    public string StatusText => _game.State switch
    {
        XiangqiGameState.Finished when _game.Winner is { } winner => $"{SideName(winner)}方获胜",
        XiangqiGameState.Finished => "双方和棋",
        _ when IsComputerThinking => "电脑思考中",
        _ when IsHintThinking => "正在计算提示",
        XiangqiGameState.Ready => "准备开始",
        _ when XiangqiRules.IsInCheck(_game.CreateSnapshot(), _game.CurrentSide) => "将军",
        _ => "进行中",
    };
    public string ResultText => _game.State == XiangqiGameState.Finished
        ? TerminationText(_game.TerminationReason, _game.Winner)
        : string.Empty;

    internal XiangqiGameSnapshot CurrentSnapshot => _game.CreateSnapshot();
    internal XiangqiPosition? SelectedPosition => _selectedPosition;
    internal IReadOnlySet<XiangqiPosition> LegalTargets => _legalTargets;
    internal XiangqiMove? HintMove => _hintMove;
    internal XiangqiGameState GameState => _game.State;
    internal XiangqiSide CurrentSide => _game.CurrentSide;
    internal bool IsTimerRunning => _gameTimer.IsRunning;
    internal Task WaitForComputerAsync() => _pendingComputerTask ?? Task.CompletedTask;
    internal Task WaitForHintAsync() => _pendingHintTask ?? Task.CompletedTask;

    /// <summary>
    /// 接收棋盘控件换算后的固定领域坐标。第一次点击选择当前方棋子，第二次点击合法目标提交；
    /// 点击另一枚己方棋子会切换选择，所有目标仍由领域预检生成和二次验证。
    /// </summary>
    internal void PlayPosition(XiangqiPosition position)
    {
        if (!CanInteract)
        {
            return;
        }

        CancelResignConfirmation();
        var clicked = _game.GetPiece(position);
        if (clicked is { Side: var side } && side == _game.CurrentSide)
        {
            SelectPiece(position);
            return;
        }

        if (_selectedPosition is not { } selected)
        {
            MessageText = clicked is null ? "请先选择要走的棋子" : "当前只能选择本方棋子";
            RefreshPresentation();
            return;
        }

        var move = new XiangqiMove(selected, position);
        var validation = _game.ValidateMove(move);
        if (!validation.IsLegal)
        {
            MessageText = MoveErrorText(validation.Error);
            RefreshPresentation();
            return;
        }

        if (_game.Move(move) is not { } result)
        {
            return;
        }

        ApplyMoveResult(result, isComputer: false);
        QueueComputerTurnIfNeeded();
    }

    internal string DescribePosition(XiangqiPosition position)
    {
        var piece = _game.GetPiece(position);
        return piece is null ? position.DisplayName : $"{position.DisplayName} {SideName(piece.Value.Side)}{piece.Value.DisplayName}";
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

        var computerWasThinking = IsComputerThinking;
        CancelBackgroundWork();
        var undoCount = 0;
        if (_game.Undo() is not null)
        {
            undoCount++;
        }

        if (IsHumanVsComputer && !computerWasThinking && _game.CurrentSide != HumanSide && _game.CanUndo &&
            HasHumanMoveToUndo())
        {
            _game.Undo();
            undoCount++;
        }

        RemoveLastMoveRecords(undoCount);
        ClearSelectionAndHint();
        CancelResignConfirmation();
        if (_game.MoveCount == 0)
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
        }
        else if (_game.State != XiangqiGameState.Finished)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        MessageText = IsHumanVsComputer
            ? "已撤销到玩家上一个决策点"
            : "已撤销上一手";
        HistoryItems.Add(new XiangqiHistoryItem($"撤销：{MessageText}"));
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteHint))]
    private void Hint()
    {
        if (!CanHint)
        {
            return;
        }

        CancelResignConfirmation();
        CancelHint();
        var version = _gameVersion;
        var snapshot = _game.CreateSnapshot();
        _hintCancellation = new CancellationTokenSource();
        IsHintThinking = true;
        MessageText = "正在计算稳定提示…";
        RefreshPresentation();
        _pendingHintTask = RunHintAsync(snapshot, version, _hintCancellation.Token);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteResign))]
    private void Resign()
    {
        if (!CanResign)
        {
            return;
        }

        if (!IsResignConfirmationPending)
        {
            IsResignConfirmationPending = true;
            MessageText = "再次点击“确认认输”结束本局";
            RefreshPresentation();
            return;
        }

        CancelBackgroundWork();
        var resigningSide = IsHumanVsComputer ? HumanSide : _game.CurrentSide;
        _game.Resign(resigningSide);
        _gameTimer.Stop();
        _displayRefreshTimer?.Stop();
        IsResignConfirmationPending = false;
        ClearSelectionAndHint();
        MessageText = $"{SideName(resigningSide)}方认输，{SideName(_game.Winner!.Value)}方获胜";
        HistoryItems.Add(new XiangqiHistoryItem(MessageText));
        RefreshPresentation();
    }

    partial void OnSelectedModeChanged(XiangqiGameModeOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"已切换为{value.DisplayName}");
        }
    }

    partial void OnSelectedDifficultyChanged(XiangqiDifficultyOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"电脑难度已切换为{value.DisplayName}");
        }
    }

    partial void OnSelectedHumanSideChanged(XiangqiSideOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"已切换为{value.DisplayName}");
        }
    }

    partial void OnIsResignConfirmationPendingChanged(bool value) =>
        OnPropertyChanged(nameof(ResignButtonText));

    /// <summary>级联取消电脑和提示任务，并停止 DispatcherTimer 与累计计时。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelBackgroundWork();
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

    private XiangqiSide HumanSide => SelectedHumanSide.Definition;

    private void SelectPiece(XiangqiPosition position)
    {
        _selectedPosition = position;
        _legalTargets = _game.GetLegalMoves()
            .Where(move => move.From == position)
            .Select(move => move.To)
            .ToHashSet();
        var piece = _game.GetPiece(position)!.Value;
        MessageText = _legalTargets.Count == 0
            ? $"{piece.DisplayName}当前没有合法走法"
            : $"已选择{SideName(piece.Side)}{piece.DisplayName}，请选择目标位置";
        RefreshPresentation();
    }

    private void StartNewGame(string message)
    {
        CancelBackgroundWork();
        _game.StartNewGame();
        _gameTimer.Reset();
        _displayRefreshTimer?.Stop();
        ElapsedSeconds = 0;
        HistoryItems.Clear();
        ClearSelectionAndHint();
        IsResignConfirmationPending = false;
        MessageText = message;
        RefreshPresentation();
        QueueComputerTurnIfNeeded();
    }

    private void ApplyMoveResult(XiangqiMoveResult result, bool isComputer)
    {
        if (!_gameTimer.IsRunning)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        ClearSelectionAndHint();
        var actor = isComputer ? $"电脑（{SideName(result.MovingPiece.Side)}方）" : $"{SideName(result.MovingPiece.Side)}方";
        var checkText = result.GaveCheck ? "，将军" : string.Empty;
        var text = $"{result.After.MoveCount}. {actor} {result.Notation}{checkText}";
        HistoryItems.Add(new XiangqiHistoryItem(text, IsMove: true));
        MessageText = text;
        if (_game.State == XiangqiGameState.Finished)
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
            var finish = $"对局结束：{TerminationText(_game.TerminationReason, _game.Winner)}";
            HistoryItems.Add(new XiangqiHistoryItem(finish));
            MessageText = finish;
        }

        RefreshPresentation();
    }

    private void QueueComputerTurnIfNeeded()
    {
        if (_disposed || !IsHumanVsComputer || _game.State == XiangqiGameState.Finished ||
            _game.CurrentSide == HumanSide || IsComputerThinking)
        {
            return;
        }

        CancelHint();
        var version = _gameVersion;
        var snapshot = _game.CreateSnapshot();
        _computerCancellation = new CancellationTokenSource();
        IsComputerThinking = true;
        RefreshPresentation();
        _pendingComputerTask = RunComputerTurnAsync(snapshot, version, _computerCancellation.Token);
    }

    private async Task RunComputerTurnAsync(
        XiangqiGameSnapshot snapshot,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            var strategy = _computerStrategies[SelectedDifficulty.Definition];
            var move = await Task.Run(
                () => strategy.SelectMove(snapshot, snapshot.CurrentSide, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || version != _gameVersion || move is not { } selected)
            {
                return;
            }

            if (_game.Move(selected) is not { } result)
            {
                MessageText = "电脑策略返回了过期或非法走法，请重新开始对局";
                return;
            }

            ApplyMoveResult(result, isComputer: true);
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

    private async Task RunHintAsync(
        XiangqiGameSnapshot snapshot,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            var move = await Task.Run(
                () => _hintStrategy.SelectMove(snapshot, snapshot.CurrentSide, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || version != _gameVersion)
            {
                return;
            }

            _hintMove = move;
            MessageText = move is { } hint
                ? $"提示：建议走 {XiangqiNotation.Format(snapshot, hint)}"
                : "当前没有可提示的合法走法";
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

    private void CancelBackgroundWork()
    {
        _gameVersion++;
        _computerCancellation?.Cancel();
        _computerCancellation?.Dispose();
        _computerCancellation = null;
        _pendingComputerTask = null;
        IsComputerThinking = false;
        CancelHint();
    }

    private void CancelHint()
    {
        _hintCancellation?.Cancel();
        _hintCancellation?.Dispose();
        _hintCancellation = null;
        _pendingHintTask = null;
        IsHintThinking = false;
    }

    private bool HasHumanMoveToUndo() => _game.CreateSnapshot().PositionHistory.Any(record => record.Mover == HumanSide);

    private void RemoveLastMoveRecords(int count)
    {
        for (var remaining = count; remaining > 0; remaining--)
        {
            var index = -1;
            for (var candidate = HistoryItems.Count - 1; candidate >= 0; candidate--)
            {
                if (HistoryItems[candidate].IsMove)
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0)
            {
                break;
            }

            for (var trailing = HistoryItems.Count - 1; trailing > index; trailing--)
            {
                if (HistoryItems[trailing].Text.StartsWith("对局结束：", StringComparison.Ordinal))
                {
                    HistoryItems.RemoveAt(trailing);
                }
            }

            HistoryItems.RemoveAt(index);
        }
    }

    private void ClearSelectionAndHint()
    {
        _selectedPosition = null;
        _legalTargets = new HashSet<XiangqiPosition>();
        _hintMove = null;
    }

    private void CancelResignConfirmation()
    {
        if (IsResignConfirmationPending)
        {
            IsResignConfirmationPending = false;
        }
    }

    private void RefreshPresentation()
    {
        RefreshElapsedTime();
        OnPropertyChanged(nameof(RedCount));
        OnPropertyChanged(nameof(BlackCount));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(RedCountText));
        OnPropertyChanged(nameof(BlackCountText));
        OnPropertyChanged(nameof(MoveCountText));
        OnPropertyChanged(nameof(IsHumanVsComputer));
        OnPropertyChanged(nameof(IsBoardFlipped));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanHint));
        OnPropertyChanged(nameof(CanResign));
        OnPropertyChanged(nameof(CurrentTurnText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(CurrentSnapshot));
        OnPropertyChanged(nameof(SelectedPosition));
        OnPropertyChanged(nameof(LegalTargets));
        OnPropertyChanged(nameof(HintMove));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        UndoCommand.NotifyCanExecuteChanged();
        HintCommand.NotifyCanExecuteChanged();
        ResignCommand.NotifyCanExecuteChanged();
    }

    private bool CanExecuteUndo() => CanUndo;
    private bool CanExecuteHint() => CanHint;
    private bool CanExecuteResign() => CanResign;
    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();
    private static string SideName(XiangqiSide side) => side == XiangqiSide.Red ? "红" : "黑";

    internal static string MoveErrorText(XiangqiMoveError error) => error switch
    {
        XiangqiMoveError.GameFinished => "对局已经结束",
        XiangqiMoveError.OutOfBounds => "目标位置超出棋盘",
        XiangqiMoveError.EmptyOrigin => "起点没有棋子",
        XiangqiMoveError.WrongSide => "当前不能移动对方棋子",
        XiangqiMoveError.FriendlyDestination => "目标位置已有己方棋子",
        XiangqiMoveError.PathBlocked => "行棋路径被阻挡",
        XiangqiMoveError.HorseLegBlocked => "马腿被挡",
        XiangqiMoveError.ElephantEyeBlocked => "象眼被塞",
        XiangqiMoveError.PalaceRestricted => "帅将或仕士不能离开九宫",
        XiangqiMoveError.ElephantCrossesRiver => "相象不能过河",
        XiangqiMoveError.CannonScreen => "炮的炮架数量不符合规则",
        XiangqiMoveError.SoldierDirection => "兵卒不能后退，未过河也不能横走",
        XiangqiMoveError.GeneralCaptureNotAllowed => "将死即结束，不直接吃帅将",
        XiangqiMoveError.ExposesGeneral => "该走法会暴露己方帅将",
        XiangqiMoveError.GeneralsFace => "该走法会造成将帅照面",
        XiangqiMoveError.PerpetualCheck => "长将，请变着",
        _ => "该棋子不能这样移动",
    };

    private static string TerminationText(
        XiangqiTerminationReason? reason,
        XiangqiSide? winner) => reason switch
    {
        XiangqiTerminationReason.Checkmate => $"{SideName(winner!.Value)}方将死对手",
        XiangqiTerminationReason.Stalemate => $"{SideName(winner!.Value)}方困毙对手",
        XiangqiTerminationReason.Resignation => $"{SideName(winner!.Value)}方因对手认输获胜",
        XiangqiTerminationReason.ThreefoldRepetition => "三次重复局面，双方和棋",
        XiangqiTerminationReason.NoCaptureLimit => "连续 120 手未吃子，双方和棋",
        _ => string.Empty,
    };
}
