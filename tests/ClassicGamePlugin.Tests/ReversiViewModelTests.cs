using ClassicGamePlugin.Features.Reversi.Domain;
using ClassicGamePlugin.Features.Reversi.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ReversiViewModelTests
{
    [Fact]
    public void 本地双人落子刷新棋盘计数回合计时和记录()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(timeProvider);

        viewModel.PlayCell(FindCell(viewModel, 2, 3));
        timeProvider.Advance(TimeSpan.FromSeconds(3.8));
        viewModel.RefreshElapsedTime();

        Assert.Equal(4, viewModel.BlackCount);
        Assert.Equal(1, viewModel.WhiteCount);
        Assert.Equal(ReversiDiscColor.White, viewModel.CurrentPlayer);
        Assert.Equal(3, viewModel.ElapsedSeconds);
        Assert.True(viewModel.IsTimerRunning);
        Assert.Contains("D3", Assert.Single(viewModel.HistoryItems).Text);
        Assert.True(FindCell(viewModel, 2, 2).IsLegalMove);
    }

    [Fact]
    public void 提示只标记稳定建议位置而不修改棋局()
    {
        using var viewModel = CreateViewModel();

        viewModel.HintCommand.Execute(null);

        Assert.Equal(0, viewModel.MoveCount);
        Assert.Single(viewModel.BoardCells, cell => cell.IsHint);
        Assert.Contains("提示：建议", viewModel.MessageText);
        Assert.Empty(viewModel.HistoryItems);
    }

    [Fact]
    public void 本地双人每次只撤销一手()
    {
        using var viewModel = CreateViewModel();
        viewModel.PlayCell(FindCell(viewModel, 2, 3));
        viewModel.PlayCell(FindCell(viewModel, 2, 2));

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal(ReversiDiscColor.White, viewModel.CurrentPlayer);
        Assert.Contains("回退上一手", viewModel.HistoryItems[^1].Text);
    }

    [Fact]
    public async Task 人机模式在玩家落子后自动完成电脑回合()
    {
        using var viewModel = CreateViewModel();
        viewModel.SelectedMode = viewModel.ModeOptions[1];

        viewModel.PlayCell(FindCell(viewModel, 2, 3));
        await viewModel.WaitForComputerAsync();

        Assert.Equal(2, viewModel.MoveCount);
        Assert.Equal(ReversiDiscColor.Black, viewModel.CurrentPlayer);
        Assert.False(viewModel.IsComputerThinking);
        Assert.Contains(viewModel.HistoryItems, item => item.Text.Contains("电脑（白方）", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 玩家选择白方时电脑自动完成黑方开局()
    {
        using var viewModel = CreateViewModel();
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.SelectedHumanColor = viewModel.HumanColorOptions[1];

        await viewModel.WaitForComputerAsync();

        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal(ReversiDiscColor.White, viewModel.CurrentPlayer);
        Assert.True(viewModel.CanInteract);
        Assert.False(viewModel.CanUndo);
        Assert.Contains("电脑（黑方）", Assert.Single(viewModel.HistoryItems).Text);
    }

    [Fact]
    public async Task 人机撤销回退完整一轮并返回玩家决策点()
    {
        using var viewModel = CreateViewModel();
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.PlayCell(FindCell(viewModel, 2, 3));
        await viewModel.WaitForComputerAsync();

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(ReversiDiscColor.Black, viewModel.CurrentPlayer);
        Assert.Contains("回退 2 手", viewModel.HistoryItems[^1].Text);
    }

    [Fact]
    public async Task 电脑计算期间重新开始会取消旧结果并阻止陈旧提交()
    {
        using var blocking = new BlockingReversiMoveStrategy();
        var strategies = Enum.GetValues<ReversiAiDifficulty>()
            .ToDictionary(difficulty => difficulty, _ => (IReversiMoveStrategy)blocking);
        using var viewModel = new ReversiViewModel(
            new ManualTimeProvider(),
            enableDisplayRefreshTimer: false,
            strategies);
        viewModel.SelectedMode = viewModel.ModeOptions[1];
        viewModel.PlayCell(FindCell(viewModel, 2, 3));
        Assert.True(blocking.Started.Wait(TimeSpan.FromSeconds(2)));
        var oldTask = viewModel.WaitForComputerAsync();

        viewModel.RestartCommand.Execute(null);
        await oldTask;

        Assert.Equal(ReversiGameState.Ready, viewModel.GameState);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.False(viewModel.IsComputerThinking);
        Assert.Empty(viewModel.HistoryItems);
    }

    [Fact]
    public void 完成整局后停止计时并写入终局记录()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(timeProvider);
        var safety = 0;
        while (viewModel.GameState != ReversiGameState.Finished && safety++ < 64)
        {
            var move = Assert.Single(viewModel.BoardCells.Where(cell => cell.IsPlayable).Take(1));
            viewModel.PlayCell(move);
            timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(ReversiGameState.Finished, viewModel.GameState);
        Assert.False(viewModel.IsTimerRunning);
        Assert.Contains(viewModel.HistoryItems, item => item.Text.StartsWith("自动跳过", StringComparison.Ordinal));
        Assert.Contains(viewModel.HistoryItems, item => item.Text.StartsWith("对局结束", StringComparison.Ordinal));
    }

    [Fact]
    public void 切换设置建立全新独立棋局并清空辅助状态()
    {
        using var first = CreateViewModel();
        using var second = CreateViewModel();
        first.PlayCell(FindCell(first, 2, 3));
        first.HintCommand.Execute(null);

        first.SelectedDifficulty = first.DifficultyOptions[2];

        Assert.Equal(ReversiGameState.Ready, first.GameState);
        Assert.Equal(0, first.MoveCount);
        Assert.Empty(first.HistoryItems);
        Assert.DoesNotContain(first.BoardCells, cell => cell.IsHint);
        Assert.Equal(0, second.MoveCount);
        Assert.Equal(2, second.BlackCount);
    }

    private static ReversiViewModel CreateViewModel(TimeProvider? timeProvider = null) =>
        new(
            timeProvider ?? new ManualTimeProvider(),
            enableDisplayRefreshTimer: false,
            ReversiTestStrategies.CreateFirstLegal());

    private static ReversiCellViewModel FindCell(
        ReversiViewModel viewModel,
        int row,
        int column) =>
        Assert.Single(viewModel.BoardCells, cell => cell.Row == row && cell.Column == column);
}
