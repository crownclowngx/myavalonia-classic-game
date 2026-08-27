namespace ClassicGamePlugin.Features.Game2048.Domain;

/// <summary>
/// 提供与随机、分数总量和 UI 无关的纯移动规则。每条行或列先向移动方向压缩，再从靠近移动方向的一端
/// 成对合并，已经合并出的方块不会在同一次移动中再次参与合并。
/// </summary>
internal static class Game2048Rules
{
    internal const int BoardSize = 4;
    internal const int CellCount = BoardSize * BoardSize;

    /// <summary>在不修改输入棋盘的前提下计算一个方向的候选结果。</summary>
    internal static Game2048MoveProjection ProjectMove(
        IReadOnlyList<int> cells,
        Game2048Direction direction)
    {
        ValidateBoard(cells);
        var result = cells.ToArray();
        var scoreDelta = 0;
        var motions = new List<Game2048TileMotion>();
        var mergedPositions = new List<Game2048Position>();

        for (var lineIndex = 0; lineIndex < BoardSize; lineIndex++)
        {
            var line = new List<LineTile>(BoardSize);
            for (var offset = 0; offset < BoardSize; offset++)
            {
                var position = GetPosition(direction, lineIndex, offset);
                var value = cells[ToIndex(position.Row, position.Column)];
                if (value != 0)
                {
                    line.Add(new LineTile(value, position));
                }
            }

            var lineResult = CollapseAndMerge(line, direction, lineIndex);
            scoreDelta = checked(scoreDelta + lineResult.ScoreDelta);
            motions.AddRange(lineResult.Motions);
            mergedPositions.AddRange(lineResult.MergedPositions);
            for (var offset = 0; offset < BoardSize; offset++)
            {
                var position = GetPosition(direction, lineIndex, offset);
                result[ToIndex(position.Row, position.Column)] = lineResult.Cells[offset];
            }
        }

        return new Game2048MoveProjection(
            result,
            scoreDelta,
            !result.SequenceEqual(cells),
            motions.AsReadOnly(),
            mergedPositions.AsReadOnly());
    }

    /// <summary>
    /// 判断棋盘是否仍存在合法移动。只要存在空格，或任意一对水平/垂直相邻方块相等，就至少有一个方向可用。
    /// </summary>
    internal static bool HasAvailableMove(IReadOnlyList<int> cells)
    {
        ValidateBoard(cells);
        if (cells.Contains(0))
        {
            return true;
        }

        for (var row = 0; row < BoardSize; row++)
        {
            for (var column = 0; column < BoardSize; column++)
            {
                var value = cells[ToIndex(row, column)];
                if (column + 1 < BoardSize && value == cells[ToIndex(row, column + 1)] ||
                    row + 1 < BoardSize && value == cells[ToIndex(row + 1, column)])
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static int ToIndex(int row, int column) => (row * BoardSize) + column;

    private static LineProjection CollapseAndMerge(
        IReadOnlyList<LineTile> line,
        Game2048Direction direction,
        int lineIndex)
    {
        var merged = new int[BoardSize];
        var motions = new List<Game2048TileMotion>(line.Count);
        var mergedPositions = new List<Game2048Position>(line.Count / 2);
        var writeIndex = 0;
        var scoreDelta = 0;

        for (var readIndex = 0; readIndex < line.Count; readIndex++)
        {
            var tile = line[readIndex];
            var target = GetPosition(direction, lineIndex, writeIndex);
            var value = tile.Value;
            if (readIndex + 1 < line.Count && line[readIndex + 1].Value == value)
            {
                var other = line[readIndex + 1];
                motions.Add(new Game2048TileMotion(tile.Source, target, tile.Value, true));
                motions.Add(new Game2048TileMotion(other.Source, target, other.Value, true));
                mergedPositions.Add(target);
                value = checked(value * 2);
                scoreDelta = checked(scoreDelta + value);
                readIndex++;
            }
            else
            {
                motions.Add(new Game2048TileMotion(tile.Source, target, tile.Value, false));
            }

            merged[writeIndex++] = value;
        }

        return new LineProjection(merged, scoreDelta, motions, mergedPositions);
    }

    private static Game2048Position GetPosition(
        Game2048Direction direction,
        int lineIndex,
        int offset) =>
        direction switch
        {
            Game2048Direction.Left => new Game2048Position(lineIndex, offset),
            Game2048Direction.Right => new Game2048Position(lineIndex, BoardSize - 1 - offset),
            Game2048Direction.Up => new Game2048Position(offset, lineIndex),
            Game2048Direction.Down => new Game2048Position(BoardSize - 1 - offset, lineIndex),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "未知的移动方向。"),
        };

    private static void ValidateBoard(IReadOnlyList<int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count != CellCount)
        {
            throw new ArgumentException($"2048 棋盘必须恰好包含 {CellCount} 个格子。", nameof(cells));
        }
    }

    private readonly record struct LineTile(int Value, Game2048Position Source);

    private sealed record LineProjection(
        int[] Cells,
        int ScoreDelta,
        IReadOnlyList<Game2048TileMotion> Motions,
        IReadOnlyList<Game2048Position> MergedPositions);
}
