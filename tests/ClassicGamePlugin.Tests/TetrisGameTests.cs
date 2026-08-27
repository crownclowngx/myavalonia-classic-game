using ClassicGamePlugin.Features.Tetris.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class TetrisGameTests
{
    [Fact]
    public void 七袋随机每袋都恰好包含七种方块()
    {
        var source = new SevenBagTetrominoSource(new Random(12345));
        var pieces = Enumerable.Range(0, 21).Select(_ => source.Next()).ToArray();

        for (var bag = 0; bag < 3; bag++)
        {
            Assert.Equal(
                Enum.GetValues<TetrominoType>().Order(),
                pieces.Skip(bag * 7).Take(7).Order());
        }
    }

    [Fact]
    public void 新局提供活动方块和五个预览且暂存每枚只能使用一次()
    {
        var game = CreateGame();
        var first = game.ActivePiece.Type;
        var next = game.NextPieces.ToArray();

        Assert.Equal(5, next.Length);
        Assert.True(game.Hold());
        Assert.Equal(first, game.HeldPiece);
        Assert.Equal(next[0], game.ActivePiece.Type);
        Assert.False(game.Hold());

        game.HardDrop();
        Assert.True(game.CanHold);
        var current = game.ActivePiece.Type;
        Assert.True(game.Hold());
        Assert.Equal(first, game.ActivePiece.Type);
        Assert.Equal(current, game.HeldPiece);
        Assert.Equal(TetrisRotation.Spawn, game.ActivePiece.Rotation);
    }

    [Fact]
    public void 幽灵块落到最低合法位置但不修改活动方块()
    {
        var game = CreateGame();
        var before = game.ActivePiece;
        var ghost = game.GetGhostPiece();

        Assert.True(ghost.Row > before.Row);
        Assert.Equal(before, game.ActivePiece);
        Assert.True(game.CanPlace(ghost));
        Assert.False(game.CanPlace(ghost with { Row = ghost.Row + 1 }));
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 300)]
    [InlineData(3, 500)]
    [InlineData(4, 800)]
    public void 普通消行使用固定现代基础分(int lineCount, int expectedScore)
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        for (var row = TetrisRules.BoardHeight - lineCount; row < TetrisRules.BoardHeight; row++)
        {
            TetrisTestBoard.FillRowExcept(cells, row, 0);
        }

        cells[TetrisRules.ToIndex(10, 8)] = TetrominoType.O;
        var piece = new TetrisPiece(TetrominoType.I, TetrisRotation.Right, 20, -2);
        game.LoadStateForTest(cells, piece);

        var transition = Assert.IsType<TetrisTransition>(game.LockActivePiece());

        Assert.Equal(lineCount, transition.ClearedRows.Count);
        Assert.Equal(expectedScore, game.Score);
        Assert.Equal(lineCount, game.TotalLines);
    }

    [Fact]
    public void 单行全清同时获得基础分和全清奖励()
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        TetrisTestBoard.FillRowExcept(cells, 23, 0, 1, 2, 3);
        game.LoadStateForTest(cells, new TetrisPiece(TetrominoType.I, TetrisRotation.Spawn, 22, 0));

        var transition = Assert.IsType<TetrisTransition>(game.LockActivePiece());

        Assert.True(transition.IsPerfectClear);
        Assert.Equal(900, game.Score);
    }

    [Fact]
    public void 连续困难消行获得一点五倍基础分和特殊四消全清奖励()
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        for (var row = 20; row < 24; row++)
        {
            TetrisTestBoard.FillRowExcept(cells, row, 0);
        }

        game.LoadStateForTest(
            cells,
            new TetrisPiece(TetrominoType.I, TetrisRotation.Right, 20, -2),
            backToBack: true);

        var transition = Assert.IsType<TetrisTransition>(game.LockActivePiece());

        Assert.True(transition.IsBackToBack);
        Assert.True(transition.IsPerfectClear);
        Assert.Equal(4400, game.Score);
    }

    [Fact]
    public void Combo从第二次连续消行开始加分且未消行重置()
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        TetrisTestBoard.FillRowExcept(cells, 23, 0, 1, 2, 3);
        cells[TetrisRules.ToIndex(10, 8)] = TetrominoType.J;
        game.LoadStateForTest(
            cells,
            new TetrisPiece(TetrominoType.I, TetrisRotation.Spawn, 22, 0),
            combo: 0);

        game.LockActivePiece();

        Assert.Equal(1, game.Combo);
        Assert.Equal(150, game.Score);

        var noClear = TetrisTestBoard.Empty();
        game.LoadStateForTest(noClear, new TetrisPiece(TetrominoType.O, TetrisRotation.Spawn, 22, 3), combo: game.Combo);
        game.LockActivePiece();
        Assert.Equal(-1, game.Combo);
    }

    [Theory]
    [InlineData(true, 800, TetrisSpinKind.Full)]
    [InlineData(false, 200, TetrisSpinKind.Mini)]
    public void TSpin根据朝前角区分Full和Mini并计分(bool fillFrontCorner, int expected, TetrisSpinKind kind)
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        TetrisTestBoard.FillRowExcept(cells, 23, 4);
        cells[TetrisRules.ToIndex(21, 3)] = TetrominoType.J;
        if (fillFrontCorner)
        {
            cells[TetrisRules.ToIndex(21, 5)] = TetrominoType.J;
        }

        game.LoadStateForTest(cells, new TetrisPiece(TetrominoType.T, TetrisRotation.Spawn, 21, 3));
        Assert.True(game.TryRotate(clockwise: true, out _));

        var transition = Assert.IsType<TetrisTransition>(game.LockActivePiece());

        Assert.Equal(kind, transition.Spin);
        Assert.Equal(expected, game.Score);
    }

    [Fact]
    public void 第五级踢墙会把三角占位的Mini升级为Full()
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        cells[TetrisRules.ToIndex(21, 3)] = TetrominoType.J;
        cells[TetrisRules.ToIndex(23, 3)] = TetrominoType.J;
        cells[TetrisRules.ToIndex(23, 5)] = TetrominoType.J;
        game.LoadStateForTest(
            cells,
            new TetrisPiece(TetrominoType.T, TetrisRotation.Right, 21, 3),
            rotationKickIndex: 4);

        var transition = Assert.IsType<TetrisTransition>(game.LockActivePiece());

        Assert.Equal(TetrisSpinKind.Full, transition.Spin);
        Assert.Equal(400, game.Score);
    }

    [Fact]
    public void 锁定后隐藏区仍有方块会结束游戏()
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        game.LoadStateForTest(cells, TetrisRules.CreateSpawnPiece(TetrominoType.O));

        game.LockActivePiece();

        Assert.Equal(TetrisGameState.GameOver, game.State);
    }

    [Fact]
    public void 水平碰撞和完全失败旋转都保持状态不变()
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        var piece = new TetrisPiece(TetrominoType.O, TetrisRotation.Spawn, 10, -1);
        game.LoadStateForTest(cells, piece);

        Assert.False(game.TryMoveHorizontal(-1));
        Assert.Equal(piece, game.ActivePiece);

        var rotatingPiece = new TetrisPiece(TetrominoType.T, TetrisRotation.Spawn, 10, 3);
        var currentCells = TetrisRules.GetCells(rotatingPiece).ToHashSet();
        cells = TetrisTestBoard.Empty();
        var targetRotation = TetrisRotation.Right;
        foreach (var kick in TetrisRules.GetKickTests(TetrominoType.T, rotatingPiece.Rotation, targetRotation))
        {
            var candidate = rotatingPiece with
            {
                Rotation = targetRotation,
                Row = rotatingPiece.Row - kick.Y,
                Column = rotatingPiece.Column + kick.X,
            };
            var blocker = Assert.Single(
                TetrisRules.GetCells(candidate).Where(position => !currentCells.Contains(position)).Take(1));
            cells[TetrisRules.ToIndex(blocker.Row, blocker.Column)] = TetrominoType.Z;
        }

        game.LoadStateForTest(cells, rotatingPiece);
        Assert.False(game.TryRotate(true, out _));
        Assert.Equal(rotatingPiece, game.ActivePiece);
    }

    [Fact]
    public void SRS在左墙分别为T和I选择正确踢墙测试()
    {
        var game = CreateGame();
        game.LoadStateForTest(
            TetrisTestBoard.Empty(),
            new TetrisPiece(TetrominoType.T, TetrisRotation.Right, 10, -1));

        Assert.True(game.TryRotate(clockwise: true, out var tKick));
        Assert.Equal(1, tKick);
        Assert.Equal(0, game.ActivePiece.Column);

        game.LoadStateForTest(
            TetrisTestBoard.Empty(),
            new TetrisPiece(TetrominoType.I, TetrisRotation.Right, 10, -2));

        Assert.True(game.TryRotate(clockwise: true, out var iKick));
        Assert.Equal(2, iKick);
        Assert.Equal(0, game.ActivePiece.Column);
    }

    [Fact]
    public void 跨越十行后升级但本次消行仍使用消行前等级()
    {
        var game = CreateGame();
        var cells = TetrisTestBoard.Empty();
        TetrisTestBoard.FillRowExcept(cells, 23, 0, 1, 2, 3);
        cells[TetrisRules.ToIndex(10, 8)] = TetrominoType.J;
        game.LoadStateForTest(
            cells,
            new TetrisPiece(TetrominoType.I, TetrisRotation.Spawn, 22, 0),
            totalLines: 9);

        game.LockActivePiece();

        Assert.Equal(10, game.TotalLines);
        Assert.Equal(2, game.Level);
        Assert.Equal(100, game.Score);
    }

    private static TetrisGame CreateGame() => new(new SequenceTetrominoSource());
}
