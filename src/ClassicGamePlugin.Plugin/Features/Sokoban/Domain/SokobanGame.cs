namespace ClassicGamePlugin.Features.Sokoban.Domain;

/// <summary>
/// 持有单关推箱子的动态状态。每次有效移动先生成走前快照，再在局部变量中验证完整推动路径，最后一次性修改玩家、
/// 箱子和计数；墙撞击或非法推动不会留下历史节点，也不会让 UI 收到半完成的移动。
/// </summary>
internal sealed class SokobanGame
{
    private readonly Stack<SokobanGameSnapshot> _history = new();
    private HashSet<SokobanPosition> _boxes;

    internal SokobanGame(SokobanLevelDefinition level)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        _boxes = level.InitialBoxes.ToHashSet();
        Player = level.InitialPlayer;
        IsCompleted = _boxes.All(level.IsGoal);
    }

    internal SokobanLevelDefinition Level { get; }
    internal SokobanPosition Player { get; private set; }
    internal int MoveCount { get; private set; }
    internal int PushCount { get; private set; }
    internal bool IsCompleted { get; private set; }
    internal bool CanUndo => _history.Count > 0;
    internal int BoxesOnGoals => _boxes.Count(Level.IsGoal);
    internal IReadOnlyCollection<SokobanPosition> Boxes => _boxes.ToArray();

    internal bool HasBox(SokobanPosition position) => _boxes.Contains(position);

    internal SokobanMoveResult? Move(SokobanDirection direction)
    {
        if (IsCompleted)
        {
            return null;
        }

        var playerTo = Player.Move(direction);
        if (Level.TerrainAt(playerTo) == SokobanTerrain.Wall)
        {
            return null;
        }

        SokobanPosition? boxFrom = null;
        SokobanPosition? boxTo = null;
        if (_boxes.Contains(playerTo))
        {
            var candidateBoxTo = playerTo.Move(direction);
            if (Level.TerrainAt(candidateBoxTo) == SokobanTerrain.Wall || _boxes.Contains(candidateBoxTo))
            {
                return null;
            }

            boxFrom = playerTo;
            boxTo = candidateBoxTo;
        }

        var before = CreateSnapshot();
        if (boxFrom is { } oldBox && boxTo is { } newBox)
        {
            _boxes.Remove(oldBox);
            _boxes.Add(newBox);
            PushCount++;
        }

        Player = playerTo;
        MoveCount++;
        IsCompleted = _boxes.All(Level.IsGoal);
        _history.Push(before);
        return new SokobanMoveResult(before, CreateSnapshot(), direction, boxFrom, boxTo);
    }

    /// <summary>恢复最近一次有效移动前的完整状态；非法移动从未入栈，因此一次 Undo 始终对应一次玩家看得见的移动。</summary>
    internal bool Undo()
    {
        if (!_history.TryPop(out var snapshot))
        {
            return false;
        }

        Restore(snapshot);
        return true;
    }

    internal void Restart()
    {
        _history.Clear();
        _boxes = Level.InitialBoxes.ToHashSet();
        Player = Level.InitialPlayer;
        MoveCount = 0;
        PushCount = 0;
        IsCompleted = _boxes.All(Level.IsGoal);
    }

    internal SokobanGameSnapshot CreateSnapshot() =>
        new(Player, _boxes, MoveCount, PushCount, IsCompleted);

    private void Restore(SokobanGameSnapshot snapshot)
    {
        Player = snapshot.Player;
        _boxes = snapshot.Boxes.ToHashSet();
        MoveCount = snapshot.MoveCount;
        PushCount = snapshot.PushCount;
        IsCompleted = snapshot.IsCompleted;
    }
}
