using ClassicGamePlugin.Features.Sudoku.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SudokuSolverAndProviderTests
{
    [Fact]
    public void 求解经典题目得到预期终盘且确认唯一解()
    {
        var solver = new SudokuSolver();
        var puzzle = SudokuTestPuzzles.Parse(SudokuTestPuzzles.PuzzleText);

        Assert.True(solver.TrySolve(puzzle, out var solution));
        Assert.Equal(SudokuTestPuzzles.Parse(SudokuTestPuzzles.SolutionText), solution);
        Assert.Equal(1, solver.CountSolutions(puzzle));
    }

    [Fact]
    public void 冲突盘无解而空盘计数达到两个即停止()
    {
        var solver = new SudokuSolver();
        var invalid = new int[SudokuRules.CellCount];
        invalid[0] = 5;
        invalid[1] = 5;

        Assert.False(solver.TrySolve(invalid, out _));
        Assert.Equal(0, solver.CountSolutions(invalid));
        Assert.Equal(2, solver.CountSolutions(new int[SudokuRules.CellCount], 2));
    }

    [Fact]
    public void 求解和唯一性计数响应取消()
    {
        var solver = new SudokuSolver();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            solver.CountSolutions(new int[SudokuRules.CellCount], 2, cancellation.Token));
    }

    [Fact]
    public void 内置题库每档八题且全部满足范围与唯一解()
    {
        var solver = new SudokuSolver();
        var provider = new SudokuPuzzleProvider(solver, new Random(1234));

        Assert.Equal(24, provider.BuiltInPuzzles.Count);
        foreach (var difficulty in Enum.GetValues<SudokuDifficulty>())
        {
            var profile = SudokuDifficultyProfile.For(difficulty);
            var puzzles = provider.BuiltInPuzzles.Where(puzzle => puzzle.Difficulty == difficulty).ToArray();
            Assert.Equal(8, puzzles.Length);
            Assert.Equal(8, puzzles.Select(puzzle => puzzle.Id).Distinct().Count());
            foreach (var puzzle in puzzles)
            {
                Assert.InRange(puzzle.ClueCount, profile.MinimumClues, profile.MaximumClues);
                Assert.Equal(1, solver.CountSolutions(puzzle.Givens));
                Assert.True(puzzle.Givens.Select((value, index) => value == 0 || value == puzzle.Solution[index]).All(match => match));
            }
        }
    }

    [Fact]
    public void 题库新局会排除当前题目()
    {
        var provider = new SudokuPuzzleProvider(new SudokuSolver(), new Random(8));
        var first = provider.GetBuiltInPuzzle(SudokuDifficulty.Easy);
        var second = provider.GetBuiltInPuzzle(SudokuDifficulty.Easy, first.Id);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData((int)SudokuDifficulty.Easy)]
    [InlineData((int)SudokuDifficulty.Medium)]
    [InlineData((int)SudokuDifficulty.Hard)]
    public async Task 运行时生成结果位于难度范围且保持唯一解(int difficultyValue)
    {
        var difficulty = (SudokuDifficulty)difficultyValue;
        var solver = new SudokuSolver();
        var provider = new SudokuPuzzleProvider(solver, new Random(20260827 + (int)difficulty));

        var puzzle = await provider.GeneratePuzzleAsync(difficulty, CancellationToken.None);

        var profile = SudokuDifficultyProfile.For(difficulty);
        Assert.Equal(SudokuPuzzleSource.Generated, puzzle.Source);
        Assert.InRange(puzzle.ClueCount, profile.MinimumClues, profile.MaximumClues);
        Assert.Equal(1, solver.CountSolutions(puzzle.Givens));
    }
}
