using Avalonia;
using Avalonia.Input;
using ClassicGamePlugin.Features.Sokoban.Domain;
using ClassicGamePlugin.Features.Sokoban.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SokobanBoardControlTests
{
    [Theory]
    [MemberData(nameof(MovementKeys))]
    public void 方向键与WASD映射为领域方向(Key key, int expected)
    {
        Assert.True(SokobanBoardControl.TryMapInput(key, KeyModifiers.None, out var action));
        Assert.Equal(SokobanInputActionKind.Move, action.Kind);
        Assert.Equal((SokobanDirection)expected, action.Direction);
    }

    [Theory]
    [InlineData(Key.U, KeyModifiers.None)]
    [InlineData(Key.Z, KeyModifiers.Control)]
    public void U与CtrlZ映射为撤销(Key key, KeyModifiers modifiers)
    {
        Assert.True(SokobanBoardControl.TryMapInput(key, modifiers, out var action));
        Assert.Equal(SokobanInputActionKind.Undo, action.Kind);
    }

    [Fact]
    public void R映射为重开而无关或带修饰按键不被吞掉()
    {
        Assert.True(SokobanBoardControl.TryMapInput(Key.R, KeyModifiers.None, out var restart));
        Assert.Equal(SokobanInputActionKind.Restart, restart.Kind);
        Assert.False(SokobanBoardControl.TryMapInput(Key.Enter, KeyModifiers.None, out _));
        Assert.False(SokobanBoardControl.TryMapInput(Key.W, KeyModifiers.Control, out _));
        Assert.False(SokobanBoardControl.TryMapInput(Key.Z, KeyModifiers.None, out _));
    }

    [Fact]
    public void 自适应棋盘命中四角并拒绝外部坐标()
    {
        Assert.True(SokobanBoardControl.TryHitTest(new Size(700, 410), 7, 5, new Point(81, 13), out var topLeft));
        Assert.Equal(new SokobanPosition(0, 0), topLeft);
        Assert.True(SokobanBoardControl.TryHitTest(new Size(700, 410), 7, 5, new Point(619, 397), out var bottomRight));
        Assert.Equal(new SokobanPosition(4, 6), bottomRight);
        Assert.False(SokobanBoardControl.TryHitTest(new Size(700, 410), 7, 5, new Point(5, 5), out _));
    }

    public static TheoryData<Key, int> MovementKeys => new()
    {
        { Key.Up, (int)SokobanDirection.Up }, { Key.W, (int)SokobanDirection.Up },
        { Key.Down, (int)SokobanDirection.Down }, { Key.S, (int)SokobanDirection.Down },
        { Key.Left, (int)SokobanDirection.Left }, { Key.A, (int)SokobanDirection.Left },
        { Key.Right, (int)SokobanDirection.Right }, { Key.D, (int)SokobanDirection.Right },
    };
}
