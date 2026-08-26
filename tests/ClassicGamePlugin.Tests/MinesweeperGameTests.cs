using ClassicGamePlugin.Features.Minesweeper.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class MinesweeperGameTests
{
    [Theory]
    [InlineData(0, 9, 9, 10)]
    [InlineData(1, 16, 16, 40)]
    [InlineData(2, 16, 30, 99)]
    public void 固定难度符合经典尺寸和雷数(
        int difficultyValue,
        int rows,
        int columns,
        int mines)
    {
        var difficulty = (MinesweeperDifficulty)difficultyValue;
        var definition = MinesweeperDifficultyDefinition.From(difficulty);

        Assert.Equal(rows, definition.Rows);
        Assert.Equal(columns, definition.Columns);
        Assert.Equal(mines, definition.MineCount);
    }

    [Fact]
    public void 首次翻格后九宫格安全且雷数准确()
    {
        var game = CreateBeginnerRunningGame();

        Assert.True(game.Reveal(4, 4));

        Assert.Equal(MinesweeperGameState.Running, game.State);
        Assert.Equal(10, game.Cells.Count(cell => cell.IsMine));
        for (var row = 3; row <= 5; row++)
        {
            for (var column = 3; column <= 5; column++)
            {
                Assert.False(game.GetCell(row, column).IsMine);
            }
        }

        Assert.Equal(0, game.GetCell(4, 4).AdjacentMineCount);
        Assert.Equal(MinesweeperCellState.Revealed, game.GetCell(4, 4).State);
    }

    [Fact]
    public void 角落首击只排除棋盘内的相邻格()
    {
        var game = CreateBeginnerGame();

        game.Reveal(0, 0);

        Assert.False(game.GetCell(0, 0).IsMine);
        Assert.False(game.GetCell(0, 1).IsMine);
        Assert.False(game.GetCell(1, 0).IsMine);
        Assert.False(game.GetCell(1, 1).IsMine);
        Assert.Equal(10, game.Cells.Count(cell => cell.IsMine));
    }

    [Fact]
    public void 相邻数字按八邻域准确计算()
    {
        var difficulty = new MinesweeperDifficultyDefinition(
            MinesweeperDifficulty.Beginner,
            "测试",
            5,
            5,
            3);
        var game = new MinesweeperGame(
            difficulty,
            new FixedMinePlacementStrategy(
                new CellCoordinate(0, 0),
                new CellCoordinate(0, 2),
                new CellCoordinate(0, 4)));

        game.Reveal(4, 4);

        Assert.Equal(1, game.GetCell(1, 0).AdjacentMineCount);
        Assert.Equal(2, game.GetCell(1, 1).AdjacentMineCount);
        Assert.Equal(1, game.GetCell(1, 2).AdjacentMineCount);
        Assert.Equal(0, game.GetCell(3, 3).AdjacentMineCount);
    }

    [Fact]
    public void 零格使用区域展开并在数字边界停止()
    {
        var game = CreateThreeMineTestGame();

        game.Reveal(4, 4);

        Assert.Equal(MinesweeperCellState.Revealed, game.GetCell(4, 4).State);
        Assert.Equal(MinesweeperCellState.Revealed, game.GetCell(1, 2).State);
        Assert.Equal(MinesweeperCellState.Covered, game.GetCell(0, 2).State);
    }

    [Fact]
    public void 旗帜可以切换且旗帜格不能翻开()
    {
        var game = CreateBeginnerGame();

        Assert.True(game.ToggleFlag(0, 0));
        Assert.Equal(MinesweeperCellState.Flagged, game.GetCell(0, 0).State);
        Assert.Equal(9, game.RemainingMineCount);
        Assert.False(game.Reveal(0, 0));
        Assert.Equal(MinesweeperGameState.Ready, game.State);

        Assert.True(game.ToggleFlag(0, 0));
        Assert.Equal(MinesweeperCellState.Covered, game.GetCell(0, 0).State);
        Assert.Equal(10, game.RemainingMineCount);
    }

    [Fact]
    public void 旗帜数不匹配时数字格快速展开不生效()
    {
        var game = CreateThreeMineTestGame();
        game.Reveal(4, 4);
        var coveredBefore = game.Cells.Count(cell => cell.State == MinesweeperCellState.Covered);

        Assert.False(game.Reveal(1, 2));

        Assert.Equal(coveredBefore, game.Cells.Count(cell => cell.State == MinesweeperCellState.Covered));
        Assert.Equal(MinesweeperGameState.Running, game.State);
    }

    [Fact]
    public void 正确旗帜允许数字格快速展开并完成胜利()
    {
        var game = CreateThreeMineTestGame();
        game.Reveal(4, 4);
        game.ToggleFlag(0, 0);
        game.ToggleFlag(0, 2);
        game.ToggleFlag(0, 4);

        Assert.True(game.Reveal(1, 2));

        Assert.Equal(MinesweeperGameState.Won, game.State);
        Assert.Equal(22, game.Cells.Count(cell => cell.State == MinesweeperCellState.Revealed));
    }

    [Fact]
    public void 错误旗帜数量匹配时快速展开会踩雷()
    {
        var game = CreateThreeMineTestGame();
        game.Reveal(4, 4);
        game.ToggleFlag(0, 1);

        Assert.True(game.Reveal(1, 2));

        Assert.Equal(MinesweeperGameState.Lost, game.State);
        Assert.True(game.GetCell(0, 2).IsExploded);
    }

    [Fact]
    public void 终局后翻格与插旗均不再改变状态()
    {
        var game = CreateThreeMineTestGame();
        game.Reveal(4, 4);
        game.Reveal(0, 0);
        var snapshot = game.Cells.Select(cell => cell.State).ToArray();

        Assert.False(game.Reveal(0, 1));
        Assert.False(game.ToggleFlag(0, 3));
        Assert.Equal(snapshot, game.Cells.Select(cell => cell.State));
    }

    [Fact]
    public void 重新开局会清除雷位旗帜和翻开状态()
    {
        var game = CreateBeginnerGame();
        game.ToggleFlag(0, 0);
        game.Reveal(4, 4);

        game.StartNewGame(MinesweeperDifficultyDefinition.Intermediate);

        Assert.Equal(MinesweeperGameState.Ready, game.State);
        Assert.Equal(16 * 16, game.Cells.Count);
        Assert.All(game.Cells, cell =>
        {
            Assert.False(cell.IsMine);
            Assert.Equal(MinesweeperCellState.Covered, cell.State);
        });
    }

    [Fact]
    public void 两个游戏实例保持完全独立()
    {
        var first = CreateBeginnerGame();
        var second = CreateBeginnerGame();

        first.ToggleFlag(0, 0);
        first.Reveal(4, 4);

        Assert.Equal(MinesweeperGameState.Ready, second.State);
        Assert.Equal(0, second.FlagCount);
        Assert.All(second.Cells, cell => Assert.Equal(MinesweeperCellState.Covered, cell.State));
    }

    [Fact]
    public void 游戏引擎拒绝布雷策略返回重复坐标()
    {
        var mines = Enumerable.Repeat(new CellCoordinate(0, 0), 10).ToArray();
        var game = new MinesweeperGame(
            MinesweeperDifficultyDefinition.Beginner,
            new UncheckedMinePlacementStrategy(mines));

        Assert.Throws<InvalidOperationException>(() => game.Reveal(4, 4));
        Assert.Equal(MinesweeperGameState.Ready, game.State);
        Assert.All(game.Cells, cell => Assert.False(cell.IsMine));
    }

    [Fact]
    public void 游戏引擎拒绝布雷策略返回越界坐标()
    {
        var mines = new[]
        {
            new CellCoordinate(0, 0),
            new CellCoordinate(0, 1),
            new CellCoordinate(0, 2),
            new CellCoordinate(0, 3),
            new CellCoordinate(0, 4),
            new CellCoordinate(0, 5),
            new CellCoordinate(0, 6),
            new CellCoordinate(0, 7),
            new CellCoordinate(0, 8),
            new CellCoordinate(99, 99),
        };
        var game = new MinesweeperGame(
            MinesweeperDifficultyDefinition.Beginner,
            new UncheckedMinePlacementStrategy(mines));

        Assert.Throws<InvalidOperationException>(() => game.Reveal(4, 4));
        Assert.Equal(MinesweeperGameState.Ready, game.State);
        Assert.All(game.Cells, cell => Assert.False(cell.IsMine));
    }

    private static MinesweeperGame CreateBeginnerGame() =>
        new(MinesweeperDifficultyDefinition.Beginner, new SequentialMinePlacementStrategy());

    private static MinesweeperGame CreateBeginnerRunningGame() =>
        new(
            MinesweeperDifficultyDefinition.Beginner,
            new FixedMinePlacementStrategy(
                new CellCoordinate(0, 0),
                new CellCoordinate(1, 0),
                new CellCoordinate(1, 1),
                new CellCoordinate(1, 2),
                new CellCoordinate(1, 3),
                new CellCoordinate(1, 4),
                new CellCoordinate(1, 5),
                new CellCoordinate(1, 6),
                new CellCoordinate(1, 7),
                new CellCoordinate(1, 8)));

    private static MinesweeperGame CreateThreeMineTestGame()
    {
        var difficulty = new MinesweeperDifficultyDefinition(
            MinesweeperDifficulty.Beginner,
            "测试",
            5,
            5,
            3);
        return new MinesweeperGame(
            difficulty,
            new FixedMinePlacementStrategy(
                new CellCoordinate(0, 0),
                new CellCoordinate(0, 2),
                new CellCoordinate(0, 4)));
    }
}
