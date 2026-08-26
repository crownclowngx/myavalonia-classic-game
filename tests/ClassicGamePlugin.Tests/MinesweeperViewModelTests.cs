using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class MinesweeperViewModelTests
{
    [Fact]
    public void 首次翻格启动计时而插旗不会启动()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(timeProvider);
        viewModel.ToggleFlag(FindCell(viewModel, 0, 0));

        Assert.False(viewModel.IsTimerRunning);

        viewModel.RevealCell(FindCell(viewModel, 8, 8));
        timeProvider.Advance(TimeSpan.FromSeconds(4.2));
        viewModel.RefreshElapsedTime();

        Assert.True(viewModel.IsTimerRunning);
        Assert.Equal(4, viewModel.ElapsedSeconds);
    }

    [Fact]
    public void 失败后停止计时且耗时不再增长()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(timeProvider);
        viewModel.RevealCell(FindCell(viewModel, 8, 8));
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        viewModel.RevealCell(FindCell(viewModel, 0, 0));
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        viewModel.RefreshElapsedTime();

        Assert.Equal(MinesweeperGameState.Lost, viewModel.GameState);
        Assert.False(viewModel.IsTimerRunning);
        Assert.Equal(2, viewModel.ElapsedSeconds);
    }

    [Fact]
    public void 胜利后停止计时且耗时不再增长()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(timeProvider);
        viewModel.RevealCell(FindCell(viewModel, 8, 8));
        timeProvider.Advance(TimeSpan.FromSeconds(3));

        for (var column = 1; column <= 8; column++)
        {
            viewModel.RevealCell(FindCell(viewModel, 0, column));
        }

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        viewModel.RefreshElapsedTime();

        Assert.Equal(MinesweeperGameState.Won, viewModel.GameState);
        Assert.False(viewModel.IsTimerRunning);
        Assert.Equal(3, viewModel.ElapsedSeconds);
    }

    [Fact]
    public void 切换难度会建立新棋盘并清空计时()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(timeProvider);
        viewModel.RevealCell(FindCell(viewModel, 8, 8));
        timeProvider.Advance(TimeSpan.FromSeconds(3));
        viewModel.RefreshElapsedTime();

        viewModel.SelectedDifficulty = viewModel.DifficultyOptions[1];

        Assert.Equal(MinesweeperGameState.Ready, viewModel.GameState);
        Assert.Equal(16, viewModel.RowCount);
        Assert.Equal(16, viewModel.ColumnCount);
        Assert.Equal(256, viewModel.BoardCells.Count);
        Assert.Equal(0, viewModel.ElapsedSeconds);
        Assert.False(viewModel.IsTimerRunning);
    }

    [Fact]
    public void 重新开始命令重置当前难度和全部局内状态()
    {
        using var viewModel = CreateViewModel();
        viewModel.ToggleFlag(FindCell(viewModel, 0, 0));
        viewModel.RevealCell(FindCell(viewModel, 8, 8));

        viewModel.RestartCommand.Execute(null);

        Assert.Equal(MinesweeperGameState.Ready, viewModel.GameState);
        Assert.Equal(10, viewModel.RemainingMineCount);
        Assert.Equal(0, viewModel.ElapsedSeconds);
    }

    [Fact]
    public void 释放ViewModel会停止仍在运行的计时器()
    {
        var viewModel = CreateViewModel();
        viewModel.RevealCell(FindCell(viewModel, 8, 8));
        Assert.True(viewModel.IsTimerRunning);

        viewModel.Dispose();

        Assert.False(viewModel.IsTimerRunning);
    }

    [Fact]
    public void 两个ViewModel的棋盘和旗帜互不影响()
    {
        using var first = CreateViewModel();
        using var second = CreateViewModel();

        first.ToggleFlag(FindCell(first, 0, 0));

        Assert.Equal(9, first.RemainingMineCount);
        Assert.Equal(10, second.RemainingMineCount);
        Assert.Equal("⚑", FindCell(first, 0, 0).DisplayText);
        Assert.Equal(string.Empty, FindCell(second, 0, 0).DisplayText);
    }

    private static MinesweeperViewModel CreateViewModel(TimeProvider? timeProvider = null) =>
        new(
            MinesweeperDocumentTests.CreateBarrierMinePlacementStrategy(),
            timeProvider ?? new ManualTimeProvider(),
            false);

    private static MinesweeperCellViewModel FindCell(
        MinesweeperViewModel viewModel,
        int row,
        int column) =>
        Assert.Single(viewModel.BoardCells, cell => cell.Row == row && cell.Column == column);
}
