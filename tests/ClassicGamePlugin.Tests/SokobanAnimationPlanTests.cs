using ClassicGamePlugin.Features.Sokoban.Domain;
using ClassicGamePlugin.Features.Sokoban.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SokobanAnimationPlanTests
{
    [Fact]
    public void 普通移动使用一百二十毫秒三次减速()
    {
        var game = SokobanGameTests.CreateGame();
        var plan = new SokobanAnimationPlan(Assert.IsType<SokobanMoveResult>(game.Move(SokobanDirection.Right)));

        Assert.Equal(TimeSpan.FromMilliseconds(120), plan.TotalDuration);
        Assert.Equal(0, plan.GetMoveProgress(TimeSpan.FromMilliseconds(-1)));
        Assert.Equal(0.875, plan.GetMoveProgress(TimeSpan.FromMilliseconds(60)), 6);
        Assert.Equal(1, plan.GetMoveProgress(TimeSpan.FromMilliseconds(120)));
        Assert.False(plan.IsComplete(TimeSpan.FromMilliseconds(119)));
        Assert.True(plan.IsComplete(TimeSpan.FromMilliseconds(120)));
    }

    [Fact]
    public void 完成移动追加三百六十毫秒脉冲反馈()
    {
        var game = new SokobanGame(SokobanLevelCatalog.Levels[0]);
        var plan = new SokobanAnimationPlan(Assert.IsType<SokobanMoveResult>(game.Move(SokobanDirection.Down)));

        Assert.Equal(TimeSpan.FromMilliseconds(480), plan.TotalDuration);
        Assert.Equal(0, plan.GetCompletionPulse(TimeSpan.FromMilliseconds(120)), 6);
        Assert.Equal(1, plan.GetCompletionPulse(TimeSpan.FromMilliseconds(300)), 6);
        Assert.Equal(0, plan.GetCompletionPulse(TimeSpan.FromMilliseconds(480)), 6);
    }
}
