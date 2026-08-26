namespace ClassicGamePlugin.Features.SpiderSolitaire.Domain;

/// <summary>
/// 一次完整且不可变的棋局快照。撤销恢复牌列、库存、完成组和领域状态，
/// 但不会恢复 ViewModel 拥有的耗时与累计操作惩罚，这是经典计分语义的一部分。
/// </summary>
internal sealed class SpiderGameSnapshot
{
    internal SpiderGameSnapshot(
        IEnumerable<IEnumerable<SpiderCardState>> columns,
        IEnumerable<SpiderCardState> stock,
        IEnumerable<IEnumerable<SpiderCardState>> completedRuns,
        SpiderGameState state)
    {
        Columns = columns.Select(column => (IReadOnlyList<SpiderCardState>)column.ToArray()).ToArray();
        Stock = stock.ToArray();
        CompletedRuns = completedRuns
            .Select(run => (IReadOnlyList<SpiderCardState>)run.ToArray())
            .ToArray();
        State = state;
    }

    internal IReadOnlyList<IReadOnlyList<SpiderCardState>> Columns { get; }
    internal IReadOnlyList<SpiderCardState> Stock { get; }
    internal IReadOnlyList<IReadOnlyList<SpiderCardState>> CompletedRuns { get; }
    internal SpiderGameState State { get; }
}

/// <summary>会产生棋局变化的玩家动作类型，供计分、测试和视图动画选择节奏。</summary>
internal enum SpiderActionKind
{
    Move,
    Deal,
    Undo,
}

/// <summary>
/// 领域动作的完整结果。领域状态已经提交，View 只根据前后快照播放视觉过渡，
/// 因此动画被中断时可以直接显示 <see cref="After"/>，不会留下半局状态。
/// </summary>
internal sealed record SpiderGameTransition(
    SpiderActionKind Kind,
    SpiderGameSnapshot Before,
    SpiderGameSnapshot After,
    IReadOnlyList<int> FlippedCardIds,
    IReadOnlyList<int> CompletedCardIds);

/// <summary>提示的种类；提示只描述一步合法意图，不修改棋局。</summary>
internal enum SpiderHintKind
{
    Move,
    Deal,
}

/// <summary>确定性提示结果。发牌提示的三个位置字段均为 -1。</summary>
internal readonly record struct SpiderHint(
    SpiderHintKind Kind,
    int SourceColumn,
    int SourceIndex,
    int DestinationColumn);
