using ClassicGamePlugin.Features.Game2048.Domain;
using ClassicGamePlugin.Features.Game2048.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Game2048AnimationPlanTests
{
    [Fact]
    public void 动画计划固定先滑动再反馈且总时长为二百毫秒()
    {
        var plan = CreatePlan();

        Assert.Collection(
            plan.Stages,
            slide =>
            {
                Assert.Equal(Game2048AnimationStageKind.Slide, slide.Kind);
                Assert.Equal(TimeSpan.FromMilliseconds(110), slide.Duration);
            },
            feedback =>
            {
                Assert.Equal(Game2048AnimationStageKind.Feedback, feedback.Kind);
                Assert.Equal(TimeSpan.FromMilliseconds(90), feedback.Duration);
            });
        Assert.Equal(TimeSpan.FromMilliseconds(200), plan.TotalDuration);
    }

    [Fact]
    public void 滑动使用三次减速且进度始终限制在零到一()
    {
        var plan = CreatePlan();

        Assert.Equal(0, plan.GetSlideProgress(TimeSpan.FromMilliseconds(-5)));
        Assert.Equal(0.875, plan.GetSlideProgress(TimeSpan.FromMilliseconds(55)), 6);
        Assert.Equal(1, plan.GetSlideProgress(TimeSpan.FromMilliseconds(110)));
        Assert.Equal(1, plan.GetSlideProgress(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void 反馈阶段同时提供合并弹跳与新生缩放()
    {
        var plan = CreatePlan();

        Assert.Equal(0, plan.GetFeedbackProgress(TimeSpan.FromMilliseconds(110)));
        Assert.Equal(0.5, plan.GetFeedbackProgress(TimeSpan.FromMilliseconds(155)), 6);
        Assert.Equal(1, plan.GetFeedbackProgress(TimeSpan.FromMilliseconds(200)));

        Assert.Equal(1, plan.GetMergeScale(TimeSpan.FromMilliseconds(110)), 6);
        Assert.Equal(1.12, plan.GetMergeScale(TimeSpan.FromMilliseconds(155)), 6);
        Assert.Equal(1, plan.GetMergeScale(TimeSpan.FromMilliseconds(200)), 6);

        Assert.Equal(0, plan.GetSpawnScale(TimeSpan.FromMilliseconds(110)), 6);
        Assert.Equal(0.875, plan.GetSpawnScale(TimeSpan.FromMilliseconds(155)), 6);
        Assert.Equal(1, plan.GetSpawnScale(TimeSpan.FromMilliseconds(200)), 6);
        Assert.False(plan.IsComplete(TimeSpan.FromMilliseconds(199)));
        Assert.True(plan.IsComplete(TimeSpan.FromMilliseconds(200)));
    }

    private static Game2048AnimationPlan CreatePlan()
    {
        var game = new Game2048Game(
            new FirstEmptyTileSpawnStrategy(2),
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine));
        var transition = Assert.IsType<Game2048Transition>(game.Move(Game2048Direction.Left));
        return new Game2048AnimationPlan(transition);
    }
}
