namespace ClassicGamePlugin.Features.Minesweeper.Domain;

/// <summary>
/// 使用 Fisher-Yates 局部洗牌生成无重复随机雷位。实现无状态，可以安全地被不同 Document 实例复用。
/// </summary>
internal sealed class RandomMinePlacementStrategy : IMinePlacementStrategy
{
    /// <inheritdoc />
    public IReadOnlyCollection<CellCoordinate> CreateMines(
        int rows,
        int columns,
        int mineCount,
        IReadOnlySet<CellCoordinate> excludedCoordinates)
    {
        ArgumentNullException.ThrowIfNull(excludedCoordinates);

        var candidates = new List<CellCoordinate>(rows * columns - excludedCoordinates.Count);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var coordinate = new CellCoordinate(row, column);
                if (!excludedCoordinates.Contains(coordinate))
                {
                    candidates.Add(coordinate);
                }
            }
        }

        if (mineCount < 0 || mineCount > candidates.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mineCount),
                "雷数必须落在首击安全区之外的可用格子数量范围内。");
        }

        // 只洗牌前 mineCount 个位置，既保持均匀随机，也避免无意义地打乱整张棋盘。
        for (var index = 0; index < mineCount; index++)
        {
            var swapIndex = Random.Shared.Next(index, candidates.Count);
            (candidates[index], candidates[swapIndex]) = (candidates[swapIndex], candidates[index]);
        }

        return candidates.GetRange(0, mineCount);
    }
}
