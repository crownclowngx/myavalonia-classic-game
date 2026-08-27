using Avalonia;
using ClassicGamePlugin.Features.Xiangqi.Domain;
using ClassicGamePlugin.Features.Xiangqi.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class XiangqiBoardControlTests
{
    [Fact]
    public void 正向与翻转命中映射到固定领域坐标()
    {
        var bounds = new Size(560, 630);
        var topLeft = new Point(38, 42.75);

        Assert.True(XiangqiBoardControl.TryHitTest(bounds, topLeft, flipped: false, out var normal));
        Assert.Equal(new XiangqiPosition(0, 0), normal);

        Assert.True(XiangqiBoardControl.TryHitTest(bounds, topLeft, flipped: true, out var flipped));
        Assert.Equal(new XiangqiPosition(9, 8), flipped);
    }

    [Fact]
    public void 远离交叉点与棋盘外位置被拒绝()
    {
        var bounds = new Size(560, 630);

        Assert.False(XiangqiBoardControl.TryHitTest(bounds, new Point(68, 73), false, out _));
        Assert.False(XiangqiBoardControl.TryHitTest(bounds, new Point(2, 2), false, out _));
    }
}
