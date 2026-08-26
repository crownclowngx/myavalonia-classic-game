namespace ClassicGamePlugin.Features.Reversi.Domain;

/// <summary>
/// 编排一局黑白棋的领域状态、合法落子和撤销历史。该类型不认识 Avalonia、计时器、
/// 人机模式或用户输入设备，因此规则可以通过普通单元测试完整验证。
/// </summary>
internal sealed class ReversiGame
{
    private readonly Stack<ReversiMoveResult> _history = [];
    private ReversiGameSnapshot _snapshot;

    internal ReversiGame()
        : this(ReversiRules.CreateInitialSnapshot())
    {
    }

    /// <summary>使用指定快照创建棋局，供 AI 场景和确定性领域测试建立残局。</summary>
    internal ReversiGame(ReversiGameSnapshot snapshot)
    {
        _snapshot = snapshot?.Clone() ?? throw new ArgumentNullException(nameof(snapshot));
    }

    internal ReversiGameState State => _snapshot.State;
    internal ReversiDiscColor CurrentPlayer => _snapshot.CurrentPlayer;
    internal int MoveCount => _snapshot.MoveCount;
    internal int BlackCount => _snapshot.BlackCount;
    internal int WhiteCount => _snapshot.WhiteCount;
    internal ReversiPosition? LastMove => _snapshot.LastMove;
    internal bool CanUndo => _history.Count > 0;
    internal ReversiDiscColor? Winner => State != ReversiGameState.Finished || BlackCount == WhiteCount
        ? null
        : BlackCount > WhiteCount ? ReversiDiscColor.Black : ReversiDiscColor.White;

    internal ReversiDiscColor? GetDisc(ReversiPosition position) => _snapshot.GetDisc(position);

    internal IReadOnlyList<ReversiPosition> GetLegalMoves() =>
        State == ReversiGameState.Finished
            ? []
            : ReversiRules.GetLegalMoves(_snapshot, CurrentPlayer);

    /// <summary>执行一次合法落子；非法落子不修改快照，也不污染撤销历史。</summary>
    internal ReversiMoveResult? PlaceDisc(ReversiPosition position)
    {
        var result = ReversiRules.TryApplyMove(_snapshot, CurrentPlayer, position);
        if (result is null)
        {
            return null;
        }

        _history.Push(result);
        _snapshot = result.After;
        return result;
    }

    /// <summary>恢复最近一次成功落子之前的完整状态，并返回被撤销的动作。</summary>
    internal ReversiMoveResult? Undo()
    {
        if (!_history.TryPop(out var result))
        {
            return null;
        }

        _snapshot = result.Before.Clone();
        return result;
    }

    /// <summary>判断当前历史中是否至少包含指定玩家的一手，用于人机模式决策点撤销。</summary>
    internal bool HasMoveBy(ReversiDiscColor player) =>
        _history.Any(result => result.Player == player);

    /// <summary>重新建立标准初始棋盘，并清除旧局的全部撤销历史。</summary>
    internal void StartNewGame()
    {
        _snapshot = ReversiRules.CreateInitialSnapshot();
        _history.Clear();
    }

    internal ReversiGameSnapshot CreateSnapshot() => _snapshot.Clone();
}
