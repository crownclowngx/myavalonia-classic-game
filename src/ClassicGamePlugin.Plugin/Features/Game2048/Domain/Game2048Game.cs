namespace ClassicGamePlugin.Features.Game2048.Domain;

/// <summary>
/// 持有单局 2048 状态并编排纯移动规则与方块生成策略。所有写操作都先在候选数组中完成；只有移动、
/// 生成和校验全部成功后才覆盖真实棋盘，从而避免随机依赖异常时留下半步状态。
/// </summary>
internal sealed class Game2048Game
{
    internal const int TargetValue = 2048;

    private readonly ITileSpawnStrategy _tileSpawnStrategy;
    private readonly int[] _cells = new int[Game2048Rules.CellCount];

    /// <summary>创建一局生产游戏，并立即按经典规则生成两个初始方块。</summary>
    internal Game2048Game(ITileSpawnStrategy tileSpawnStrategy)
    {
        _tileSpawnStrategy = tileSpawnStrategy ??
            throw new ArgumentNullException(nameof(tileSpawnStrategy));
        StartNewGame();
    }

    /// <summary>
    /// 使用确定棋盘创建测试实例。该入口仅用于验证难以通过随机开局抵达的合并、胜利和终局边界，
    /// 不代表插件提供存档或恢复能力。
    /// </summary>
    internal Game2048Game(
        ITileSpawnStrategy tileSpawnStrategy,
        IReadOnlyList<int> initialCells,
        int initialScore = 0,
        Game2048GameState initialState = Game2048GameState.Playing)
    {
        _tileSpawnStrategy = tileSpawnStrategy ??
            throw new ArgumentNullException(nameof(tileSpawnStrategy));
        ValidateCandidateBoard(initialCells);
        ArgumentOutOfRangeException.ThrowIfNegative(initialScore);

        initialCells.ToArray().CopyTo(_cells, 0);
        Score = initialScore;
        State = initialState;
    }

    /// <summary>获取按行优先排列的只读棋盘视图；零表示空格。</summary>
    internal IReadOnlyList<int> Cells => _cells;

    /// <summary>获取所有成功合并产生的新方块数值之和。</summary>
    internal int Score { get; private set; }

    /// <summary>获取当前交互阶段。</summary>
    internal Game2048GameState State { get; private set; }

    /// <summary>
    /// 尝试完成一次方向移动。没有任何格子变化时返回 null，并且不会请求生成策略、增加分数或改变状态；
    /// 成功时返回包含提交前后快照和视觉轨迹的不可变结果。
    /// </summary>
    internal Game2048Transition? Move(Game2048Direction direction)
    {
        if (State is Game2048GameState.WonAwaitingContinue or Game2048GameState.Lost)
        {
            return null;
        }

        var before = new Game2048Snapshot(_cells, Score, State);
        var projection = Game2048Rules.ProjectMove(_cells, direction);
        if (!projection.HasChanged)
        {
            return null;
        }

        var candidate = projection.Cells;
        var spawnedTile = AddValidatedSpawn(candidate);
        var nextScore = checked(Score + projection.ScoreDelta);
        var nextState = State;

        if (State == Game2048GameState.Playing && candidate.Contains(TargetValue))
        {
            nextState = Game2048GameState.WonAwaitingContinue;
        }
        else if (!Game2048Rules.HasAvailableMove(candidate))
        {
            nextState = Game2048GameState.Lost;
        }

        Array.Copy(candidate, _cells, _cells.Length);
        Score = nextScore;
        State = nextState;
        return new Game2048Transition(
            direction,
            before,
            new Game2048Snapshot(_cells, Score, State),
            projection.Motions,
            projection.MergedPositions,
            spawnedTile);
    }

    /// <summary>
    /// 确认首次达成 2048 后继续挑战。若胜利棋盘已经没有合法移动，则直接进入失败终态；
    /// 否则进入不会再次弹出胜利确认的继续阶段。
    /// </summary>
    internal bool ContinueAfterWin()
    {
        if (State != Game2048GameState.WonAwaitingContinue)
        {
            return false;
        }

        State = Game2048Rules.HasAvailableMove(_cells)
            ? Game2048GameState.Continuing
            : Game2048GameState.Lost;
        return true;
    }

    /// <summary>原子创建一局双方块、零分的全新游戏。</summary>
    internal void StartNewGame()
    {
        var candidate = new int[Game2048Rules.CellCount];
        AddValidatedSpawn(candidate);
        AddValidatedSpawn(candidate);

        Array.Copy(candidate, _cells, _cells.Length);
        Score = 0;
        State = Game2048GameState.Playing;
    }

    private Game2048TileSpawn AddValidatedSpawn(int[] candidate)
    {
        var emptyPositions = GetEmptyPositions(candidate);
        if (emptyPositions.Count == 0)
        {
            throw new InvalidOperationException("没有空格时不能生成新的 2048 方块。");
        }

        var spawn = _tileSpawnStrategy.CreateSpawn(emptyPositions);
        var position = spawn.Position;
        if (position.Row < 0 || position.Row >= Game2048Rules.BoardSize ||
            position.Column < 0 || position.Column >= Game2048Rules.BoardSize)
        {
            throw new InvalidOperationException("方块生成策略返回了棋盘范围外的位置。");
        }

        var index = Game2048Rules.ToIndex(position.Row, position.Column);
        if (candidate[index] != 0 || !emptyPositions.Contains(position))
        {
            throw new InvalidOperationException("方块生成策略必须选择当前候选棋盘中的空格。");
        }

        if (spawn.Value is not (2 or 4))
        {
            throw new InvalidOperationException("方块生成策略只能生成数值 2 或 4。");
        }

        candidate[index] = spawn.Value;
        return spawn;
    }

    private static List<Game2048Position> GetEmptyPositions(IReadOnlyList<int> cells)
    {
        var result = new List<Game2048Position>();
        for (var row = 0; row < Game2048Rules.BoardSize; row++)
        {
            for (var column = 0; column < Game2048Rules.BoardSize; column++)
            {
                if (cells[Game2048Rules.ToIndex(row, column)] == 0)
                {
                    result.Add(new Game2048Position(row, column));
                }
            }
        }

        return result;
    }

    private static void ValidateCandidateBoard(IReadOnlyList<int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count != Game2048Rules.CellCount)
        {
            throw new ArgumentException(
                $"2048 棋盘必须恰好包含 {Game2048Rules.CellCount} 个格子。",
                nameof(cells));
        }

        if (cells.Any(value => value < 0 || value != 0 && !IsPowerOfTwo(value)))
        {
            throw new ArgumentException("2048 棋盘只能包含空格或正的 2 次幂方块。", nameof(cells));
        }
    }

    private static bool IsPowerOfTwo(int value) => (value & (value - 1)) == 0;
}
