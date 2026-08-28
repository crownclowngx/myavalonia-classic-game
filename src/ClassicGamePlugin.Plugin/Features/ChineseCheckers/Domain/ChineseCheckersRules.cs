namespace ClassicGamePlugin.Features.ChineseCheckers.Domain;

/// <summary>
/// 中国跳棋纯规则入口。棋盘拓扑、单步、连续跳、营地约束、强制撤营和终局全部集中于此；
/// 本类型不保存真实棋局，也不处理撤销、异步电脑、计时或界面提示。
/// </summary>
internal static class ChineseCheckersRules
{
    internal const int CellCount = 121;
    internal static readonly IReadOnlyList<ChineseCheckersPosition> Directions =
    [
        new(1, -1, 0), new(1, 0, -1), new(0, 1, -1),
        new(-1, 1, 0), new(-1, 0, 1), new(0, -1, 1),
    ];

    private static readonly (ChineseCheckersPosition[] Positions, HashSet<ChineseCheckersPosition>[] Camps) Topology =
        CreatePositionsAndCamps();
    private static readonly ChineseCheckersPosition[] Positions = Topology.Positions;
    private static readonly IReadOnlyDictionary<ChineseCheckersPosition, int> PositionIndexes =
        Positions.Select((position, index) => (position, index)).ToDictionary(item => item.position, item => item.index);
    private static readonly HashSet<ChineseCheckersPosition>[] Camps = Topology.Camps;

    internal static IReadOnlyList<ChineseCheckersPosition> AllPositions => Positions;
    internal static IReadOnlySet<ChineseCheckersPosition> BlueHome => Camps[2];
    internal static IReadOnlySet<ChineseCheckersPosition> RedHome => Camps[5];

    internal static ChineseCheckersGameSnapshot CreateInitialSnapshot()
    {
        var board = new ChineseCheckersSide?[CellCount];
        foreach (var position in BlueHome)
        {
            board[PositionIndexes[position]] = ChineseCheckersSide.Blue;
        }

        foreach (var position in RedHome)
        {
            board[PositionIndexes[position]] = ChineseCheckersSide.Red;
        }

        return new ChineseCheckersGameSnapshot(
            board,
            ChineseCheckersSide.Blue,
            ChineseCheckersGameState.Ready,
            0,
            null,
            null,
            null);
    }

    internal static bool TryGetIndex(ChineseCheckersPosition position, out int index) =>
        PositionIndexes.TryGetValue(position, out index);

    internal static ChineseCheckersSide OpponentOf(ChineseCheckersSide side) =>
        side == ChineseCheckersSide.Blue ? ChineseCheckersSide.Red : ChineseCheckersSide.Blue;

    internal static IReadOnlySet<ChineseCheckersPosition> HomeOf(ChineseCheckersSide side) =>
        side == ChineseCheckersSide.Blue ? BlueHome : RedHome;

    internal static IReadOnlySet<ChineseCheckersPosition> GoalOf(ChineseCheckersSide side) =>
        side == ChineseCheckersSide.Blue ? RedHome : BlueHome;

    internal static int CountInHome(ChineseCheckersGameSnapshot snapshot, ChineseCheckersSide side) =>
        HomeOf(side).Count(position => snapshot.GetPiece(position) == side);

    internal static int CountInGoal(ChineseCheckersGameSnapshot snapshot, ChineseCheckersSide side) =>
        GoalOf(side).Count(position => snapshot.GetPiece(position) == side);

    /// <summary>
    /// 返回当前方所有合法完整回合。若对手已经进入当前方起始营，则优先筛选真正减少营内己方棋子数的走法；
    /// 这把“不得故意堵住目标营”的口头约定变成确定、可测试的规则。
    /// </summary>
    internal static IReadOnlyList<ChineseCheckersMove> GetLegalMoves(ChineseCheckersGameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State == ChineseCheckersGameState.Finished)
        {
            return [];
        }

        var raw = GetRawLegalMoves(snapshot, snapshot.CurrentSide);
        if (!HasOpponentEnteredHome(snapshot, snapshot.CurrentSide) ||
            CountInHome(snapshot, snapshot.CurrentSide) == 0)
        {
            return raw;
        }

        var home = HomeOf(snapshot.CurrentSide);
        var evacuation = raw.Where(move => home.Contains(move.From) && !home.Contains(move.To)).ToArray();
        return evacuation.Length > 0 ? evacuation : raw;
    }

    internal static IReadOnlyList<ChineseCheckersMove> GetRawLegalMoves(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var moves = new List<ChineseCheckersMove>(80);
        foreach (var from in Positions)
        {
            if (snapshot.GetPiece(from) != side)
            {
                continue;
            }

            foreach (var direction in Directions)
            {
                var to = from.Add(direction);
                if (TryGetIndex(to, out _) && snapshot.GetPiece(to) is null &&
                    IsCampPathAllowed(side, [from, to]))
                {
                    moves.Add(new ChineseCheckersMove(from, to, ChineseCheckersMoveKind.Step, [from, to]));
                }
            }

            moves.AddRange(GetHopMoves(snapshot, side, from));
        }

        return moves;
    }

    internal static ChineseCheckersMove? FindLegalMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersPosition from,
        ChineseCheckersPosition to) =>
        GetLegalMoves(snapshot).FirstOrDefault(move => move.From == from && move.To == to);

    /// <summary>
    /// 在不可变副本上提交整次移动。普通胜利优先；否则检查下一方是否因对手已入营却无任何撤营着法而被堵塞。
    /// </summary>
    internal static ChineseCheckersMoveResult? TryApplyMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersPosition from,
        ChineseCheckersPosition to)
    {
        var move = FindLegalMove(snapshot, from, to);
        if (move is null)
        {
            return null;
        }

        var mover = snapshot.CurrentSide;
        var board = snapshot.CopyBoard();
        board[PositionIndexes[from]] = null;
        board[PositionIndexes[to]] = mover;
        var next = OpponentOf(mover);
        var provisional = new ChineseCheckersGameSnapshot(
            board,
            next,
            ChineseCheckersGameState.Running,
            snapshot.MoveCount + 1,
            move,
            null,
            null);

        ChineseCheckersSide? winner = null;
        ChineseCheckersTerminationReason? reason = null;
        if (CountInGoal(provisional, mover) == 10)
        {
            winner = mover;
            reason = ChineseCheckersTerminationReason.GoalFilled;
        }
        else if (HasOpponentEnteredHome(provisional, next) && CountInHome(provisional, next) > 0)
        {
            var nextHome = HomeOf(next);
            var canEvacuate = GetRawLegalMoves(provisional, next)
                .Any(candidate => nextHome.Contains(candidate.From) && !nextHome.Contains(candidate.To));
            if (!canEvacuate)
            {
                winner = mover;
                reason = ChineseCheckersTerminationReason.BlockedHome;
            }
        }

        var after = new ChineseCheckersGameSnapshot(
            board,
            next,
            winner is null ? ChineseCheckersGameState.Running : ChineseCheckersGameState.Finished,
            snapshot.MoveCount + 1,
            move,
            winner,
            reason);
        return new ChineseCheckersMoveResult(snapshot, after, move, mover);
    }

    internal static ChineseCheckersGameSnapshot WithCurrentSide(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side) =>
        new(snapshot.CopyBoard(), side, snapshot.State, snapshot.MoveCount, snapshot.LastMove,
            snapshot.Winner, snapshot.TerminationReason);

    internal static string CreateSignature(ChineseCheckersGameSnapshot snapshot)
    {
        var characters = new char[CellCount + 1];
        for (var index = 0; index < CellCount; index++)
        {
            characters[index] = snapshot.CopyBoard()[index] switch
            {
                ChineseCheckersSide.Blue => 'B',
                ChineseCheckersSide.Red => 'R',
                _ => '.',
            };
        }

        characters[^1] = snapshot.CurrentSide == ChineseCheckersSide.Blue ? 'B' : 'R';
        return new string(characters);
    }

    private static IReadOnlyList<ChineseCheckersMove> GetHopMoves(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        ChineseCheckersPosition origin)
    {
        var queue = new Queue<ChineseCheckersPosition>();
        var visited = new HashSet<ChineseCheckersPosition> { origin };
        var paths = new Dictionary<ChineseCheckersPosition, ChineseCheckersPosition[]>();
        queue.Enqueue(origin);
        paths[origin] = [origin];

        while (queue.TryDequeue(out var current))
        {
            foreach (var direction in Directions)
            {
                var middle = current.Add(direction);
                var landing = current.Add(direction, 2);
                if (!TryGetIndex(middle, out _) || !TryGetIndex(landing, out _) || visited.Contains(landing) ||
                    !IsOccupiedDuringHop(snapshot, origin, current, middle) ||
                    IsOccupiedDuringHop(snapshot, origin, current, landing))
                {
                    continue;
                }

                var path = paths[current].Append(landing).ToArray();
                if (!IsCampPathAllowed(side, path))
                {
                    continue;
                }

                // BFS 加固定方向顺序让相同终点始终选到相同最短路径，动画和测试不会受集合枚举顺序影响。
                visited.Add(landing);
                paths[landing] = path;
                queue.Enqueue(landing);
            }
        }

        return paths
            .Where(item => item.Key != origin)
            .Select(item => new ChineseCheckersMove(
                origin,
                item.Key,
                ChineseCheckersMoveKind.Hop,
                item.Value))
            .ToArray();
    }

    private static bool IsOccupiedDuringHop(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersPosition origin,
        ChineseCheckersPosition current,
        ChineseCheckersPosition position)
    {
        if (position == current)
        {
            return true;
        }

        if (position == origin)
        {
            return false;
        }

        return snapshot.GetPiece(position) is not null;
    }

    private static bool IsCampPathAllowed(
        ChineseCheckersSide side,
        IReadOnlyList<ChineseCheckersPosition> path)
    {
        var goal = GoalOf(side);
        var enteredGoal = goal.Contains(path[0]);
        foreach (var position in path.Skip(1))
        {
            if (enteredGoal && !goal.Contains(position))
            {
                return false;
            }

            enteredGoal |= goal.Contains(position);
        }

        return true;
    }

    private static bool HasOpponentEnteredHome(ChineseCheckersGameSnapshot snapshot, ChineseCheckersSide side)
    {
        var opponent = OpponentOf(side);
        return HomeOf(side).Any(position => snapshot.GetPiece(position) == opponent);
    }

    private static (ChineseCheckersPosition[] Positions, HashSet<ChineseCheckersPosition>[] Camps)
        CreatePositionsAndCamps()
    {
        var center = new HashSet<ChineseCheckersPosition>();
        for (var x = -4; x <= 4; x++)
        {
            for (var z = -4; z <= 4; z++)
            {
                var position = new ChineseCheckersPosition(x, z);
                if (Math.Abs(position.Y) <= 4)
                {
                    center.Add(position);
                }
            }
        }

        var baseArm = new HashSet<ChineseCheckersPosition>();
        for (var x = 5; x <= 8; x++)
        {
            for (var y = -4; y <= -(x - 4); y++)
            {
                baseArm.Add(new ChineseCheckersPosition(x, y, -x - y));
            }
        }

        var camps = new HashSet<ChineseCheckersPosition>[6];
        var arm = baseArm;
        for (var index = 0; index < camps.Length; index++)
        {
            camps[index] = arm;
            arm = arm.Select(RotateClockwise).ToHashSet();
        }

        var all = new HashSet<ChineseCheckersPosition>(center);
        foreach (var camp in camps)
        {
            all.UnionWith(camp);
        }

        if (all.Count != CellCount || camps.Any(camp => camp.Count != 10))
        {
            throw new InvalidOperationException("中国跳棋静态拓扑生成失败。" );
        }

        var positions = all.OrderBy(position => position.Z)
            .ThenBy(position => position.X)
            .ThenBy(position => position.Y)
            .ToArray();
        return (positions, camps);
    }

    private static ChineseCheckersPosition RotateClockwise(ChineseCheckersPosition position) =>
        new(-position.Z, -position.X, -position.Y);
}
