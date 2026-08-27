using ClassicGamePlugin.Features.Sudoku.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SudokuGameTests
{
    [Fact]
    public void 给定格不可修改而普通空格可以柔性填入冲突数字()
    {
        var game = new SudokuGame(SudokuTestPuzzles.Create());

        Assert.Null(game.SetValue(new SudokuPosition(0, 0), 9));
        var result = Assert.IsType<SudokuMoveResult>(game.SetValue(new SudokuPosition(0, 2), 5));

        Assert.Equal(5, game.Values[2]);
        Assert.Contains(new SudokuPosition(0, 0), result.Conflicts);
        Assert.Contains(new SudokuPosition(0, 2), result.Conflicts);
    }

    [Fact]
    public void 填数清除本格与同伴的相同候选且撤销完整恢复()
    {
        var game = new SudokuGame(SudokuTestPuzzles.Create());
        var target = new SudokuPosition(0, 2);
        var peer = new SudokuPosition(0, 3);
        game.ToggleNote(target, 4);
        game.ToggleNote(peer, 4);

        game.SetValue(target, 4);

        Assert.Equal(0, game.Notes[SudokuRules.ToIndex(target)]);
        Assert.Equal(0, game.Notes[SudokuRules.ToIndex(peer)] & (1 << 4));
        game.Undo();
        Assert.NotEqual(0, game.Notes[SudokuRules.ToIndex(target)] & (1 << 4));
        Assert.NotEqual(0, game.Notes[SudokuRules.ToIndex(peer)] & (1 << 4));
    }

    [Fact]
    public void 候选只在空白可编辑格切换()
    {
        var game = new SudokuGame(SudokuTestPuzzles.Create());
        var empty = new SudokuPosition(0, 2);

        Assert.NotNull(game.ToggleNote(empty, 7));
        Assert.NotEqual(0, game.Notes[2] & (1 << 7));
        Assert.NotNull(game.ToggleNote(empty, 7));
        Assert.Equal(0, game.Notes[2] & (1 << 7));
        Assert.Null(game.ToggleNote(new SudokuPosition(0, 0), 7));
    }

    [Fact]
    public void 提示优先选中空格并锁定直到撤销()
    {
        var game = new SudokuGame(SudokuTestPuzzles.Create());
        var target = new SudokuPosition(0, 2);

        var result = Assert.IsType<SudokuMoveResult>(game.RevealHint(target));

        Assert.Equal(SudokuMoveKind.Hint, result.Kind);
        Assert.Equal(4, game.Values[2]);
        Assert.True(game.IsHint(target));
        Assert.Null(game.ClearValue(target));
        game.Undo();
        Assert.Equal(0, game.Values[2]);
        Assert.False(game.IsHint(target));
    }

    [Fact]
    public void 清除普通数字可撤销但不会自行恢复自动清理的候选()
    {
        var game = new SudokuGame(SudokuTestPuzzles.Create());
        var target = new SudokuPosition(0, 2);
        game.SetValue(target, 4);

        Assert.NotNull(game.ClearValue(target));
        Assert.Equal(0, game.Values[2]);
        game.Undo();
        Assert.Equal(4, game.Values[2]);
    }

    [Fact]
    public void 最后一格完成后锁定输入且撤销重新开放()
    {
        var puzzle = SudokuTestPuzzles.Create();
        var game = new SudokuGame(new SudokuPuzzle(
            "one-empty",
            SudokuDifficulty.Easy,
            SudokuPuzzleSource.BuiltIn,
            puzzle.Solution.Select((value, index) => index == 0 ? 0 : value).ToArray(),
            puzzle.Solution));

        var completed = Assert.IsType<SudokuMoveResult>(game.SetValue(new SudokuPosition(0, 0), 5));

        Assert.True(completed.IsCompleted);
        Assert.True(game.IsCompleted);
        Assert.Null(game.ClearValue(new SudokuPosition(0, 0)));
        game.Undo();
        Assert.False(game.IsCompleted);
        Assert.Equal(0, game.Values[0]);
    }

    [Fact]
    public void 重新开始保留题目但清除输入笔记提示与历史()
    {
        var game = new SudokuGame(SudokuTestPuzzles.Create());
        game.ToggleNote(new SudokuPosition(0, 2), 4);
        game.RevealHint(new SudokuPosition(0, 3));

        game.Restart();

        Assert.Equal(SudokuTestPuzzles.Parse(SudokuTestPuzzles.PuzzleText), game.Values);
        Assert.All(game.Notes, note => Assert.Equal(0, note));
        Assert.All(game.HintCells, hint => Assert.False(hint));
        Assert.False(game.CanUndo);
    }
}
