using Avalonia;
using ClassicGamePlugin.Features.Gomoku.Domain;
using ClassicGamePlugin.Features.Gomoku.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GomokuBoardControlTests
{
    [Fact]
    public void 棋盘中心和四角可靠换算为交叉点()
    {
        var size = new Size(570, 570);

        Assert.True(GomokuBoardControl.TryHitTest(size, new Point(285, 285), out var center));
        Assert.Equal(new GomokuPosition(7, 7), center);
        Assert.True(GomokuBoardControl.TryHitTest(size, new Point(30, 30), out var topLeft));
        Assert.Equal(new GomokuPosition(0, 0), topLeft);
        Assert.True(GomokuBoardControl.TryHitTest(size, new Point(540, 540), out var bottomRight));
        Assert.Equal(new GomokuPosition(14, 14), bottomRight);
    }

    [Fact]
    public void 网格外和远离交叉点的位置被拒绝()
    {
        var size = new Size(570, 570);

        Assert.False(GomokuBoardControl.TryHitTest(size, new Point(5, 5), out _));
        Assert.False(GomokuBoardControl.TryHitTest(size, new Point(48, 48), out _));
    }
}
