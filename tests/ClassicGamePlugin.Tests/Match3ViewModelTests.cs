using ClassicGamePlugin.Features.Match3.Domain;
using ClassicGamePlugin.Features.Match3.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Match3ViewModelTests
{
    [Fact]
    public void 默认展示固定目标步数和已开启动画()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(0, viewModel.Score);
        Assert.Equal(1500, viewModel.TargetScore);
        Assert.Equal(30, viewModel.RemainingMoves);
        Assert.Equal("0 / 1500", viewModel.ScoreText);
        Assert.True(viewModel.AnimationsEnabled);
        Assert.True(viewModel.CanInteract);
    }

    [Fact]
    public void 两次点击相邻格复用交换入口且清除选择()
    {
        var viewModel = CreateViewModel();
        viewModel.AnimationsEnabled = false;

        Assert.True(viewModel.HandleCellClick(new Match3Position(0, 1)));
        Assert.Equal(new Match3Position(0, 1), viewModel.SelectedPosition);
        Assert.True(viewModel.HandleCellClick(new Match3Position(1, 1)));

        Assert.Null(viewModel.SelectedPosition);
        Assert.Equal(29, viewModel.RemainingMoves);
        Assert.True(viewModel.Score > 0);
    }

    [Fact]
    public void 拖动只接受正交相邻目标()
    {
        var viewModel = CreateViewModel();
        viewModel.AnimationsEnabled = false;

        Assert.False(viewModel.HandleDragSwap(new Match3Position(0, 0), new Match3Position(2, 0)));
        Assert.True(viewModel.HandleDragSwap(new Match3Position(0, 1), new Match3Position(1, 1)));
        Assert.Equal(29, viewModel.RemainingMoves);
    }

    [Fact]
    public void 提示高亮稳定合法交换并在下一次输入时清除()
    {
        var viewModel = CreateViewModel();

        viewModel.HintCommand.Execute(null);

        Assert.True(viewModel.IsHinted(new Match3Position(0, 1)));
        Assert.True(viewModel.IsHinted(new Match3Position(1, 1)));

        viewModel.HandleCellClick(new Match3Position(2, 2));
        Assert.False(viewModel.IsHinted(new Match3Position(0, 1)));
    }

    [Fact]
    public void 动画期间锁定输入且关闭开关立即落定()
    {
        var viewModel = CreateViewModel();
        Match3AnimationPlan? requested = null;
        viewModel.AnimationRequested += (_, plan) => requested = plan;

        Assert.True(viewModel.HandleDragSwap(new Match3Position(0, 1), new Match3Position(1, 1)));
        Assert.NotNull(requested);
        Assert.True(viewModel.IsAnimationRunning);
        Assert.False(viewModel.CanInteract);
        Assert.False(viewModel.HandleCellClick(new Match3Position(2, 2)));

        viewModel.AnimationsEnabled = false;
        Assert.False(viewModel.IsAnimationRunning);
        Assert.True(viewModel.CanInteract);
    }

    [Fact]
    public void 无效交换也请求换回动画但不扣步()
    {
        var viewModel = CreateViewModel();
        Match3AnimationPlan? requested = null;
        viewModel.AnimationRequested += (_, plan) => requested = plan;

        Assert.False(viewModel.HandleDragSwap(new Match3Position(7, 6), new Match3Position(7, 7)));

        Assert.NotNull(requested);
        Assert.False(requested.Transition.IsAccepted);
        Assert.Equal(TimeSpan.FromMilliseconds(240), requested.TotalDuration);
        Assert.Equal(30, viewModel.RemainingMoves);
    }

    private static Match3ViewModel CreateViewModel() =>
        new(new Match3Game(new CyclingMatch3Random(), Match3Boards.Stable()));
}
