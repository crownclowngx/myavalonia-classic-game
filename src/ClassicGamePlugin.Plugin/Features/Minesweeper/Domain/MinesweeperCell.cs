namespace ClassicGamePlugin.Features.Minesweeper.Domain;

/// <summary>表示一枚格子在玩家视角下的覆盖状态。</summary>
internal enum MinesweeperCellState
{
    Covered,
    Flagged,
    Revealed,
}

/// <summary>表示一局扫雷当前所处的生命周期阶段。</summary>
internal enum MinesweeperGameState
{
    Ready,
    Running,
    Won,
    Lost,
}

/// <summary>
/// 表示棋盘中的稳定坐标。行列均从零开始，坐标只负责寻址，不携带格子状态。
/// </summary>
internal readonly record struct CellCoordinate(int Row, int Column);

/// <summary>
/// 保存单个格子的领域状态。状态只能由 <see cref="MinesweeperGame"/> 修改，
/// 从而保证布雷、翻开数量和整局状态始终一致。
/// </summary>
internal sealed class MinesweeperCell(int row, int column)
{
    /// <summary>获取格子所在行，从零开始。</summary>
    internal int Row { get; } = row;

    /// <summary>获取格子所在列，从零开始。</summary>
    internal int Column { get; } = column;

    /// <summary>获取当前玩家可操作的覆盖状态。</summary>
    internal MinesweeperCellState State { get; set; } = MinesweeperCellState.Covered;

    /// <summary>获取当前格子是否为雷。首次有效翻格前始终为 false。</summary>
    internal bool IsMine { get; set; }

    /// <summary>获取周围八格中的雷数。</summary>
    internal int AdjacentMineCount { get; set; }

    /// <summary>获取当前格子是否为本局直接引爆的位置。</summary>
    internal bool IsExploded { get; set; }

    /// <summary>获取稳定坐标。</summary>
    internal CellCoordinate Coordinate => new(Row, Column);
}
