namespace ClassicGamePlugin.Features.Minesweeper.Domain;

/// <summary>
/// 定义一个最小的雷位生成策略。游戏引擎只依赖这一个能力，生产代码可以随机布雷，
/// 单元测试则可以给出完全确定的雷位，无需把随机数细节泄漏到其他职责中。
/// </summary>
internal interface IMinePlacementStrategy
{
    /// <summary>
    /// 在给定棋盘中选择互不重复的雷位，并且不得返回任何排除坐标。
    /// </summary>
    /// <param name="rows">棋盘行数。</param>
    /// <param name="columns">棋盘列数。</param>
    /// <param name="mineCount">需要生成的雷数。</param>
    /// <param name="excludedCoordinates">首击安全规则排除的坐标集合。</param>
    IReadOnlyCollection<CellCoordinate> CreateMines(
        int rows,
        int columns,
        int mineCount,
        IReadOnlySet<CellCoordinate> excludedCoordinates);
}
