using ClassicGamePlugin.Features.Gomoku.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GomokuGameTests
{
    [Fact]
    public void 空盘黑方先手且第一手不强制天元()
    {
        var game = new GomokuGame();

        var result = game.PlaceStone(new GomokuPosition(0, 0));

        Assert.NotNull(result);
        Assert.Equal(GomokuStone.Black, game.GetStone(new GomokuPosition(0, 0)));
        Assert.Equal(GomokuStone.White, game.CurrentPlayer);
        Assert.Equal(GomokuGameState.Running, game.State);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, -1)]
    public void 四个方向均能形成五连并保留获胜线(int rowDirection, int columnDirection)
    {
        var board = EmptyBoard();
        var origin = new GomokuPosition(7, 7);
        for (var offset = -2; offset <= 1; offset++)
        {
            Set(board, origin.Row + (offset * rowDirection), origin.Column + (offset * columnDirection), GomokuStone.Black);
        }

        var game = new GomokuGame(Snapshot(board, GomokuRuleSet.Freestyle, GomokuStone.Black));

        var result = game.PlaceStone(new GomokuPosition(
            origin.Row + (2 * rowDirection),
            origin.Column + (2 * columnDirection)));

        Assert.NotNull(result);
        Assert.Equal(GomokuGameState.Finished, game.State);
        Assert.Equal(GomokuStone.Black, game.Winner);
        Assert.Equal(5, Assert.Single(game.WinningLines).Count);
    }

    [Fact]
    public void 自由规则长连获胜而禁手规则阻止黑方长连()
    {
        var board = EmptyBoard();
        for (var column = 3; column <= 7; column++)
        {
            Set(board, 7, column, GomokuStone.Black);
        }

        var free = new GomokuGame(Snapshot(board, GomokuRuleSet.Freestyle, GomokuStone.Black));
        var forbidden = new GomokuGame(Snapshot(board, GomokuRuleSet.Forbidden, GomokuStone.Black));

        Assert.NotNull(free.PlaceStone(new GomokuPosition(7, 8)));
        var validation = forbidden.ValidateMove(new GomokuPosition(7, 8));

        Assert.Equal(GomokuStone.Black, free.Winner);
        Assert.False(validation.IsLegal);
        Assert.True(validation.ForbiddenReasons.HasFlag(GomokuForbiddenReason.Overline));
        Assert.Null(forbidden.PlaceStone(new GomokuPosition(7, 8)));
        Assert.False(forbidden.CanUndo);
    }

    [Fact]
    public void 禁手规则识别双四并保持棋盘不变()
    {
        var board = EmptyBoard();
        foreach (var offset in new[] { -2, -1, 1 })
        {
            Set(board, 7, 7 + offset, GomokuStone.Black);
            Set(board, 7 + offset, 7, GomokuStone.Black);
        }

        var game = new GomokuGame(Snapshot(board, GomokuRuleSet.Forbidden, GomokuStone.Black));

        var validation = game.ValidateMove(new GomokuPosition(7, 7));

        Assert.False(validation.IsLegal);
        Assert.True(validation.ForbiddenReasons.HasFlag(GomokuForbiddenReason.DoubleFour));
        Assert.Null(game.GetStone(new GomokuPosition(7, 7)));
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void 禁手规则识别双三并排除靠近边界的假活三()
    {
        var doubleThree = EmptyBoard();
        Set(doubleThree, 7, 6, GomokuStone.Black);
        Set(doubleThree, 7, 8, GomokuStone.Black);
        Set(doubleThree, 6, 7, GomokuStone.Black);
        Set(doubleThree, 8, 7, GomokuStone.Black);
        var doubleThreeGame = new GomokuGame(Snapshot(doubleThree, GomokuRuleSet.Forbidden, GomokuStone.Black));

        var reasons = doubleThreeGame.ValidateMove(new GomokuPosition(7, 7)).ForbiddenReasons;

        Assert.True(reasons.HasFlag(GomokuForbiddenReason.DoubleThree));

        var fake = EmptyBoard();
        Set(fake, 1, 6, GomokuStone.Black);
        Set(fake, 1, 8, GomokuStone.Black);
        Set(fake, 0, 7, GomokuStone.Black);
        Set(fake, 2, 7, GomokuStone.Black);
        var fakeGame = new GomokuGame(Snapshot(fake, GomokuRuleSet.Forbidden, GomokuStone.Black));

        Assert.True(fakeGame.ValidateMove(new GomokuPosition(1, 7)).IsLegal);
    }

    [Fact]
    public void 一条四和一条三组成四三是合法落点()
    {
        var board = EmptyBoard();
        Set(board, 7, 5, GomokuStone.Black);
        Set(board, 7, 6, GomokuStone.Black);
        Set(board, 7, 8, GomokuStone.Black);
        Set(board, 6, 7, GomokuStone.Black);
        Set(board, 8, 7, GomokuStone.Black);
        var game = new GomokuGame(Snapshot(board, GomokuRuleSet.Forbidden, GomokuStone.Black));

        Assert.True(game.ValidateMove(new GomokuPosition(7, 7)).IsLegal);
    }

    [Fact]
    public void 黑方恰好五连优先于同手产生的其他威胁()
    {
        var board = EmptyBoard();
        foreach (var column in new[] { 3, 4, 5, 6 })
        {
            Set(board, 7, column, GomokuStone.Black);
        }

        Set(board, 6, 7, GomokuStone.Black);
        Set(board, 8, 7, GomokuStone.Black);
        var game = new GomokuGame(Snapshot(board, GomokuRuleSet.Forbidden, GomokuStone.Black));

        var result = game.PlaceStone(new GomokuPosition(7, 7));

        Assert.NotNull(result);
        Assert.Equal(GomokuStone.Black, game.Winner);
    }

    [Fact]
    public void 禁手规则下白方长连仍然获胜()
    {
        var board = EmptyBoard();
        for (var column = 3; column <= 7; column++)
        {
            Set(board, 7, column, GomokuStone.White);
        }

        var game = new GomokuGame(Snapshot(board, GomokuRuleSet.Forbidden, GomokuStone.White));

        game.PlaceStone(new GomokuPosition(7, 8));

        Assert.Equal(GomokuStone.White, game.Winner);
        Assert.Equal(6, Assert.Single(game.WinningLines).Count);
    }

    [Fact]
    public void 一手同时形成横竖五连时保留全部获胜线()
    {
        var board = EmptyBoard();
        foreach (var offset in new[] { -2, -1, 1, 2 })
        {
            Set(board, 7, 7 + offset, GomokuStone.Black);
            Set(board, 7 + offset, 7, GomokuStone.Black);
        }

        var game = new GomokuGame(Snapshot(board, GomokuRuleSet.Forbidden, GomokuStone.Black));

        game.PlaceStone(new GomokuPosition(7, 7));

        Assert.Equal(GomokuStone.Black, game.Winner);
        Assert.Equal(2, game.WinningLines.Count);
        Assert.All(game.WinningLines, line => Assert.Equal(5, line.Count));
    }

    [Fact]
    public void 最后一个空点没有形成五连时满盘平局()
    {
        var board = EmptyBoard();
        for (var row = 0; row < GomokuRules.BoardSize; row++)
        {
            for (var column = 0; column < GomokuRules.BoardSize; column++)
            {
                board[(row * GomokuRules.BoardSize) + column] =
                    ((row + (2 * column)) % 4) < 2 ? GomokuStone.Black : GomokuStone.White;
            }
        }

        board[(14 * GomokuRules.BoardSize) + 14] = null;
        var snapshot = new GomokuGameSnapshot(
            board,
            GomokuRuleSet.Freestyle,
            GomokuStone.White,
            GomokuGameState.Running,
            GomokuRules.CellCount - 1,
            null,
            null);
        var game = new GomokuGame(snapshot);

        game.PlaceStone(new GomokuPosition(14, 14));

        Assert.Equal(GomokuGameState.Finished, game.State);
        Assert.Null(game.Winner);
        Assert.Empty(game.WinningLines);
    }

    [Fact]
    public void 占用越界与禁手均不产生撤销历史且快照数组隔离()
    {
        var game = new GomokuGame();
        game.PlaceStone(new GomokuPosition(3, 4));
        var snapshot = game.CreateSnapshot();
        var board = snapshot.CopyBoard();
        board[0] = GomokuStone.White;

        Assert.Null(game.PlaceStone(new GomokuPosition(3, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => game.PlaceStone(new GomokuPosition(-1, 0)));
        Assert.Null(game.GetStone(new GomokuPosition(0, 0)));
        Assert.Null(snapshot.GetStone(new GomokuPosition(0, 0)));

        game.Undo();
        Assert.Equal(GomokuGameState.Ready, game.State);
        Assert.Equal(0, game.MoveCount);
        Assert.False(game.CanUndo);
    }

    private static GomokuStone?[] EmptyBoard() => new GomokuStone?[GomokuRules.CellCount];

    private static GomokuGameSnapshot Snapshot(
        IEnumerable<GomokuStone?> board,
        GomokuRuleSet ruleSet,
        GomokuStone currentPlayer) =>
        new(board, ruleSet, currentPlayer, GomokuGameState.Running, 0, null, null);

    private static void Set(GomokuStone?[] board, int row, int column, GomokuStone stone) =>
        board[(row * GomokuRules.BoardSize) + column] = stone;
}
