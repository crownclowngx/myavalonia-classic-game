using Avalonia;
using ClassicGamePlugin.Features.Go.Domain;
using ClassicGamePlugin.Features.Go.ViewModels;
using ClassicGamePlugin.Features.Go.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GoAnimationAndControlTests
{
    [Fact]
    public void 普通落子动画固定一百四十毫秒并提供受限淡入缩放曲线()
    {
        var game = new GoGame();
        var move = Assert.IsType<GoMoveResult>(game.PlaceStone(new GoPosition(3, 3)));
        var plan = new GoAnimationPlan(move);

        Assert.Equal(TimeSpan.FromMilliseconds(140), plan.TotalDuration);
        Assert.Equal(0.25, plan.GetPlacementScale(TimeSpan.Zero), 6);
        Assert.InRange(plan.GetPlacementScale(TimeSpan.FromMilliseconds(70)), 0.9, 1);
        Assert.Equal(1, plan.GetPlacementScale(TimeSpan.FromMilliseconds(140)), 6);
        Assert.Equal(0, plan.GetPlacementOpacity(TimeSpan.FromMilliseconds(-1)));
        Assert.Equal(1, plan.GetPlacementOpacity(TimeSpan.FromSeconds(1)));
        Assert.False(plan.IsComplete(TimeSpan.FromMilliseconds(139)));
        Assert.True(plan.IsComplete(TimeSpan.FromMilliseconds(140)));
    }

    [Fact]
    public void 提子动画在落子后增加一百八十毫秒缩小淡出阶段()
    {
        var snapshot = GoRulesTests.Snapshot(
            GoRulesTests.Board(
                (new GoPosition(0, 0), GoStone.White),
                (new GoPosition(0, 1), GoStone.Black)),
            GoStone.Black);
        var game = new GoGame(snapshot);
        var move = Assert.IsType<GoMoveResult>(game.PlaceStone(new GoPosition(1, 0)));
        var plan = new GoAnimationPlan(move);

        Assert.Equal(TimeSpan.FromMilliseconds(320), plan.TotalDuration);
        Assert.Equal(1, plan.GetCaptureScale(TimeSpan.FromMilliseconds(140)), 6);
        Assert.Equal(0.675, plan.GetCaptureScale(TimeSpan.FromMilliseconds(230)), 6);
        Assert.Equal(0.35, plan.GetCaptureScale(TimeSpan.FromMilliseconds(320)), 6);
        Assert.Equal(0.5, plan.GetCaptureOpacity(TimeSpan.FromMilliseconds(230)), 6);
    }

    [Fact]
    public void 棋盘命中使用十九路交叉点并拒绝远离网格的区域()
    {
        Assert.True(GoBoardControl.TryHitTest(new Size(650, 650), new Point(34, 34), out var topLeft));
        Assert.Equal(new GoPosition(0, 0), topLeft);

        Assert.True(GoBoardControl.TryHitTest(new Size(650, 650), new Point(616, 616), out var bottomRight));
        Assert.Equal(new GoPosition(18, 18), bottomRight);

        Assert.False(GoBoardControl.TryHitTest(new Size(650, 650), new Point(2, 2), out _));
    }

    [Fact]
    public void 棋盘列坐标按围棋惯例跳过字母I()
    {
        Assert.Equal("H", GoBoardControl.GetColumnLabel(7));
        Assert.Equal("J", GoBoardControl.GetColumnLabel(8));
        Assert.Equal("T", GoBoardControl.GetColumnLabel(18));
        Assert.Throws<ArgumentOutOfRangeException>(() => GoBoardControl.GetColumnLabel(19));
    }
}
