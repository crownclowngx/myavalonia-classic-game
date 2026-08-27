namespace ClassicGamePlugin.Features.FreeCell.Domain;

/// <summary>
/// 空当接龙领域聚合。它只保存当前不可变快照与撤销历史；规则计算委托给 <see cref="FreeCellRules"/>，
/// 计时、命令、求解线程、拖拽坐标和动画均位于领域层之外。
/// </summary>
internal sealed class FreeCellGame
{
    private readonly Stack<FreeCellSnapshot> _history = [];

    internal FreeCellGame(FreeCellDeal deal, bool autoCollect) =>
        Current = FreeCellRules.CreateInitialSnapshot(deal, autoCollect);

    /// <summary>只供确定性测试和领域协作者从完整快照起步，不绕过后续规则提交。</summary>
    internal FreeCellGame(FreeCellSnapshot snapshot) =>
        Current = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    internal FreeCellSnapshot Current { get; private set; }
    internal bool CanUndo => _history.Count > 0;

    internal void Start(FreeCellDeal deal, bool autoCollect)
    {
        Current = FreeCellRules.CreateInitialSnapshot(deal, autoCollect);
        _history.Clear();
    }

    internal bool CanMove(FreeCellMove move) =>
        Current.State != FreeCellGameState.Won && FreeCellRules.CanMove(Current, move);

    internal FreeCellTransition? Move(FreeCellMove move, bool autoCollect)
    {
        if (Current.State == FreeCellGameState.Won ||
            FreeCellRules.TryApplyMove(Current, move, autoCollect) is not { } result)
        {
            return null;
        }

        var before = Current;
        _history.Push(before);
        Current = result.Snapshot;
        return new FreeCellTransition(
            FreeCellActionKind.Move,
            before,
            Current,
            result.PrimaryIds,
            result.AutoIds);
    }

    internal FreeCellTransition? CollectSafeCards()
    {
        if (Current.State == FreeCellGameState.Won)
        {
            return null;
        }

        var result = FreeCellRules.CollectSafeCards(Current, incrementMove: true);
        if (result.CardIds.Count == 0)
        {
            return null;
        }

        var before = Current;
        _history.Push(before);
        Current = result.Snapshot;
        return new FreeCellTransition(
            FreeCellActionKind.AutoCollect,
            before,
            Current,
            Array.Empty<int>(),
            result.CardIds);
    }

    internal FreeCellTransition? Undo()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        var before = Current;
        Current = _history.Pop();
        return new FreeCellTransition(
            FreeCellActionKind.Undo,
            before,
            Current,
            Array.Empty<int>(),
            Array.Empty<int>());
    }
}
