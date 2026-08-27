using ClassicGamePlugin.Features.Xiangqi.Domain;
using ClassicGamePlugin.Features.Xiangqi.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class XiangqiViewModelTests
{
    [Fact]
    public void 默认配置为本地双人中等难度玩家执红()
    {
        using var viewModel = CreateViewModel();

        Assert.Same(viewModel.ModeOptions[0], viewModel.SelectedMode);
        Assert.Same(viewModel.DifficultyOptions[1], viewModel.SelectedDifficulty);
        Assert.Same(viewModel.HumanSideOptions[0], viewModel.SelectedHumanSide);
        Assert.Equal(XiangqiSide.Red, viewModel.CurrentSide);
        Assert.False(viewModel.IsBoardFlipped);
    }

    [Fact]
    public void 本地双人选择合法点走棋记谱计时并单手撤销()
    {
        var time = new ManualTimeProvider();
        using var viewModel = CreateViewModel(time);

        viewModel.PlayPosition(new XiangqiPosition(6, 0));
        Assert.Contains(new XiangqiPosition(5, 0), viewModel.LegalTargets);
        viewModel.PlayPosition(new XiangqiPosition(5, 0));
        time.Advance(TimeSpan.FromSeconds(3.8));
        viewModel.RefreshElapsedTime();

        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal(3, viewModel.ElapsedSeconds);
        Assert.Contains("兵一进一", Assert.Single(viewModel.HistoryItems, item => item.IsMove).Text);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.False(viewModel.IsTimerRunning);
    }

    [Fact]
    public async Task 人机模式玩家执黑时电脑先行且棋盘翻转()
    {
        using var viewModel = CreateViewModel();
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.SelectedHumanSide = viewModel.HumanSideOptions[1];

        await viewModel.WaitForComputerAsync();

        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal(XiangqiSide.Black, viewModel.CurrentSide);
        Assert.True(viewModel.IsBoardFlipped);
        Assert.True(viewModel.CanInteract);
        Assert.Contains("电脑（红方）", Assert.Single(viewModel.HistoryItems, item => item.IsMove).Text);
    }

    [Fact]
    public async Task 人机撤销恢复到玩家决策点()
    {
        using var viewModel = CreateViewModel();
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.PlayPosition(new XiangqiPosition(6, 0));
        viewModel.PlayPosition(new XiangqiPosition(5, 0));
        await viewModel.WaitForComputerAsync();
        Assert.Equal(2, viewModel.MoveCount);

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(XiangqiSide.Red, viewModel.CurrentSide);
        Assert.True(viewModel.CanInteract);
    }

    [Fact]
    public async Task 电脑思考期间撤销取消旧结果并只回退玩家一手()
    {
        using var blocking = new BlockingXiangqiMoveStrategy();
        var strategies = Enum.GetValues<XiangqiAiDifficulty>()
            .ToDictionary(difficulty => difficulty, _ => (IXiangqiMoveStrategy)blocking);
        using var viewModel = new XiangqiViewModel(new ManualTimeProvider(), false, strategies);
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.PlayPosition(new XiangqiPosition(6, 0));
        viewModel.PlayPosition(new XiangqiPosition(5, 0));
        Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(2)));
        var oldTask = viewModel.WaitForComputerAsync();

        viewModel.UndoCommand.Execute(null);
        await oldTask;

        Assert.Equal(0, viewModel.MoveCount);
        Assert.False(viewModel.IsComputerThinking);
    }

    [Fact]
    public async Task 提示不修改棋局且认输需要二次确认()
    {
        using var viewModel = CreateViewModel();

        viewModel.HintCommand.Execute(null);
        await viewModel.WaitForHintAsync();

        Assert.Equal(0, viewModel.MoveCount);
        Assert.NotNull(viewModel.HintMove);
        Assert.Empty(viewModel.HistoryItems);

        viewModel.ResignCommand.Execute(null);
        Assert.True(viewModel.IsResignConfirmationPending);
        Assert.NotEqual(XiangqiGameState.Finished, viewModel.GameState);
        viewModel.ResignCommand.Execute(null);

        Assert.Equal(XiangqiGameState.Finished, viewModel.GameState);
        Assert.Contains("认输", viewModel.MessageText);
    }

    private static XiangqiViewModel CreateViewModel(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? new ManualTimeProvider(), false, XiangqiTestFactory.FirstLegalStrategies());
}
