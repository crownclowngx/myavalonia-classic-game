using ClassicGamePlugin.Features.Reversi.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ReversiAiStrategyTests
{
    [Fact]
    public void 简单策略返回合法位置且不修改输入快照()
    {
        var snapshot = ReversiRules.CreateInitialSnapshot();
        var before = snapshot.CopyBoard();
        var strategy = new RandomReversiMoveStrategy(new Random(42));

        var move = strategy.SelectMove(snapshot, ReversiDiscColor.Black, CancellationToken.None);

        Assert.Contains(Assert.IsType<ReversiPosition>(move), ReversiRules.GetLegalMoves(snapshot, ReversiDiscColor.Black));
        Assert.Equal(before, snapshot.CopyBoard());
    }

    [Fact]
    public void 稳定策略优先选择角位并按行列打破同分()
    {
        var board = new ReversiDiscColor?[64];
        Set(board, 0, 1, ReversiDiscColor.White);
        Set(board, 0, 2, ReversiDiscColor.Black);
        Set(board, 7, 6, ReversiDiscColor.White);
        Set(board, 7, 5, ReversiDiscColor.Black);
        Set(board, 3, 2, ReversiDiscColor.White);
        Set(board, 3, 3, ReversiDiscColor.Black);
        var snapshot = new ReversiGameSnapshot(
            board,
            ReversiDiscColor.Black,
            ReversiGameState.Running,
            0,
            null);

        var move = new StableReversiMoveStrategy().SelectMove(
            snapshot,
            ReversiDiscColor.Black,
            CancellationToken.None);

        Assert.Equal(new ReversiPosition(0, 0), move);
    }

    [Fact]
    public void 困难策略在标准开局返回合法位置且不修改快照()
    {
        var snapshot = ReversiRules.CreateInitialSnapshot();
        var before = snapshot.CopyBoard();

        var move = new HardReversiMoveStrategy().SelectMove(
            snapshot,
            ReversiDiscColor.Black,
            CancellationToken.None);

        Assert.Contains(Assert.IsType<ReversiPosition>(move), ReversiRules.GetLegalMoves(snapshot, ReversiDiscColor.Black));
        Assert.Equal(before, snapshot.CopyBoard());
    }

    [Fact]
    public void 困难策略响应已经取消的搜索请求()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new HardReversiMoveStrategy().SelectMove(
                ReversiRules.CreateInitialSnapshot(),
                ReversiDiscColor.Black,
                cancellation.Token));
    }

    [Fact]
    public void 困难策略可以在强制跳过残局中选择唯一终局胜着()
    {
        var board = Enumerable.Repeat<ReversiDiscColor?>(ReversiDiscColor.Black, 64).ToArray();
        board[63] = null;
        board[62] = ReversiDiscColor.White;
        var snapshot = new ReversiGameSnapshot(
            board,
            ReversiDiscColor.Black,
            ReversiGameState.Running,
            0,
            null);

        var move = new HardReversiMoveStrategy().SelectMove(
            snapshot,
            ReversiDiscColor.Black,
            CancellationToken.None);
        var result = ReversiRules.TryApplyMove(snapshot, ReversiDiscColor.Black, Assert.IsType<ReversiPosition>(move));

        Assert.NotNull(result);
        Assert.Equal(new ReversiPosition(7, 7), move);
        Assert.Equal(ReversiGameState.Finished, result.After.State);
        Assert.Equal(64, result.After.BlackCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void 三级生产策略对有棋可下局面均返回合法位置(int difficulty)
    {
        IReversiMoveStrategy strategy = (ReversiAiDifficulty)difficulty switch
        {
            ReversiAiDifficulty.Easy => new RandomReversiMoveStrategy(new Random(1)),
            ReversiAiDifficulty.Medium => new StableReversiMoveStrategy(),
            ReversiAiDifficulty.Hard => new HardReversiMoveStrategy(),
            _ => throw new InvalidOperationException(),
        };
        var snapshot = ReversiRules.CreateInitialSnapshot();

        var move = strategy.SelectMove(snapshot, snapshot.CurrentPlayer, CancellationToken.None);

        Assert.Contains(Assert.IsType<ReversiPosition>(move), ReversiRules.GetLegalMoves(snapshot, snapshot.CurrentPlayer));
    }

    private static void Set(
        ReversiDiscColor?[] board,
        int row,
        int column,
        ReversiDiscColor color) => board[(row * 8) + column] = color;
}
