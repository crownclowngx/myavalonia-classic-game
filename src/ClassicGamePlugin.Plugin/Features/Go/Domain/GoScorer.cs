namespace ClassicGamePlugin.Features.Go.Domain;

/// <summary>
/// 按中国数子法对一个数子阶段快照进行纯计算。算法先把双方共同标记的死子视为空点，再对每个空区做一次洪泛搜索；
/// 空区只接触一种颜色便归该方，接触双方或完全没有边界棋子则为中立点。
/// </summary>
internal static class GoScorer
{
    internal static GoScoreResult Calculate(GoGameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State != GoGameState.Scoring)
        {
            throw new InvalidOperationException("只有数子阶段可以计算围棋终局分数。");
        }

        var board = snapshot.CopyBoard();
        foreach (var dead in snapshot.GetDeadStones())
        {
            board[GoRules.IndexOf(dead)] = null;
        }

        var blackStones = board.Count(stone => stone == GoStone.Black);
        var whiteStones = board.Count(stone => stone == GoStone.White);
        var blackTerritory = 0;
        var whiteTerritory = 0;
        var neutralPoints = 0;
        var owners = new Dictionary<GoPosition, GoStone>();
        var visited = new HashSet<GoPosition>();

        for (var row = 0; row < GoRules.BoardSize; row++)
        {
            for (var column = 0; column < GoRules.BoardSize; column++)
            {
                var origin = new GoPosition(row, column);
                if (board[GoRules.IndexOf(origin)] is not null || !visited.Add(origin))
                {
                    continue;
                }

                var region = new List<GoPosition>();
                var borderingColors = new HashSet<GoStone>();
                var pending = new Queue<GoPosition>();
                pending.Enqueue(origin);
                while (pending.TryDequeue(out var current))
                {
                    region.Add(current);
                    foreach (var neighbor in GoRules.NeighborsOf(current))
                    {
                        var stone = board[GoRules.IndexOf(neighbor)];
                        if (stone is { } color)
                        {
                            borderingColors.Add(color);
                        }
                        else if (visited.Add(neighbor))
                        {
                            pending.Enqueue(neighbor);
                        }
                    }
                }

                if (borderingColors.Count == 1)
                {
                    var owner = borderingColors.Single();
                    foreach (var position in region)
                    {
                        owners[position] = owner;
                    }

                    if (owner == GoStone.Black)
                    {
                        blackTerritory += region.Count;
                    }
                    else
                    {
                        whiteTerritory += region.Count;
                    }
                }
                else
                {
                    neutralPoints += region.Count;
                }
            }
        }

        return new GoScoreResult(
            blackStones,
            whiteStones,
            blackTerritory,
            whiteTerritory,
            neutralPoints,
            owners);
    }
}
