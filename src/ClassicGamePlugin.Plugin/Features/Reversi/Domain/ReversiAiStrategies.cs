namespace ClassicGamePlugin.Features.Reversi.Domain;

/// <summary>为一份不可变棋盘快照选择一个合法位置，不负责提交落子或修改真实棋局。</summary>
internal interface IReversiMoveStrategy
{
    ReversiPosition? SelectMove(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        CancellationToken cancellationToken);
}

/// <summary>从全部合法位置中随机选择一步，作为入门难度电脑。</summary>
internal sealed class RandomReversiMoveStrategy(Random random) : IReversiMoveStrategy
{
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    public ReversiPosition? SelectMove(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var moves = ReversiRules.GetLegalMoves(snapshot, player);
        return moves.Count == 0 ? null : moves[_random.Next(moves.Count)];
    }
}

/// <summary>
/// 使用稳定、可解释的局部启发式选择一步。它既是中等电脑，也为玩家提示服务，
/// 不进行深层搜索，避免提示按钮因当前电脑难度不同而产生随机或长时间等待。
/// </summary>
internal sealed class StableReversiMoveStrategy : IReversiMoveStrategy
{
    private static readonly ReversiPosition[] Corners =
    [
        new(0, 0), new(0, 7), new(7, 0), new(7, 7),
    ];

    public ReversiPosition? SelectMove(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReversiRules.GetLegalMoves(snapshot, player)
            .Select(position => new
            {
                Position = position,
                Category = GetCategory(snapshot, position),
                FlipCount = ReversiRules.GetFlippedPositions(snapshot, player, position).Count,
            })
            .OrderBy(candidate => candidate.Category)
            .ThenByDescending(candidate => candidate.FlipCount)
            .ThenBy(candidate => candidate.Position.Row)
            .ThenBy(candidate => candidate.Position.Column)
            .Select(candidate => (ReversiPosition?)candidate.Position)
            .FirstOrDefault();
    }

    private static int GetCategory(ReversiGameSnapshot snapshot, ReversiPosition position)
    {
        if (Corners.Contains(position))
        {
            return 0;
        }

        if (IsAdjacentToEmptyCorner(snapshot, position))
        {
            return 3;
        }

        return position.Row is 0 or 7 || position.Column is 0 or 7 ? 1 : 2;
    }

    private static bool IsAdjacentToEmptyCorner(
        ReversiGameSnapshot snapshot,
        ReversiPosition position)
    {
        foreach (var corner in Corners)
        {
            if (snapshot.GetDisc(corner) is null &&
                Math.Abs(position.Row - corner.Row) <= 1 &&
                Math.Abs(position.Column - corner.Column) <= 1)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 使用固定五层 alpha-beta 搜索选择一步。搜索只操作不可变快照，终局胜负权重远高于
/// 位置、行动力和临时棋子数，防止电脑为了眼前多翻棋而放弃可见的胜局。
/// </summary>
internal sealed class HardReversiMoveStrategy : IReversiMoveStrategy
{
    private const int SearchDepth = 5;
    private static readonly int[] PositionWeights =
    [
        120, -20, 20, 5, 5, 20, -20, 120,
        -20, -40, -5, -5, -5, -5, -40, -20,
        20, -5, 15, 3, 3, 15, -5, 20,
        5, -5, 3, 3, 3, 3, -5, 5,
        5, -5, 3, 3, 3, 3, -5, 5,
        20, -5, 15, 3, 3, 15, -5, 20,
        -20, -40, -5, -5, -5, -5, -40, -20,
        120, -20, 20, 5, 5, 20, -20, 120,
    ];

    public ReversiPosition? SelectMove(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var moves = ReversiRules.GetLegalMoves(snapshot, player);
        ReversiPosition? bestMove = null;
        var bestScore = int.MinValue;
        foreach (var move in moves)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = ReversiRules.TryApplyMove(snapshot, player, move);
            if (result is null)
            {
                continue;
            }

            var score = Search(
                result.After,
                SearchDepth - 1,
                int.MinValue + 1,
                int.MaxValue,
                player,
                cancellationToken);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        return bestMove;
    }

    private static int Search(
        ReversiGameSnapshot snapshot,
        int depth,
        int alpha,
        int beta,
        ReversiDiscColor maximizingPlayer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (depth == 0 || snapshot.State == ReversiGameState.Finished)
        {
            return Evaluate(snapshot, maximizingPlayer);
        }

        var moves = ReversiRules.GetLegalMoves(snapshot, snapshot.CurrentPlayer);
        if (moves.Count == 0)
        {
            var opponent = ReversiRules.OpponentOf(snapshot.CurrentPlayer);
            if (ReversiRules.GetLegalMoves(snapshot, opponent).Count == 0)
            {
                return Evaluate(snapshot, maximizingPlayer);
            }

            return Search(
                snapshot.WithCurrentPlayer(opponent),
                depth - 1,
                alpha,
                beta,
                maximizingPlayer,
                cancellationToken);
        }

        var maximizing = snapshot.CurrentPlayer == maximizingPlayer;
        var best = maximizing ? int.MinValue : int.MaxValue;
        foreach (var move in moves)
        {
            var result = ReversiRules.TryApplyMove(snapshot, snapshot.CurrentPlayer, move)!;
            var score = Search(
                result.After,
                depth - 1,
                alpha,
                beta,
                maximizingPlayer,
                cancellationToken);
            if (maximizing)
            {
                best = Math.Max(best, score);
                alpha = Math.Max(alpha, best);
            }
            else
            {
                best = Math.Min(best, score);
                beta = Math.Min(beta, best);
            }

            if (beta <= alpha)
            {
                break;
            }
        }

        return best;
    }

    private static int Evaluate(ReversiGameSnapshot snapshot, ReversiDiscColor player)
    {
        var opponent = ReversiRules.OpponentOf(player);
        var discDifference = Count(snapshot, player) - Count(snapshot, opponent);
        if (snapshot.State == ReversiGameState.Finished || snapshot.EmptyCount == 0)
        {
            return discDifference switch
            {
                > 0 => 1_000_000 + discDifference,
                < 0 => -1_000_000 + discDifference,
                _ => 0,
            };
        }

        var positionalScore = 0;
        for (var row = 0; row < ReversiRules.BoardSize; row++)
        {
            for (var column = 0; column < ReversiRules.BoardSize; column++)
            {
                var position = new ReversiPosition(row, column);
                var color = snapshot.GetDisc(position);
                var weight = PositionWeights[(row * ReversiRules.BoardSize) + column];
                positionalScore += color == player ? weight : color == opponent ? -weight : 0;
            }
        }

        var mobility = ReversiRules.GetLegalMoves(snapshot, player).Count -
            ReversiRules.GetLegalMoves(snapshot, opponent).Count;
        return (positionalScore * 10) + (mobility * 20) + (discDifference * 2);
    }

    private static int Count(ReversiGameSnapshot snapshot, ReversiDiscColor color) =>
        color == ReversiDiscColor.Black ? snapshot.BlackCount : snapshot.WhiteCount;
}
