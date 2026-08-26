using ClassicGamePlugin.Features.SpiderSolitaire.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

/// <summary>固定保留输入顺序的洗牌策略，让蜘蛛纸牌测试完全不依赖随机结果。</summary>
internal sealed class IdentitySpiderCardShuffler : ISpiderCardShuffler
{
    public IReadOnlyList<SpiderCardDefinition> Shuffle(IReadOnlyList<SpiderCardDefinition> cards) =>
        cards.ToArray();
}

/// <summary>允许测试显式构造非法或特定牌序的洗牌策略。</summary>
internal sealed class DelegateSpiderCardShuffler(
    Func<IReadOnlyList<SpiderCardDefinition>, IReadOnlyList<SpiderCardDefinition>> shuffle)
    : ISpiderCardShuffler
{
    public IReadOnlyList<SpiderCardDefinition> Shuffle(IReadOnlyList<SpiderCardDefinition> cards) =>
        shuffle(cards);
}

/// <summary>
/// 领域规则测试使用的最小棋盘装配器。它只在测试程序集内直接调整内部集合，
/// 避免生产引擎为了测试暴露“任意载入非法棋局”的公共入口。
/// </summary>
internal static class SpiderTestBoard
{
    internal static void Clear(SpiderSolitaireGame game)
    {
        foreach (var column in MutableColumns(game))
        {
            column.Clear();
        }

        MutableStock(game).Clear();
        MutableCompletedRuns(game).Clear();
    }

    internal static List<List<SpiderCard>> MutableColumns(SpiderSolitaireGame game) =>
        Assert.IsType<List<List<SpiderCard>>>(game.Columns);

    internal static List<SpiderCard> MutableStock(SpiderSolitaireGame game) =>
        Assert.IsType<List<SpiderCard>>(game.Stock);

    internal static List<List<SpiderCard>> MutableCompletedRuns(SpiderSolitaireGame game) =>
        Assert.IsType<List<List<SpiderCard>>>(game.CompletedRuns);

    internal static SpiderCard Card(
        int id,
        int rank,
        SpiderCardSuit suit = SpiderCardSuit.Spades,
        bool faceUp = true) =>
        new(new SpiderCardDefinition(id, suit, rank), faceUp);

    internal static List<SpiderCard> Run(
        int firstId,
        SpiderCardSuit suit = SpiderCardSuit.Spades) =>
        Enumerable.Range(0, 13)
            .Select(offset => Card(firstId + offset, 13 - offset, suit))
            .ToList();
}
