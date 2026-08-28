namespace ClassicGamePlugin.Features.Gomoku.Domain;

/// <summary>
/// 集中提供无状态的五子棋规则计算。禁手判断先在副本上假设落入黑棋，再按“合法五连优先、长连、
/// 双四、双三”的顺序判定；活三必须能够通过一个本身合法的后续点形成两端均可成五的直四，
/// 因而不会把靠近边界、白棋或后续禁手点的假活三误算为双三。
/// </summary>
internal static class GomokuRules
{
    internal const int BoardSize = 15;
    internal const int CellCount = BoardSize * BoardSize;

    private static readonly (int Row, int Column)[] Directions =
    [
        (0, 1),
        (1, 0),
        (1, 1),
        (1, -1),
    ];

    internal static GomokuGameSnapshot CreateInitialSnapshot(GomokuRuleSet ruleSet) =>
        new(new GomokuStone?[CellCount], ruleSet, GomokuStone.Black, GomokuGameState.Ready, 0, null, null);

    internal static GomokuMoveValidation ValidateMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        GomokuPosition position)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidatePosition(position);
        if (snapshot.State == GomokuGameState.Finished)
        {
            return new(false, GomokuMoveInvalidReason.Finished, GomokuForbiddenReason.None);
        }

        if (snapshot.CurrentPlayer != player)
        {
            return new(false, GomokuMoveInvalidReason.WrongPlayer, GomokuForbiddenReason.None);
        }

        if (snapshot.GetStone(position) is not null)
        {
            return new(false, GomokuMoveInvalidReason.Occupied, GomokuForbiddenReason.None);
        }

        if (snapshot.RuleSet == GomokuRuleSet.Forbidden && player == GomokuStone.Black)
        {
            var reasons = GetForbiddenReasons(snapshot, position);
            if (reasons != GomokuForbiddenReason.None)
            {
                return new(false, GomokuMoveInvalidReason.Forbidden, reasons);
            }
        }

        return GomokuMoveValidation.Legal;
    }

    internal static GomokuMoveResult? TryApplyMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        GomokuPosition position)
    {
        var validation = ValidateMove(snapshot, player, position);
        if (!validation.IsLegal)
        {
            return null;
        }

        var board = snapshot.CopyBoard();
        board[IndexOf(position)] = player;
        var winningLines = GetWinningLines(board, position, player, snapshot.RuleSet);
        var finished = winningLines.Count > 0 || board.All(stone => stone is not null);
        var after = new GomokuGameSnapshot(
            board,
            snapshot.RuleSet,
            OpponentOf(player),
            finished ? GomokuGameState.Finished : GomokuGameState.Running,
            snapshot.MoveCount + 1,
            position,
            winningLines.Count > 0 ? player : null,
            winningLines);
        return new GomokuMoveResult(player, position, snapshot.Clone(), after);
    }

    /// <summary>按行优先返回合法点，使 AI 同分、提示和测试结果保持可重复。</summary>
    internal static IReadOnlyList<GomokuPosition> GetLegalMoves(
        GomokuGameSnapshot snapshot,
        GomokuStone player)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State == GomokuGameState.Finished || snapshot.CurrentPlayer != player)
        {
            return [];
        }

        var moves = new List<GomokuPosition>();
        var forbidden = snapshot.RuleSet == GomokuRuleSet.Forbidden && player == GomokuStone.Black
            ? GetForbiddenPoints(snapshot)
            : null;
        for (var row = 0; row < BoardSize; row++)
        {
            for (var column = 0; column < BoardSize; column++)
            {
                var position = new GomokuPosition(row, column);
                if (snapshot.GetStone(position) is null && (forbidden is null || !forbidden.ContainsKey(position)))
                {
                    moves.Add(position);
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// 返回黑方当前全部禁手点。一次扫描共享递归缓存，避免界面逐点请求时重复分析同一派生棋形。
    /// </summary>
    internal static IReadOnlyDictionary<GomokuPosition, GomokuForbiddenReason> GetForbiddenPoints(
        GomokuGameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.RuleSet != GomokuRuleSet.Forbidden || snapshot.State == GomokuGameState.Finished)
        {
            return new Dictionary<GomokuPosition, GomokuForbiddenReason>();
        }

        var board = snapshot.CopyBoard();
        var context = new ForbiddenEvaluationContext();
        var result = new Dictionary<GomokuPosition, GomokuForbiddenReason>();
        for (var row = 0; row < BoardSize; row++)
        {
            for (var column = 0; column < BoardSize; column++)
            {
                var position = new GomokuPosition(row, column);
                if (board[IndexOf(position)] is not null)
                {
                    continue;
                }

                var reasons = EvaluateForbiddenMove(board, position, context, recursionDepth: 0);
                if (reasons != GomokuForbiddenReason.None)
                {
                    result[position] = reasons;
                }
            }
        }

        return result;
    }

    internal static GomokuForbiddenReason GetForbiddenReasons(
        GomokuGameSnapshot snapshot,
        GomokuPosition position)
    {
        ValidatePosition(position);
        if (snapshot.RuleSet != GomokuRuleSet.Forbidden || snapshot.GetStone(position) is not null)
        {
            return GomokuForbiddenReason.None;
        }

        return EvaluateForbiddenMove(snapshot.CopyBoard(), position, new ForbiddenEvaluationContext(), 0);
    }

    /// <summary>生成 AI 候选点；规则仍由 ValidateMove 最终把关，邻域裁剪只降低搜索分支数。</summary>
    internal static IReadOnlyList<GomokuPosition> GetCandidateMoves(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        int radius = 2)
    {
        if (snapshot.MoveCount == 0)
        {
            return [new GomokuPosition(BoardSize / 2, BoardSize / 2)];
        }

        var candidates = new HashSet<GomokuPosition>();
        for (var row = 0; row < BoardSize; row++)
        {
            for (var column = 0; column < BoardSize; column++)
            {
                if (snapshot.GetStone(new GomokuPosition(row, column)) is null)
                {
                    continue;
                }

                for (var rowOffset = -radius; rowOffset <= radius; rowOffset++)
                {
                    for (var columnOffset = -radius; columnOffset <= radius; columnOffset++)
                    {
                        var candidate = new GomokuPosition(row + rowOffset, column + columnOffset);
                        if (IsInside(candidate.Row, candidate.Column) &&
                            snapshot.GetStone(candidate) is null &&
                            ValidateMove(snapshot, player, candidate).IsLegal)
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
            }
        }

        return candidates.OrderBy(position => position.Row).ThenBy(position => position.Column).ToArray();
    }

    internal static int GetPlacementPatternScore(
        GomokuGameSnapshot snapshot,
        GomokuPosition position,
        GomokuStone player)
    {
        if (snapshot.GetStone(position) is not null)
        {
            return int.MinValue;
        }

        var board = snapshot.CopyBoard();
        board[IndexOf(position)] = player;
        var total = 0;
        foreach (var direction in Directions)
        {
            var before = CountDirection(board, position, player, -direction.Row, -direction.Column);
            var after = CountDirection(board, position, player, direction.Row, direction.Column);
            var length = before + 1 + after;
            var first = new GomokuPosition(
                position.Row - ((before + 1) * direction.Row),
                position.Column - ((before + 1) * direction.Column));
            var last = new GomokuPosition(
                position.Row + ((after + 1) * direction.Row),
                position.Column + ((after + 1) * direction.Column));
            var openEnds = (IsEmpty(board, first) ? 1 : 0) + (IsEmpty(board, last) ? 1 : 0);
            total += (length, openEnds) switch
            {
                ( >= 5, _) => 1_000_000,
                (4, 2) => 120_000,
                (4, 1) => 35_000,
                (3, 2) => 9_000,
                (3, 1) => 1_500,
                (2, 2) => 500,
                (2, 1) => 80,
                _ => 5,
            };
        }

        var center = BoardSize / 2;
        total += 20 - (Math.Abs(position.Row - center) + Math.Abs(position.Column - center));
        return total;
    }

    internal static GomokuStone OpponentOf(GomokuStone stone) =>
        stone == GomokuStone.Black ? GomokuStone.White : GomokuStone.Black;

    internal static void ValidatePosition(GomokuPosition position)
    {
        if (!IsInside(position.Row, position.Column))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "五子棋坐标必须位于 15×15 棋盘内。");
        }
    }

    internal static bool IsInside(int row, int column) =>
        row >= 0 && row < BoardSize && column >= 0 && column < BoardSize;

    private static GomokuForbiddenReason EvaluateForbiddenMove(
        GomokuStone?[] source,
        GomokuPosition position,
        ForbiddenEvaluationContext context,
        int recursionDepth)
    {
        if (source[IndexOf(position)] is not null)
        {
            return GomokuForbiddenReason.None;
        }

        var board = (GomokuStone?[])source.Clone();
        board[IndexOf(position)] = GomokuStone.Black;
        var cacheKey = CreateCacheKey(board, position);
        if (context.Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // 国际连珠口径中，黑方同一手已经形成恰好五连时优先获胜，不再按该手产生的其他形状判禁。
        if (HasExactFive(board, position, GomokuStone.Black))
        {
            context.Cache[cacheKey] = GomokuForbiddenReason.None;
            return GomokuForbiddenReason.None;
        }

        var reasons = HasOverline(board, position, GomokuStone.Black)
            ? GomokuForbiddenReason.Overline
            : GomokuForbiddenReason.None;
        if (CountFourShapes(board, position) >= 2)
        {
            reasons |= GomokuForbiddenReason.DoubleFour;
        }

        // 极端构造局面可能形成很深的递归活三依赖。十二层足以覆盖实际棋形，同时给 UI/AI 明确的复杂度上界。
        if (recursionDepth < 12 && CountOpenThrees(board, position, context, recursionDepth) >= 2)
        {
            reasons |= GomokuForbiddenReason.DoubleThree;
        }

        context.Cache[cacheKey] = reasons;
        return reasons;
    }

    private static int CountFourShapes(GomokuStone?[] board, GomokuPosition origin)
    {
        var shapes = new HashSet<string>(StringComparer.Ordinal);
        for (var directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
        {
            var direction = Directions[directionIndex];
            for (var startOffset = -4; startOffset <= 0; startOffset++)
            {
                var positions = Enumerable.Range(0, 5)
                    .Select(index => new GomokuPosition(
                        origin.Row + ((startOffset + index) * direction.Row),
                        origin.Column + ((startOffset + index) * direction.Column)))
                    .ToArray();
                if (positions.Any(position => !IsInside(position.Row, position.Column)) || !positions.Contains(origin))
                {
                    continue;
                }

                var black = positions.Where(position => board[IndexOf(position)] == GomokuStone.Black).ToArray();
                var empty = positions.Where(position => board[IndexOf(position)] is null).ToArray();
                if (black.Length != 4 || empty.Length != 1)
                {
                    continue;
                }

                var winningBoard = (GomokuStone?[])board.Clone();
                winningBoard[IndexOf(empty[0])] = GomokuStone.Black;
                if (!HasExactFive(winningBoard, empty[0], GomokuStone.Black))
                {
                    continue;
                }

                var ordered = black.OrderBy(position => position.Row).ThenBy(position => position.Column);
                shapes.Add($"{directionIndex}:{string.Join(';', ordered.Select(position => IndexOf(position)))}");
            }
        }

        return shapes.Count;
    }

    private static int CountOpenThrees(
        GomokuStone?[] board,
        GomokuPosition origin,
        ForbiddenEvaluationContext context,
        int recursionDepth)
    {
        var count = 0;
        foreach (var direction in Directions)
        {
            if (DirectionContainsOpenThree(board, origin, direction, context, recursionDepth))
            {
                count++;
            }
        }

        return count;
    }

    private static bool DirectionContainsOpenThree(
        GomokuStone?[] board,
        GomokuPosition origin,
        (int Row, int Column) direction,
        ForbiddenEvaluationContext context,
        int recursionDepth)
    {
        // 活三的必要条件是：当前已经有三枚黑棋能够通过唯一一个空点补成连续四子，且该四子的两端均为空。
        // 先做这个结构剪枝，再递归验证补四点本身是否合法，避免在空棋盘上枚举无意义的未来落子。
        for (var startOffset = -3; startOffset <= 0; startOffset++)
        {
            var straightFour = Enumerable.Range(0, 4)
                .Select(index => new GomokuPosition(
                    origin.Row + ((startOffset + index) * direction.Row),
                    origin.Column + ((startOffset + index) * direction.Column)))
                .ToArray();
            if (!straightFour.Contains(origin) ||
                straightFour.Any(position => !IsInside(position.Row, position.Column)))
            {
                continue;
            }

            var blackCount = straightFour.Count(position => board[IndexOf(position)] == GomokuStone.Black);
            var empty = straightFour.Where(position => board[IndexOf(position)] is null).ToArray();
            if (blackCount != 3 || empty.Length != 1)
            {
                continue;
            }

            var before = new GomokuPosition(
                origin.Row + ((startOffset - 1) * direction.Row),
                origin.Column + ((startOffset - 1) * direction.Column));
            var after = new GomokuPosition(
                origin.Row + ((startOffset + 4) * direction.Row),
                origin.Column + ((startOffset + 4) * direction.Column));
            if (!IsEmpty(board, before) || !IsEmpty(board, after))
            {
                continue;
            }

            var follow = empty[0];
            if (EvaluateForbiddenMove(board, follow, context, recursionDepth + 1) != GomokuForbiddenReason.None)
            {
                continue;
            }

            var next = (GomokuStone?[])board.Clone();
            next[IndexOf(follow)] = GomokuStone.Black;
            if (IsLegalWinningEnd(next, before) && IsLegalWinningEnd(next, after))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegalWinningEnd(GomokuStone?[] board, GomokuPosition position)
    {
        if (!IsInside(position.Row, position.Column) || board[IndexOf(position)] is not null)
        {
            return false;
        }

        var next = (GomokuStone?[])board.Clone();
        next[IndexOf(position)] = GomokuStone.Black;
        return HasExactFive(next, position, GomokuStone.Black);
    }

    private static IReadOnlyList<IReadOnlyList<GomokuPosition>> GetWinningLines(
        GomokuStone?[] board,
        GomokuPosition lastMove,
        GomokuStone player,
        GomokuRuleSet ruleSet)
    {
        var lines = new List<IReadOnlyList<GomokuPosition>>();
        foreach (var direction in Directions)
        {
            var line = GetRun(board, lastMove, player, direction.Row, direction.Column);
            var wins = ruleSet == GomokuRuleSet.Freestyle || player == GomokuStone.White
                ? line.Count >= 5
                : line.Count == 5;
            if (wins)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static bool HasExactFive(GomokuStone?[] board, GomokuPosition origin, GomokuStone player) =>
        Directions.Any(direction => GetRun(board, origin, player, direction.Row, direction.Column).Count == 5);

    private static bool HasOverline(GomokuStone?[] board, GomokuPosition origin, GomokuStone player) =>
        Directions.Any(direction => GetRun(board, origin, player, direction.Row, direction.Column).Count >= 6);

    private static IReadOnlyList<GomokuPosition> GetRun(
        GomokuStone?[] board,
        GomokuPosition origin,
        GomokuStone player,
        int rowDirection,
        int columnDirection)
    {
        var before = new List<GomokuPosition>();
        var row = origin.Row - rowDirection;
        var column = origin.Column - columnDirection;
        while (IsInside(row, column) && board[IndexOf(new GomokuPosition(row, column))] == player)
        {
            before.Add(new GomokuPosition(row, column));
            row -= rowDirection;
            column -= columnDirection;
        }

        before.Reverse();
        before.Add(origin);
        row = origin.Row + rowDirection;
        column = origin.Column + columnDirection;
        while (IsInside(row, column) && board[IndexOf(new GomokuPosition(row, column))] == player)
        {
            before.Add(new GomokuPosition(row, column));
            row += rowDirection;
            column += columnDirection;
        }

        return before;
    }

    private static int CountDirection(
        GomokuStone?[] board,
        GomokuPosition origin,
        GomokuStone player,
        int rowDirection,
        int columnDirection)
    {
        var count = 0;
        var row = origin.Row + rowDirection;
        var column = origin.Column + columnDirection;
        while (IsInside(row, column) && board[IndexOf(new GomokuPosition(row, column))] == player)
        {
            count++;
            row += rowDirection;
            column += columnDirection;
        }

        return count;
    }

    private static bool IsEmpty(GomokuStone?[] board, GomokuPosition position) =>
        IsInside(position.Row, position.Column) && board[IndexOf(position)] is null;

    private static string CreateCacheKey(GomokuStone?[] board, GomokuPosition position)
    {
        var characters = new char[CellCount + 1];
        for (var index = 0; index < board.Length; index++)
        {
            characters[index] = board[index] switch
            {
                GomokuStone.Black => 'B',
                GomokuStone.White => 'W',
                _ => '.',
            };
        }

        characters[^1] = (char)IndexOf(position);
        return new string(characters);
    }

    private static int IndexOf(GomokuPosition position) =>
        (position.Row * BoardSize) + position.Column;

    private sealed class ForbiddenEvaluationContext
    {
        internal Dictionary<string, GomokuForbiddenReason> Cache { get; } = new(StringComparer.Ordinal);
    }
}
