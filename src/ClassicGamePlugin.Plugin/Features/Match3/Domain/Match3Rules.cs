namespace ClassicGamePlugin.Features.Match3.Domain;

/// <summary>
/// 消消乐的纯棋盘规则。这里不持有分数、步数、随机源或 UI 状态，使匹配和合法性可以独立验证。
/// </summary>
internal static class Match3Rules
{
    internal const int BoardSize = 8;
    internal const int CellCount = BoardSize * BoardSize;

    internal static int ToIndex(Match3Position position) =>
        (position.Row * BoardSize) + position.Column;

    internal static Match3Position ToPosition(int index) =>
        new(index / BoardSize, index % BoardSize);

    internal static bool IsInside(Match3Position position) =>
        position.Row >= 0 && position.Row < BoardSize &&
        position.Column >= 0 && position.Column < BoardSize;

    internal static bool AreAdjacent(Match3Position first, Match3Position second) =>
        Math.Abs(first.Row - second.Row) + Math.Abs(first.Column - second.Column) == 1;

    internal static IReadOnlyList<Match3MatchRun> FindRuns(IReadOnlyList<Match3Tile?> board)
    {
        ValidateBoard(board);
        var runs = new List<Match3MatchRun>();
        for (var row = 0; row < BoardSize; row++)
        {
            ScanLine(board, runs, isHorizontal: true, row);
        }

        for (var column = 0; column < BoardSize; column++)
        {
            ScanLine(board, runs, isHorizontal: false, column);
        }

        return runs;
    }

    internal static bool HasAnyMatch(IReadOnlyList<Match3Tile?> board) => FindRuns(board).Count > 0;

    internal static bool IsDirectSpecialSwap(Match3Tile first, Match3Tile second) =>
        first.Special == Match3SpecialKind.Rainbow ||
        second.Special == Match3SpecialKind.Rainbow ||
        first.Special != Match3SpecialKind.None && second.Special != Match3SpecialKind.None;

    internal static bool IsLegalSwap(
        IReadOnlyList<Match3Tile?> board,
        Match3Position source,
        Match3Position target)
    {
        ValidateBoard(board);
        if (!IsInside(source) || !IsInside(target) || !AreAdjacent(source, target))
        {
            return false;
        }

        var first = board[ToIndex(source)];
        var second = board[ToIndex(target)];
        if (first is null || second is null)
        {
            return false;
        }

        if (IsDirectSpecialSwap(first.Value, second.Value))
        {
            return true;
        }

        var candidate = board.ToArray();
        Swap(candidate, source, target);
        return FindRuns(candidate)
            .SelectMany(run => run.Positions)
            .Any(position => position == source || position == target);
    }

    internal static bool TryFindFirstLegalSwap(
        IReadOnlyList<Match3Tile?> board,
        out Match3Position source,
        out Match3Position target)
    {
        for (var row = 0; row < BoardSize; row++)
        {
            for (var column = 0; column < BoardSize; column++)
            {
                source = new Match3Position(row, column);
                if (column + 1 < BoardSize)
                {
                    target = new Match3Position(row, column + 1);
                    if (IsLegalSwap(board, source, target))
                    {
                        return true;
                    }
                }

                if (row + 1 < BoardSize)
                {
                    target = new Match3Position(row + 1, column);
                    if (IsLegalSwap(board, source, target))
                    {
                        return true;
                    }
                }
            }
        }

        source = default;
        target = default;
        return false;
    }

    internal static void Swap(Match3Tile?[] board, Match3Position source, Match3Position target) =>
        (board[ToIndex(source)], board[ToIndex(target)]) =
            (board[ToIndex(target)], board[ToIndex(source)]);

    internal static void ValidateBoard(IReadOnlyList<Match3Tile?> board)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (board.Count != CellCount)
        {
            throw new ArgumentException($"消消乐棋盘必须恰好包含 {CellCount} 个格子。", nameof(board));
        }

        foreach (var tile in board.Where(tile => tile is not null).Select(tile => tile!.Value))
        {
            if (tile.Special == Match3SpecialKind.Rainbow && tile.Kind is not null ||
                tile.Special != Match3SpecialKind.Rainbow && tile.Kind is null)
            {
                throw new ArgumentException("只有彩虹球可以没有普通颜色。", nameof(board));
            }
        }
    }

    private static void ScanLine(
        IReadOnlyList<Match3Tile?> board,
        ICollection<Match3MatchRun> runs,
        bool isHorizontal,
        int fixedCoordinate)
    {
        var start = 0;
        while (start < BoardSize)
        {
            var position = isHorizontal
                ? new Match3Position(fixedCoordinate, start)
                : new Match3Position(start, fixedCoordinate);
            var tile = board[ToIndex(position)];
            if (tile is null || tile.Value.Kind is null)
            {
                start++;
                continue;
            }

            var end = start + 1;
            while (end < BoardSize)
            {
                var nextPosition = isHorizontal
                    ? new Match3Position(fixedCoordinate, end)
                    : new Match3Position(end, fixedCoordinate);
                var next = board[ToIndex(nextPosition)];
                if (next is null || next.Value.Kind != tile.Value.Kind)
                {
                    break;
                }

                end++;
            }

            if (end - start >= 3)
            {
                var positions = Enumerable.Range(start, end - start)
                    .Select(variable => isHorizontal
                        ? new Match3Position(fixedCoordinate, variable)
                        : new Match3Position(variable, fixedCoordinate))
                    .ToArray();
                runs.Add(new Match3MatchRun(isHorizontal, positions));
            }

            start = end;
        }
    }
}
