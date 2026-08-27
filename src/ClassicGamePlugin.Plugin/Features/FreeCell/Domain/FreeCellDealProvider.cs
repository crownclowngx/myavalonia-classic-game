namespace ClassicGamePlugin.Features.FreeCell.Domain;

/// <summary>向页面提供经过完整解路径证明的、可复现的编号牌局。</summary>
internal interface IFreeCellDealProvider
{
    Task<FreeCellDeal> CreateSolvableDealAsync(int number, CancellationToken cancellationToken);
}

/// <summary>
/// 同一编号按固定候选序列生成牌组，并只返回求解器在固定节点预算内实际找到胜利路径的候选。
/// 固定 PRNG、固定节点预算和固定尝试上限共同保证结果不依赖机器速度与 .NET 的 Random 实现。
/// </summary>
internal sealed class FreeCellDealProvider(IFreeCellSolver solver) : IFreeCellDealProvider
{
    internal const int NodeLimitPerCandidate = 300_000;
    internal const int MaximumCandidateCount = 32;
    private readonly IFreeCellSolver _solver = solver ?? throw new ArgumentNullException(nameof(solver));

    internal FreeCellDealProvider()
        : this(new FreeCellSolver())
    {
    }

    public Task<FreeCellDeal> CreateSolvableDealAsync(int number, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return Task.Run(() => Create(number, cancellationToken), cancellationToken);
    }

    internal static FreeCellDeal CreateCandidate(int number, int candidateIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfNegative(candidateIndex);
        var deck = new List<FreeCellCard>(52);
        var id = 0;
        foreach (var suit in Enum.GetValues<FreeCellSuit>())
        {
            for (var rank = 1; rank <= 13; rank++)
            {
                deck.Add(new FreeCellCard(id++, suit, rank));
            }
        }

        var random = new StableRandom(CombineSeed(number, candidateIndex));
        for (var index = deck.Count - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (deck[index], deck[swap]) = (deck[swap], deck[index]);
        }

        return new FreeCellDeal(number, candidateIndex, deck.AsReadOnly());
    }

    private FreeCellDeal Create(int number, CancellationToken cancellationToken)
    {
        for (var candidate = 0; candidate < MaximumCandidateCount; candidate++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deal = CreateCandidate(number, candidate);
            var snapshot = FreeCellRules.CreateInitialSnapshot(deal, autoCollect: true);
            var result = _solver.Solve(snapshot, NodeLimitPerCandidate, cancellationToken);
            if (result.Status == FreeCellSolveStatus.Solved)
            {
                return deal;
            }
        }

        throw new InvalidOperationException(
            $"牌局 {number} 的 {MaximumCandidateCount} 个确定性候选均未能在节点预算内证明可解。");
    }

    private static ulong CombineSeed(int number, int candidateIndex) =>
        ((ulong)(uint)number << 32) | (uint)candidateIndex;

    /// <summary>SplitMix64 只承担稳定洗牌，不用于安全或统计随机。</summary>
    private sealed class StableRandom(ulong seed)
    {
        private ulong _state = seed;

        internal int Next(int exclusiveUpperBound)
        {
            _state += 0x9E3779B97F4A7C15UL;
            var value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (int)(value % (uint)exclusiveUpperBound);
        }
    }
}
