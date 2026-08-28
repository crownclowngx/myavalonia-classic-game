using Avalonia;
using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using ClassicGamePlugin.Features.ChineseCheckers.ViewModels;
using ClassicGamePlugin.Features.ChineseCheckers.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ChineseCheckersAnimationAndControlTests
{
    [Fact]
    public void 单段移动为一百二十毫秒并追加一百六十毫秒到达脉冲()
    {
        var snapshot = ChineseCheckersRules.CreateInitialSnapshot();
        var move = ChineseCheckersRules.GetLegalMoves(snapshot)[0];
        var result = Assert.IsType<ChineseCheckersMoveResult>(
            ChineseCheckersRules.TryApplyMove(snapshot, move.From, move.To));
        var plan = new ChineseCheckersAnimationPlan(result);

        Assert.Equal(TimeSpan.FromMilliseconds((move.Path.Count - 1) * 120), plan.MovementDuration);
        Assert.Equal(plan.MovementDuration + TimeSpan.FromMilliseconds(160), plan.TotalDuration);
        Assert.Equal(0, plan.GetMovementFrame(TimeSpan.Zero).Progress);
        Assert.InRange(plan.GetArrivalScale(plan.MovementDuration + TimeSpan.FromMilliseconds(80)), 1.11, 1.13);
        Assert.True(plan.IsComplete(plan.TotalDuration));
    }

    [Fact]
    public void 棋盘正反命中都返回相同领域坐标并拒绝外部边界()
    {
        var size = new Size(680, 610);
        var position = ChineseCheckersRules.BlueHome.First();
        var normal = ChineseCheckersBoardControl.GetCenter(size, position, rotated: false);
        var rotated = ChineseCheckersBoardControl.GetCenter(size, position, rotated: true);

        Assert.True(ChineseCheckersBoardControl.TryHitTest(size, normal, false, out var normalHit));
        Assert.True(ChineseCheckersBoardControl.TryHitTest(size, rotated, true, out var rotatedHit));
        Assert.Equal(position, normalHit);
        Assert.Equal(position, rotatedHit);
        Assert.False(ChineseCheckersBoardControl.TryHitTest(size, new Point(2, 2), false, out _));
    }
}
