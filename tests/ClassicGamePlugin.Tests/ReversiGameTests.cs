using ClassicGamePlugin.Features.Reversi.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ReversiGameTests
{
    [Fact]
    public void 标准开局包含中央四子黑方先手和四个合法位置()
    {
        var game = new ReversiGame();

        Assert.Equal(ReversiGameState.Ready, game.State);
        Assert.Equal(ReversiDiscColor.Black, game.CurrentPlayer);
        Assert.Equal(2, game.BlackCount);
        Assert.Equal(2, game.WhiteCount);
        Assert.Equal(
            [new ReversiPosition(2, 3), new(3, 2), new(4, 5), new(5, 4)],
            game.GetLegalMoves());
        Assert.Equal(ReversiDiscColor.White, game.GetDisc(new ReversiPosition(3, 3)));
        Assert.Equal(ReversiDiscColor.Black, game.GetDisc(new ReversiPosition(3, 4)));
    }

    [Fact]
    public void 合法落子翻转被夹棋子并切换回合()
    {
        var game = new ReversiGame();

        var result = game.PlaceDisc(new ReversiPosition(2, 3));

        Assert.NotNull(result);
        Assert.Equal([new ReversiPosition(3, 3)], result.FlippedPositions);
        Assert.Equal(ReversiGameState.Running, game.State);
        Assert.Equal(ReversiDiscColor.White, game.CurrentPlayer);
        Assert.Equal(4, game.BlackCount);
        Assert.Equal(1, game.WhiteCount);
    }

    [Fact]
    public void 一次落子可以同时完成八个方向翻转()
    {
        var board = EmptyBoard();
        var center = new ReversiPosition(3, 3);
        var directions = new (int Row, int Column)[]
        {
            (-1, -1), (-1, 0), (-1, 1), (0, -1),
            (0, 1), (1, -1), (1, 0), (1, 1),
        };
        foreach (var direction in directions)
        {
            Set(board, center.Row + direction.Row, center.Column + direction.Column, ReversiDiscColor.White);
            Set(board, center.Row + (direction.Row * 2), center.Column + (direction.Column * 2), ReversiDiscColor.Black);
        }

        var game = new ReversiGame(Snapshot(board, ReversiDiscColor.Black));

        var result = game.PlaceDisc(center);

        Assert.NotNull(result);
        Assert.Equal(8, result.FlippedPositions.Count);
        Assert.All(result.FlippedPositions, position =>
            Assert.Equal(ReversiDiscColor.Black, game.GetDisc(position)));
    }

    [Fact]
    public void 没有己方棋子封口时不能形成翻转()
    {
        var board = EmptyBoard();
        Set(board, 3, 4, ReversiDiscColor.White);
        Set(board, 3, 5, ReversiDiscColor.White);
        var game = new ReversiGame(Snapshot(board, ReversiDiscColor.Black));

        Assert.Null(game.PlaceDisc(new ReversiPosition(3, 3)));
        Assert.False(game.CanUndo);
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void 占用格和普通空格均被拒绝且不产生历史()
    {
        var game = new ReversiGame();
        var before = game.CreateSnapshot();

        Assert.Null(game.PlaceDisc(new ReversiPosition(3, 3)));
        Assert.Null(game.PlaceDisc(new ReversiPosition(0, 0)));
        Assert.False(game.CanUndo);
        Assert.Equal(before.BlackCount, game.BlackCount);
        Assert.Equal(before.WhiteCount, game.WhiteCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => game.PlaceDisc(new ReversiPosition(-1, 0)));
    }

    [Fact]
    public void 对方无棋可下时自动跳过并由原玩家继续()
    {
        var game = new ReversiGame(CreateForcedPassSnapshot());

        var result = game.PlaceDisc(new ReversiPosition(0, 0));

        Assert.NotNull(result);
        Assert.Equal(ReversiDiscColor.White, result.SkippedPlayer);
        Assert.Equal(ReversiDiscColor.Black, game.CurrentPlayer);
        Assert.Equal(ReversiGameState.Running, game.State);
        Assert.Equal([new ReversiPosition(7, 7)], game.GetLegalMoves());
    }

    [Fact]
    public void 双方均无棋可下时结束并按棋子数判胜()
    {
        var game = new ReversiGame(CreateForcedPassSnapshot());
        game.PlaceDisc(new ReversiPosition(0, 0));

        var result = game.PlaceDisc(new ReversiPosition(7, 7));

        Assert.NotNull(result);
        Assert.Equal(ReversiGameState.Finished, game.State);
        Assert.Equal(ReversiDiscColor.Black, game.Winner);
        Assert.Equal(64, game.BlackCount);
        Assert.Empty(game.GetLegalMoves());
    }

    [Fact]
    public void 最后一手可以形成三十二比三十二平局()
    {
        var board = Enumerable.Repeat<ReversiDiscColor?>(ReversiDiscColor.White, 64).ToArray();
        Set(board, 0, 0, null);
        Set(board, 0, 1, ReversiDiscColor.White);
        Set(board, 0, 2, ReversiDiscColor.Black);
        Set(board, 1, 0, ReversiDiscColor.Black);
        Set(board, 1, 1, ReversiDiscColor.Black);
        var blackNeeded = 30 - board.Count(color => color == ReversiDiscColor.Black);
        for (var index = 0; index < board.Length && blackNeeded > 0; index++)
        {
            if (index is 0 or 1 || board[index] == ReversiDiscColor.Black)
            {
                continue;
            }

            board[index] = ReversiDiscColor.Black;
            blackNeeded--;
        }

        var game = new ReversiGame(Snapshot(board, ReversiDiscColor.Black));

        game.PlaceDisc(new ReversiPosition(0, 0));

        Assert.Equal(ReversiGameState.Finished, game.State);
        Assert.Equal(32, game.BlackCount);
        Assert.Equal(32, game.WhiteCount);
        Assert.Null(game.Winner);
    }

    [Fact]
    public void 撤销恢复落子前的棋盘回合跳过信息和终局()
    {
        var game = new ReversiGame(CreateForcedPassSnapshot());
        var before = game.CreateSnapshot();
        game.PlaceDisc(new ReversiPosition(0, 0));
        game.PlaceDisc(new ReversiPosition(7, 7));
        Assert.Equal(ReversiGameState.Finished, game.State);

        game.Undo();
        game.Undo();

        Assert.Equal(before.State, game.State);
        Assert.Equal(before.CurrentPlayer, game.CurrentPlayer);
        Assert.Equal(before.BlackCount, game.BlackCount);
        Assert.Equal(before.WhiteCount, game.WhiteCount);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void 返回的快照数组副本不能修改真实棋局()
    {
        var game = new ReversiGame();
        var snapshot = game.CreateSnapshot();
        var copy = snapshot.CopyBoard();

        copy[0] = ReversiDiscColor.Black;

        Assert.Null(game.GetDisc(new ReversiPosition(0, 0)));
        Assert.Null(snapshot.GetDisc(new ReversiPosition(0, 0)));
    }

    private static ReversiGameSnapshot CreateForcedPassSnapshot()
    {
        var board = Enumerable.Repeat<ReversiDiscColor?>(ReversiDiscColor.Black, 64).ToArray();
        Set(board, 0, 0, null);
        Set(board, 0, 1, ReversiDiscColor.White);
        Set(board, 7, 7, null);
        Set(board, 7, 6, ReversiDiscColor.White);
        return Snapshot(board, ReversiDiscColor.Black);
    }

    private static ReversiDiscColor?[] EmptyBoard() => new ReversiDiscColor?[64];

    private static ReversiGameSnapshot Snapshot(
        IEnumerable<ReversiDiscColor?> board,
        ReversiDiscColor currentPlayer) =>
        new(board, currentPlayer, ReversiGameState.Running, 0, null);

    private static void Set(
        ReversiDiscColor?[] board,
        int row,
        int column,
        ReversiDiscColor? color) => board[(row * 8) + column] = color;
}
