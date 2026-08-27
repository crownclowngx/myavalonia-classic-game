using System.Globalization;
using System.Text;

namespace ClassicGamePlugin.Features.FreeCell.Domain;

internal enum FreeCellSolveStatus
{
    Solved,
    Unsolvable,
    NodeLimitReached,
}

internal sealed record FreeCellSolveResult(
    FreeCellSolveStatus Status,
    IReadOnlyList<FreeCellMove> Moves,
    int ExpandedNodes);

/// <summary>求解能力的窄边界，便于编号牌局供应器和 ViewModel 使用确定性测试替身。</summary>
internal interface IFreeCellSolver
{
    FreeCellSolveResult Solve(FreeCellSnapshot snapshot, int nodeLimit, CancellationToken cancellationToken);
}

/// <summary>
/// 确定性的启发式优先求解器。优先级只改变搜索顺序，不删除合法分支；空闲单元与牌列在访问键中
/// 规范化为无序集合，因为这些位置在规则上完全对称。这样能显著减少重复状态，同时保留找到解和
/// 在队列耗尽时证明无解的语义。
/// </summary>
internal sealed class FreeCellSolver : IFreeCellSolver
{
    public FreeCellSolveResult Solve(
        FreeCellSnapshot snapshot,
        int nodeLimit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nodeLimit);
        var queue = new PriorityQueue<SearchNode, (int Score, long Order)>();
        var root = new SearchNode(snapshot, null, null, 0);
        var visited = new HashSet<string>(StringComparer.Ordinal) { CreateCanonicalKey(snapshot) };
        long order = 0;
        queue.Enqueue(root, (GetPriority(snapshot, 0), order++));
        var expanded = 0;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expanded >= nodeLimit)
            {
                return new FreeCellSolveResult(FreeCellSolveStatus.NodeLimitReached, Array.Empty<FreeCellMove>(), expanded);
            }

            var node = queue.Dequeue();
            if (node.Snapshot.State == FreeCellGameState.Won)
            {
                return new FreeCellSolveResult(FreeCellSolveStatus.Solved, BuildPath(node), expanded);
            }

            expanded++;
            foreach (var move in FreeCellRules.EnumerateLegalMoves(node.Snapshot, reduceSymmetricDestinations: true))
            {
                if (FreeCellRules.TryApplyMove(node.Snapshot, move, autoCollect: true) is not { } result)
                {
                    continue;
                }

                var key = CreateCanonicalKey(result.Snapshot);
                if (!visited.Add(key))
                {
                    continue;
                }

                var child = new SearchNode(result.Snapshot, node, move, node.Depth + 1);
                queue.Enqueue(child, (GetPriority(result.Snapshot, child.Depth), order++));
            }
        }

        return new FreeCellSolveResult(FreeCellSolveStatus.Unsolvable, Array.Empty<FreeCellMove>(), expanded);
    }

    private static IReadOnlyList<FreeCellMove> BuildPath(SearchNode node)
    {
        var path = new List<FreeCellMove>();
        for (var current = node; current.Move is { } move; current = current.Parent!)
        {
            path.Add(move);
        }

        path.Reverse();
        return path;
    }

    private static int GetPriority(FreeCellSnapshot snapshot, int depth)
    {
        var remaining = 52 - snapshot.FoundationCardCount;
        var buriedLowCards = snapshot.Tableaus.Sum(column =>
            column.Select((card, index) => card.Rank <= 5 ? column.Count - index - 1 : 0).Sum());
        var occupiedCells = snapshot.FreeCells.Count(card => card is not null);
        var emptyColumns = snapshot.Tableaus.Count(column => column.Count == 0);
        return (remaining * 200) + (buriedLowCards * 3) + (occupiedCells * 8) - (emptyColumns * 12) + depth;
    }

    private static string CreateCanonicalKey(FreeCellSnapshot snapshot)
    {
        var builder = new StringBuilder(240);
        foreach (var rank in snapshot.Foundations)
        {
            builder.Append(rank.ToString("D2", CultureInfo.InvariantCulture)).Append(',');
        }

        builder.Append('|');
        foreach (var card in snapshot.FreeCells.Where(card => card is not null).Select(card => card!.Value.Id).Order())
        {
            builder.Append(card.ToString("D2", CultureInfo.InvariantCulture)).Append(',');
        }

        builder.Append('|');
        foreach (var column in snapshot.Tableaus
                     .Select(column => string.Join('.', column.Select(card => card.Id.ToString("D2", CultureInfo.InvariantCulture))))
                     .Order(StringComparer.Ordinal))
        {
            builder.Append(column).Append('/');
        }

        return builder.ToString();
    }

    private sealed record SearchNode(
        FreeCellSnapshot Snapshot,
        SearchNode? Parent,
        FreeCellMove? Move,
        int Depth);
}
