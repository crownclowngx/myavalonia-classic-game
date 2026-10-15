using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class MinesweeperAutoExploreTests
{
    [Fact]
    public void 两个选项默认关闭且开局前开启不会布雷或计时()
    {
        using var viewModel = CreateViewModel();
        Assert.False(viewModel.IsAutoFlagEnabled);
        Assert.False(viewModel.IsAutoExploreEnabled);

        viewModel.IsAutoExploreEnabled = true;

        Assert.False(viewModel.IsAutoFlagEnabled);
        Assert.Equal(MinesweeperGameState.Ready, viewModel.GameState);
        Assert.False(viewModel.IsTimerRunning);
        Assert.Equal(0, viewModel.LastAutoExploreCount);
        Assert.All(viewModel.BoardCells, cell => Assert.Equal(MinesweeperCellState.Covered, cell.CellState));
    }

    [Fact]
    public void 探索关闭时自动插旗不会顺带打开安全格()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsAutoFlagEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        viewModel.RevealCell(Cell(viewModel, 0, 4));

        Assert.Equal(MinesweeperGameState.Running, viewModel.GameState);
        Assert.False(Cell(viewModel, 0, 3).IsRevealed);
        Assert.False(Cell(viewModel, 0, 5).IsRevealed);
        Assert.Equal(0, viewModel.LastAutoExploreCount);
    }

    [Fact]
    public void 插旗探索反复交替并在新数字出现后继续插旗直到获胜()
    {
        var time = new ManualTimeProvider();
        using var viewModel = CreateViewModel(time);
        viewModel.IsAutoFlagEnabled = true;
        viewModel.IsAutoExploreEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        Assert.Equal(9, viewModel.LastAutoFlagCount);
        Assert.Equal(0, viewModel.LastAutoExploreCount);
        time.Advance(TimeSpan.FromSeconds(3));

        viewModel.RevealCell(Cell(viewModel, 0, 4));

        Assert.Equal(MinesweeperGameState.Won, viewModel.GameState);
        Assert.Equal(1, viewModel.LastAutoFlagCount);
        Assert.Equal(7, viewModel.LastAutoExploreCount);
        Assert.Equal(0, viewModel.RemainingMineCount);
        Assert.Equal(MinesweeperCellState.Flagged, Cell(viewModel, 0, 0).CellState);
        Assert.False(viewModel.IsTimerRunning);
        Assert.Equal(3, viewModel.ElapsedSeconds);
        Assert.Equal("对局已结束", viewModel.AutoFlagStatusText);
    }

    [Fact]
    public void 后开启继续探索立即利用已有数字与旗帜()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsAutoFlagEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        viewModel.RevealCell(Cell(viewModel, 0, 4));

        viewModel.IsAutoExploreEnabled = true;

        Assert.Equal(MinesweeperGameState.Won, viewModel.GameState);
        Assert.Equal(1, viewModel.LastAutoFlagCount);
        Assert.Equal(7, viewModel.LastAutoExploreCount);
    }

    [Fact]
    public void 只开继续探索也能使用手动旗帜且不自动补旗()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsAutoExploreEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        for (var column = 0; column < 9; column++)
        {
            viewModel.ToggleFlag(Cell(viewModel, 1, column));
        }

        viewModel.RevealCell(Cell(viewModel, 0, 4));

        Assert.False(viewModel.IsAutoFlagEnabled);
        Assert.Equal(MinesweeperGameState.Won, viewModel.GameState);
        Assert.Equal(0, viewModel.LastAutoFlagCount);
        Assert.Equal(7, viewModel.LastAutoExploreCount);
        Assert.Equal(1, viewModel.RemainingMineCount);
        Assert.Equal(MinesweeperCellState.Covered, Cell(viewModel, 0, 0).CellState);
    }

    [Fact]
    public void 玩家补足旗帜后立即执行与左键数字格相同的探索()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsAutoExploreEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        viewModel.RevealCell(Cell(viewModel, 0, 4));
        viewModel.ToggleFlag(Cell(viewModel, 1, 3));
        viewModel.ToggleFlag(Cell(viewModel, 1, 4));
        Assert.False(Cell(viewModel, 0, 3).IsRevealed);

        viewModel.ToggleFlag(Cell(viewModel, 1, 5));

        Assert.True(Cell(viewModel, 0, 3).IsRevealed);
        Assert.True(Cell(viewModel, 0, 5).IsRevealed);
        Assert.Equal(2, viewModel.LastAutoExploreCount);
        Assert.Equal(0, viewModel.LastAutoFlagCount);
        Assert.Equal(MinesweeperGameState.Running, viewModel.GameState);
    }

    [Fact]
    public void 手动错旗会使自动探索踩雷并停止计时()
    {
        using var viewModel = CreateViewModel();
        viewModel.ToggleFlag(Cell(viewModel, 2, 1));
        viewModel.ToggleFlag(Cell(viewModel, 1, 1));
        viewModel.IsAutoExploreEnabled = true;

        viewModel.RevealCell(Cell(viewModel, 8, 8));

        Assert.Equal(MinesweeperGameState.Lost, viewModel.GameState);
        Assert.Equal("×", Cell(viewModel, 2, 1).DisplayText);
        Assert.False(viewModel.IsTimerRunning);
        var states = viewModel.BoardCells.Select(cell => cell.CellState).ToArray();
        viewModel.IsAutoFlagEnabled = true;
        Assert.Equal(states, viewModel.BoardCells.Select(cell => cell.CellState));
    }

    [Fact]
    public void 无推理进展时停止且再次执行不会改变棋盘()
    {
        var game = new MinesweeperGame(
            new MinesweeperDifficultyDefinition(MinesweeperDifficulty.Beginner, "测试", 5, 5, 3),
            new FixedMinePlacementStrategy(new(0, 0), new(0, 2), new(0, 4)));
        game.Reveal(4, 4);
        var states = game.Cells.Select(cell => cell.State).ToArray();

        Assert.Equal(new MinesweeperAutomationResult(0, 0), game.RunAutomation(true, true));
        Assert.Equal(new MinesweeperAutomationResult(0, 0), game.RunAutomation(true, true));
        Assert.Equal(states, game.Cells.Select(cell => cell.State));
        Assert.Equal(MinesweeperGameState.Running, game.State);
    }

    [Fact]
    public void 重开切难度保留两个独立选项并清零结果且不同窗口隔离()
    {
        using var first = CreateViewModel();
        using var second = CreateViewModel();
        first.IsAutoFlagEnabled = true;
        first.IsAutoExploreEnabled = true;
        first.RevealCell(Cell(first, 8, 8));
        first.RevealCell(Cell(first, 0, 4));
        first.RestartCommand.Execute(null);

        Assert.True(first.IsAutoFlagEnabled);
        Assert.True(first.IsAutoExploreEnabled);
        Assert.Equal(0, first.LastAutoFlagCount);
        Assert.Equal(0, first.LastAutoExploreCount);
        Assert.Equal(MinesweeperGameState.Ready, first.GameState);
        first.SelectedDifficulty = first.DifficultyOptions[2];
        Assert.True(first.IsAutoExploreEnabled);
        Assert.Equal(0, first.LastAutoExploreCount);
        Assert.False(second.IsAutoExploreEnabled);
        Assert.False(second.IsAutoFlagEnabled);
        Assert.Equal(MinesweeperGameState.Ready, second.GameState);
    }

    [Fact]
    public void 释放后启用继续探索不会修改棋盘()
    {
        var viewModel = CreateViewModel();
        viewModel.IsAutoFlagEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        viewModel.RevealCell(Cell(viewModel, 0, 4));
        var states = viewModel.BoardCells.Select(cell => cell.CellState).ToArray();
        viewModel.Dispose();

        viewModel.IsAutoExploreEnabled = true;

        Assert.Equal(states, viewModel.BoardCells.Select(cell => cell.CellState));
        Assert.Equal(0, viewModel.LastAutoExploreCount);
        Assert.False(viewModel.IsTimerRunning);
    }

    private static MinesweeperViewModel CreateViewModel(TimeProvider? time = null) => new(
        MinesweeperDocumentTests.CreateBarrierMinePlacementStrategy(), time ?? new ManualTimeProvider(), false);

    private static MinesweeperCellViewModel Cell(MinesweeperViewModel viewModel, int row, int column) =>
        Assert.Single(viewModel.BoardCells, cell => cell.Row == row && cell.Column == column);
}
