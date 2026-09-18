namespace ClassicGamePlugin.Features.RichTown.Domain;

/// <summary>SplitMix64 v1 的完整可恢复状态。State 是下一次递增前的 64 位状态，不是最初种子。</summary>
internal readonly record struct RichTownRandomState(int Version, ulong State);

/// <summary>
/// 无外部效果的 SplitMix64 纯函数，算法来源为 Sebastiano Vigna 的公开实现：
/// https://prng.di.unimi.it/splitmix64.c 。原始许可保存在本子领域 Random-LICENSE.txt。
/// 固定 unchecked 溢出是模 2^64 算法的一部分，不能受项目 checked 编译配置影响；不可用于密码学。
/// </summary>
internal static class RichTownRandom
{
    public const int AlgorithmVersion = 1;

    public static (RichTownRandomState State, ulong Value) NextUInt64(RichTownRandomState state)
    {
        if (state.Version != AlgorithmVersion) throw new ArgumentException("不支持的随机算法版本。", nameof(state));
        unchecked
        {
            var next = state.State + 0x9e3779b97f4a7c15UL;
            var value = (next ^ (next >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            return (state with { State = next }, value ^ (value >> 31));
        }
    }

    /// <summary>
    /// 返回 [0, exclusiveMaximum) 的等概率整数。丢弃不足一个完整余数周期的低端样本，避免直接取模偏差。
    /// 每次重试也推进内部状态，因此保存返回的 State 才能恢复完整序列；非法上界不消耗状态。
    /// </summary>
    public static (RichTownRandomState State, int Value) Next(RichTownRandomState state, int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        var bound = (ulong)exclusiveMaximum;
        var threshold = unchecked(0UL - bound) % bound;
        while (true)
        {
            var sample = NextUInt64(state);
            state = sample.State;
            if (sample.Value >= threshold) return (state, (int)(sample.Value % bound));
        }
    }
}
