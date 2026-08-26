using ClassicGamePlugin.Features.Gomoku.Domain;
using ClassicGamePlugin.Features.Gomoku.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GomokuViewModelTests
{
    [Fact]
    public void 默认配置为自由规则本地双人中等难度和玩家执黑()
    {
        using var viewModel = CreateViewModel();

        Assert.Same(viewModel.RuleOptions[0], viewModel.SelectedRule);
        Assert.Same(viewModel.ModeOptions[0], viewModel.SelectedMode);
        Assert.Same(viewModel.DifficultyOptions[1], viewModel.SelectedDifficulty);
        Assert.Same(viewModel.HumanColorOptions[0], viewModel.SelectedHumanColor);
        Assert.Equal(GomokuStone.Black, viewModel.CurrentPlayer);
    }

    [Fact]
    public void 本地双人落子提示计时记录和单手撤销保持一致()
    {
        var time = new ManualTimeProvider();
        using var viewModel = CreateViewModel(time);

        viewModel.HintCommand.Execute(null);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Empty(viewModel.HistoryItems);
        Assert.NotNull(viewModel.HintPosition);

        viewModel.PlayPosition(new GomokuPosition(0, 0));
        time.Advance(TimeSpan.FromSeconds(3.8));
        viewModel.RefreshElapsedTime();

        Assert.Equal(1, viewModel.BlackCount);
        Assert.Equal(3, viewModel.ElapsedSeconds);
        Assert.True(viewModel.IsTimerRunning);
        Assert.Contains("A1", Assert.Single(viewModel.HistoryItems).Text);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.False(viewModel.IsTimerRunning);
        Assert.False(viewModel.IsRewinding);
    }

    [Fact]
    public async Task 人机模式自动完成电脑回合且玩家执白时电脑先行()
    {
        using var black = CreateViewModel();
        black.SelectedMode = black.ModeOptions[1];
        black.PlayPosition(new GomokuPosition(7, 7));
        await black.WaitForComputerAsync();

        Assert.Equal(2, black.MoveCount);
        Assert.Equal(GomokuStone.Black, black.CurrentPlayer);
        Assert.Contains(black.HistoryItems, item => item.Text.Contains("电脑（白方）", StringComparison.Ordinal));

        using var white = CreateViewModel();
        white.SelectedMode = white.ModeOptions[1];
        white.SelectedHumanColor = white.HumanColorOptions[1];
        await white.WaitForComputerAsync();

        Assert.Equal(1, white.MoveCount);
        Assert.Equal(GomokuStone.White, white.CurrentPlayer);
        Assert.True(white.CanInteract);
        Assert.Contains("电脑（黑方）", Assert.Single(white.HistoryItems).Text);
    }

    [Fact]
    public async Task 人机严格单步回退暂停计时并可连续撤销后继续()
    {
        var time = new ManualTimeProvider();
        using var viewModel = CreateViewModel(time);
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.PlayPosition(new GomokuPosition(7, 7));
        await viewModel.WaitForComputerAsync();
        time.Advance(TimeSpan.FromSeconds(2));

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(1, viewModel.MoveCount);
        Assert.True(viewModel.IsRewinding);
        Assert.False(viewModel.IsTimerRunning);
        Assert.False(viewModel.CanInteract);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(GomokuStone.Black, viewModel.CurrentPlayer);

        viewModel.ContinueCommand.Execute(null);
        Assert.False(viewModel.IsRewinding);
        Assert.True(viewModel.CanInteract);
        Assert.False(viewModel.IsTimerRunning);
        Assert.Contains("继续对局", viewModel.HistoryItems[^1].Text);
    }

    [Fact]
    public async Task 电脑思考期间撤销取消旧结果并只回退玩家一手()
    {
        using var blocking = new BlockingGomokuMoveStrategy();
        var strategies = Enum.GetValues<GomokuAiDifficulty>()
            .ToDictionary(difficulty => difficulty, _ => (IGomokuMoveStrategy)blocking);
        using var viewModel = new GomokuViewModel(new ManualTimeProvider(), false, strategies);
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.PlayPosition(new GomokuPosition(7, 7));
        Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(2)));
        var oldTask = viewModel.WaitForComputerAsync();

        viewModel.UndoCommand.Execute(null);
        await oldTask;

        Assert.Equal(0, viewModel.MoveCount);
        Assert.True(viewModel.IsRewinding);
        Assert.False(viewModel.IsComputerThinking);
    }

    [Fact]
    public void 切换规则模式难度或颜色均建立全新棋局并清空辅助状态()
    {
        using var viewModel = CreateViewModel();
        viewModel.PlayPosition(new GomokuPosition(2, 2));
        viewModel.HintCommand.Execute(null);

        viewModel.SelectedRule = viewModel.RuleOptions[1];

        Assert.Equal(GomokuGameState.Ready, viewModel.GameState);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Empty(viewModel.HistoryItems);
        Assert.Null(viewModel.HintPosition);
        Assert.Contains("禁手规则", viewModel.MessageText);
    }

    private static GomokuViewModel CreateViewModel(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? new ManualTimeProvider(), false, GomokuTestStrategies.CreateFirstLegal());
}
