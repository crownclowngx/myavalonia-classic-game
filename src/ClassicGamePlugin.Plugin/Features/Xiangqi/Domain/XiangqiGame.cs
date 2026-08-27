namespace ClassicGamePlugin.Features.Xiangqi.Domain;

/// <summary>
/// 保存一局中国象棋的当前快照和成功走棋前的快照栈。所有规则计算委托给 <see cref="XiangqiRules"/>，
/// 本类型不包含玩家模式、AI、计时或界面状态。
/// </summary>
internal sealed class XiangqiGame
{
    private readonly Stack<XiangqiGameSnapshot> _history = new();
    private XiangqiGameSnapshot _snapshot;

    internal XiangqiGame()
        : this(XiangqiRules.CreateInitialSnapshot())
    {
    }

    internal XiangqiGame(XiangqiGameSnapshot snapshot) =>
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    internal XiangqiSide CurrentSide => _snapshot.CurrentSide;
    internal XiangqiGameState State => _snapshot.State;
    internal XiangqiSide? Winner => _snapshot.Winner;
    internal XiangqiTerminationReason? TerminationReason => _snapshot.TerminationReason;
    internal int MoveCount => _snapshot.MoveCount;
    internal bool CanUndo => _history.Count > 0;

    internal XiangqiGameSnapshot CreateSnapshot() => new(
        _snapshot.CopyBoard(),
        _snapshot.CurrentSide,
        _snapshot.State,
        _snapshot.MoveCount,
        _snapshot.LastMove,
        _snapshot.Winner,
        _snapshot.TerminationReason,
        _snapshot.NoCapturePlyCount,
        _snapshot.CopyPositionHistory());

    internal XiangqiPiece? GetPiece(XiangqiPosition position) => _snapshot.GetPiece(position);
    internal XiangqiMoveValidation ValidateMove(XiangqiMove move) => XiangqiRules.ValidateMove(_snapshot, move);
    internal IReadOnlyList<XiangqiMove> GetLegalMoves() => XiangqiRules.GetLegalMoves(_snapshot);

    internal XiangqiMoveResult? Move(XiangqiMove move)
    {
        var result = XiangqiRules.TryApplyMove(_snapshot, move);
        if (result is null)
        {
            return null;
        }

        _history.Push(_snapshot);
        _snapshot = result.After;
        return result;
    }

    internal XiangqiGameSnapshot? Undo()
    {
        if (!_history.TryPop(out var previous))
        {
            return null;
        }

        _snapshot = previous;
        return CreateSnapshot();
    }

    internal void Resign(XiangqiSide side)
    {
        if (_snapshot.State == XiangqiGameState.Finished)
        {
            return;
        }

        _snapshot = new XiangqiGameSnapshot(
            _snapshot.CopyBoard(),
            _snapshot.CurrentSide,
            XiangqiGameState.Finished,
            _snapshot.MoveCount,
            _snapshot.LastMove,
            XiangqiRules.OpponentOf(side),
            XiangqiTerminationReason.Resignation,
            _snapshot.NoCapturePlyCount,
            _snapshot.CopyPositionHistory());
    }

    internal void StartNewGame()
    {
        _history.Clear();
        _snapshot = XiangqiRules.CreateInitialSnapshot();
    }
}
