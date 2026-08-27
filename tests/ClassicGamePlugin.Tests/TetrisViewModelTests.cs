using ClassicGamePlugin.Features.Tetris.Domain;
using ClassicGamePlugin.Features.Tetris.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class TetrisViewModelTests
{
    [Fact]
    public void 默认立即开始且动画开启并提供五个预览()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.AnimationsEnabled);
        Assert.True(viewModel.CanPlay);
        Assert.Equal(1, viewModel.Level);
        Assert.Equal(5, viewModel.Game.NextPieces.Count);
    }

    [Fact]
    public void 硬降先提交领域再请求动画且完成后恢复输入()
    {
        var viewModel = CreateViewModel();
        TetrisAnimationPlan? requested = null;
        viewModel.AnimationRequested += (_, plan) => requested = plan;
        var first = viewModel.Game.ActivePiece.Type;

        viewModel.HardDrop();

        Assert.NotNull(requested);
        Assert.True(viewModel.IsAnimationRunning);
        Assert.False(viewModel.CanPlay);
        Assert.NotEqual(first, viewModel.Game.ActivePiece.Type);

        viewModel.CompleteAnimation();
        Assert.True(viewModel.CanPlay);
    }

    [Fact]
    public void 播放中关闭动画立即落定且生命周期暂停不会遗留动画()
    {
        var viewModel = CreateViewModel();
        var cancellations = 0;
        viewModel.AnimationRequested += (_, _) => { };
        viewModel.AnimationCancellationRequested += (_, _) => cancellations++;
        viewModel.HardDrop();

        viewModel.AnimationsEnabled = false;

        Assert.Equal(1, cancellations);
        Assert.False(viewModel.IsAnimationRunning);
        viewModel.PauseForLifecycle();
        Assert.True(viewModel.IsPaused);
    }

    [Fact]
    public void 重开清除分数暂停和动画但保留动画偏好()
    {
        var viewModel = CreateViewModel();
        viewModel.AnimationsEnabled = false;
        viewModel.HardDrop();
        Assert.True(viewModel.Score > 0);
        viewModel.TogglePause();

        viewModel.Restart();

        Assert.Equal(0, viewModel.Score);
        Assert.False(viewModel.IsPaused);
        Assert.False(viewModel.AnimationsEnabled);
    }

    private static TetrisViewModel CreateViewModel() =>
        new(new SequenceTetrominoSource());
}

