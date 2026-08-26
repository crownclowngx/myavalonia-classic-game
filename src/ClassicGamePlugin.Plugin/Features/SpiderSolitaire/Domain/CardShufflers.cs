namespace ClassicGamePlugin.Features.SpiderSolitaire.Domain;

/// <summary>
/// 只抽象蜘蛛纸牌真正需要替换的洗牌能力。生产代码使用随机策略，测试可以注入固定牌序；
/// 游戏引擎仍会校验策略结果，不能因为依赖被注入就信任外部返回值。
/// </summary>
internal interface ISpiderCardShuffler
{
    IReadOnlyList<SpiderCardDefinition> Shuffle(IReadOnlyList<SpiderCardDefinition> cards);
}

/// <summary>使用 Fisher–Yates 算法生成无偏随机排列。</summary>
internal sealed class RandomSpiderCardShuffler : ISpiderCardShuffler
{
    public IReadOnlyList<SpiderCardDefinition> Shuffle(IReadOnlyList<SpiderCardDefinition> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var result = cards.ToArray();
        for (var index = result.Length - 1; index > 0; index--)
        {
            var other = Random.Shared.Next(index + 1);
            (result[index], result[other]) = (result[other], result[index]);
        }

        return result;
    }
}
