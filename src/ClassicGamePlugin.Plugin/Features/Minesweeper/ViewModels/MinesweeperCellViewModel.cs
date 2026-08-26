using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ClassicGamePlugin.Features.Minesweeper.Domain;

namespace ClassicGamePlugin.Features.Minesweeper.ViewModels;

/// <summary>
/// 把一个领域格子投影为 Avalonia 可绑定的文本和颜色。它不修改游戏状态，所有玩家操作仍交给上层 ViewModel。
/// </summary>
public sealed class MinesweeperCellViewModel : ObservableObject
{
    private static readonly IBrush CoveredBrush = Brush.Parse("#AEB8C4");
    private static readonly IBrush RevealedBrush = Brush.Parse("#E4E8ED");
    private static readonly IBrush MineBrush = Brush.Parse("#CDD3DA");
    private static readonly IBrush ExplodedBrush = Brush.Parse("#E45B5B");
    private static readonly IBrush IncorrectFlagBrush = Brush.Parse("#F2B8B5");
    private static readonly IBrush BorderBrushValue = Brush.Parse("#7D8998");
    private static readonly IBrush DefaultForegroundBrush = Brush.Parse("#253142");
    private static readonly IReadOnlyDictionary<int, IBrush> NumberBrushes =
        new Dictionary<int, IBrush>
        {
            [1] = Brush.Parse("#1D4ED8"),
            [2] = Brush.Parse("#15803D"),
            [3] = Brush.Parse("#DC2626"),
            [4] = Brush.Parse("#4338CA"),
            [5] = Brush.Parse("#7F1D1D"),
            [6] = Brush.Parse("#0F766E"),
            [7] = Brush.Parse("#111827"),
            [8] = Brush.Parse("#64748B"),
        };

    private readonly MinesweeperCell _cell;
    private readonly Func<MinesweeperGameState> _gameState;
    private bool _isChordPreviewed;

    internal MinesweeperCellViewModel(
        MinesweeperCell cell,
        Func<MinesweeperGameState> gameState)
    {
        _cell = cell ?? throw new ArgumentNullException(nameof(cell));
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
    }

    /// <summary>获取格子所在行，从零开始。</summary>
    public int Row => _cell.Row;

    /// <summary>获取格子所在列，从零开始。</summary>
    public int Column => _cell.Column;

    /// <summary>
    /// 获取格子是否已经翻开。该语义状态供 View 判断左右键组合是否属于经典快速展开，
    /// View 不读取领域对象，也不自行判断旗帜数量或相邻雷数。
    /// </summary>
    public bool IsRevealed => _cell.State == MinesweeperCellState.Revealed;

    /// <summary>
    /// 获取该格子是否处于数字格组合按键的邻域预览中。它只影响显示，不会提前翻格，
    /// 因而玩家在松开按键前可以清楚确认中心数字所对应的最多八个邻格。
    /// </summary>
    public bool IsChordPreviewed
    {
        get => _isChordPreviewed;
        private set => SetProperty(ref _isChordPreviewed, value);
    }

    /// <summary>获取领域格子的当前覆盖状态，供页面级 ViewModel 筛选预览目标。</summary>
    internal MinesweeperCellState CellState => _cell.State;

    /// <summary>获取相邻雷数，供页面级 ViewModel 拒绝对空白格启动组合预览。</summary>
    internal int AdjacentMineCount => _cell.AdjacentMineCount;

    /// <summary>获取当前格子的经典符号或相邻雷数。</summary>
    public string DisplayText
    {
        get
        {
            if (_gameState() == MinesweeperGameState.Lost)
            {
                if (_cell.IsMine)
                {
                    return "✹";
                }

                if (_cell.State == MinesweeperCellState.Flagged)
                {
                    return "×";
                }
            }

            return _cell.State switch
            {
                MinesweeperCellState.Flagged => "⚑",
                MinesweeperCellState.Revealed when _cell.AdjacentMineCount > 0 =>
                    _cell.AdjacentMineCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => string.Empty,
            };
        }
    }

    /// <summary>获取按覆盖、翻开和失败状态计算出的背景色。</summary>
    public IBrush Background
    {
        get
        {
            if (_gameState() == MinesweeperGameState.Lost)
            {
                if (_cell.IsExploded)
                {
                    return ExplodedBrush;
                }

                if (_cell.State == MinesweeperCellState.Flagged && !_cell.IsMine)
                {
                    return IncorrectFlagBrush;
                }

                if (_cell.IsMine)
                {
                    return MineBrush;
                }
            }

            return _cell.State == MinesweeperCellState.Revealed ? RevealedBrush : CoveredBrush;
        }
    }

    /// <summary>获取数字、旗帜和雷符号对应的前景色。</summary>
    public IBrush Foreground =>
        _cell.State == MinesweeperCellState.Revealed &&
        NumberBrushes.TryGetValue(_cell.AdjacentMineCount, out var numberBrush)
            ? numberBrush
            : DefaultForegroundBrush;

    /// <summary>获取统一的格子边框颜色。</summary>
    public IBrush BorderBrush => BorderBrushValue;

    /// <summary>
    /// 通知绑定重新读取全部派生视觉属性。领域状态一次操作可能批量变化，因此集中刷新比复制状态更不易失配。
    /// </summary>
    internal void Refresh()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(IsRevealed));
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(Foreground));
    }

    /// <summary>设置纯展示用的组合按键预览状态，不修改任何领域数据。</summary>
    internal void SetChordPreview(bool value) => IsChordPreviewed = value;
}
