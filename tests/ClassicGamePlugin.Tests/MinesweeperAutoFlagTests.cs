using ClassicGamePlugin.Features.Minesweeper.Domain;
using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class MinesweeperAutoFlagTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void 数字邻域在角落边缘和中心都能确定全部雷(int row, int column)
    {
        var cells = Enumerable.Repeat(new MinesweeperVisibleCell(null), 9).ToArray();
        var expected = Enumerable.Range(0, 9)
            .Select(index => new CellCoordinate(index / 3, index % 3))
            .Where(cell => cell != new CellCoordinate(row, column) &&
                Math.Abs(cell.Row - row) <= 1 && Math.Abs(cell.Column - column) <= 1)
            .ToArray();
        cells[(row * 3) + column] = new MinesweeperVisibleCell(expected.Length);

        Assert.Equal(expected, MinesweeperDeduction.FindCertainMines(3, 3, cells));
    }

    [Fact]
    public void 没有数字或数字信息不足时不猜雷()
    {
        var cells = Enumerable.Repeat(new MinesweeperVisibleCell(null), 9).ToArray();
        Assert.Empty(MinesweeperDeduction.FindCertainMines(3, 3, cells));
        cells[4] = new MinesweeperVisibleCell(1);
        Assert.Empty(MinesweeperDeduction.FindCertainMines(3, 3, cells));
        cells[4] = new MinesweeperVisibleCell(0);
        Assert.Empty(MinesweeperDeduction.FindCertainMines(3, 3, cells));
    }

    [Fact]
    public void 多个数字确认同一雷格时只返回一次()
    {
        MinesweeperVisibleCell[] cells = [new(1), new(null), new(1)];
        Assert.Equal([new CellCoordinate(0, 1)], MinesweeperDeduction.FindCertainMines(1, 3, cells));
    }

    [Theory]
    [InlineData(9, 9, 10)]
    [InlineData(16, 16, 40)]
    [InlineData(16, 30, 99)]
    public void 三种尺寸的确定性公开棋盘在旗帜正确时不会误判安全格(int rows, int columns, int mineCount)
    {
        var random = new Random(20261014);
        var totalDeductions = 0;
        for (var sample = 0; sample < 100; sample++)
        {
            var mines = Enumerable.Range(0, rows * columns).OrderBy(_ => random.Next()).Take(mineCount)
                .Select(index => new CellCoordinate(index / columns, index % columns)).ToHashSet();
            var cells = Enumerable.Range(0, rows * columns).Select(index =>
            {
                var coordinate = new CellCoordinate(index / columns, index % columns);
                if (mines.Contains(coordinate))
                {
                    return new MinesweeperVisibleCell(null, random.NextDouble() < 0.3);
                }

                if (random.NextDouble() < 0.2)
                {
                    return new MinesweeperVisibleCell(null);
                }

                return new MinesweeperVisibleCell(mines.Count(mine =>
                    Math.Abs(mine.Row - coordinate.Row) <= 1 && Math.Abs(mine.Column - coordinate.Column) <= 1));
            }).ToArray();

            var certainMines = MinesweeperDeduction.FindCertainMines(rows, columns, cells);
            Assert.All(certainMines, mine => Assert.Contains(mine, mines));
            totalDeductions += certainMines.Count;
        }

        Assert.True(totalDeductions > 0);
    }

    [Fact]
    public void 自动插旗幂等且保留安全格和手动旗帜()
    {
        var game = CreateSmallGame();
        game.Reveal(4, 4);
        game.ToggleFlag(0, 4);
        Assert.Equal(0, game.AutoFlagCertainMines());
        game.Reveal(0, 1);
        var revealed = game.Cells.Where(cell => cell.State == MinesweeperCellState.Revealed).ToArray();

        Assert.Equal(2, game.AutoFlagCertainMines());
        Assert.Equal(0, game.AutoFlagCertainMines());
        Assert.Equal(3, game.FlagCount);
        Assert.Equal(MinesweeperCellState.Covered, game.GetCell(0, 3).State);
        Assert.Equal(revealed, game.Cells.Where(cell => cell.State == MinesweeperCellState.Revealed));
        Assert.All(game.Cells.Where(cell => cell.State == MinesweeperCellState.Flagged), cell => Assert.True(cell.IsMine));
    }

    [Fact]
    public void 手动旗帜参与计数且信息不足时不继续插旗()
    {
        var game = CreateSmallGame();
        game.ToggleFlag(0, 1);
        game.Reveal(4, 4);

        Assert.Equal(0, game.AutoFlagCertainMines());
        Assert.Equal(1, game.FlagCount);
        Assert.Equal(MinesweeperCellState.Flagged, game.GetCell(0, 1).State);
    }

    [Fact]
    public void 现有旗帜按雷计数且只返回尚未插旗的格子()
    {
        MinesweeperVisibleCell[] cells = [new(null, true), new(2), new(null)];
        Assert.Equal([new CellCoordinate(0, 2)], MinesweeperDeduction.FindCertainMines(1, 3, cells));
        cells[0] = new MinesweeperVisibleCell(null);
        Assert.Equal([new CellCoordinate(0, 0), new CellCoordinate(0, 2)],
            MinesweeperDeduction.FindCertainMines(1, 3, cells));
    }

    [Fact]
    public void 旗帜超过数字时不再自动插旗且不会纠正玩家标记()
    {
        MinesweeperVisibleCell[] cells = [new(null, true), new(1), new(null, true), new(null)];
        Assert.Empty(MinesweeperDeduction.FindCertainMines(1, 4, cells));
    }

    [Fact]
    public void 手动错旗参与快速展开并可能踩雷()
    {
        var game = CreateSmallGame();
        game.ToggleFlag(0, 1);
        game.Reveal(4, 4);
        game.AutoFlagCertainMines();

        Assert.True(game.Reveal(1, 2));
        Assert.Equal(MinesweeperGameState.Lost, game.State);
        Assert.True(game.GetCell(0, 2).IsExploded);
    }

    [Fact]
    public void 相同可见信息在不同隐藏雷位下得到相同结果()
    {
        var first = CreateBarrierGame(new CellCoordinate(0, 0));
        var second = CreateBarrierGame(new CellCoordinate(0, 8));
        first.Reveal(8, 8);
        second.Reveal(8, 8);

        Assert.Equal(9, first.AutoFlagCertainMines());
        Assert.Equal(9, second.AutoFlagCertainMines());
        Assert.Equal(first.Cells.Select(cell => cell.State), second.Cells.Select(cell => cell.State));
        Assert.Equal(MinesweeperCellState.Covered, first.GetCell(0, 0).State);
        Assert.Equal(MinesweeperCellState.Covered, second.GetCell(0, 8).State);
    }

    [Fact]
    public void 准备和终局状态不自动插旗或触发布雷()
    {
        var game = CreateSmallGame();
        Assert.Equal(0, game.AutoFlagCertainMines());
        Assert.All(game.Cells, cell => Assert.False(cell.IsMine));
        game.Reveal(4, 4);
        game.Reveal(0, 0);
        Assert.Equal(MinesweeperGameState.Lost, game.State);
        Assert.Equal(0, game.AutoFlagCertainMines());
        game.StartNewGame(game.Difficulty);
        game.Reveal(4, 4);
        game.Reveal(0, 1);
        game.Reveal(0, 3);
        Assert.Equal(MinesweeperGameState.Won, game.State);
        Assert.Equal(0, game.AutoFlagCertainMines());
    }

    [Fact]
    public void 默认关闭且开启立即分析之后翻格继续分析()
    {
        using var viewModel = CreateViewModel();
        Assert.False(viewModel.IsAutoFlagEnabled);
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        Assert.Equal(10, viewModel.RemainingMineCount);

        viewModel.IsAutoFlagEnabled = true;

        Assert.Equal(9, viewModel.LastAutoFlagCount);
        Assert.Equal("本次自动插旗 9 格", viewModel.AutoFlagStatusText);
        Assert.Equal(1, viewModel.RemainingMineCount);
        Assert.False(Cell(viewModel, 0, 1).IsRevealed);
        viewModel.IsAutoFlagEnabled = false;
        viewModel.ToggleFlag(Cell(viewModel, 1, 0));
        viewModel.IsAutoFlagEnabled = true;
        Assert.Equal(1, viewModel.LastAutoFlagCount);
    }

    [Fact]
    public void 开局前开启不会启动计时且首次翻格自动插旗()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsAutoFlagEnabled = true;
        Assert.False(viewModel.IsTimerRunning);
        Assert.Equal(0, viewModel.LastAutoFlagCount);

        viewModel.RevealCell(Cell(viewModel, 8, 8));

        Assert.Equal(9, viewModel.LastAutoFlagCount);
        Assert.True(viewModel.IsTimerRunning);
        Assert.Equal(MinesweeperGameState.Running, viewModel.GameState);
    }

    [Fact]
    public void 取消自动旗帜不会立即重新插上但下次有效翻格会重新分析()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsAutoFlagEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        viewModel.ToggleFlag(Cell(viewModel, 1, 0));
        Assert.Equal(MinesweeperCellState.Covered, Cell(viewModel, 1, 0).CellState);
        viewModel.RevealCell(Cell(viewModel, 0, 8));
        Assert.Equal(MinesweeperCellState.Flagged, Cell(viewModel, 1, 0).CellState);
        Assert.Equal(1, viewModel.LastAutoFlagCount);
    }

    [Fact]
    public void 自动旗帜仍可参与玩家触发的数字格快速展开()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsAutoFlagEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        var revealedBefore = viewModel.BoardCells.Count(cell => cell.IsRevealed);
        viewModel.RevealCell(Cell(viewModel, 2, 4));
        Assert.Equal(revealedBefore, viewModel.BoardCells.Count(cell => cell.IsRevealed));
        viewModel.RevealCell(Cell(viewModel, 0, 4));
        viewModel.RevealCell(Cell(viewModel, 0, 4));
        Assert.True(Cell(viewModel, 0, 3).IsRevealed);
        Assert.True(Cell(viewModel, 0, 5).IsRevealed);
    }

    [Fact]
    public void 重开保留开关并清零结果且另一个窗口不受影响()
    {
        using var first = CreateViewModel();
        using var second = CreateViewModel();
        first.IsAutoFlagEnabled = true;
        first.RevealCell(Cell(first, 8, 8));
        first.RestartCommand.Execute(null);

        Assert.True(first.IsAutoFlagEnabled);
        Assert.Equal(0, first.LastAutoFlagCount);
        Assert.Equal(10, first.RemainingMineCount);
        Assert.Equal(MinesweeperGameState.Ready, first.GameState);
        Assert.False(first.IsTimerRunning);
        Assert.False(second.IsAutoFlagEnabled);
        Assert.Equal(10, second.RemainingMineCount);
        first.SelectedDifficulty = first.DifficultyOptions[2];
        Assert.True(first.IsAutoFlagEnabled);
        Assert.Equal(99, first.RemainingMineCount);
        Assert.Equal(0, first.LastAutoFlagCount);
    }

    [Fact]
    public void 释放后开启自动化不会再修改棋盘()
    {
        var viewModel = CreateViewModel();
        viewModel.RevealCell(Cell(viewModel, 8, 8));
        viewModel.Dispose();
        viewModel.IsAutoFlagEnabled = true;
        viewModel.RevealCell(Cell(viewModel, 0, 1));
        Assert.Equal(10, viewModel.RemainingMineCount);
        Assert.Equal(0, viewModel.LastAutoFlagCount);
        Assert.False(viewModel.IsTimerRunning);
    }

    private static MinesweeperGame CreateSmallGame() => new(
        new MinesweeperDifficultyDefinition(MinesweeperDifficulty.Beginner, "测试", 5, 5, 3),
        new FixedMinePlacementStrategy(new(0, 0), new(0, 2), new(0, 4)));

    private static MinesweeperGame CreateBarrierGame(CellCoordinate extraMine) => new(
        MinesweeperDifficultyDefinition.Beginner,
        new FixedMinePlacementStrategy(Enumerable.Range(0, 9).Select(column => new CellCoordinate(1, column))
            .Append(extraMine).ToArray()));

    private static MinesweeperViewModel CreateViewModel() => new(
        MinesweeperDocumentTests.CreateBarrierMinePlacementStrategy(), new ManualTimeProvider(), false);

    private static MinesweeperCellViewModel Cell(MinesweeperViewModel viewModel, int row, int column) =>
        Assert.Single(viewModel.BoardCells, cell => cell.Row == row && cell.Column == column);
}
