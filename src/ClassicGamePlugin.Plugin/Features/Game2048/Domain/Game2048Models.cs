namespace ClassicGamePlugin.Features.Game2048.Domain;

/// <summary>表示玩家推动整张棋盘的四个方向。</summary>
internal enum Game2048Direction
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// 表示一局 2048 的交互阶段。胜利确认单独占用一个状态，避免 ViewModel 通过弹窗或布尔组合
/// 猜测领域层是否应继续接受移动；选择继续后使用独立状态，确保 4096 等更高数字不会重复触发确认。
/// </summary>
internal enum Game2048GameState
{
    Playing,
    WonAwaitingContinue,
    Continuing,
    Lost,
}

/// <summary>表示 4×4 棋盘中的零基行列坐标。</summary>
internal readonly record struct Game2048Position(int Row, int Column);

/// <summary>表示生成策略选择的新方块位置和数值。</summary>
internal readonly record struct Game2048TileSpawn(Game2048Position Position, int Value);

/// <summary>
/// 描述一个移动前方块的视觉轨迹。两个同值方块合并时会各自产生一条指向同一目标的轨迹，
/// <see cref="IsMergeParticipant"/> 只供视觉反馈识别，不参与领域规则判断。
/// </summary>
internal readonly record struct Game2048TileMotion(
    Game2048Position Source,
    Game2048Position Target,
    int Value,
    bool IsMergeParticipant);

/// <summary>
/// 抽象 2048 唯一需要替换的随机能力。生产策略负责随机选空格和 2/4 数值，测试策略则提供完全确定的序列；
/// 游戏引擎仍会验证返回结果，策略不能直接修改棋盘。
/// </summary>
internal interface ITileSpawnStrategy
{
    /// <summary>从当前空格中选择一个位置，并返回值为 2 或 4 的新方块。</summary>
    Game2048TileSpawn CreateSpawn(IReadOnlyList<Game2048Position> emptyPositions);
}

/// <summary>保存一次纯移动计算的候选棋盘、分数和视觉轨迹，不包含随机生成或状态提交。</summary>
internal sealed record Game2048MoveProjection(
    int[] Cells,
    int ScoreDelta,
    bool HasChanged,
    IReadOnlyList<Game2048TileMotion> Motions,
    IReadOnlyList<Game2048Position> MergedPositions);

/// <summary>
/// 一份不可变棋盘快照。构造时复制输入数组，再通过只读包装暴露，动画或测试无法反向修改真实对局。
/// </summary>
internal sealed class Game2048Snapshot
{
    internal Game2048Snapshot(
        IReadOnlyList<int> cells,
        int score,
        Game2048GameState state)
    {
        var copy = cells.ToArray();
        Cells = Array.AsReadOnly(copy);
        Score = score;
        State = state;
    }

    internal IReadOnlyList<int> Cells { get; }
    internal int Score { get; }
    internal Game2048GameState State { get; }
}

/// <summary>
/// 一次已经原子提交的有效移动。领域状态以 <see cref="After"/> 为准；View 只读取前后快照和轨迹回放动画，
/// 即使动画被取消也不需要回滚游戏。
/// </summary>
internal sealed class Game2048Transition
{
    internal Game2048Transition(
        Game2048Direction direction,
        Game2048Snapshot before,
        Game2048Snapshot after,
        IEnumerable<Game2048TileMotion> motions,
        IEnumerable<Game2048Position> mergedPositions,
        Game2048TileSpawn spawnedTile)
    {
        Direction = direction;
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Motions = Array.AsReadOnly(motions.ToArray());
        MergedPositions = Array.AsReadOnly(mergedPositions.Distinct().ToArray());
        SpawnedTile = spawnedTile;
    }

    internal Game2048Direction Direction { get; }
    internal Game2048Snapshot Before { get; }
    internal Game2048Snapshot After { get; }
    internal IReadOnlyList<Game2048TileMotion> Motions { get; }
    internal IReadOnlyList<Game2048Position> MergedPositions { get; }
    internal Game2048TileSpawn SpawnedTile { get; }
}
