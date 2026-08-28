using System.Diagnostics;

namespace ClassicGamePlugin.Features.ChineseCheckers.Domain;

/// <summary>只从不可变快照选择合法完整回合，不提交棋局、不切换回合也不操作 UI。</summary>
internal interface IChineseCheckersMoveStrategy
{
    ChineseCheckersMove? SelectMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        CancellationToken cancellationToken);
}

internal sealed class RandomChineseCheckersMoveStrategy(Random random) : IChineseCheckersMoveStrategy
{
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    public ChineseCheckersMove? SelectMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var view = snapshot.CurrentSide == side ? snapshot : ChineseCheckersRules.WithCurrentSide(snapshot, side);
        var moves = ChineseCheckersRules.GetLegalMoves(view);
        return moves.Count == 0 ? null : moves[_random.Next(moves.Count)];
    }
}

/// <summary>
/// 使用可解释的一层评价选择稳定着法：进入目标营和撤离起始营优先，其次缩短到目标营的六角距离、
/// 增加连跳跨度。固定坐标排序用于提示和困难搜索的兜底。
/// </summary>
internal sealed class StableChineseCheckersMoveStrategy : IChineseCheckersMoveStrategy
{
    public ChineseCheckersMove? SelectMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var view = snapshot.CurrentSide == side ? snapshot : ChineseCheckersRules.WithCurrentSide(snapshot, side);
        return ChineseCheckersRules.GetLegalMoves(view)
            .Select(move =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return (Move: move, Score: ScoreMove(view, move, side));
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Move.From.Z)
            .ThenBy(candidate => candidate.Move.From.X)
            .ThenBy(candidate => candidate.Move.To.Z)
            .ThenBy(candidate => candidate.Move.To.X)
            .Select(candidate => candidate.Move)
            .FirstOrDefault();
    }

    internal static int Evaluate(ChineseCheckersGameSnapshot snapshot, ChineseCheckersSide side)
    {
        var opponent = ChineseCheckersRules.OpponentOf(side);
        return EvaluateSide(snapshot, side) - EvaluateSide(snapshot, opponent);
    }

    internal static int ScoreMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersMove move,
        ChineseCheckersSide side)
    {
        var result = ChineseCheckersRules.TryApplyMove(snapshot, move.From, move.To);
        if (result is null)
        {
            return int.MinValue;
        }

        return Evaluate(result.After, side) + ((move.Path.Count - 2) * 12);
    }

    private static int EvaluateSide(ChineseCheckersGameSnapshot snapshot, ChineseCheckersSide side)
    {
        var goal = ChineseCheckersRules.GoalOf(side);
        var home = ChineseCheckersRules.HomeOf(side);
        var distance = 0;
        foreach (var position in ChineseCheckersRules.AllPositions.Where(position => snapshot.GetPiece(position) == side))
        {
            distance += goal.Min(target => CubeDistance(position, target));
        }

        return (ChineseCheckersRules.CountInGoal(snapshot, side) * 12_000) -
            (ChineseCheckersRules.CountInHome(snapshot, side) * 1_500) -
            (distance * 110) +
            (home.Count(position => snapshot.GetPiece(position) != side) * 40);
    }

    private static int CubeDistance(ChineseCheckersPosition left, ChineseCheckersPosition right) =>
        (Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y) + Math.Abs(left.Z - right.Z)) / 2;
}

/// <summary>
/// 困难电脑使用限时迭代加深 Minimax/Alpha-Beta。根节点保留全部合法着法，深层只展开启发式最优的
/// 十六着；到达五秒、六层或测试节点预算时，返回最后完整深度的结果。
/// </summary>
internal sealed class HardChineseCheckersMoveStrategy : IChineseCheckersMoveStrategy
{
    private readonly TimeSpan _timeLimit;
    private readonly int _maximumDepth;
    private readonly int _nodeLimit;
    private readonly StableChineseCheckersMoveStrategy _fallback = new();
    private long _started;
    private int _visitedNodes;
    private CancellationToken _cancellationToken;
    private Dictionary<(string Signature, int Depth), int> _cache = [];

    internal HardChineseCheckersMoveStrategy()
        : this(TimeSpan.FromSeconds(5), maximumDepth: 6)
    {
    }

    internal HardChineseCheckersMoveStrategy(TimeSpan timeLimit, int maximumDepth, int nodeLimit = int.MaxValue)
    {
        if (timeLimit <= TimeSpan.Zero || maximumDepth <= 0 || nodeLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeLimit), "搜索时间、深度和节点预算都必须为正数。");
        }

        _timeLimit = timeLimit;
        _maximumDepth = maximumDepth;
        _nodeLimit = nodeLimit;
    }

    public ChineseCheckersMove? SelectMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var view = snapshot.CurrentSide == side ? snapshot : ChineseCheckersRules.WithCurrentSide(snapshot, side);
        var fallback = _fallback.SelectMove(view, side, cancellationToken);
        if (fallback is null)
        {
            return null;
        }

        _started = Stopwatch.GetTimestamp();
        _visitedNodes = 0;
        _cancellationToken = cancellationToken;
        _cache = [];
        var bestCompleted = fallback;
        for (var depth = 1; depth <= _maximumDepth; depth++)
        {
            try
            {
                var result = SearchRoot(view, side, depth);
                bestCompleted = result.Move ?? bestCompleted;
                if (Math.Abs(result.Score) >= 900_000)
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
        return bestCompleted;
    }

    private (ChineseCheckersMove? Move, int Score) SearchRoot(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide root,
        int depth)
    {
        ChineseCheckersMove? bestMove = null;
        var bestScore = int.MinValue + 1;
        var alpha = int.MinValue + 1;
        foreach (var move in OrderMoves(snapshot, snapshot.CurrentSide, maximumCount: null))
        {
            CheckBudget();
            var after = ChineseCheckersRules.TryApplyMove(snapshot, move.From, move.To)!.After;
            var score = Search(after, root, depth - 1, alpha, int.MaxValue, new HashSet<string>());
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
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide root,
        int depth,
        int alpha,
        int beta,
        HashSet<string> path)
    {
        CheckBudget();
        if (snapshot.State == ChineseCheckersGameState.Finished)
        {
            return snapshot.Winner == root ? 1_000_000 + depth : -1_000_000 - depth;
        }

        if (depth == 0)
        {
            return StableChineseCheckersMoveStrategy.Evaluate(snapshot, root);
        }

        var signature = ChineseCheckersRules.CreateSignature(snapshot);
        if (!path.Add(signature))
        {
            return 0;
        }

        if (_cache.TryGetValue((signature, depth), out var cached))
        {
            path.Remove(signature);
            return cached;
        }

        var maximizing = snapshot.CurrentSide == root;
        var best = maximizing ? int.MinValue + 1 : int.MaxValue;
        var cutOff = false;
        foreach (var move in OrderMoves(snapshot, snapshot.CurrentSide, maximumCount: 16))
        {
            var after = ChineseCheckersRules.TryApplyMove(snapshot, move.From, move.To)!.After;
            var score = Search(after, root, depth - 1, alpha, beta, path);
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

            if (alpha >= beta)
            {
                cutOff = true;
                break;
            }
        }

        path.Remove(signature);
        if (!cutOff)
        {
            _cache[(signature, depth)] = best;
        }

        return best;
    }

    private static IEnumerable<ChineseCheckersMove> OrderMoves(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        int? maximumCount)
    {
        var ordered = ChineseCheckersRules.GetLegalMoves(snapshot)
            .OrderByDescending(move => StableChineseCheckersMoveStrategy.ScoreMove(snapshot, move, side))
            .ThenBy(move => move.From.Z)
            .ThenBy(move => move.From.X)
            .ThenBy(move => move.To.Z)
            .ThenBy(move => move.To.X);
        return maximumCount is { } count ? ordered.Take(count) : ordered;
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

    private sealed class SearchBudgetExceededException : Exception;
}
