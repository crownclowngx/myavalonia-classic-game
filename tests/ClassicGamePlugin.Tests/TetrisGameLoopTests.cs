using ClassicGamePlugin.Features.Tetris.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class TetrisGameLoopTests
{
    [Fact]
    public void 一级自然重力每秒下降一格且大时间片补偿全部步数()
    {
        var loop = CreateLoop();
        var startRow = loop.Game.ActivePiece.Row;

        loop.Advance(TimeSpan.FromMilliseconds(999), softDrop: false);
        Assert.Equal(startRow, loop.Game.ActivePiece.Row);

        loop.Advance(TimeSpan.FromMilliseconds(2001), softDrop: false);
        Assert.Equal(startRow + 3, loop.Game.ActivePiece.Row);
    }

    [Fact]
    public void 软降使用二十分之一周期并逐格计分()
    {
        var loop = CreateLoop();
        var row = loop.Game.ActivePiece.Row;

        loop.Advance(TimeSpan.FromMilliseconds(100), softDrop: true);

        Assert.Equal(row + 2, loop.Game.ActivePiece.Row);
        Assert.Equal(2, loop.Game.Score);
    }

    [Fact]
    public void 普通重力余量不会在切换软降时被重复折算()
    {
        var loop = CreateLoop();
        var row = loop.Game.ActivePiece.Row;
        loop.Advance(TimeSpan.FromMilliseconds(900), softDrop: false);

        loop.Advance(TimeSpan.FromMilliseconds(50), softDrop: true);

        Assert.Equal(row + 1, loop.Game.ActivePiece.Row);
        Assert.Equal(1, loop.Game.Score);
    }

    [Fact]
    public void 接地五百毫秒才锁定而硬降立即锁定()
    {
        var loop = CreateGroundedLoop();
        var type = loop.Game.ActivePiece.Type;

        Assert.Empty(loop.Advance(TimeSpan.FromMilliseconds(499), softDrop: false));
        Assert.Equal(type, loop.Game.ActivePiece.Type);
        Assert.Single(loop.Advance(TimeSpan.FromMilliseconds(1), softDrop: false));

        var nextType = loop.Game.ActivePiece.Type;
        Assert.NotNull(loop.HardDrop());
        Assert.NotEqual(nextType, loop.Game.ActivePiece.Type);
    }

    [Fact]
    public void 接地移动最多重置十五次锁定延迟()
    {
        var loop = CreateGroundedLoop();
        loop.Advance(TimeSpan.FromMilliseconds(400), softDrop: false);
        for (var index = 0; index < TetrisGameLoop.MaximumLockResets; index++)
        {
            Assert.True(loop.MoveHorizontal(index % 2 == 0 ? -1 : 1));
            Assert.Equal(TimeSpan.Zero, loop.LockElapsed);
            loop.Advance(TimeSpan.FromMilliseconds(400), softDrop: false);
        }

        Assert.Equal(TetrisGameLoop.MaximumLockResets, loop.LockResetCount);
        Assert.True(loop.MoveHorizontal(1));
        Assert.Equal(TimeSpan.FromMilliseconds(400), loop.LockElapsed);
        Assert.Single(loop.Advance(TimeSpan.FromMilliseconds(100), softDrop: false));
    }

    [Fact]
    public void 暂停期间不推进且恢复后继续使用原累计时间()
    {
        var loop = CreateLoop();
        var row = loop.Game.ActivePiece.Row;
        loop.Advance(TimeSpan.FromMilliseconds(600), softDrop: false);
        Assert.True(loop.TogglePause());
        loop.Advance(TimeSpan.FromSeconds(10), softDrop: false);
        Assert.Equal(row, loop.Game.ActivePiece.Row);

        Assert.True(loop.TogglePause());
        loop.Advance(TimeSpan.FromMilliseconds(400), softDrop: false);
        Assert.Equal(row + 1, loop.Game.ActivePiece.Row);
    }

    [Fact]
    public void 重力公式从一级一秒开始且始终不低于十六毫秒()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), TetrisGameLoop.GetGravityInterval(1, false));
        Assert.Equal(TimeSpan.FromMilliseconds(50), TetrisGameLoop.GetGravityInterval(1, true));
        Assert.True(TetrisGameLoop.GetGravityInterval(10, false) < TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromMilliseconds(16), TetrisGameLoop.GetGravityInterval(100, false));
    }

    private static TetrisGameLoop CreateLoop() =>
        new(new TetrisGame(new SequenceTetrominoSource()));

    private static TetrisGameLoop CreateGroundedLoop()
    {
        var game = new TetrisGame(new SequenceTetrominoSource());
        game.LoadStateForTest(
            TetrisTestBoard.Empty(),
            new TetrisPiece(TetrominoType.O, TetrisRotation.Spawn, 22, 3));
        return new TetrisGameLoop(game);
    }
}
