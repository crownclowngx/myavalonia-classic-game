using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Minesweeper.Domain;

namespace ClassicGamePlugin.Features.Minesweeper.ViewModels;

/// <summary>
/// 承担扫雷 View 的全部可观察状态和用户命令。ViewModel 不实现 Plugin SDK 接口，也不解释鼠标设备；
/// 它只把抽象的“翻开格子、切换旗帜、重新开始”操作交给领域引擎，并投影结果。
/// </summary>
public sealed partial class MinesweeperViewModel : ObservableObject, IDisposable
{
    private const double CellSize = 30;
    private readonly MinesweeperGame _game;
    private readonly GameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private MinesweeperCellViewModel? _chordPreviewCenter;
    private bool _disposed;

    [ObservableProperty]
    private MinesweeperDifficultyOption _selectedDifficulty;

    [ObservableProperty]
    private int _elapsedSeconds;

    /// <summary>使用真实随机策略和系统时间创建生产环境 ViewModel。</summary>
    public MinesweeperViewModel()
        : this(new RandomMinePlacementStrategy(), TimeProvider.System, enableDisplayRefreshTimer: true)
    {
    }

    /// <summary>使用确定性布雷和时间源创建可测试 ViewModel。</summary>
    internal MinesweeperViewModel(
        IMinePlacementStrategy minePlacementStrategy,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
    {
        DifficultyOptions =
        [
            new(MinesweeperDifficultyDefinition.Beginner),
            new(MinesweeperDifficultyDefinition.Intermediate),
            new(MinesweeperDifficultyDefinition.Expert),
        ];
        _selectedDifficulty = DifficultyOptions[0];
        _game = new MinesweeperGame(_selectedDifficulty.Definition, minePlacementStrategy);
        _gameTimer = new GameTimer(timeProvider);
        RebuildBoardProjection();

        if (enableDisplayRefreshTimer)
        {
            _displayRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            _displayRefreshTimer.Tick += OnDisplayRefreshTimerTick;
        }
    }

    /// <summary>获取固定的经典三级选项。</summary>
    public IReadOnlyList<MinesweeperDifficultyOption> DifficultyOptions { get; }

    /// <summary>获取按行优先顺序排列的格子展示集合。</summary>
    public ObservableCollection<MinesweeperCellViewModel> BoardCells { get; } = [];

    /// <summary>获取当前棋盘行数。</summary>
    public int RowCount => _game.Difficulty.Rows;

    /// <summary>获取当前棋盘列数。</summary>
    public int ColumnCount => _game.Difficulty.Columns;

    /// <summary>获取固定格子尺寸与边框所需的棋盘宽度。</summary>
    public double BoardWidth => (ColumnCount * CellSize) + 4;

    /// <summary>获取固定格子尺寸与边框所需的棋盘高度。</summary>
    public double BoardHeight => (RowCount * CellSize) + 4;

    /// <summary>获取总雷数。</summary>
    public int TotalMineCount => _game.Difficulty.MineCount;

    /// <summary>获取总雷数减去当前旗帜数。</summary>
    public int RemainingMineCount => _game.RemainingMineCount;

    /// <summary>获取面向用户的当前对局状态。</summary>
    public string StatusText => _game.State switch
    {
        MinesweeperGameState.Ready => "准备开始",
        MinesweeperGameState.Running => "进行中",
        MinesweeperGameState.Won => "恭喜获胜",
        MinesweeperGameState.Lost => "游戏结束",
        _ => throw new InvalidOperationException("遇到了未知的扫雷状态。"),
    };

    /// <summary>获取重新开始按钮的简短状态符号。</summary>
    public string RestartSymbol => _game.State switch
    {
        MinesweeperGameState.Won => "😎",
        MinesweeperGameState.Lost => "😵",
        _ => "🙂",
    };

    /// <summary>获取内部状态，供同程序集逻辑和单元测试验证生命周期。</summary>
    internal MinesweeperGameState GameState => _game.State;

    /// <summary>获取内部计时状态，供生命周期测试验证释放行为。</summary>
    internal bool IsTimerRunning => _gameTimer.IsRunning;

    /// <summary>处理格子的主要操作：覆盖格翻开，已翻开的数字格尝试快速展开。</summary>
    public void RevealCell(MinesweeperCellViewModel cell)
    {
        if (_disposed || cell is null || !BoardCells.Contains(cell))
        {
            return;
        }

        var previousState = _game.State;
        if (!_game.Reveal(cell.Row, cell.Column))
        {
            return;
        }

        // 布雷和计时都从首次有效翻格开始；仅插旗不会消耗游戏时间。
        if (previousState == MinesweeperGameState.Ready && _game.State != MinesweeperGameState.Ready)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }

        StopTimerAfterTerminalState();
        RefreshProjection();
    }

    /// <summary>处理旗帜操作；ViewModel 不关心该操作来自鼠标右键还是其他输入设备。</summary>
    public void ToggleFlag(MinesweeperCellViewModel cell)
    {
        if (_disposed || cell is null || !BoardCells.Contains(cell) ||
            !_game.ToggleFlag(cell.Row, cell.Column))
        {
            return;
        }

        RefreshProjection();
    }

    /// <summary>
    /// 开始数字格的经典邻域按压预览。只有进行中的已翻开数字格可以成为中心；预览对象是其周围
    /// 仍被覆盖且未插旗的格子。这里不调用领域引擎，保证按键尚未松开时棋盘状态不会提前改变。
    /// </summary>
    /// <returns>是否成功建立了一个有效预览。</returns>
    internal bool BeginChordPreview(MinesweeperCellViewModel center)
    {
        ClearChordPreview();
        if (_disposed || !BoardCells.Contains(center) ||
            _game.State != MinesweeperGameState.Running ||
            !center.IsRevealed || center.AdjacentMineCount == 0)
        {
            return false;
        }

        _chordPreviewCenter = center;
        foreach (var cell in BoardCells.Where(cell =>
                     IsNeighbor(center, cell) &&
                     cell.CellState == MinesweeperCellState.Covered))
        {
            cell.SetChordPreview(true);
        }

        return true;
    }

    /// <summary>
    /// 结束预览并执行快速展开。只有松开事件仍对应原中心格时才提交操作；随后复用
    /// <see cref="RevealCell"/>，由领域层统一检查旗帜数量、展开邻格以及判定胜负。
    /// </summary>
    /// <returns>是否消费了一个与该中心格匹配的预览。</returns>
    internal bool CompleteChordPreview(MinesweeperCellViewModel center)
    {
        if (!ReferenceEquals(_chordPreviewCenter, center))
        {
            ClearChordPreview();
            return false;
        }

        ClearChordPreview();
        RevealCell(center);
        return true;
    }

    /// <summary>取消尚未提交的邻域预览，例如指针捕获丢失或重新开始时。</summary>
    internal void CancelChordPreview() => ClearChordPreview();

    /// <summary>创建当前难度的一张全新棋盘，并清空计时。</summary>
    [RelayCommand]
    private void Restart()
    {
        if (!_disposed)
        {
            StartNewGame(SelectedDifficulty.Definition);
        }
    }

    partial void OnSelectedDifficultyChanged(MinesweeperDifficultyOption value)
    {
        if (!_disposed && value is not null && _game.Difficulty != value.Definition)
        {
            StartNewGame(value.Definition);
        }
    }

    /// <summary>停止 UI 刷新和游戏计时，释放后所有玩家操作都会被忽略。</summary>
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
    }

    /// <summary>显式刷新计时投影，供 DispatcherTimer 和确定性单元测试共同调用。</summary>
    internal void RefreshElapsedTime()
    {
        ElapsedSeconds = _gameTimer.ElapsedSeconds;
    }

    private void StartNewGame(MinesweeperDifficultyDefinition difficulty)
    {
        ClearChordPreview();
        _displayRefreshTimer?.Stop();
        _gameTimer.Reset();
        ElapsedSeconds = 0;
        _game.StartNewGame(difficulty);
        RebuildBoardProjection();
        RefreshSummaryProperties();
    }

    private void RebuildBoardProjection()
    {
        BoardCells.Clear();
        foreach (var cell in _game.Cells)
        {
            BoardCells.Add(new MinesweeperCellViewModel(cell, () => _game.State));
        }

        OnPropertyChanged(nameof(RowCount));
        OnPropertyChanged(nameof(ColumnCount));
        OnPropertyChanged(nameof(BoardWidth));
        OnPropertyChanged(nameof(BoardHeight));
    }

    private void RefreshProjection()
    {
        foreach (var cell in BoardCells)
        {
            cell.Refresh();
        }

        RefreshElapsedTime();
        RefreshSummaryProperties();
    }

    private void RefreshSummaryProperties()
    {
        OnPropertyChanged(nameof(TotalMineCount));
        OnPropertyChanged(nameof(RemainingMineCount));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RestartSymbol));
    }

    private void StopTimerAfterTerminalState()
    {
        if (_game.State is not (MinesweeperGameState.Won or MinesweeperGameState.Lost))
        {
            return;
        }

        _gameTimer.Stop();
        _displayRefreshTimer?.Stop();
    }

    private static bool IsNeighbor(
        MinesweeperCellViewModel center,
        MinesweeperCellViewModel candidate) =>
        !ReferenceEquals(center, candidate) &&
        Math.Abs(center.Row - candidate.Row) <= 1 &&
        Math.Abs(center.Column - candidate.Column) <= 1;

    private void ClearChordPreview()
    {
        _chordPreviewCenter = null;
        foreach (var cell in BoardCells.Where(cell => cell.IsChordPreviewed))
        {
            cell.SetChordPreview(false);
        }
    }

    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();
}
