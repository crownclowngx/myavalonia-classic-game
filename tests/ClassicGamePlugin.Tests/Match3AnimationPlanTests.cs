using ClassicGamePlugin.Features.Match3.Domain;
using ClassicGamePlugin.Features.Match3.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Match3AnimationPlanTests
{
    [Fact]
    public void 有效回合按交换消除下落和洗牌顺序计算时长()
    {
        var board = Match3Boards.Stable();
        var step = new Match3ResolutionStep(1, board, [new Match3Position(0, 0)], [], board, 10);
        var transition = new Match3TurnTransition(
            new Match3Position(0, 0),
            new Match3Position(0, 1),
            true,
            board,
            board,
            [step],
            10,
            wasShuffled: true);
        var plan = new Match3AnimationPlan(transition);

        Assert.Equal(TimeSpan.FromMilliseconds(640), plan.TotalDuration);
        Assert.Equal(Match3AnimationPhaseKind.Swap, plan.GetFrame(TimeSpan.Zero).Phase);
        Assert.Equal(Match3AnimationPhaseKind.Clear, plan.GetFrame(TimeSpan.FromMilliseconds(120)).Phase);
        Assert.Equal(Match3AnimationPhaseKind.Fall, plan.GetFrame(TimeSpan.FromMilliseconds(260)).Phase);
        Assert.Equal(Match3AnimationPhaseKind.Shuffle, plan.GetFrame(TimeSpan.FromMilliseconds(420)).Phase);
        Assert.True(plan.IsComplete(TimeSpan.FromMilliseconds(640)));
    }

    [Fact]
    public void 无效交换固定播放交换和换回两段()
    {
        var board = Match3Boards.Stable();
        var transition = new Match3TurnTransition(
            new Match3Position(0, 0), new Match3Position(0, 1), false, board, board);
        var plan = new Match3AnimationPlan(transition);

        Assert.Equal(TimeSpan.FromMilliseconds(240), plan.TotalDuration);
        Assert.Equal(Match3AnimationPhaseKind.Swap, plan.GetFrame(TimeSpan.FromMilliseconds(60)).Phase);
        Assert.Equal(Match3AnimationPhaseKind.SwapBack, plan.GetFrame(TimeSpan.FromMilliseconds(180)).Phase);
        Assert.Equal(0.875, plan.GetFrame(TimeSpan.FromMilliseconds(60)).Progress, 6);
    }
}
