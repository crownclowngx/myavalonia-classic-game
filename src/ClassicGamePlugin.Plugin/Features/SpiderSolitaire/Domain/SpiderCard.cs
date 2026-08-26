namespace ClassicGamePlugin.Features.SpiderSolitaire.Domain;

/// <summary>蜘蛛纸牌使用的四种标准花色。</summary>
internal enum SpiderCardSuit
{
    Spades,
    Hearts,
    Clubs,
    Diamonds,
}

/// <summary>蜘蛛纸牌难度。数值直接表示牌局使用的花色数量。</summary>
internal enum SpiderSolitaireDifficulty
{
    OneSuit = 1,
    TwoSuits = 2,
    FourSuits = 4,
}

/// <summary>牌局生命周期。无可用移动不是不可逆失败，因此不单独建 Lost 状态。</summary>
internal enum SpiderGameState
{
    Ready,
    Running,
    Won,
}

/// <summary>
/// 一张不会因洗牌而改变的牌定义。唯一 ID 把两副牌中点数、花色相同的牌区分开，
/// 同时为撤销和动画差分提供稳定身份。
/// </summary>
internal readonly record struct SpiderCardDefinition(
    int Id,
    SpiderCardSuit Suit,
    int Rank);

/// <summary>棋局内部的可变牌状态；它只比牌定义多保存正反面，不包含任何展示属性。</summary>
internal sealed class SpiderCard
{
    internal SpiderCard(SpiderCardDefinition definition, bool isFaceUp = false)
    {
        Definition = definition;
        IsFaceUp = isFaceUp;
    }

    internal SpiderCardDefinition Definition { get; }
    internal int Id => Definition.Id;
    internal SpiderCardSuit Suit => Definition.Suit;
    internal int Rank => Definition.Rank;
    internal bool IsFaceUp { get; set; }
}

/// <summary>快照中使用的不可变牌状态。</summary>
internal readonly record struct SpiderCardState(
    int Id,
    SpiderCardSuit Suit,
    int Rank,
    bool IsFaceUp)
{
    internal SpiderCard ToMutableCard() =>
        new(new SpiderCardDefinition(Id, Suit, Rank), IsFaceUp);
}
