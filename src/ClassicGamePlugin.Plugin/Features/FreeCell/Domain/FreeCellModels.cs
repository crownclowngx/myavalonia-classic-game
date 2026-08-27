namespace ClassicGamePlugin.Features.FreeCell.Domain;

/// <summary>标准扑克牌的四种花色。枚举值同时作为四个基础区的稳定索引。</summary>
internal enum FreeCellSuit
{
    Spades,
    Hearts,
    Clubs,
    Diamonds,
}

/// <summary>
/// 一张不可变的扑克牌。唯一编号只用于快照差分、控件复用和动画定位；
/// 规则始终只读取花色与点数，避免把展示身份混入领域判断。
/// </summary>
internal readonly record struct FreeCellCard(int Id, FreeCellSuit Suit, int Rank)
{
    internal bool IsRed => Suit is FreeCellSuit.Hearts or FreeCellSuit.Diamonds;
}

internal enum FreeCellLocationKind
{
    Tableau,
    FreeCell,
    Foundation,
}

/// <summary>领域位置使用“区域 + 索引”描述，不包含任何像素坐标。</summary>
internal readonly record struct FreeCellLocation(FreeCellLocationKind Kind, int Index)
{
    internal static FreeCellLocation Tableau(int index) => new(FreeCellLocationKind.Tableau, index);
    internal static FreeCellLocation Cell(int index) => new(FreeCellLocationKind.FreeCell, index);
    internal static FreeCellLocation Foundation(FreeCellSuit suit) => new(FreeCellLocationKind.Foundation, (int)suit);
}

/// <summary>
/// 一次玩家移动意图。牌列来源的 <see cref="SourceCardIndex"/> 指向要移动序列的首牌；
/// 空闲单元来源固定为零，目标区域不使用该字段。
/// </summary>
internal readonly record struct FreeCellMove(
    FreeCellLocation Source,
    int SourceCardIndex,
    FreeCellLocation Destination);

internal enum FreeCellGameState
{
    Ready,
    Running,
    Won,
}

/// <summary>
/// 完整不可变棋局快照。所有集合在构造时复制，当前棋局、撤销历史、求解器和动画
/// 因而不会共享可变集合引用。
/// </summary>
internal sealed class FreeCellSnapshot
{
    internal FreeCellSnapshot(
        IEnumerable<IEnumerable<FreeCellCard>> tableaus,
        IEnumerable<FreeCellCard?> freeCells,
        IEnumerable<int> foundations,
        int moveCount,
        FreeCellGameState state,
        int dealNumber,
        int candidateIndex)
    {
        Tableaus = tableaus.Select(column => (IReadOnlyList<FreeCellCard>)column.ToArray()).ToArray();
        FreeCells = freeCells.ToArray();
        Foundations = foundations.ToArray();
        if (Tableaus.Count != 8 || FreeCells.Count != 4 || Foundations.Count != 4)
        {
            throw new ArgumentException("空当接龙快照必须包含八个牌列、四个空闲单元和四个基础区。");
        }

        MoveCount = moveCount;
        State = state;
        DealNumber = dealNumber;
        CandidateIndex = candidateIndex;
    }

    internal IReadOnlyList<IReadOnlyList<FreeCellCard>> Tableaus { get; }
    internal IReadOnlyList<FreeCellCard?> FreeCells { get; }
    internal IReadOnlyList<int> Foundations { get; }
    internal int MoveCount { get; }
    internal FreeCellGameState State { get; }
    internal int DealNumber { get; }
    internal int CandidateIndex { get; }
    internal int FoundationCardCount => Foundations.Sum();
}

internal enum FreeCellActionKind
{
    Move,
    AutoCollect,
    Undo,
}

/// <summary>
/// 一次已经完整提交的领域变化。View 只在前后快照间播放视觉过渡；动画被关闭或中断时，
/// 可以直接呈现 <see cref="After"/>，不会留下半提交状态。
/// </summary>
internal sealed record FreeCellTransition(
    FreeCellActionKind Kind,
    FreeCellSnapshot Before,
    FreeCellSnapshot After,
    IReadOnlyList<int> PrimaryCardIds,
    IReadOnlyList<int> AutoCollectedCardIds);

/// <summary>编号牌局的最终牌序及求解筛选所采用的候选序号。</summary>
internal sealed record FreeCellDeal(
    int Number,
    int CandidateIndex,
    IReadOnlyList<FreeCellCard> Deck);
