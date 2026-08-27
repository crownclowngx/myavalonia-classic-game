using Avalonia;
using Avalonia.Input;
using ClassicGamePlugin.Features.Tetris.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class TetrisBoardControlTests
{
    [Theory]
    [InlineData(Key.Left, (int)TetrisInputAction.MoveLeft)]
    [InlineData(Key.A, (int)TetrisInputAction.MoveLeft)]
    [InlineData(Key.Right, (int)TetrisInputAction.MoveRight)]
    [InlineData(Key.D, (int)TetrisInputAction.MoveRight)]
    [InlineData(Key.Down, (int)TetrisInputAction.SoftDrop)]
    [InlineData(Key.S, (int)TetrisInputAction.SoftDrop)]
    [InlineData(Key.Up, (int)TetrisInputAction.RotateClockwise)]
    [InlineData(Key.X, (int)TetrisInputAction.RotateClockwise)]
    [InlineData(Key.Z, (int)TetrisInputAction.RotateCounterClockwise)]
    [InlineData(Key.Space, (int)TetrisInputAction.HardDrop)]
    [InlineData(Key.C, (int)TetrisInputAction.Hold)]
    [InlineData(Key.LeftShift, (int)TetrisInputAction.Hold)]
    [InlineData(Key.P, (int)TetrisInputAction.TogglePause)]
    public void 约定键位映射为局内操作(Key key, int expected)
    {
        Assert.True(TetrisBoardControl.TryMapInput(key, KeyModifiers.None, out var actual));
        Assert.Equal((TetrisInputAction)expected, actual);
    }

    [Fact]
    public void 无关键和带修饰键不会被游戏吞掉()
    {
        Assert.False(TetrisBoardControl.TryMapInput(Key.Enter, KeyModifiers.None, out _));
        Assert.False(TetrisBoardControl.TryMapInput(Key.C, KeyModifiers.Control, out _));
    }

    [Fact]
    public void 布局保持十比二十棋盘并在两侧预留面板()
    {
        var layout = TetrisBoardControl.GetLayout(new Size(760, 500));

        Assert.Equal(0.5, layout.Board.Width / layout.Board.Height, 6);
        Assert.True(layout.HoldPanel.Right < layout.Board.X);
        Assert.True(layout.NextPanel.X > layout.Board.Right);
        Assert.True(layout.CellSize > 0);
    }

    [Fact]
    public void DAS与ARR保持一百五十和四十毫秒()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(150), TetrisBoardControl.DasDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(40), TetrisBoardControl.ArrInterval);
    }
}
