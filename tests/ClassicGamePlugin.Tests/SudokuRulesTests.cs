using ClassicGamePlugin.Features.Sudoku.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SudokuRulesTests
{
    [Fact]
    public void 行列索引与九宫格同伴关系保持一致()
    {
        Assert.Equal(40, SudokuRules.ToIndex(new SudokuPosition(4, 4)));
        Assert.Equal(new SudokuPosition(8, 8), SudokuRules.FromIndex(80));
        Assert.True(SudokuRules.ArePeers(new SudokuPosition(0, 0), new SudokuPosition(0, 8)));
        Assert.True(SudokuRules.ArePeers(new SudokuPosition(0, 0), new SudokuPosition(8, 0)));
        Assert.True(SudokuRules.ArePeers(new SudokuPosition(0, 0), new SudokuPosition(2, 2)));
        Assert.False(SudokuRules.ArePeers(new SudokuPosition(0, 0), new SudokuPosition(4, 4)));
    }

    [Fact]
    public void 候选集合排除同行同列同宫已有数字()
    {
        var board = new int[SudokuRules.CellCount];
        board[SudokuRules.ToIndex(new SudokuPosition(0, 8))] = 1;
        board[SudokuRules.ToIndex(new SudokuPosition(8, 0))] = 2;
        board[SudokuRules.ToIndex(new SudokuPosition(2, 2))] = 3;

        var mask = SudokuRules.GetCandidateMask(board, new SudokuPosition(0, 0));

        Assert.Equal(0, mask & (1 << 1));
        Assert.Equal(0, mask & (1 << 2));
        Assert.Equal(0, mask & (1 << 3));
        Assert.NotEqual(0, mask & (1 << 4));
    }

    [Theory]
    [InlineData(0, 0, 0, 8)]
    [InlineData(0, 0, 8, 0)]
    [InlineData(0, 0, 2, 2)]
    public void 重复数字同时标记冲突双方(int firstRow, int firstColumn, int secondRow, int secondColumn)
    {
        var board = new int[SudokuRules.CellCount];
        var first = new SudokuPosition(firstRow, firstColumn);
        var second = new SudokuPosition(secondRow, secondColumn);
        board[SudokuRules.ToIndex(first)] = 7;
        board[SudokuRules.ToIndex(second)] = 7;

        var conflicts = SudokuRules.FindConflicts(board);

        Assert.Equal(2, conflicts.Count);
        Assert.Contains(first, conflicts);
        Assert.Contains(second, conflicts);
    }

    [Fact]
    public void 完成必须逐格匹配唯一答案()
    {
        var solution = SudokuTestPuzzles.Parse(SudokuTestPuzzles.SolutionText);
        Assert.True(SudokuRules.IsCompleted(solution, solution));

        var incomplete = solution.ToArray();
        incomplete[0] = 0;
        Assert.False(SudokuRules.IsCompleted(incomplete, solution));
    }
}
