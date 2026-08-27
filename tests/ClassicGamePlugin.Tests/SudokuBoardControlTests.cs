using Avalonia;
using Avalonia.Input;
using ClassicGamePlugin.Features.Sudoku.Domain;
using ClassicGamePlugin.Features.Sudoku.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SudokuBoardControlTests
{
    [Fact]
    public void 棋盘四角与中心可靠换算为数独坐标()
    {
        var size = new Size(540, 540);

        Assert.True(SudokuBoardControl.TryHitTest(size, new Point(1, 1), out var topLeft));
        Assert.Equal(new SudokuPosition(0, 0), topLeft);
        Assert.True(SudokuBoardControl.TryHitTest(size, new Point(270, 270), out var center));
        Assert.Equal(new SudokuPosition(4, 4), center);
        Assert.True(SudokuBoardControl.TryHitTest(size, new Point(539, 539), out var bottomRight));
        Assert.Equal(new SudokuPosition(8, 8), bottomRight);
    }

    [Fact]
    public void 棋盘外坐标不会命中格子()
    {
        Assert.False(SudokuBoardControl.TryHitTest(new Size(540, 540), new Point(-1, 10), out _));
        Assert.False(SudokuBoardControl.TryHitTest(new Size(540, 540), new Point(541, 10), out _));
    }

    [Theory]
    [InlineData(Key.D1, (int)SudokuKeyActionKind.Number, 1)]
    [InlineData(Key.NumPad9, (int)SudokuKeyActionKind.Number, 9)]
    [InlineData(Key.Delete, (int)SudokuKeyActionKind.Clear, 0)]
    [InlineData(Key.N, (int)SudokuKeyActionKind.ToggleNotes, 0)]
    [InlineData(Key.Left, (int)SudokuKeyActionKind.MoveSelection, 0)]
    public void 局部键位映射为抽象数独操作(Key key, int expectedKind, int number)
    {
        Assert.True(SudokuBoardControl.TryMapKey(key, out var action));
        Assert.Equal((SudokuKeyActionKind)expectedKind, action.Kind);
        Assert.Equal(number, action.Number);
    }

    [Fact]
    public void 无关按键不会被数独吞掉()
    {
        Assert.False(SudokuBoardControl.TryMapKey(Key.Enter, out _));
    }
}
