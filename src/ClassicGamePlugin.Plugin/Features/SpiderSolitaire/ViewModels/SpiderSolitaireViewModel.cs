using Avalonia.Threading;
using ClassicGamePlugin.Features.SpiderSolitaire.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;

/// <summary>
/// 蜘蛛纸牌页面的可观察状态与用户意图入口。它组合纯领域引擎和计时器，
/// 但不解释指针坐标、拖拽阈值或牌面绘制；这些设备相关职责保留在 View。
/// </summary>
public sealed partial class SpiderSolitaireViewModel : ObservableObject, IDisposable
{
    private readonly ISpiderCardShuffler _shuffler;
    private readonly TimeProvider _timeProvider;
    private readonly bool _enableDisplayRefreshTimer;
    private readonly SpiderGameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private SpiderSolitaireGame _game;
    private (int Column, int CardIndex)? _selection;
    private bool _disposed;
    private bool _animationRunning;

    [ObservableProperty]
    private SpiderSolitaireDifficultyOption _selectedDifficulty;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private string _messageText = "选择一张正面牌开始整理";

    /// <summary>使用随机洗牌和系统时间创建生产环境 ViewModel。</summary>
    public SpiderSolitaireViewModel()
        : this(new RandomSpiderCardShuffler(), TimeProvider.System, enableDisplayRefreshTimer: true)
    {
    }

    /// <summary>使用确定洗牌与时间源创建可测试 ViewModel。</summary>
    internal SpiderSolitaireViewModel(
        ISpiderCardShuffler shuffler,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
    {
        _shuffler = shuffler ?? throw new ArgumentNullException(nameof(shuffler));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _enableDisplayRefreshTimer = enableDisplayRefreshTimer;
        DifficultyOptions =
        [
            new(SpiderSolitaireDifficulty.OneSuit, "初级 · 1 花色"),
            new(SpiderSolitaireDifficulty.TwoSuits, "中级 · 2 花色"),
            new(SpiderSolitaireDifficulty.FourSuits, "高级 · 4 花色"),
        ];
        _selectedDifficulty = DifficultyOptions[0];
        _game = new SpiderSolitaireGame(_selectedDifficulty.Definition, _shuffler);
        _gameTimer = new SpiderGameTimer(_timeProvider);

        if (enableDisplayRefreshTimer)
        {
            _displayRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            _displayRefreshTimer.Tick += OnDisplayRefreshTimerTick;
        }
    }

    public IReadOnlyList<SpiderSolitaireDifficultyOption> DifficultyOptions { get; }
    public int StockDealCount => _game.StockDealCount;
    public int CompletedRunCount => _game.CompletedRunCount;
    public int MoveCount => _game.ActionCount;
    public int Score => _game.Score;
    public bool IsWon => _game.State == SpiderGameState.Won;
    public bool IsWinOverlayVisible => IsWon && !_animationRunning;
    public bool CanInteract => !_disposed && !_animationRunning;
    public bool CanUndo => _game.CanUndo && !_animationRunning && !_disposed;
    public bool CanDeal => _game.CanDeal && !_animationRunning && !_disposed;
    public double BoardWidth => 840;

    /// <summary>按最长牌列计算绘制区域高度；极长牌列交给外层 ScrollViewer 承载。</summary>
    public double BoardHeight => Math.Max(
        620,
        160 + _game.Columns.Max(column => CalculateColumnHeight(column)));

    public string StatusText => _game.State switch
    {
        SpiderGameState.Won => "恭喜获胜",
        SpiderGameState.Ready => "准备开始",
        SpiderGameState.Running when _game.FindHint() is null => "无可用移动",
        SpiderGameState.Running => "进行中",
        _ => throw new InvalidOperationException("遇到了未知的蜘蛛纸牌状态。"),
    };

    internal SpiderGameSnapshot CurrentSnapshot => _game.CreateSnapshot();
    internal SpiderHint? CurrentHint { get; private set; }
    internal (int Column, int CardIndex)? Selection => _selection;
    internal bool IsTimerRunning => _gameTimer.IsRunning;
    internal bool IsAnimationRunning => _animationRunning;
    internal SpiderGameState GameState => _game.State;

    /// <summary>领域动作提交后发布动画描述。没有 View 订阅时领域与测试仍能独立运行。</summary>
    internal event EventHandler<SpiderAnimationPlan>? AnimationRequested;

    internal bool CanSelectSequence(int column, int cardIndex) =>
        !_disposed && !_animationRunning && _game.CanSelectSequence(column, cardIndex);

    /// <summary>供 View 在拖拽期间高亮目标；这里只转发领域判断，不在 View 中复制点数或花色规则。</summary>
    internal bool CanMove(int sourceColumn, int sourceIndex, int destinationColumn) =>
        !_disposed && !_animationRunning &&
        _game.CanMove(sourceColumn, sourceIndex, destinationColumn);

    /// <summary>向玩家解释落在牌列之外的无效拖拽；它不改变棋局、历史、计时或计分。</summary>
    internal void ReportInvalidDrop()
    {
        if (_disposed)
        {
            return;
        }

        MessageText = "无效落点，牌组已返回原位";
        NotifyBoardStateChanged();
    }

    internal bool IsLegalDestination(int destinationColumn) =>
        _selection is { } selection &&
        _game.CanMove(selection.Column, selection.CardIndex, destinationColumn);

    /// <summary>
    /// 处理不含物理设备信息的点击语义：先选择牌组，再点击目标列移动；
    /// 非法目标上的另一段合法牌组会替换当前选择。
    /// </summary>
    internal void HandleColumnClick(int column, int? cardIndex)
    {
        if (_disposed || _animationRunning || column < 0 || column >= 10)
        {
            return;
        }

        if (_selection is { } selected)
        {
            if (cardIndex == selected.CardIndex && column == selected.Column)
            {
                ClearSelection();
                return;
            }

            if (_game.CanMove(selected.Column, selected.CardIndex, column))
            {
                Move(selected.Column, selected.CardIndex, column);
                return;
            }
        }

        if (cardIndex is { } index && _game.CanSelectSequence(column, index))
        {
            _selection = (column, index);
            CurrentHint = null;
            MessageText = "已选择牌组，请点击目标列或直接拖动";
            NotifyBoardStateChanged();
            return;
        }

        MessageText = "这里不能放置当前牌组";
        NotifyBoardStateChanged();
    }

    /// <summary>由拖拽输入直接提交已确定的源牌组与目标列。</summary>
    internal bool Move(int sourceColumn, int sourceIndex, int destinationColumn)
    {
        if (_disposed || _animationRunning)
        {
            return false;
        }

        var transition = _game.Move(sourceColumn, sourceIndex, destinationColumn);
        if (transition is null)
        {
            MessageText = "该牌组不能移动到目标列";
            NotifyBoardStateChanged();
            return false;
        }

        ApplyTransition(transition);
        return true;
    }

    /// <summary>发一轮库存；空列或库存耗尽时只更新说明文字，不改变棋局。</summary>
    [RelayCommand(CanExecute = nameof(CanExecuteDeal))]
    private void Deal()
    {
        var transition = _game.Deal();
        if (transition is null)
        {
            MessageText = _game.StockDealCount == 0 ? "库存已经发完" : "请先填满所有空列再发牌";
            NotifyBoardStateChanged();
            return;
        }

        ApplyTransition(transition);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
    private void Undo()
    {
        var wasWon = _game.State == SpiderGameState.Won;
        var transition = _game.Undo();
        if (transition is null)
        {
            return;
        }

        if (wasWon)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        ApplyTransition(transition, startTimer: false);
    }

    /// <summary>显示确定性的一步提示；提示不计步、不扣分，也不提前执行移动。</summary>
    [RelayCommand(CanExecute = nameof(CanUseToolbar))]
    private void Hint()
    {
        CurrentHint = _game.FindHint();
        MessageText = CurrentHint switch
        {
            { Kind: SpiderHintKind.Move } hint =>
                $"提示：第 {hint.SourceColumn + 1} 列移动到第 {hint.DestinationColumn + 1} 列",
            { Kind: SpiderHintKind.Deal } => "提示：当前没有更合适的牌列移动，可以发一轮库存",
            _ when _game.StockDealCount > 0 && !_game.CanDeal => "请先填满空列，之后才能继续发牌",
            _ => "当前没有可用移动，可以撤销或重新开始",
        };
        NotifyBoardStateChanged();
    }

    /// <summary>按本局最初的洗牌顺序重新开始，方便玩家尝试另一条解法。</summary>
    [RelayCommand(CanExecute = nameof(CanUseToolbar))]
    private void ReplaySameDeal()
    {
        ResetPresentationState();
        _game.ReplaySameDeal();
        MessageText = "已按原牌序重新开始";
        NotifyBoardStateChanged();
    }

    /// <summary>保持当前难度并重新洗牌。</summary>
    [RelayCommand(CanExecute = nameof(CanUseToolbar))]
    private void NewGame()
    {
        ResetPresentationState();
        _game.StartNewGame(SelectedDifficulty.Definition);
        MessageText = "新牌局已准备好";
        NotifyBoardStateChanged();
    }

    partial void OnSelectedDifficultyChanged(SpiderSolitaireDifficultyOption value)
    {
        if (_disposed || value is null || _game.Difficulty == value.Definition)
        {
            return;
        }

        ResetPresentationState();
        _game.StartNewGame(value.Definition);
        MessageText = $"已切换到{value.DisplayName}";
        NotifyBoardStateChanged();
    }

    /// <summary>取消选择或拖拽时清理纯展示状态，不触碰领域棋局。</summary>
    internal void ClearSelection()
    {
        _selection = null;
        MessageText = "已取消选择";
        NotifyBoardStateChanged();
    }

    /// <summary>由棋盘动画开始/结束时设置，统一阻止工具栏和牌面并发修改状态。</summary>
    internal void SetAnimationRunning(bool value)
    {
        if (_disposed || _animationRunning == value)
        {
            return;
        }

        _animationRunning = value;
        NotifyCommandStates();
    }

    internal void RefreshElapsedTime() => ElapsedSeconds = _gameTimer.ElapsedSeconds;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _displayRefreshTimer?.Stop();
        if (_displayRefreshTimer is not null)
        {
            _displayRefreshTimer.Tick -= OnDisplayRefreshTimerTick;
        }

        _gameTimer.Stop();
        RefreshElapsedTime();
        _selection = null;
        CurrentHint = null;
        NotifyCommandStates();
    }

    private void ApplyTransition(SpiderGameTransition transition, bool startTimer = true)
    {
        if (startTimer && !_gameTimer.IsRunning && transition.Before.State == SpiderGameState.Ready)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        if (_game.State == SpiderGameState.Won)
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
        }

        _selection = null;
        CurrentHint = null;
        MessageText = _game.State == SpiderGameState.Won
            ? "八组同花色序列已经全部完成"
            : transition.Kind switch
            {
                SpiderActionKind.Deal => "已发一轮库存牌",
                SpiderActionKind.Undo => "已撤销上一步",
                _ => "移动成功",
            };
        RefreshElapsedTime();
        NotifyBoardStateChanged();
        AnimationRequested?.Invoke(this, SpiderAnimationPlan.Create(transition));
    }

    private void ResetPresentationState()
    {
        _displayRefreshTimer?.Stop();
        _gameTimer.Reset();
        ElapsedSeconds = 0;
        _selection = null;
        CurrentHint = null;
    }

    private void NotifyBoardStateChanged()
    {
        OnPropertyChanged(nameof(StockDealCount));
        OnPropertyChanged(nameof(CompletedRunCount));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(IsWon));
        OnPropertyChanged(nameof(IsWinOverlayVisible));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanDeal));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(BoardHeight));
        OnPropertyChanged(nameof(CurrentSnapshot));
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(CurrentHint));
        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(IsWinOverlayVisible));
        DealCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        HintCommand.NotifyCanExecuteChanged();
        ReplaySameDealCommand.NotifyCanExecuteChanged();
        NewGameCommand.NotifyCanExecuteChanged();
    }

    private bool CanExecuteDeal() => CanDeal;
    private bool CanExecuteUndo() => CanUndo;
    private bool CanUseToolbar() => !_disposed && !_animationRunning;

    private static double CalculateColumnHeight(IReadOnlyList<SpiderCard> column)
    {
        if (column.Count == 0)
        {
            return 100;
        }

        var height = 94d;
        for (var index = 0; index < column.Count - 1; index++)
        {
            height += column[index].IsFaceUp ? 26 : 13;
        }

        return height;
    }

    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();
}
