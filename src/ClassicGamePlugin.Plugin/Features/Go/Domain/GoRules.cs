namespace ClassicGamePlugin.Features.Go.Domain;

/// <summary>
/// 集中提供无状态的围棋落子计算。算法先在棋盘副本上放入新子，再逐块提取无气的相邻对方棋组，
/// 最后检查己方棋组是否仍有气以及新棋盘是否违反位置全局同形；任何失败都不会修改传入快照。
/// </summary>
internal static class GoRules
{
    internal const int BoardSize = 19;
    internal const int CellCount = BoardSize * BoardSize;

    private static readonly (int Row, int Column)[] Directions =
    [
        (-1, 0),
        (1, 0),
        (0, -1),
        (0, 1),
    ];

    internal static GoGameSnapshot CreateInitialSnapshot() =>
        new(
            new GoStone?[CellCount],
            GoStone.Black,
            GoGameState.Ready,
            moveCount: 0,
            actionCount: 0,
            consecutivePasses: 0,
            blackCaptures: 0,
            whiteCaptures: 0,
            lastMove: null);

    internal static GoMoveValidation ValidateMove(
        GoGameSnapshot snapshot,
        GoPosition position,
        IReadOnlySet<string> seenBoardKeys)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(seenBoardKeys);
        if (!IsInside(position.Row, position.Column))
        {
            return new(false, GoMoveInvalidReason.OutsideBoard);
        }

        if (snapshot.State is not (GoGameState.Ready or GoGameState.Playing))
        {
            return new(false, GoMoveInvalidReason.WrongPhase);
        }

        if (snapshot.GetStone(position) is not null)
        {
            return new(false, GoMoveInvalidReason.Occupied);
        }

        var simulation = SimulateMove(snapshot, snapshot.CurrentPlayer, position);
        if (simulation is null)
        {
            return new(false, GoMoveInvalidReason.Suicide);
        }

        return seenBoardKeys.Contains(CreateBoardKey(simulation.Value.Board))
            ? new(false, GoMoveInvalidReason.Superko)
            : GoMoveValidation.Legal;
    }

    internal static GoMoveResult? TryApplyMove(
        GoGameSnapshot snapshot,
        GoPosition position,
        IReadOnlySet<string> seenBoardKeys)
    {
        var validation = ValidateMove(snapshot, position, seenBoardKeys);
        if (!validation.IsLegal)
        {
            return null;
        }

        var player = snapshot.CurrentPlayer;
        var simulation = SimulateMove(snapshot, player, position)!.Value;
        var capturedCount = simulation.CapturedPositions.Count;
        var after = new GoGameSnapshot(
            simulation.Board,
            OpponentOf(player),
            GoGameState.Playing,
            snapshot.MoveCount + 1,
            snapshot.ActionCount + 1,
            consecutivePasses: 0,
            snapshot.BlackCaptures + (player == GoStone.Black ? capturedCount : 0),
            snapshot.WhiteCaptures + (player == GoStone.White ? capturedCount : 0),
            position);
        return new GoMoveResult(
            player,
            position,
            simulation.CapturedPositions.AsReadOnly(),
            snapshot.Clone(),
            after);
    }

    /// <summary>返回指定棋子的完整正交连接棋组；空点返回空集合。</summary>
    internal static IReadOnlyList<GoPosition> GetGroup(GoGameSnapshot snapshot, GoPosition origin)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidatePosition(origin);
        var board = snapshot.CopyBoard();
        return GetGroup(board, origin).AsReadOnly();
    }

    /// <summary>返回棋组的不同气点数量，边界和重复相邻空点只计算一次。</summary>
    internal static int CountLiberties(GoGameSnapshot snapshot, GoPosition origin)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidatePosition(origin);
        var board = snapshot.CopyBoard();
        var group = GetGroup(board, origin);
        return CountLiberties(board, group);
    }

    internal static GoStone OpponentOf(GoStone stone) =>
        stone == GoStone.Black ? GoStone.White : GoStone.Black;

    internal static bool IsInside(int row, int column) =>
        row >= 0 && row < BoardSize && column >= 0 && column < BoardSize;

    internal static void ValidatePosition(GoPosition position)
    {
        if (!IsInside(position.Row, position.Column))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "围棋坐标必须位于 19×19 棋盘内。");
        }
    }

    internal static int IndexOf(GoPosition position) =>
        (position.Row * BoardSize) + position.Column;

    internal static GoPosition PositionOf(int index) =>
        new(index / BoardSize, index % BoardSize);

    internal static string CreateBoardKey(IEnumerable<GoStone?> board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return new string(board.Select(stone => stone switch
        {
            GoStone.Black => 'B',
            GoStone.White => 'W',
            _ => '.',
        }).ToArray());
    }

    internal static IEnumerable<GoPosition> NeighborsOf(GoPosition position)
    {
        foreach (var direction in Directions)
        {
            var row = position.Row + direction.Row;
            var column = position.Column + direction.Column;
            if (IsInside(row, column))
            {
                yield return new GoPosition(row, column);
            }
        }
    }

    private static (GoStone?[] Board, List<GoPosition> CapturedPositions)? SimulateMove(
        GoGameSnapshot snapshot,
        GoStone player,
        GoPosition position)
    {
        var board = snapshot.CopyBoard();
        board[IndexOf(position)] = player;
        var opponent = OpponentOf(player);
        var captured = new HashSet<GoPosition>();

        foreach (var neighbor in NeighborsOf(position))
        {
            if (board[IndexOf(neighbor)] != opponent)
            {
                continue;
            }

            var group = GetGroup(board, neighbor);
            if (CountLiberties(board, group) != 0)
            {
                continue;
            }

            foreach (var capturedPosition in group)
            {
                board[IndexOf(capturedPosition)] = null;
                captured.Add(capturedPosition);
            }
        }

        var ownGroup = GetGroup(board, position);
        if (CountLiberties(board, ownGroup) == 0)
        {
            return null;
        }

        return (board, captured.OrderBy(item => item.Row).ThenBy(item => item.Column).ToList());
    }

    private static List<GoPosition> GetGroup(GoStone?[] board, GoPosition origin)
    {
        var color = board[IndexOf(origin)];
        if (color is null)
        {
            return [];
        }

        var group = new List<GoPosition>();
        var visited = new HashSet<GoPosition> { origin };
        var pending = new Queue<GoPosition>();
        pending.Enqueue(origin);
        while (pending.TryDequeue(out var current))
        {
            group.Add(current);
            foreach (var neighbor in NeighborsOf(current))
            {
                if (board[IndexOf(neighbor)] == color && visited.Add(neighbor))
                {
                    pending.Enqueue(neighbor);
                }
            }
        }

        return group;
    }

    private static int CountLiberties(GoStone?[] board, IEnumerable<GoPosition> group)
    {
        var liberties = new HashSet<GoPosition>();
        foreach (var stone in group)
        {
            foreach (var neighbor in NeighborsOf(stone))
            {
                if (board[IndexOf(neighbor)] is null)
                {
                    liberties.Add(neighbor);
                }
            }
        }

        return liberties.Count;
    }
}
