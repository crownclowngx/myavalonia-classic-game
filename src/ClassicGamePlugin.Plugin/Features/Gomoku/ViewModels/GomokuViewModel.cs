using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Gomoku.Domain;

namespace ClassicGamePlugin.Features.Gomoku.ViewModels;

/// <summary>
/// 编排五子棋页面的命令、单步回退暂停、异步电脑、计时和展示投影。所有胜负与禁手判断均委托给
/// <see cref="GomokuRules"/>，本类型不复制领域规则；对局版本确保已取消的后台搜索不能写回新棋局。
/// </summary>
public sealed partial class GomokuViewModel : ObservableObject, IDisposable
{
    private readonly GomokuGame _game = new();
    private readonly GomokuGameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private readonly IReadOnlyDictionary<GomokuAiDifficulty, IGomokuMoveStrategy> _computerStrategies;
    private readonly IGomokuMoveStrategy _hintStrategy;
    private CancellationTokenSource? _computerCancellation;
    private Task? _pendingComputerTask;
    private GomokuPosition? _hintPosition;
    private IReadOnlyDictionary<GomokuPosition, GomokuForbiddenReason> _forbiddenPoints =
        new Dictionary<GomokuPosition, GomokuForbiddenReason>();
    private int _gameVersion;
    private bool _disposed;

    [ObservableProperty]
    private GomokuRuleOption _selectedRule;

    [ObservableProperty]
    private GomokuGameModeOption _selectedMode;

    [ObservableProperty]
    private GomokuDifficultyOption _selectedDifficulty;

    [ObservableProperty]
    private GomokuColorOption _selectedHumanColor;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private bool _isComputerThinking;

    [ObservableProperty]
    private bool _isRewinding;

    [ObservableProperty]
    private string _messageText = "黑方先手，可在任意交叉点落子";

    /// <summary>使用系统计时和三级生产策略创建页面。</summary>
    public GomokuViewModel()
        : this(
            TimeProvider.System,
            enableDisplayRefreshTimer: true,
            new Dictionary<GomokuAiDifficulty, IGomokuMoveStrategy>
            {
                [GomokuAiDifficulty.Easy] = new RandomGomokuMoveStrategy(Random.Shared),
                [GomokuAiDifficulty.Medium] = new StableGomokuMoveStrategy(),
                [GomokuAiDifficulty.Hard] = new HardGomokuMoveStrategy(),
            },
            new StableGomokuMoveStrategy())
    {
    }

    /// <summary>注入可控时间和策略，保证单元测试不依赖真实墙钟、随机数或两秒搜索。</summary>
    internal GomokuViewModel(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        IReadOnlyDictionary<GomokuAiDifficulty, IGomokuMoveStrategy> computerStrategies,
        IGomokuMoveStrategy? hintStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(computerStrategies);
        foreach (var difficulty in Enum.GetValues<GomokuAiDifficulty>())
        {
            if (!computerStrategies.ContainsKey(difficulty))
            {
                throw new ArgumentException("必须为三级五子棋电脑分别提供策略。", nameof(computerStrategies));
            }
        }

        _computerStrategies = computerStrategies;
        _hintStrategy = hintStrategy ?? new StableGomokuMoveStrategy();
        _gameTimer = new GomokuGameTimer(timeProvider);
        RuleOptions =
        [
            new(GomokuRuleSet.Freestyle, "自由规则"),
            new(GomokuRuleSet.Forbidden, "禁手规则"),
        ];
        ModeOptions =
        [
            new(GomokuGameMode.LocalTwoPlayer, "本地双人"),
            new(GomokuGameMode.HumanVsComputer, "人机对战"),
        ];
        DifficultyOptions =
        [
            new(GomokuAiDifficulty.Easy, "简单"),
            new(GomokuAiDifficulty.Medium, "中等"),
            new(GomokuAiDifficulty.Hard, "困难"),
        ];
        HumanColorOptions =
        [
            new(GomokuStone.Black, "玩家执黑"),
            new(GomokuStone.White, "玩家执白"),
        ];
        _selectedRule = RuleOptions[0];
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

    public IReadOnlyList<GomokuRuleOption> RuleOptions { get; }
    public IReadOnlyList<GomokuGameModeOption> ModeOptions { get; }
    public IReadOnlyList<GomokuDifficultyOption> DifficultyOptions { get; }
    public IReadOnlyList<GomokuColorOption> HumanColorOptions { get; }
    public ObservableCollection<GomokuHistoryItem> HistoryItems { get; } = [];
    public int BlackCount => _game.BlackCount;
    public int WhiteCount => _game.WhiteCount;
    public int MoveCount => _game.MoveCount;
    public string BlackCountText => $"● 黑 {BlackCount}";
    public string WhiteCountText => $"○ 白 {WhiteCount}";
    public string MoveCountText => $"共 {MoveCount} 手";
    public bool IsHumanVsComputer => SelectedMode.Definition == GomokuGameMode.HumanVsComputer;
    public bool CanInteract => !_disposed && !IsComputerThinking && !IsRewinding &&
        _game.State != GomokuGameState.Finished &&
        (!IsHumanVsComputer || _game.CurrentPlayer == HumanColor);
    public bool CanUndo => !_disposed && _game.CanUndo;
    public bool CanContinue => !_disposed && IsHumanVsComputer && IsRewinding;
    public bool CanHint => CanInteract;
    public string CurrentTurnText => _game.State == GomokuGameState.Finished
        ? "对局已结束"
        : $"{StoneName(_game.CurrentPlayer)}方回合";
    public string StatusText => _game.State switch
    {
        GomokuGameState.Finished when _game.Winner is { } winner => $"{StoneName(winner)}方获胜",
        GomokuGameState.Finished => "双方平局",
        _ when IsRewinding => "回退暂停中",
        _ when IsComputerThinking => "电脑思考中",
        GomokuGameState.Ready => "准备开始",
        _ => "进行中",
    };
    public string ResultText => _game.State == GomokuGameState.Finished
        ? _game.Winner is { } winner
            ? $"第 {_game.MoveCount} 手，{StoneName(winner)}方形成五连"
            : $"棋盘已满，共 {_game.MoveCount} 手，双方平局"
        : string.Empty;

    internal GomokuGameSnapshot CurrentSnapshot => _game.CreateSnapshot();
    internal GomokuPosition? HintPosition => _hintPosition;
    internal IReadOnlyDictionary<GomokuPosition, GomokuForbiddenReason> ForbiddenPoints => _forbiddenPoints;
    internal GomokuGameState GameState => _game.State;
    internal GomokuStone CurrentPlayer => _game.CurrentPlayer;
    internal bool IsTimerRunning => _gameTimer.IsRunning;
    internal Task WaitForComputerAsync() => _pendingComputerTask ?? Task.CompletedTask;

    /// <summary>接收棋盘控件换算后的交叉点；禁手和占用点由领域预检拒绝并返回中文原因。</summary>
    internal void PlayPosition(GomokuPosition position)
    {
        if (!CanInteract)
        {
            return;
        }

        var validation = _game.ValidateMove(position);
        if (!validation.IsLegal)
        {
            MessageText = validation.InvalidReason switch
            {
                GomokuMoveInvalidReason.Occupied => $"{position.DisplayName} 已有棋子",
                GomokuMoveInvalidReason.Forbidden =>
                    $"{position.DisplayName} 是黑方禁手：{ForbiddenReasonText(validation.ForbiddenReasons)}",
                _ => $"{position.DisplayName} 当前不能落子",
            };
            RefreshPresentation();
            return;
        }

        var result = _game.PlaceStone(position);
        if (result is null)
        {
            return;
        }

        ApplyMoveResult(result, isComputer: false);
        QueueComputerTurnIfNeeded();
    }

    internal string DescribePosition(GomokuPosition position) =>
        _forbiddenPoints.TryGetValue(position, out var reason)
            ? $"{position.DisplayName}，黑方禁手：{ForbiddenReasonText(reason)}"
            : position.DisplayName;

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
        var wasFinished = _game.State == GomokuGameState.Finished;
        if (_game.Undo() is null)
        {
            return;
        }

        _hintPosition = null;
        if (IsHumanVsComputer)
        {
            IsRewinding = true;
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
            MessageText = "已回退一手并暂停，可继续撤销或恢复对局";
        }
        else
        {
            if (_game.State == GomokuGameState.Ready)
            {
                _gameTimer.Stop();
                _displayRefreshTimer?.Stop();
            }
            else if (wasFinished)
            {
                _gameTimer.Start();
                _displayRefreshTimer?.Start();
            }

            MessageText = "已撤销上一手，按恢复后的回合继续";
        }

        HistoryItems.Add(new GomokuHistoryItem($"撤销：{MessageText}"));
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteContinue))]
    private void Continue()
    {
        if (!CanContinue)
        {
            return;
        }

        CancelComputerTurn();
        IsRewinding = false;
        if (_game.MoveCount > 0 && _game.State != GomokuGameState.Finished)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        MessageText = $"继续对局：当前由{StoneName(_game.CurrentPlayer)}方落子";
        HistoryItems.Add(new GomokuHistoryItem(MessageText));
        RefreshPresentation();
        QueueComputerTurnIfNeeded();
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
            ? $"提示：建议{StoneName(_game.CurrentPlayer)}方落在 {hint.DisplayName}"
            : "当前没有可提示的合法位置";
        RefreshPresentation();
    }

    partial void OnSelectedRuleChanged(GomokuRuleOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"已切换为{value.DisplayName}");
        }
    }

    partial void OnSelectedModeChanged(GomokuGameModeOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"已切换为{value.DisplayName}");
        }
    }

    partial void OnSelectedDifficultyChanged(GomokuDifficultyOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"电脑难度已切换为{value.DisplayName}");
        }
    }

    partial void OnSelectedHumanColorChanged(GomokuColorOption value)
    {
        if (!_disposed && value is not null)
        {
            StartNewGame($"已切换为{value.DisplayName}");
        }
    }

    /// <summary>停止后台搜索、刷新计时器和累计计时；释放后旧任务即使完成也不能提交。</summary>
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

    private GomokuStone HumanColor => SelectedHumanColor.Definition;

    private void StartNewGame(string message)
    {
        CancelComputerTurn();
        _game.StartNewGame(SelectedRule.Definition);
        _gameTimer.Reset();
        _displayRefreshTimer?.Stop();
        ElapsedSeconds = 0;
        IsRewinding = false;
        _hintPosition = null;
        HistoryItems.Clear();
        MessageText = message;
        RefreshPresentation();
        QueueComputerTurnIfNeeded();
    }

    private void ApplyMoveResult(GomokuMoveResult result, bool isComputer)
    {
        if (!_gameTimer.IsRunning)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        _hintPosition = null;
        var actor = isComputer ? $"电脑（{StoneName(result.Player)}方）" : $"{StoneName(result.Player)}方";
        var text = $"{result.After.MoveCount}. {actor} {result.Position.DisplayName}";
        HistoryItems.Add(new GomokuHistoryItem(text));
        MessageText = text;
        if (_game.State == GomokuGameState.Finished)
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
            var finish = _game.Winner is { } winner
                ? $"对局结束：{StoneName(winner)}方在第 {_game.MoveCount} 手获胜"
                : $"对局结束：棋盘放满，共 {_game.MoveCount} 手，双方平局";
            HistoryItems.Add(new GomokuHistoryItem(finish));
            MessageText = finish;
        }

        RefreshPresentation();
    }

    private void QueueComputerTurnIfNeeded()
    {
        if (_disposed || !IsHumanVsComputer || IsRewinding ||
            _game.State == GomokuGameState.Finished || _game.CurrentPlayer == HumanColor || IsComputerThinking)
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
            var snapshot = _game.CreateSnapshot();
            var player = snapshot.CurrentPlayer;
            var strategy = _computerStrategies[SelectedDifficulty.Definition];
            var move = await Task.Run(
                () => strategy.SelectMove(snapshot, player, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || version != _gameVersion)
            {
                return;
            }

            if (move is not { } selected || _game.PlaceStone(selected) is not { } result)
            {
                MessageText = "电脑策略未返回合法位置，请重新开始对局";
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
        _forbiddenPoints = CanInteract && _game.RuleSet == GomokuRuleSet.Forbidden &&
            _game.CurrentPlayer == GomokuStone.Black
            ? GomokuRules.GetForbiddenPoints(_game.CreateSnapshot())
            : new Dictionary<GomokuPosition, GomokuForbiddenReason>();
        OnPropertyChanged(nameof(BlackCount));
        OnPropertyChanged(nameof(WhiteCount));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(BlackCountText));
        OnPropertyChanged(nameof(WhiteCountText));
        OnPropertyChanged(nameof(MoveCountText));
        OnPropertyChanged(nameof(IsHumanVsComputer));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(CanHint));
        OnPropertyChanged(nameof(CurrentTurnText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(CurrentSnapshot));
        OnPropertyChanged(nameof(HintPosition));
        OnPropertyChanged(nameof(ForbiddenPoints));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        UndoCommand.NotifyCanExecuteChanged();
        ContinueCommand.NotifyCanExecuteChanged();
        HintCommand.NotifyCanExecuteChanged();
    }

    private bool CanExecuteUndo() => CanUndo;
    private bool CanExecuteContinue() => CanContinue;
    private bool CanExecuteHint() => CanHint;
    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();
    private static string StoneName(GomokuStone stone) => stone == GomokuStone.Black ? "黑" : "白";

    internal static string ForbiddenReasonText(GomokuForbiddenReason reasons)
    {
        var parts = new List<string>();
        if (reasons.HasFlag(GomokuForbiddenReason.Overline))
        {
            parts.Add("长连");
        }

        if (reasons.HasFlag(GomokuForbiddenReason.DoubleFour))
        {
            parts.Add("双四");
        }

        if (reasons.HasFlag(GomokuForbiddenReason.DoubleThree))
        {
            parts.Add("双三");
        }

        return parts.Count == 0 ? "未知禁手" : string.Join("、", parts);
    }
}
