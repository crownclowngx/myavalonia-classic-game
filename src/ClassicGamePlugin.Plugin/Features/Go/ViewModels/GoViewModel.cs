using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Go.Domain;

namespace ClassicGamePlugin.Features.Go.ViewModels;

/// <summary>
/// 编排本地双人围棋页面的命令、计时、动画通知、操作记录和展示文案。所有落子、提子、
/// 全局同形与数子判断都委托给领域对象，本类型不维护第二份棋盘规则。
/// </summary>
public sealed partial class GoViewModel : ObservableObject, IDisposable
{
    private readonly GoGame _game;
    private readonly GoGameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private bool _animationsEnabled = true;
    private bool _isAnimationRunning;
    private bool _disposed;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private string _messageText = "黑方先手，请在交叉点落子";

    /// <summary>使用系统时间创建生产页面。</summary>
    public GoViewModel()
        : this(TimeProvider.System, enableDisplayRefreshTimer: true)
    {
    }

    /// <summary>使用可控时间创建测试页面，避免单元测试依赖真实墙钟。</summary>
    internal GoViewModel(
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer,
        GoGame? game = null)
    {
        _game = game ?? new GoGame();
        _gameTimer = new GoGameTimer(timeProvider);
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

    public ObservableCollection<GoHistoryItem> HistoryItems { get; } = [];

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (!SetProperty(ref _animationsEnabled, value))
            {
                return;
            }

            if (!value)
            {
                CancelAnimation();
            }
        }
    }

    public bool CanBoardInteract => !_disposed && !_isAnimationRunning &&
        _game.State != GoGameState.Finished;
    public bool CanPlay => CanBoardInteract && _game.State is GoGameState.Ready or GoGameState.Playing;
    public bool CanMarkDead => CanBoardInteract && _game.State == GoGameState.Scoring;
    public bool CanUndo => !_disposed && _game.CanUndo;
    public bool CanPass => CanPlay;
    public bool CanResign => CanPlay;
    public bool IsScoring => _game.State == GoGameState.Scoring;
    public bool IsFinished => _game.State == GoGameState.Finished;
    public int MoveCount => _game.MoveCount;
    public int BlackCaptures => _game.BlackCaptures;
    public int WhiteCaptures => _game.WhiteCaptures;
    public string BlackCaptureText => $"● 黑方提子 {_game.BlackCaptures}";
    public string WhiteCaptureText => $"○ 白方提子 {_game.WhiteCaptures}";
    public string MoveCountText => $"落子 {_game.MoveCount} 手";
    public string CurrentTurnText => _game.State switch
    {
        GoGameState.Scoring => "数子阶段",
        GoGameState.Finished => "对局已结束",
        _ => $"{StoneName(_game.CurrentPlayer)}方回合",
    };
    public string StatusText => _game.State switch
    {
        GoGameState.Ready => "准备开始",
        GoGameState.Playing => "对局进行中",
        GoGameState.Scoring => "请共同标记死子",
        GoGameState.Finished when _game.FinishReason == GoFinishReason.Resignation =>
            $"{StoneName(_game.Winner!.Value)}方获胜（认输）",
        GoGameState.Finished => $"{StoneName(_game.Winner!.Value)}方获胜",
        _ => string.Empty,
    };
    public string ResultText => _game.Score is { } score
        ? $"黑 {score.BlackScore:0.#}：白 {score.WhiteScore:0.#}，{StoneName(score.Winner)}胜 {score.Margin:0.#} 目"
        : _game.FinishReason == GoFinishReason.Resignation
            ? $"{StoneName(_game.Winner!.Value)}方中盘胜"
            : string.Empty;
    public string AccessibleBoardText =>
        $"19路围棋棋盘，{StatusText}，{CurrentTurnText}，黑方提子 {_game.BlackCaptures}，白方提子 {_game.WhiteCaptures}";

    internal GoGameSnapshot CurrentSnapshot => _game.CreateSnapshot();
    internal bool IsAnimationRunning => _isAnimationRunning;
    internal bool IsTimerRunning => _gameTimer.IsRunning;
    internal event EventHandler<GoAnimationPlan>? AnimationRequested;
    internal event EventHandler? AnimationCancellationRequested;

    /// <summary>棋盘控件只负责把命中位置转发到这里；当前阶段决定它表示落子还是整组死子切换。</summary>
    public void PlayPosition(int row, int column)
    {
        var position = new GoPosition(row, column);
        if (!CanBoardInteract)
        {
            return;
        }

        if (_game.State == GoGameState.Scoring)
        {
            if (_game.ToggleDeadGroup(position))
            {
                MessageText = $"已切换 {position.DisplayName} 所在棋组的死子标记";
                HistoryItems.Add(new GoHistoryItem(MessageText));
                RefreshPresentation();
            }
            else
            {
                MessageText = "数子阶段请点击棋子，整组切换死子标记";
            }

            return;
        }

        var validation = _game.ValidateMove(position);
        if (!validation.IsLegal)
        {
            MessageText = validation.Reason switch
            {
                GoMoveInvalidReason.OutsideBoard => "落点超出 19×19 棋盘",
                GoMoveInvalidReason.Occupied => $"{position.DisplayName} 已有棋子",
                GoMoveInvalidReason.Suicide => $"{position.DisplayName} 为禁入点：落子后己方棋组无气",
                GoMoveInvalidReason.Superko => $"{position.DisplayName} 会重现本局已有棋盘，违反全局同形禁着",
                _ => "当前阶段不能落子",
            };
            return;
        }

        var result = _game.PlaceStone(position)!;
        StartTimerIfNeeded();
        var captureText = result.CapturedPositions.Count > 0
            ? $"，提 {result.CapturedPositions.Count} 子"
            : string.Empty;
        MessageText = $"{result.After.MoveCount}. {StoneName(result.Player)}方 {position.DisplayName}{captureText}";
        HistoryItems.Add(new GoHistoryItem(MessageText));
        PublishAnimation(result);
        RefreshPresentation();
    }

    [RelayCommand]
    private void Restart()
    {
        CancelAnimation();
        _game.StartNewGame();
        _gameTimer.Reset();
        _displayRefreshTimer?.Stop();
        ElapsedSeconds = 0;
        HistoryItems.Clear();
        MessageText = "已重新开始，黑方先手";
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
    private void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        CancelAnimation();
        if (!_game.Undo())
        {
            return;
        }

        MessageText = "已撤销上一个有效操作";
        HistoryItems.Add(new GoHistoryItem(MessageText));
        SynchronizeTimerWithState();
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecutePass))]
    private void Pass()
    {
        if (!CanPass)
        {
            return;
        }

        var player = _game.CurrentPlayer;
        if (!_game.Pass())
        {
            return;
        }

        StartTimerIfNeeded();
        MessageText = _game.State == GoGameState.Scoring
            ? $"{StoneName(player)}方停一手；双方连续停手，进入死子标记与数子阶段"
            : $"{StoneName(player)}方停一手";
        HistoryItems.Add(new GoHistoryItem(MessageText));
        SynchronizeTimerWithState();
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteResign))]
    private void Resign()
    {
        if (!CanResign)
        {
            return;
        }

        var player = _game.CurrentPlayer;
        if (!_game.Resign())
        {
            return;
        }

        MessageText = $"{StoneName(player)}方认输，{StoneName(_game.Winner!.Value)}方获胜";
        HistoryItems.Add(new GoHistoryItem(MessageText));
        SynchronizeTimerWithState();
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteScoringAction))]
    private void ResumePlay()
    {
        if (!_game.ResumePlay())
        {
            return;
        }

        MessageText = $"已清除死子标记，由{StoneName(_game.CurrentPlayer)}方恢复行棋";
        HistoryItems.Add(new GoHistoryItem(MessageText));
        SynchronizeTimerWithState();
        RefreshPresentation();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteScoringAction))]
    private void ConfirmScore()
    {
        if (!_game.ConfirmScore())
        {
            return;
        }

        MessageText = $"数子完成：{ResultText}";
        HistoryItems.Add(new GoHistoryItem(MessageText));
        SynchronizeTimerWithState();
        RefreshPresentation();
    }

    /// <summary>由棋盘控件在动画自然结束或被卸载时调用，解除输入锁定。</summary>
    internal void CompleteAnimation()
    {
        if (!_isAnimationRunning)
        {
            return;
        }

        _isAnimationRunning = false;
        RefreshPresentation();
    }

    /// <summary>停止 UI 刷新、累计计时和动画通知；释放后所有输入都会被忽略。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelAnimation();
        _displayRefreshTimer?.Stop();
        if (_displayRefreshTimer is not null)
        {
            _displayRefreshTimer.Tick -= OnDisplayRefreshTimerTick;
        }

        _gameTimer.Stop();
        RefreshElapsedTime();
        RefreshPresentation();
    }

    internal void RefreshElapsedTime() => ElapsedSeconds = _gameTimer.ElapsedSeconds;

    internal string DescribePosition(GoPosition position)
    {
        var snapshot = _game.CreateSnapshot();
        var stone = snapshot.GetStone(position);
        if (stone is null)
        {
            return $"{position.DisplayName}，空点";
        }

        var deadText = snapshot.IsMarkedDead(position) ? "，已标记为死子" : string.Empty;
        return $"{position.DisplayName}，{StoneName(stone.Value)}棋，棋组有 {GoRules.CountLiberties(snapshot, position)} 气{deadText}";
    }

    private void PublishAnimation(GoMoveResult result)
    {
        if (!AnimationsEnabled || AnimationRequested is null)
        {
            _isAnimationRunning = false;
            return;
        }

        _isAnimationRunning = true;
        AnimationRequested.Invoke(this, new GoAnimationPlan(result));
    }

    private void CancelAnimation()
    {
        if (!_isAnimationRunning)
        {
            return;
        }

        AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
        _isAnimationRunning = false;
        RefreshPresentation();
    }

    private void StartTimerIfNeeded()
    {
        if (!_gameTimer.IsRunning)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }
    }

    private void SynchronizeTimerWithState()
    {
        if (_game.State == GoGameState.Playing && _game.ActionCount > 0)
        {
            StartTimerIfNeeded();
        }
        else
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
            RefreshElapsedTime();
        }
    }

    private void RefreshPresentation()
    {
        RefreshElapsedTime();
        OnPropertyChanged(nameof(CanBoardInteract));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanMarkDead));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanPass));
        OnPropertyChanged(nameof(CanResign));
        OnPropertyChanged(nameof(IsScoring));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(BlackCaptures));
        OnPropertyChanged(nameof(WhiteCaptures));
        OnPropertyChanged(nameof(BlackCaptureText));
        OnPropertyChanged(nameof(WhiteCaptureText));
        OnPropertyChanged(nameof(MoveCountText));
        OnPropertyChanged(nameof(CurrentTurnText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(AccessibleBoardText));
        UndoCommand.NotifyCanExecuteChanged();
        PassCommand.NotifyCanExecuteChanged();
        ResignCommand.NotifyCanExecuteChanged();
        ResumePlayCommand.NotifyCanExecuteChanged();
        ConfirmScoreCommand.NotifyCanExecuteChanged();
    }

    private bool CanExecuteUndo() => CanUndo;
    private bool CanExecutePass() => CanPass;
    private bool CanExecuteResign() => CanResign;
    private bool CanExecuteScoringAction() => !_disposed && !_isAnimationRunning && _game.State == GoGameState.Scoring;
    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();
    private static string StoneName(GoStone stone) => stone == GoStone.Black ? "黑" : "白";
}

/// <summary>操作记录中的一条稳定中文文本。</summary>
public sealed record GoHistoryItem(string Text);
