namespace ClassicGamePlugin.Features.Minesweeper.Domain;

/// <summary>玩家可见的数字与旗帜快照；不携带真实雷位或隐藏数字。所有旗帜均按雷计数。</summary>
internal readonly record struct MinesweeperVisibleCell(int? RevealedNumber, bool IsFlagged = false);

/// <summary>仅从已打开数字的八邻域直接确定雷位，不猜测，也不返回开格操作。</summary>
internal static class MinesweeperDeduction
{
    internal static IReadOnlyCollection<CellCoordinate> FindCertainMines(
        int rows,
        int columns,
        IReadOnlyList<MinesweeperVisibleCell> cells)
    {
        if (rows <= 0 || columns <= 0 || cells.Count != rows * columns)
        {
            throw new ArgumentException("可见棋盘必须与行列尺寸一致。", nameof(cells));
        }

        var certainMines = new HashSet<CellCoordinate>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (cells[(row * columns) + column].RevealedNumber is not > 0)
                {
                    continue;
                }

                var covered = new List<CellCoordinate>(8);
                var flagCount = 0;
                for (var neighborRow = Math.Max(0, row - 1); neighborRow <= Math.Min(rows - 1, row + 1); neighborRow++)
                {
                    for (var neighborColumn = Math.Max(0, column - 1); neighborColumn <= Math.Min(columns - 1, column + 1); neighborColumn++)
                    {
                        var neighbor = cells[(neighborRow * columns) + neighborColumn];
                        if (neighbor.IsFlagged)
                        {
                            flagCount++;
                        }
                        else if (neighbor.RevealedNumber is null)
                        {
                            covered.Add(new CellCoordinate(neighborRow, neighborColumn));
                        }
                    }
                }

                // 用户主动插下的旗帜和自动旗帜一视同仁，均按雷参与计算。
                // 只插旗、不自动开格；旗帜超过数字或没有待插旗格时不继续推理。
                var remainingMines = cells[(row * columns) + column].RevealedNumber - flagCount;
                if (remainingMines > 0 && covered.Count == remainingMines)
                {
                    certainMines.UnionWith(covered);
                }
            }
        }

        return certainMines.OrderBy(coordinate => coordinate.Row).ThenBy(coordinate => coordinate.Column).ToArray();
    }
}
