namespace ClassicGamePlugin.Features.Reversi.Domain;

/// <summary>表示黑白棋棋盘上的一方棋子。</summary>
internal enum ReversiDiscColor
{
    Black,
    White,
}

/// <summary>表示一局黑白棋所处的生命周期阶段。</summary>
internal enum ReversiGameState
{
    Ready,
    Running,
    Finished,
}

/// <summary>表示人机对战采用的电脑难度。</summary>
internal enum ReversiAiDifficulty
{
    Easy,
    Medium,
    Hard,
}

/// <summary>使用从零开始的行列保存棋盘位置，并提供面向玩家的 A1-H8 坐标。</summary>
internal readonly record struct ReversiPosition(int Row, int Column)
{
    internal string DisplayName => $"{(char)('A' + Column)}{Row + 1}";
}

/// <summary>
/// 保存一次成功落子的完整领域结果。跳过方和终局信息属于该落子事务的一部分，
/// 调用方不需要在领域外重新推导回合规则。
/// </summary>
internal sealed record ReversiMoveResult(
    ReversiDiscColor Player,
    ReversiPosition Position,
    IReadOnlyList<ReversiPosition> FlippedPositions,
    ReversiDiscColor? SkippedPlayer,
    ReversiGameSnapshot Before,
    ReversiGameSnapshot After);

/// <summary>
/// 黑白棋不可变快照。构造和复制时都会复制 64 个格子的数组，避免撤销历史、
/// 后台 AI 搜索和真实棋局之间通过数组引用发生隐式共享。
/// </summary>
internal sealed class ReversiGameSnapshot
{
    private readonly ReversiDiscColor?[] _board;

    internal ReversiGameSnapshot(
        IEnumerable<ReversiDiscColor?> board,
        ReversiDiscColor currentPlayer,
        ReversiGameState state,
        int moveCount,
        ReversiPosition? lastMove)
    {
        ArgumentNullException.ThrowIfNull(board);
        _board = board.ToArray();
        if (_board.Length != ReversiRules.CellCount)
        {
            throw new ArgumentException("黑白棋快照必须恰好包含 64 个格子。", nameof(board));
        }

        CurrentPlayer = currentPlayer;
        State = state;
        MoveCount = moveCount;
        LastMove = lastMove;
    }

    internal ReversiDiscColor CurrentPlayer { get; }
    internal ReversiGameState State { get; }
    internal int MoveCount { get; }
    internal ReversiPosition? LastMove { get; }
    internal int BlackCount => _board.Count(color => color == ReversiDiscColor.Black);
    internal int WhiteCount => _board.Count(color => color == ReversiDiscColor.White);
    internal int EmptyCount => _board.Count(color => color is null);

    internal ReversiDiscColor? GetDisc(ReversiPosition position)
    {
        ReversiRules.ValidatePosition(position);
        return _board[(position.Row * ReversiRules.BoardSize) + position.Column];
    }

    internal ReversiDiscColor?[] CopyBoard() => (ReversiDiscColor?[])_board.Clone();

    internal ReversiGameSnapshot Clone() =>
        new(_board, CurrentPlayer, State, MoveCount, LastMove);

    internal ReversiGameSnapshot WithCurrentPlayer(ReversiDiscColor player) =>
        new(_board, player, State, MoveCount, LastMove);
}

/// <summary>集中提供不带状态副作用的落子计算，供真实棋局与 AI 模拟共同使用。</summary>
internal static class ReversiRules
{
    internal const int BoardSize = 8;
    internal const int CellCount = BoardSize * BoardSize;

    private static readonly (int Row, int Column)[] Directions =
    [
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1),           (0, 1),
        (1, -1),  (1, 0),  (1, 1),
    ];

    internal static ReversiGameSnapshot CreateInitialSnapshot()
    {
        var board = new ReversiDiscColor?[CellCount];
        board[IndexOf(new ReversiPosition(3, 3))] = ReversiDiscColor.White;
        board[IndexOf(new ReversiPosition(3, 4))] = ReversiDiscColor.Black;
        board[IndexOf(new ReversiPosition(4, 3))] = ReversiDiscColor.Black;
        board[IndexOf(new ReversiPosition(4, 4))] = ReversiDiscColor.White;
        return new ReversiGameSnapshot(
            board,
            ReversiDiscColor.Black,
            ReversiGameState.Ready,
            moveCount: 0,
            lastMove: null);
    }

    /// <summary>按行优先顺序枚举合法位置，使提示、AI 同分选择和测试结果保持稳定。</summary>
    internal static IReadOnlyList<ReversiPosition> GetLegalMoves(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var moves = new List<ReversiPosition>();
        for (var row = 0; row < BoardSize; row++)
        {
            for (var column = 0; column < BoardSize; column++)
            {
                var position = new ReversiPosition(row, column);
                if (snapshot.GetDisc(position) is null && GetFlippedPositions(snapshot, player, position).Count > 0)
                {
                    moves.Add(position);
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// 尝试在快照上模拟一步。只有某个方向满足“连续对方棋子后由己方棋子封口”时才翻转；
    /// 越过边界、遇到空格或没有中间对方棋子都不会形成夹取。
    /// </summary>
    internal static ReversiMoveResult? TryApplyMove(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        ReversiPosition position)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidatePosition(position);
        if (snapshot.State == ReversiGameState.Finished ||
            snapshot.CurrentPlayer != player ||
            snapshot.GetDisc(position) is not null)
        {
            return null;
        }

        var flipped = GetFlippedPositions(snapshot, player, position);
        if (flipped.Count == 0)
        {
            return null;
        }

        var board = snapshot.CopyBoard();
        board[IndexOf(position)] = player;
        foreach (var captured in flipped)
        {
            board[IndexOf(captured)] = player;
        }

        var opponent = OpponentOf(player);
        var provisional = new ReversiGameSnapshot(
            board,
            opponent,
            ReversiGameState.Running,
            snapshot.MoveCount + 1,
            position);
        var opponentMoves = GetLegalMoves(provisional, opponent);
        var playerMoves = GetLegalMoves(provisional.WithCurrentPlayer(player), player);

        ReversiDiscColor? skippedPlayer = null;
        ReversiGameSnapshot after;
        if (provisional.EmptyCount == 0 || (opponentMoves.Count == 0 && playerMoves.Count == 0))
        {
            after = new ReversiGameSnapshot(
                board,
                opponent,
                ReversiGameState.Finished,
                provisional.MoveCount,
                position);
        }
        else if (opponentMoves.Count == 0)
        {
            // 跳过不是玩家可以选择的动作，而是一次落子完成后的强制回合结算。
            skippedPlayer = opponent;
            after = new ReversiGameSnapshot(
                board,
                player,
                ReversiGameState.Running,
                provisional.MoveCount,
                position);
        }
        else
        {
            after = provisional;
        }

        return new ReversiMoveResult(player, position, flipped, skippedPlayer, snapshot.Clone(), after);
    }

    internal static IReadOnlyList<ReversiPosition> GetFlippedPositions(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        ReversiPosition position)
    {
        if (!IsInside(position.Row, position.Column) || snapshot.GetDisc(position) is not null)
        {
            return [];
        }

        var opponent = OpponentOf(player);
        var allFlipped = new List<ReversiPosition>();
        foreach (var direction in Directions)
        {
            var inDirection = new List<ReversiPosition>();
            var row = position.Row + direction.Row;
            var column = position.Column + direction.Column;
            while (IsInside(row, column))
            {
                var candidate = new ReversiPosition(row, column);
                var color = snapshot.GetDisc(candidate);
                if (color == opponent)
                {
                    inDirection.Add(candidate);
                    row += direction.Row;
                    column += direction.Column;
                    continue;
                }

                if (color == player && inDirection.Count > 0)
                {
                    allFlipped.AddRange(inDirection);
                }

                break;
            }
        }

        return allFlipped;
    }

    internal static ReversiDiscColor OpponentOf(ReversiDiscColor color) =>
        color == ReversiDiscColor.Black ? ReversiDiscColor.White : ReversiDiscColor.Black;

    internal static void ValidatePosition(ReversiPosition position)
    {
        if (!IsInside(position.Row, position.Column))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "黑白棋坐标必须位于 8×8 棋盘内。");
        }
    }

    internal static bool IsInside(int row, int column) =>
        row >= 0 && row < BoardSize && column >= 0 && column < BoardSize;

    private static int IndexOf(ReversiPosition position) =>
        (position.Row * BoardSize) + position.Column;
}
