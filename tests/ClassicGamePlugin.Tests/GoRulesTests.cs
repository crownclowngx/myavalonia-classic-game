using ClassicGamePlugin.Features.Go.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GoRulesTests
{
    [Fact]
    public void 初始棋盘固定十九路且黑方先手()
    {
        var snapshot = GoRules.CreateInitialSnapshot();

        Assert.Equal(19, GoRules.BoardSize);
        Assert.Equal(361, GoRules.CellCount);
        Assert.Equal(GoStone.Black, snapshot.CurrentPlayer);
        Assert.Equal(GoGameState.Ready, snapshot.State);
        Assert.Equal(361, snapshot.CopyBoard().Count(stone => stone is null));
    }

    [Fact]
    public void 连接棋组共享不同气点且边角不会越界重复计数()
    {
        var snapshot = Snapshot(
            Board((new GoPosition(0, 0), GoStone.Black), (new GoPosition(0, 1), GoStone.Black)),
            GoStone.White);

        var group = GoRules.GetGroup(snapshot, new GoPosition(0, 0));

        Assert.Equal([new GoPosition(0, 0), new GoPosition(0, 1)], group);
        Assert.Equal(3, GoRules.CountLiberties(snapshot, new GoPosition(0, 0)));
    }

    [Fact]
    public void 落子可以提取边角单子并一次更新提子数()
    {
        var snapshot = Snapshot(
            Board((new GoPosition(0, 0), GoStone.White), (new GoPosition(0, 1), GoStone.Black)),
            GoStone.Black);

        var result = GoRules.TryApplyMove(snapshot, new GoPosition(1, 0), Seen(snapshot));

        Assert.NotNull(result);
        Assert.Equal([new GoPosition(0, 0)], result.CapturedPositions);
        Assert.Null(result.After.GetStone(new GoPosition(0, 0)));
        Assert.Equal(1, result.After.BlackCaptures);
        Assert.Equal(GoStone.White, result.After.CurrentPlayer);
    }

    [Fact]
    public void 一次落子会完整提取没有气的相连多子棋组()
    {
        var snapshot = Snapshot(
            Board(
                (new GoPosition(1, 1), GoStone.White),
                (new GoPosition(1, 2), GoStone.White),
                (new GoPosition(0, 1), GoStone.Black),
                (new GoPosition(0, 2), GoStone.Black),
                (new GoPosition(1, 0), GoStone.Black),
                (new GoPosition(1, 3), GoStone.Black),
                (new GoPosition(2, 1), GoStone.Black)),
            GoStone.Black);

        var result = GoRules.TryApplyMove(snapshot, new GoPosition(2, 2), Seen(snapshot));

        Assert.NotNull(result);
        Assert.Equal(2, result.CapturedPositions.Count);
        Assert.All(result.CapturedPositions, position => Assert.Null(result.After.GetStone(position)));
    }

    [Fact]
    public void 真自杀会被拒绝且原快照保持不变()
    {
        var snapshot = Snapshot(
            Board(
                (new GoPosition(0, 1), GoStone.White),
                (new GoPosition(1, 0), GoStone.White),
                (new GoPosition(1, 2), GoStone.White),
                (new GoPosition(2, 1), GoStone.White)),
            GoStone.Black);
        var beforeKey = snapshot.BoardKey;

        var validation = GoRules.ValidateMove(snapshot, new GoPosition(1, 1), Seen(snapshot));

        Assert.False(validation.IsLegal);
        Assert.Equal(GoMoveInvalidReason.Suicide, validation.Reason);
        Assert.Equal(beforeKey, snapshot.BoardKey);
        Assert.Null(snapshot.GetStone(new GoPosition(1, 1)));
    }

    [Fact]
    public void 看似无气的落子若先提走对方棋子则合法()
    {
        var snapshot = Snapshot(
            Board(
                (new GoPosition(0, 1), GoStone.White),
                (new GoPosition(1, 0), GoStone.White),
                (new GoPosition(1, 2), GoStone.White),
                (new GoPosition(2, 1), GoStone.White),
                (new GoPosition(0, 0), GoStone.Black),
                (new GoPosition(0, 2), GoStone.Black)),
            GoStone.Black);

        var result = GoRules.TryApplyMove(snapshot, new GoPosition(1, 1), Seen(snapshot));

        Assert.NotNull(result);
        Assert.Contains(new GoPosition(0, 1), result.CapturedPositions);
        Assert.Equal(GoStone.Black, result.After.GetStone(new GoPosition(1, 1)));
        Assert.True(GoRules.CountLiberties(result.After, new GoPosition(1, 1)) > 0);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(19, 0)]
    public void 越界落点返回稳定原因且不会抛出(int row, int column)
    {
        var snapshot = GoRules.CreateInitialSnapshot();

        var validation = GoRules.ValidateMove(snapshot, new GoPosition(row, column), Seen(snapshot));

        Assert.False(validation.IsLegal);
        Assert.Equal(GoMoveInvalidReason.OutsideBoard, validation.Reason);
    }

    [Fact]
    public void 已占用点和非行棋阶段都会被拒绝()
    {
        var occupied = Snapshot(Board((new GoPosition(2, 2), GoStone.Black)), GoStone.White);
        var scoring = new GoGameSnapshot(
            new GoStone?[GoRules.CellCount],
            GoStone.Black,
            GoGameState.Scoring,
            0,
            2,
            2,
            0,
            0,
            null);

        Assert.Equal(
            GoMoveInvalidReason.Occupied,
            GoRules.ValidateMove(occupied, new GoPosition(2, 2), Seen(occupied)).Reason);
        Assert.Equal(
            GoMoveInvalidReason.WrongPhase,
            GoRules.ValidateMove(scoring, new GoPosition(2, 2), Seen(scoring)).Reason);
    }

    [Fact]
    public void 新棋盘若重现历史位置会触发位置全局同形禁着()
    {
        var snapshot = GoRules.CreateInitialSnapshot();
        var repeatedBoard = snapshot.CopyBoard();
        repeatedBoard[GoRules.IndexOf(new GoPosition(3, 3))] = GoStone.Black;
        var seen = new HashSet<string> { snapshot.BoardKey, GoRules.CreateBoardKey(repeatedBoard) };

        var validation = GoRules.ValidateMove(snapshot, new GoPosition(3, 3), seen);

        Assert.False(validation.IsLegal);
        Assert.Equal(GoMoveInvalidReason.Superko, validation.Reason);
    }

    [Fact]
    public void 快照复制不会共享棋盘与死子标记()
    {
        var snapshot = Snapshot(Board((new GoPosition(4, 4), GoStone.Black)), GoStone.White);
        var boardCopy = snapshot.CopyBoard();
        boardCopy[GoRules.IndexOf(new GoPosition(4, 4))] = null;

        Assert.Equal(GoStone.Black, snapshot.GetStone(new GoPosition(4, 4)));
        Assert.Equal(snapshot.BoardKey, snapshot.Clone().BoardKey);
    }

    internal static GoStone?[] Board(params (GoPosition Position, GoStone Stone)[] stones)
    {
        var board = new GoStone?[GoRules.CellCount];
        foreach (var item in stones)
        {
            board[GoRules.IndexOf(item.Position)] = item.Stone;
        }

        return board;
    }

    internal static GoGameSnapshot Snapshot(GoStone?[] board, GoStone currentPlayer) =>
        new(board, currentPlayer, GoGameState.Playing, 0, 0, 0, 0, 0, null);

    internal static HashSet<string> Seen(GoGameSnapshot snapshot) => [snapshot.BoardKey];
}
