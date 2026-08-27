using System.Diagnostics;

namespace ClassicGamePlugin.Features.Xiangqi.Domain;

/// <summary>只从只读棋局快照中选择一着合法棋，不负责提交走法或修改真实棋局。</summary>
internal interface IXiangqiMoveStrategy
{
    XiangqiMove? SelectMove(
        XiangqiGameSnapshot snapshot,
        XiangqiSide side,
        CancellationToken cancellationToken);
}

/// <summary>
/// 入门电脑先保证立即将死和基本防杀，再从静态评价较好的少量候选中带权随机。它刻意保留非最优选择，
/// 但不会像完全随机那样频繁白送强子；注入固定随机源后可重复测试。
/// </summary>
internal sealed class EasyXiangqiMoveStrategy(Random random) : IXiangqiMoveStrategy
{
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    public XiangqiMove? SelectMove(
        XiangqiGameSnapshot snapshot,
        XiangqiSide side,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var moves = XiangqiRules.GetLegalMoves(snapshot, side);
        if (moves.Count == 0)
        {
            return null;
        }

        var evaluated = moves
            .Select(move =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = XiangqiRules.TryApplyMove(snapshot, move)!;
                var isImmediateWin = result.After.Winner == side;
                var allowsImmediateLoss = !isImmediateWin && AllowsImmediateMate(result.After, side, cancellationToken);
                return new Candidate(
                    move,
                    isImmediateWin,
                    allowsImmediateLoss,
                    XiangqiPositionEvaluator.Evaluate(result.After, side));
            })
            .ToArray();
        var immediate = evaluated.Where(candidate => candidate.IsImmediateWin).ToArray();
        if (immediate.Length > 0)
        {
            return immediate.OrderBy(candidate => candidate.Move.From.Row)
                .ThenBy(candidate => candidate.Move.From.Column)
                .ThenBy(candidate => candidate.Move.To.Row)
                .ThenBy(candidate => candidate.Move.To.Column)
                .First().Move;
        }

        var safe = evaluated.Where(candidate => !candidate.AllowsImmediateLoss).ToArray();
        var pool = (safe.Length > 0 ? safe : evaluated)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Move.From.Row)
            .ThenBy(candidate => candidate.Move.From.Column)
            .ThenBy(candidate => candidate.Move.To.Row)
            .ThenBy(candidate => candidate.Move.To.Column)
            .Take(5)
            .ToArray();
        var totalWeight = pool.Length * (pool.Length + 1) / 2;
        var selectedWeight = _random.Next(totalWeight);
        for (var index = 0; index < pool.Length; index++)
        {
            selectedWeight -= pool.Length - index;
            if (selectedWeight < 0)
            {
                return pool[index].Move;
            }
        }

        return pool[0].Move;
    }

    private static bool AllowsImmediateMate(
        XiangqiGameSnapshot snapshot,
        XiangqiSide player,
        CancellationToken cancellationToken)
    {
        foreach (var reply in XiangqiRules.GetLegalMoves(snapshot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (XiangqiRules.TryApplyMove(snapshot, reply)?.After.Winner == XiangqiRules.OpponentOf(player))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record Candidate(
        XiangqiMove Move,
        bool IsImmediateWin,
        bool AllowsImmediateLoss,
        int Score);
}

/// <summary>
/// 中等和困难电脑共用的迭代加深搜索核心。难度只通过明确配置改变时间、深度和排序能力，避免复制两套
/// 容易产生规则差异的搜索代码。每次调用创建独立缓存，不会在多个 Document 之间共享可变状态。
/// </summary>
internal sealed class SearchXiangqiMoveStrategy : IXiangqiMoveStrategy
{
    private const int MateScore = 100_000_000;
    private readonly TimeSpan _timeLimit;
    private readonly int _maximumDepth;
    private readonly int _nodeLimit;
    private readonly int _quiescenceDepth;
    private readonly bool _useAdvancedOrdering;
    private long _started;
    private int _visitedNodes;
    private CancellationToken _cancellationToken;
    private Dictionary<ulong, CacheEntry> _cache = [];
    private Dictionary<XiangqiMove, int> _historyScores = [];
    private XiangqiMove?[,] _killerMoves = new XiangqiMove?[16, 2];

    internal static SearchXiangqiMoveStrategy CreateMedium() =>
        new(TimeSpan.FromMilliseconds(600), maximumDepth: 5, nodeLimit: int.MaxValue,
            quiescenceDepth: 2, useAdvancedOrdering: false);

    internal static SearchXiangqiMoveStrategy CreateHard() =>
        new(TimeSpan.FromSeconds(2), maximumDepth: 9, nodeLimit: int.MaxValue,
            quiescenceDepth: 6, useAdvancedOrdering: true);

    /// <summary>测试可固定深度和节点数，不需要等待真实的 600 毫秒或 2 秒。</summary>
    internal SearchXiangqiMoveStrategy(
        TimeSpan timeLimit,
        int maximumDepth,
        int nodeLimit,
        int quiescenceDepth = 2,
        bool useAdvancedOrdering = true)
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

        if (quiescenceDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quiescenceDepth));
        }

        _timeLimit = timeLimit;
        _maximumDepth = maximumDepth;
        _nodeLimit = nodeLimit;
        _quiescenceDepth = quiescenceDepth;
        _useAdvancedOrdering = useAdvancedOrdering;
    }

    public XiangqiMove? SelectMove(
        XiangqiGameSnapshot snapshot,
        XiangqiSide side,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.CurrentSide != side)
        {
            throw new ArgumentException("电脑只能为快照中的当前行棋方选择走法。", nameof(side));
        }

        var legalMoves = XiangqiRules.GetLegalMoves(snapshot);
        if (legalMoves.Count == 0)
        {
            return null;
        }

        _started = Stopwatch.GetTimestamp();
        _visitedNodes = 0;
        _cancellationToken = cancellationToken;
        _cache = [];
        _historyScores = [];
        _killerMoves = new XiangqiMove?[Math.Max(16, _maximumDepth + _quiescenceDepth + 4), 2];
        var bestCompletedMove = OrderMoves(snapshot, legalMoves, depth: 0).First();
        for (var depth = 1; depth <= _maximumDepth; depth++)
        {
            try
            {
                var completed = SearchRoot(snapshot, side, depth);
                if (completed.Move is { } move)
                {
                    bestCompletedMove = move;
                }

                if (Math.Abs(completed.Score) >= MateScore - 1000)
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

    private (XiangqiMove? Move, int Score) SearchRoot(
        XiangqiGameSnapshot snapshot,
        XiangqiSide perspective,
        int depth)
    {
        XiangqiMove? bestMove = null;
        var bestScore = int.MinValue + 1;
        var alpha = int.MinValue + 1;
        foreach (var move in OrderMoves(snapshot, XiangqiRules.GetLegalMoves(snapshot), depth))
        {
            CheckBudget();
            var result = XiangqiRules.TryApplyMove(snapshot, move);
            if (result is null)
            {
                continue;
            }

            var score = Search(result.After, depth - 1, alpha, int.MaxValue, perspective, depth: 1);
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
        XiangqiGameSnapshot snapshot,
        int remainingDepth,
        int alpha,
        int beta,
        XiangqiSide perspective,
        int depth)
    {
        CheckBudget();
        if (snapshot.State == XiangqiGameState.Finished)
        {
            return EvaluateTerminal(snapshot, perspective, depth);
        }

        if (remainingDepth <= 0)
        {
            return Quiescence(snapshot, alpha, beta, perspective, depth, _quiescenceDepth);
        }

        var canCache = !HasRepeatedPathPosition(snapshot);
        var cacheKey = CreateCacheKey(snapshot);
        if (canCache && _cache.TryGetValue(cacheKey, out var cached) && cached.Depth >= remainingDepth)
        {
            return cached.Score;
        }

        var maximizing = snapshot.CurrentSide == perspective;
        var best = maximizing ? int.MinValue + 1 : int.MaxValue;
        var cutOff = false;
        var moves = OrderMoves(snapshot, XiangqiRules.GetLegalMoves(snapshot), depth);
        foreach (var move in moves)
        {
            var result = XiangqiRules.TryApplyMove(snapshot, move);
            if (result is null)
            {
                continue;
            }

            var extension = result.GaveCheck && _useAdvancedOrdering && depth < 4 ? 1 : 0;
            var score = Search(
                result.After,
                remainingDepth - 1 + extension,
                alpha,
                beta,
                perspective,
                depth + 1);
            if (maximizing)
            {
                if (score > best)
                {
                    best = score;
                }

                alpha = Math.Max(alpha, best);
            }
            else
            {
                if (score < best)
                {
                    best = score;
                }

                beta = Math.Min(beta, best);
            }

            if (beta <= alpha)
            {
                cutOff = true;
                RecordCutoff(move, depth, remainingDepth, result.CapturedPiece is null);
                break;
            }
        }

        if (best is int.MinValue + 1 or int.MaxValue)
        {
            best = XiangqiPositionEvaluator.Evaluate(snapshot, perspective);
        }

        if (canCache && !cutOff)
        {
            _cache[cacheKey] = new CacheEntry(remainingDepth, best);
        }

        return best;
    }

    /// <summary>
    /// 静态搜索只延伸吃子、将军和被将后的全部应将，减少在搜索边界刚好停在吃子前后的水平线效应。
    /// 深度被显式限制；外部取消和总节点/时间预算仍在每个节点检查。
    /// </summary>
    private int Quiescence(
        XiangqiGameSnapshot snapshot,
        int alpha,
        int beta,
        XiangqiSide perspective,
        int depth,
        int remaining)
    {
        CheckBudget();
        if (snapshot.State == XiangqiGameState.Finished)
        {
            return EvaluateTerminal(snapshot, perspective, depth);
        }

        var inCheck = XiangqiRules.IsInCheck(snapshot, snapshot.CurrentSide);
        var standPat = XiangqiPositionEvaluator.Evaluate(snapshot, perspective);
        if (remaining == 0)
        {
            return standPat;
        }

        var maximizing = snapshot.CurrentSide == perspective;
        if (!inCheck && maximizing)
        {
            if (standPat >= beta)
            {
                return standPat;
            }

            alpha = Math.Max(alpha, standPat);
        }
        else if (!inCheck)
        {
            if (standPat <= alpha)
            {
                return standPat;
            }

            beta = Math.Min(beta, standPat);
        }

        var tactical = OrderMoves(snapshot, XiangqiRules.GetLegalMoves(snapshot), depth)
            .Select(move => (Move: move, Result: XiangqiRules.TryApplyMove(snapshot, move)!))
            .Where(candidate => inCheck || candidate.Result.CapturedPiece is not null || candidate.Result.GaveCheck)
            .ToArray();
        var best = inCheck
            ? maximizing ? int.MinValue + 1 : int.MaxValue
            : standPat;
        foreach (var candidate in tactical)
        {
            var score = Quiescence(
                candidate.Result.After,
                alpha,
                beta,
                perspective,
                depth + 1,
                remaining - 1);
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

    private IReadOnlyList<XiangqiMove> OrderMoves(
        XiangqiGameSnapshot snapshot,
        IReadOnlyList<XiangqiMove> moves,
        int depth) =>
        moves.Select(move =>
            {
                var moving = snapshot.GetPiece(move.From)!.Value;
                var captured = snapshot.GetPiece(move.To);
                var captureScore = captured is { } victim
                    ? 100_000 + (XiangqiPositionEvaluator.PieceValue(victim.Type) * 10) -
                        XiangqiPositionEvaluator.PieceValue(moving.Type)
                    : 0;
                var checkScore = XiangqiRules.WouldGiveCheck(snapshot, move) ? 60_000 : 0;
                var killerScore = _useAdvancedOrdering && depth < _killerMoves.GetLength(0)
                    ? _killerMoves[depth, 0] == move ? 50_000 : _killerMoves[depth, 1] == move ? 40_000 : 0
                    : 0;
                var history = _useAdvancedOrdering && _historyScores.TryGetValue(move, out var value) ? value : 0;
                return (Move: move, Score: captureScore + checkScore + killerScore + history);
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Move.From.Row)
            .ThenBy(candidate => candidate.Move.From.Column)
            .ThenBy(candidate => candidate.Move.To.Row)
            .ThenBy(candidate => candidate.Move.To.Column)
            .Select(candidate => candidate.Move)
            .ToArray();

    private void RecordCutoff(XiangqiMove move, int depth, int remainingDepth, bool quiet)
    {
        if (!_useAdvancedOrdering || !quiet)
        {
            return;
        }

        _historyScores[move] = _historyScores.GetValueOrDefault(move) + (remainingDepth * remainingDepth);
        if (depth >= _killerMoves.GetLength(0) || _killerMoves[depth, 0] == move)
        {
            return;
        }

        _killerMoves[depth, 1] = _killerMoves[depth, 0];
        _killerMoves[depth, 0] = move;
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

    private static int EvaluateTerminal(
        XiangqiGameSnapshot snapshot,
        XiangqiSide perspective,
        int depth) =>
        snapshot.Winner switch
        {
            null => 0,
            var winner when winner == perspective => MateScore - depth,
            _ => -MateScore + depth,
        };

    private static ulong CreateCacheKey(XiangqiGameSnapshot snapshot)
    {
        var position = snapshot.PositionHistory[^1].Key;
        return position ^ ((ulong)snapshot.NoCapturePlyCount * 0x9E3779B97F4A7C15UL);
    }

    private static bool HasRepeatedPathPosition(XiangqiGameSnapshot snapshot) =>
        snapshot.PositionHistory
            .GroupBy(record => (record.Key, record.Signature))
            .Any(group => group.Count() > 1);

    private sealed record CacheEntry(int Depth, int Score);
    private sealed class SearchBudgetExceededException : Exception;
}

/// <summary>
/// 稳定、可解释的局面评价。子力是主体，过河兵、中心活动、将帅受将和仕相完整度只作较小修正，
/// 避免位置奖励压过一枚真实强子的得失。
/// </summary>
internal static class XiangqiPositionEvaluator
{
    internal static int Evaluate(XiangqiGameSnapshot snapshot, XiangqiSide perspective)
    {
        if (snapshot.State == XiangqiGameState.Finished)
        {
            return snapshot.Winner switch
            {
                null => 0,
                var winner when winner == perspective => 100_000_000,
                _ => -100_000_000,
            };
        }

        var score = 0;
        var board = snapshot.CopyBoard();
        var redAdvisors = 0;
        var redElephants = 0;
        var blackAdvisors = 0;
        var blackElephants = 0;
        for (var index = 0; index < board.Length; index++)
        {
            if (board[index] is not { } piece)
            {
                continue;
            }

            var row = index / XiangqiRules.ColumnCount;
            var column = index % XiangqiRules.ColumnCount;
            var position = new XiangqiPosition(row, column);
            var value = PieceValue(piece.Type) + PositionalValue(piece, row, column) +
                ActivityValue(board, piece, position);
            if (piece.Type != XiangqiPieceType.General &&
                XiangqiRules.IsSquareAttacked(snapshot, position, piece.Side))
            {
                value += 6;
            }

            if (piece.Type == XiangqiPieceType.Advisor)
            {
                if (piece.Side == XiangqiSide.Red) redAdvisors++; else blackAdvisors++;
            }

            if (piece.Type == XiangqiPieceType.Elephant)
            {
                if (piece.Side == XiangqiSide.Red) redElephants++; else blackElephants++;
            }

            score += piece.Side == perspective ? value : -value;
        }

        var redGuardBonus = ((redAdvisors == 2 ? 18 : 0) + (redElephants == 2 ? 14 : 0));
        var blackGuardBonus = ((blackAdvisors == 2 ? 18 : 0) + (blackElephants == 2 ? 14 : 0));
        score += perspective == XiangqiSide.Red
            ? redGuardBonus - blackGuardBonus
            : blackGuardBonus - redGuardBonus;

        if (XiangqiRules.IsInCheck(snapshot, perspective))
        {
            score -= 80;
        }

        if (XiangqiRules.IsInCheck(snapshot, XiangqiRules.OpponentOf(perspective)))
        {
            score += 80;
        }

        return score;
    }

    internal static int PieceValue(XiangqiPieceType type) => type switch
    {
        XiangqiPieceType.General => 0,
        XiangqiPieceType.Chariot => 900,
        XiangqiPieceType.Cannon => 450,
        XiangqiPieceType.Horse => 400,
        XiangqiPieceType.Advisor or XiangqiPieceType.Elephant => 200,
        XiangqiPieceType.Soldier => 100,
        _ => 0,
    };

    private static int PositionalValue(XiangqiPiece piece, int row, int column)
    {
        var center = 4 - Math.Abs(4 - column);
        return piece.Type switch
        {
            XiangqiPieceType.Soldier => SoldierValue(piece.Side, row) + (center * 3),
            XiangqiPieceType.Horse => center * 5,
            XiangqiPieceType.Cannon => center * 3,
            XiangqiPieceType.Chariot => center * 2,
            XiangqiPieceType.Advisor or XiangqiPieceType.Elephant => 8,
            _ => 0,
        };
    }

    private static int SoldierValue(XiangqiSide side, int row)
    {
        var crossed = side == XiangqiSide.Red ? row <= 4 : row >= 5;
        if (!crossed)
        {
            return 0;
        }

        var advance = side == XiangqiSide.Red ? 4 - row : row - 5;
        return 60 + (advance * 18);
    }

    private static int ActivityValue(
        XiangqiPiece?[] board,
        XiangqiPiece piece,
        XiangqiPosition position) => piece.Type switch
    {
        XiangqiPieceType.Chariot => CountOpenRayPoints(board, position) * 2,
        XiangqiPieceType.Horse => CountOpenHorseLegs(board, position) * 3,
        XiangqiPieceType.Cannon => CountCannonContacts(board, piece.Side, position) * 5,
        _ => 0,
    };

    private static int CountOpenRayPoints(XiangqiPiece?[] board, XiangqiPosition position)
    {
        var count = 0;
        foreach (var (rowDirection, columnDirection) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            for (var step = 1; step < XiangqiRules.RowCount; step++)
            {
                var target = new XiangqiPosition(
                    position.Row + (rowDirection * step),
                    position.Column + (columnDirection * step));
                if (!XiangqiRules.IsInside(target))
                {
                    break;
                }

                if (board[(target.Row * XiangqiRules.ColumnCount) + target.Column] is not null)
                {
                    break;
                }

                count++;
            }
        }

        return count;
    }

    private static int CountOpenHorseLegs(XiangqiPiece?[] board, XiangqiPosition position)
    {
        var count = 0;
        foreach (var (row, column) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var leg = new XiangqiPosition(position.Row + row, position.Column + column);
            if (XiangqiRules.IsInside(leg) &&
                board[(leg.Row * XiangqiRules.ColumnCount) + leg.Column] is null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountCannonContacts(
        XiangqiPiece?[] board,
        XiangqiSide side,
        XiangqiPosition position)
    {
        var contacts = 0;
        foreach (var (rowDirection, columnDirection) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var foundScreen = false;
            for (var step = 1; step < XiangqiRules.RowCount; step++)
            {
                var target = new XiangqiPosition(
                    position.Row + (rowDirection * step),
                    position.Column + (columnDirection * step));
                if (!XiangqiRules.IsInside(target))
                {
                    break;
                }

                var targetPiece = board[(target.Row * XiangqiRules.ColumnCount) + target.Column];
                if (targetPiece is null)
                {
                    continue;
                }

                if (!foundScreen)
                {
                    foundScreen = true;
                    continue;
                }

                if (targetPiece.Value.Side != side)
                {
                    contacts++;
                }

                break;
            }
        }

        return contacts;
    }
}
