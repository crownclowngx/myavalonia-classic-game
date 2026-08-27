using ClassicGamePlugin.Features.Sudoku.Domain;
using ClassicGamePlugin.Features.Sudoku.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SudokuAnimationPlanTests
{
    [Fact]
    public void 普通填数只包含一百二十毫秒缩放阶段()
    {
        var plan = CreatePlan(new HashSet<SudokuPosition>(), completed: false);

        var stage = Assert.Single(plan.Stages);
        Assert.Equal(SudokuAnimationStageKind.Placement, stage.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(120), plan.TotalDuration);
        Assert.Equal(0.85, plan.GetPlacementScale(TimeSpan.Zero), 6);
        Assert.Equal(1, plan.GetPlacementScale(plan.TotalDuration), 6);
    }

    [Fact]
    public void 冲突填数先缩放再抖动且最终回到原位()
    {
        var conflict = new SudokuPosition(0, 2);
        var plan = CreatePlan(new HashSet<SudokuPosition> { conflict }, completed: false);

        Assert.Equal(2, plan.Stages.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(300), plan.TotalDuration);
        Assert.Equal(0, plan.GetConflictOffset(TimeSpan.FromMilliseconds(120)), 6);
        Assert.Equal(0, plan.GetConflictOffset(plan.TotalDuration), 6);
    }

    [Fact]
    public void 完成阶段按九宫格错峰产生波纹并在总时长结束()
    {
        var plan = CreatePlan(new HashSet<SudokuPosition>(), completed: true);

        Assert.Equal(TimeSpan.FromMilliseconds(570), plan.TotalDuration);
        Assert.True(plan.GetCompletionIntensity(0, TimeSpan.FromMilliseconds(220)) > 0);
        Assert.Equal(0, plan.GetCompletionIntensity(8, TimeSpan.FromMilliseconds(220)));
        Assert.True(plan.IsComplete(plan.TotalDuration));
    }

    private static SudokuAnimationPlan CreatePlan(IReadOnlySet<SudokuPosition> conflicts, bool completed) =>
        new(new SudokuMoveResult(
            SudokuMoveKind.Value,
            new SudokuPosition(0, 2),
            conflicts,
            completed));
}
