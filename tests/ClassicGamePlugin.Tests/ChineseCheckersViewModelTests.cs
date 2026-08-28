using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using ClassicGamePlugin.Features.ChineseCheckers.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ChineseCheckersViewModelTests
{
    [Fact]
    public void 本地双人选择最终落点后计步计时记录并支持单手撤销()
    {
        var time = new ManualTimeProvider();
        using var viewModel = CreateViewModel(time);
        var move = ChineseCheckersRules.GetLegalMoves(viewModel.CurrentSnapshot)[0];

        viewModel.SelectPosition(move.From);
        viewModel.SelectPosition(move.To);
        time.Advance(TimeSpan.FromSeconds(3.8));
        viewModel.RefreshElapsedTime();

        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal(3, viewModel.ElapsedSeconds);
        Assert.True(viewModel.IsTimerRunning);
        Assert.Single(viewModel.HistoryItems);
        viewModel.UndoCommand.Execute(null);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(ChineseCheckersSide.Blue, viewModel.CurrentSide);
    }

    [Fact]
    public void 提示只保存稳定建议路径而不提交棋局()
    {
        using var viewModel = CreateViewModel(new ManualTimeProvider());

        viewModel.HintCommand.Execute(null);

        Assert.NotNull(viewModel.HintMove);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Contains("提示", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public void 有棋盘订阅时移动进入动画锁并在完成后解除()
    {
        using var viewModel = CreateViewModel(new ManualTimeProvider());
        ChineseCheckersAnimationPlan? published = null;
        viewModel.AnimationRequested += (_, plan) => published = plan;
        var move = ChineseCheckersRules.GetLegalMoves(viewModel.CurrentSnapshot)[0];

        viewModel.SelectPosition(move.From);
        viewModel.SelectPosition(move.To);

        Assert.NotNull(published);
        Assert.True(viewModel.IsAnimationRunning);
        Assert.False(viewModel.CanInteract);
        viewModel.CompleteAnimation();
        Assert.False(viewModel.IsAnimationRunning);
        Assert.True(viewModel.CanInteract);
    }

    [Fact]
    public async Task 玩家执红时电脑蓝方先行且玩家尚未行动不能撤销()
    {
        using var viewModel = CreateViewModel(new ManualTimeProvider());
        viewModel.SelectedHumanColor = viewModel.HumanColorOptions[1];
        viewModel.SelectedMode = viewModel.ModeOptions[1];

        await viewModel.WaitForComputerAsync();

        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal(ChineseCheckersSide.Red, viewModel.CurrentSide);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanInteract);
    }

    [Fact]
    public async Task 模式切换取消旧电脑搜索且旧任务不能写回新棋局()
    {
        using var blocking = new BlockingChineseCheckersMoveStrategy();
        using var viewModel = new ChineseCheckersViewModel(
            new ManualTimeProvider(), false, ChineseCheckersTestData.All(blocking));
        viewModel.SelectedHumanColor = viewModel.HumanColorOptions[1];
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(2)));
        var oldTask = viewModel.WaitForComputerAsync();

        viewModel.SelectedMode = viewModel.ModeOptions[0];
        await oldTask;

        Assert.Equal(0, viewModel.MoveCount);
        Assert.False(viewModel.IsComputerThinking);
        Assert.False(viewModel.IsHumanVsComputer);
    }

    private static ChineseCheckersViewModel CreateViewModel(TimeProvider timeProvider) =>
        new(timeProvider, false, ChineseCheckersTestData.FirstLegalStrategies());
}
