using ClassicGamePlugin.Features.Sokoban.Domain;
using ClassicGamePlugin.Features.Sokoban.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SokobanViewModelTests
{
    [Fact]
    public void 默认公开十二关并从第一关零状态开始()
    {
        var viewModel = new SokobanViewModel();

        Assert.Equal(12, viewModel.LevelOptions.Count);
        Assert.Equal(0, viewModel.SelectedLevelIndex);
        Assert.Equal("初次推动", viewModel.LevelName);
        Assert.Equal("入门", viewModel.DifficultyText);
        Assert.False(viewModel.CanGoPrevious);
        Assert.True(viewModel.CanGoNext);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.AnimationsEnabled);
    }

    [Fact]
    public void 切关直接创建新局并清除当前历史()
    {
        var viewModel = new SokobanViewModel { AnimationsEnabled = false };
        viewModel.MoveDownCommand.Execute(null);
        Assert.True(viewModel.IsCompleted);

        viewModel.SelectedLevelIndex = 1;

        Assert.Equal("绕到上方", viewModel.LevelName);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(0, viewModel.PushCount);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanGoPrevious);
    }

    [Fact]
    public void 动画期间只保留最后方向并在完成后执行()
    {
        var viewModel = CreateOpenViewModel();
        var requests = 0;
        viewModel.AnimationRequested += (_, _) => requests++;

        viewModel.Move(SokobanDirection.Right);
        viewModel.Move(SokobanDirection.Down);
        viewModel.Move(SokobanDirection.Left);
        Assert.Equal(SokobanDirection.Left, viewModel.QueuedDirection);

        viewModel.CompleteAnimation();

        Assert.Equal(2, requests);
        Assert.Equal(2, viewModel.MoveCount);
        Assert.Equal(new SokobanPosition(1, 2), viewModel.Game.Player);
        Assert.True(viewModel.IsAnimationRunning);
    }

    [Fact]
    public void 关闭动画落定当前移动并继续缓存方向()
    {
        var viewModel = CreateOpenViewModel();
        viewModel.AnimationRequested += (_, _) => { };
        viewModel.AnimationCancellationRequested += (_, _) => viewModel.CompleteAnimation();
        viewModel.Move(SokobanDirection.Right);
        viewModel.Move(SokobanDirection.Left);

        viewModel.AnimationsEnabled = false;

        Assert.False(viewModel.IsAnimationRunning);
        Assert.Null(viewModel.QueuedDirection);
        Assert.Equal(2, viewModel.MoveCount);
        Assert.Equal(new SokobanPosition(1, 2), viewModel.Game.Player);
    }

    [Fact]
    public void 撤销重开和切关都会取消动画并丢弃缓存输入()
    {
        var viewModel = CreateOpenViewModel();
        var cancellations = 0;
        viewModel.AnimationRequested += (_, _) => { };
        viewModel.AnimationCancellationRequested += (_, _) => cancellations++;
        viewModel.Move(SokobanDirection.Right);
        viewModel.Move(SokobanDirection.Down);

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(1, cancellations);
        Assert.False(viewModel.IsAnimationRunning);
        Assert.Null(viewModel.QueuedDirection);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(new SokobanPosition(1, 2), viewModel.Game.Player);
    }

    [Fact]
    public void 两个ViewModel的关卡历史和动画偏好互不影响()
    {
        var first = new SokobanViewModel { AnimationsEnabled = false };
        var second = new SokobanViewModel();

        first.MoveDownCommand.Execute(null);
        first.SelectedLevelIndex = 5;

        Assert.Equal(5, first.SelectedLevelIndex);
        Assert.False(first.AnimationsEnabled);
        Assert.Equal(0, second.SelectedLevelIndex);
        Assert.True(second.AnimationsEnabled);
        Assert.Equal(0, second.MoveCount);
    }

    [Fact]
    public void 格子和棋盘提供中文辅助说明()
    {
        var viewModel = new SokobanViewModel();

        Assert.Contains("推箱子第 1 关", viewModel.AccessibleBoardText, StringComparison.Ordinal);
        Assert.Contains("玩家", viewModel.GetCellAccessibleText(1, 2), StringComparison.Ordinal);
        Assert.Contains("箱子", viewModel.GetCellAccessibleText(2, 2), StringComparison.Ordinal);
        Assert.Contains("目标点", viewModel.GetCellAccessibleText(3, 2), StringComparison.Ordinal);
        Assert.Contains("墙", viewModel.GetCellAccessibleText(0, 0), StringComparison.Ordinal);
    }

    private static SokobanViewModel CreateOpenViewModel()
    {
        var level = SokobanLevelParser.Parse(
            "open", "开放", SokobanDifficulty.Beginner,
            "#######", "# @   #", "# $ . #", "#     #", "#######");
        return new SokobanViewModel([level]);
    }
}
