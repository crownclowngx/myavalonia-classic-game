using ClassicGamePlugin.Features.Xiangqi.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class XiangqiAiStrategyTests
{
    [Fact]
    public void 简单策略使用固定随机返回合法着且不修改快照()
    {
        var snapshot = XiangqiRules.CreateInitialSnapshot();
        var before = snapshot.CopyBoard();

        var move = new EasyXiangqiMoveStrategy(new Random(42))
            .SelectMove(snapshot, XiangqiSide.Red, CancellationToken.None);

        Assert.NotNull(move);
        Assert.True(XiangqiRules.ValidateMove(snapshot, move.Value).IsLegal);
        Assert.Equal(before, snapshot.CopyBoard());
    }

    [Fact]
    public void 简单策略优先一步将死()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals(blockCenter: false);
        XiangqiTestFactory.Set(board, 1, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        XiangqiTestFactory.Set(board, 2, 4, XiangqiSide.Red, XiangqiPieceType.Chariot);
        XiangqiTestFactory.Set(board, 2, 2, XiangqiSide.Red, XiangqiPieceType.Horse);
        XiangqiTestFactory.Set(board, 2, 6, XiangqiSide.Red, XiangqiPieceType.Horse);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);

        var move = new EasyXiangqiMoveStrategy(new Random(1))
            .SelectMove(snapshot, XiangqiSide.Red, CancellationToken.None);

        Assert.NotNull(move);
        var result = XiangqiRules.TryApplyMove(snapshot, move.Value);
        Assert.Equal(XiangqiSide.Red, result?.After.Winner);
        Assert.Equal(XiangqiTerminationReason.Checkmate, result?.After.TerminationReason);
    }

    [Fact]
    public void 搜索策略在确定节点预算内保留合法结果并响应取消()
    {
        var snapshot = XiangqiRules.CreateInitialSnapshot();
        var strategy = new SearchXiangqiMoveStrategy(
            TimeSpan.FromSeconds(5), maximumDepth: 4, nodeLimit: 8);

        var move = strategy.SelectMove(snapshot, XiangqiSide.Red, CancellationToken.None);

        Assert.NotNull(move);
        Assert.True(XiangqiRules.ValidateMove(snapshot, move.Value).IsLegal);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            strategy.SelectMove(snapshot, XiangqiSide.Red, cancellation.Token));
    }

    [Fact]
    public void 局面评价以真实强子得失为主且不修改输入()
    {
        var board = XiangqiTestFactory.EmptyBoardWithGenerals();
        XiangqiTestFactory.Set(board, 8, 0, XiangqiSide.Red, XiangqiPieceType.Chariot);
        XiangqiTestFactory.Set(board, 1, 0, XiangqiSide.Black, XiangqiPieceType.Horse);
        var snapshot = XiangqiTestFactory.Snapshot(board, XiangqiSide.Red);
        var before = snapshot.CopyBoard();

        var red = XiangqiPositionEvaluator.Evaluate(snapshot, XiangqiSide.Red);
        var black = XiangqiPositionEvaluator.Evaluate(snapshot, XiangqiSide.Black);

        Assert.True(red > 0);
        Assert.True(black < 0);
        Assert.Equal(before, snapshot.CopyBoard());
    }
}
