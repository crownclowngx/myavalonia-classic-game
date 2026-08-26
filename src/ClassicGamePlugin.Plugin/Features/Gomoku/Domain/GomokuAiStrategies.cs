using System.Diagnostics;

namespace ClassicGamePlugin.Features.Gomoku.Domain;

/// <summary>从不可变快照选择一个合法交叉点，不负责提交落子、切换回合或操作 UI。</summary>
internal interface IGomokuMoveStrategy
{
    GomokuPosition? SelectMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        CancellationToken cancellationToken);
}

/// <summary>在相邻候选点中随机落子，保留基本棋形感但不进行战术搜索。</summary>
internal sealed class RandomGomokuMoveStrategy(Random random) : IGomokuMoveStrategy
{
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    public GomokuPosition? SelectMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var moves = GomokuRules.GetCandidateMoves(snapshot, player);
        return moves.Count == 0 ? null : moves[_random.Next(moves.Count)];
    }
}

/// <summary>
/// 使用稳定、可解释的攻防棋形评分。立即取胜和阻挡对手取胜拥有最高优先级，其次比较活四、
/// 冲四、活三与中心距离；它既作为中等电脑，也为提示提供不随机且响应迅速的结果。
/// </summary>
internal sealed class StableGomokuMoveStrategy : IGomokuMoveStrategy
{
    public GomokuPosition? SelectMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var candidates = GomokuRules.GetCandidateMoves(snapshot, player);
        var opponent = GomokuRules.OpponentOf(player);
        var opponentView = snapshot.WithCurrentPlayer(opponent);
        return candidates
            .Select(position =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attack = GomokuRules.GetPlacementPatternScore(snapshot, position, player);
                var defense = GomokuRules.ValidateMove(opponentView, opponent, position).IsLegal
                    ? GomokuRules.GetPlacementPatternScore(snapshot, position, opponent)
                    : 0;
                var score = attack >= 1_000_000
                    ? 10_000_000 + attack
                    : defense >= 1_000_000
                        ? 9_000_000 + defense
                        : attack + (int)(defense * 0.9);
                return (Position: position, Score: score);
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Position.Row)
            .ThenBy(candidate => candidate.Position.Column)
            .Select(candidate => (GomokuPosition?)candidate.Position)
            .FirstOrDefault();
    }
}

/// <summary>
/// 困难策略使用迭代加深 Negamax 与 alpha-beta 剪枝。候选排序和单次搜索缓存是控制 15×15 分支数的
/// 必要实现细节，而不是可扩展框架；到达时间或节点预算时返回最后一个完整深度的结果。
/// </summary>
internal sealed class HardGomokuMoveStrategy : IGomokuMoveStrategy
{
    private readonly TimeSpan _timeLimit;
    private readonly int _maximumDepth;
    private readonly int _nodeLimit;
    private readonly StableGomokuMoveStrategy _fallback = new();
    private long _started;
    private int _visitedNodes;
    private CancellationToken _cancellationToken;
    private Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    internal HardGomokuMoveStrategy()
        : this(TimeSpan.FromSeconds(2), maximumDepth: 6, nodeLimit: int.MaxValue)
    {
    }

    /// <summary>测试可使用确定的深度或节点上限，不需要依赖真实两秒墙钟。</summary>
    internal HardGomokuMoveStrategy(TimeSpan timeLimit, int maximumDepth, int nodeLimit = int.MaxValue)
    {
        if (timeLimit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeLimit));
        }

        if (maximumDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        }

        if (nodeLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeLimit));
        }

        _timeLimit = timeLimit;
        _maximumDepth = maximumDepth;
        _nodeLimit = nodeLimit;
    }

    public GomokuPosition? SelectMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var fallback = _fallback.SelectMove(snapshot, player, cancellationToken);
        if (fallback is null)
        {
            return null;
        }

        _started = Stopwatch.GetTimestamp();
        _visitedNodes = 0;
        _cancellationToken = cancellationToken;
        _cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        var bestCompletedMove = fallback;
        for (var depth = 1; depth <= _maximumDepth; depth++)
        {
            try
            {
                var result = SearchRoot(snapshot, player, depth);
                if (result.Move is { } move)
                {
                    bestCompletedMove = move;
                }

                if (Math.Abs(result.Score) >= 90_000_000)
                {
                    break;
                }
            }
            catch (SearchBudgetExceededException)
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return bestCompletedMove;
    }

    private (GomokuPosition? Move, int Score) SearchRoot(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        int depth)
    {
        GomokuPosition? bestMove = null;
        var bestScore = int.MinValue + 1;
        var alpha = int.MinValue + 1;
        foreach (var move in OrderMoves(snapshot, player, maximumCount: 20))
        {
            CheckBudget();
            var result = GomokuRules.TryApplyMove(snapshot, player, move);
            if (result is null)
            {
                continue;
            }

            var score = -Search(result.After, depth - 1, -100_000_000, -alpha);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            alpha = Math.Max(alpha, bestScore);
        }

        return (bestMove, bestScore);
    }

    private int Search(
        GomokuGameSnapshot snapshot,
        int depth,
        int alpha,
        int beta)
    {
        CheckBudget();
        if (snapshot.State == GomokuGameState.Finished)
        {
            if (snapshot.Winner is null)
            {
                return 0;
            }

            return snapshot.Winner == snapshot.CurrentPlayer
                ? 100_000_000 + depth
                : -100_000_000 - depth;
        }

        if (depth == 0)
        {
            return Evaluate(snapshot, snapshot.CurrentPlayer);
        }

        var cacheKey = CreateCacheKey(snapshot, depth);
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.Depth >= depth)
        {
            return cached.Score;
        }

        var best = int.MinValue + 1;
        var cutOff = false;
        foreach (var move in OrderMoves(snapshot, snapshot.CurrentPlayer, maximumCount: 12))
        {
            var result = GomokuRules.TryApplyMove(snapshot, snapshot.CurrentPlayer, move);
            if (result is null)
            {
                continue;
            }

            var score = -Search(result.After, depth - 1, -beta, -alpha);
            best = Math.Max(best, score);
            alpha = Math.Max(alpha, best);
            if (alpha >= beta)
            {
                cutOff = true;
                break;
            }
        }

        if (best == int.MinValue + 1)
        {
            best = Evaluate(snapshot, snapshot.CurrentPlayer);
        }

        // 发生剪枝时 best 只是边界值而非精确值；只缓存完整搜索结果，避免后续分支误用界限。
        if (!cutOff)
        {
            _cache[cacheKey] = new CacheEntry(depth, best);
        }
        return best;
    }

    private static int Evaluate(GomokuGameSnapshot snapshot, GomokuStone rootPlayer)
    {
        var opponent = GomokuRules.OpponentOf(rootPlayer);
        var rootView = snapshot.WithCurrentPlayer(rootPlayer);
        var opponentView = snapshot.WithCurrentPlayer(opponent);
        var rootBest = GomokuRules.GetCandidateMoves(rootView, rootPlayer)
            .Select(move => GomokuRules.GetPlacementPatternScore(snapshot, move, rootPlayer))
            .DefaultIfEmpty(0)
            .Max();
        var opponentBest = GomokuRules.GetCandidateMoves(opponentView, opponent)
            .Select(move => GomokuRules.GetPlacementPatternScore(snapshot, move, opponent))
            .DefaultIfEmpty(0)
            .Max();
        return rootBest - opponentBest;
    }

    private static IReadOnlyList<GomokuPosition> OrderMoves(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        int maximumCount)
    {
        var opponent = GomokuRules.OpponentOf(player);
        return GomokuRules.GetCandidateMoves(snapshot, player)
            .Select(move => new
            {
                Move = move,
                Score = GomokuRules.GetPlacementPatternScore(snapshot, move, player) +
                    GomokuRules.GetPlacementPatternScore(snapshot, move, opponent),
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Move.Row)
            .ThenBy(candidate => candidate.Move.Column)
            .Take(maximumCount)
            .Select(candidate => candidate.Move)
            .ToArray();
    }

    private void CheckBudget()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _visitedNodes++;
        if (_visitedNodes > _nodeLimit || Stopwatch.GetElapsedTime(_started) >= _timeLimit)
        {
            throw new SearchBudgetExceededException();
        }
    }

    private static string CreateCacheKey(GomokuGameSnapshot snapshot, int depth)
    {
        var board = snapshot.CopyBoard();
        var characters = new char[board.Length + 2];
        for (var index = 0; index < board.Length; index++)
        {
            characters[index] = board[index] switch
            {
                GomokuStone.Black => 'B',
                GomokuStone.White => 'W',
                _ => '.',
            };
        }

        characters[^2] = snapshot.CurrentPlayer == GomokuStone.Black ? 'B' : 'W';
        characters[^1] = (char)depth;
        return new string(characters);
    }

    private sealed record CacheEntry(int Depth, int Score);
    private sealed class SearchBudgetExceededException : Exception;
}
