using ClassicGamePlugin.Features.RichTown.Domain;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownRandomTests
{
    [Fact]
    public void SplitMix64与公开参考实现的固定向量一致()
    {
        ulong[] expected = [0xe220a8397b1dcdaf, 0x6e789e6aa1b965f4, 0x06c45d188009454f, 0xf88bb8a8724c81ec, 0x1b39896a51a8749b, 0x53cb9f0c747ea2ea];
        var state = new RichTownRandomState(1, 0);
        foreach (var value in expected)
        {
            var sample = RichTownRandom.NextUInt64(state);
            Assert.Equal(value, sample.Value);
            state = sample.State;
        }
        Assert.Equal(0xb54cda58fbbee87eUL, state.State);
    }

    [Theory]
    [InlineData(19UL, 0)]
    [InlineData(0UL, 1)]
    [InlineData(5UL, 2)]
    [InlineData(3UL, 3)]
    [InlineData(2UL, 4)]
    [InlineData(1UL, 5)]
    public void 六面骰有界取样使用固定向量(ulong seed, int expected) =>
        Assert.Equal(expected, RichTownRandom.Next(new(1, seed), 6).Value);

    [Fact]
    public void 拒绝低端余数样本并保存重试后的内部状态()
    {
        // 此状态第一次输出恰为零；2^64 mod 6 = 4，因此必须丢弃并再取一个样本。
        var sample = RichTownRandom.Next(new(1, 0x61c8864680b583ebUL), 6);
        Assert.Equal(1, sample.Value);
        Assert.Equal(0x9e3779b97f4a7c15UL, sample.State.State);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void 上下界合法且导出恢复后续序完全一致(int bound)
    {
        var current = new RichTownRandomState(1, ulong.MaxValue);
        for (var i = 0; i < 127; i++) current = RichTownRandom.Next(current, bound).State;
        var restored = new RichTownRandomState(current.Version, current.State);
        for (var i = 0; i < 1000; i++)
        {
            var left = RichTownRandom.Next(current, bound);
            var right = RichTownRandom.Next(restored, bound);
            Assert.Equal(left, right);
            Assert.InRange(left.Value, 0, bound - 1);
            current = left.State;
            restored = right.State;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 拒绝非正上界(int bound) => Assert.Throws<ArgumentOutOfRangeException>(() => RichTownRandom.Next(new(1, 0), bound));

    [Fact]
    public void 未知算法版本拒绝() => Assert.Throws<ArgumentException>(() => RichTownRandom.NextUInt64(new(2, 0)));
}
