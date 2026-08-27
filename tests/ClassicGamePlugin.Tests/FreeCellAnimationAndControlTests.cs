using Avalonia;
using ClassicGamePlugin.Features.FreeCell.Domain;
using ClassicGamePlugin.Features.FreeCell.ViewModels;
using ClassicGamePlugin.Features.FreeCell.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class FreeCellAnimationAndControlTests
{
    [Theory]
    [InlineData(0, 0, 4, 4, false)]
    [InlineData(0, 0, 6, 0, true)]
    [InlineData(10, 10, 14.3, 14.3, true)]
    public void 拖拽阈值固定为六个DIP(double x1, double y1, double x2, double y2, bool expected) =>
        Assert.Equal(expected, FreeCellBoardControl.IsDragDistance(new Point(x1, y1), new Point(x2, y2)));

    [Theory]
    [InlineData(24, 165, 0)]
    [InlineData(132, 300, 1)]
    [InlineData(900, 200, null)]
    public void 牌列命中测试使用八列稳定布局(double x, double y, int? expected) =>
        Assert.Equal(expected, FreeCellBoardControl.GetTableauColumnAt(new Point(x, y)));

    [Fact]
    public void CardControl生成详细中文辅助名称()
    {
        Assert.Equal("红桃Q", FreeCellCardControl.GetAccessibleName(
            FreeCellTestData.Card(1, 12, FreeCellSuit.Hearts)));
        Assert.Equal("黑桃A", FreeCellCardControl.GetAccessibleName(
            FreeCellTestData.Card(2, 1, FreeCellSuit.Spades)));
    }

    [Fact]
    public void 动画计划按移动自动收牌和胜利顺序生成()
    {
        var before = FreeCellTestData.Snapshot(state: FreeCellGameState.Running);
        var after = FreeCellTestData.Snapshot(foundations: [13, 13, 13, 13], moveCount: 1, state: FreeCellGameState.Won);
        var transition = new FreeCellTransition(
            FreeCellActionKind.Move,
            before,
            after,
            [1],
            [2, 3]);

        var plan = FreeCellAnimationPlan.Create(transition);

        Assert.Equal(
            [FreeCellAnimationStageKind.Move, FreeCellAnimationStageKind.AutoCollect, FreeCellAnimationStageKind.Win],
            plan.Stages.Select(stage => stage.Kind));
        Assert.Equal(TimeSpan.FromMilliseconds(510), plan.TotalDuration);
    }
}
