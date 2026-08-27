namespace ClassicGamePlugin.Features.Sokoban.Domain;

internal enum SokobanDirection
{
    Up,
    Down,
    Left,
    Right,
}

internal enum SokobanDifficulty
{
    Beginner,
    Intermediate,
    Challenge,
}

internal enum SokobanTerrain
{
    Floor,
    Wall,
    Goal,
}

internal readonly record struct SokobanPosition(int Row, int Column)
{
    internal SokobanPosition Move(SokobanDirection direction) => direction switch
    {
        SokobanDirection.Up => this with { Row = Row - 1 },
        SokobanDirection.Down => this with { Row = Row + 1 },
        SokobanDirection.Left => this with { Column = Column - 1 },
        SokobanDirection.Right => this with { Column = Column + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}

/// <summary>
/// 保存一张关卡中永远不变的地形和初始动态对象。地形与箱子分层后，箱子位于目标点时不需要发明额外的
/// “箱子目标”格类型，移动和撤销也只修改动态状态，避免地图定义被一局游戏意外污染。
/// </summary>
internal sealed class SokobanLevelDefinition
{
    private readonly SokobanTerrain[] _terrain;
    private readonly SokobanPosition[] _initialBoxes;

    internal SokobanLevelDefinition(
        string id,
        string name,
        SokobanDifficulty difficulty,
        int width,
        int height,
        IReadOnlyList<SokobanTerrain> terrain,
        SokobanPosition initialPlayer,
        IReadOnlyList<SokobanPosition> initialBoxes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(initialBoxes);

        Id = id;
        Name = name;
        Difficulty = difficulty;
        Width = width;
        Height = height;
        _terrain = terrain.ToArray();
        InitialPlayer = initialPlayer;
        _initialBoxes = initialBoxes.ToArray();
    }

    internal string Id { get; }
    internal string Name { get; }
    internal SokobanDifficulty Difficulty { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal SokobanPosition InitialPlayer { get; }
    internal IReadOnlyList<SokobanPosition> InitialBoxes => Array.AsReadOnly(_initialBoxes);
    internal int GoalCount => _terrain.Count(cell => cell == SokobanTerrain.Goal);

    internal SokobanTerrain TerrainAt(SokobanPosition position) =>
        IsInside(position) ? _terrain[(position.Row * Width) + position.Column] : SokobanTerrain.Wall;

    internal bool IsInside(SokobanPosition position) =>
        position.Row >= 0 && position.Row < Height && position.Column >= 0 && position.Column < Width;

    internal bool IsGoal(SokobanPosition position) => TerrainAt(position) == SokobanTerrain.Goal;
}

/// <summary>
/// 一次撤销所需的完整动态状态。数组在创建和恢复时都会复制，历史节点与当前棋局绝不共享可变集合；
/// 这种直接快照比为简单棋局建立 Memento 类层次更容易审计，也能可靠恢复完成状态和两个计数器。
/// </summary>
internal sealed class SokobanGameSnapshot
{
    private readonly SokobanPosition[] _boxes;

    internal SokobanGameSnapshot(
        SokobanPosition player,
        IReadOnlyCollection<SokobanPosition> boxes,
        int moveCount,
        int pushCount,
        bool isCompleted)
    {
        Player = player;
        _boxes = boxes.OrderBy(position => position.Row).ThenBy(position => position.Column).ToArray();
        MoveCount = moveCount;
        PushCount = pushCount;
        IsCompleted = isCompleted;
    }

    internal SokobanPosition Player { get; }
    internal IReadOnlyList<SokobanPosition> Boxes => Array.AsReadOnly(_boxes);
    internal int MoveCount { get; }
    internal int PushCount { get; }
    internal bool IsCompleted { get; }
    internal bool HasBox(SokobanPosition position) => Array.IndexOf(_boxes, position) >= 0;
}

internal sealed record SokobanMoveResult(
    SokobanGameSnapshot Before,
    SokobanGameSnapshot After,
    SokobanDirection Direction,
    SokobanPosition? BoxFrom,
    SokobanPosition? BoxTo)
{
    internal bool PushedBox => BoxFrom.HasValue;
}
