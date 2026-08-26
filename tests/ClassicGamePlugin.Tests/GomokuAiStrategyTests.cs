using ClassicGamePlugin.Features.Gomoku.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GomokuAiStrategyTests
{
    [Fact]
    public void 简单策略使用固定随机返回合法邻域点且不修改快照()
    {
        var snapshot = GomokuRules.CreateInitialSnapshot(GomokuRuleSet.Freestyle);
        var before = snapshot.CopyBoard();
        var strategy = new RandomGomokuMoveStrategy(new Random(42));

        var move = strategy.SelectMove(snapshot, GomokuStone.Black, CancellationToken.None);

        Assert.Equal(new GomokuPosition(7, 7), move);
        Assert.Equal(before, snapshot.CopyBoard());
    }

    [Fact]
    public void 中等策略优先立即获胜并能阻挡对方下一手获胜()
    {
        var winning = SnapshotWithLine(GomokuStone.Black, GomokuStone.Black);
        var blocking = SnapshotWithLine(GomokuStone.White, GomokuStone.Black);
        var strategy = new StableGomokuMoveStrategy();

        var winningMove = strategy.SelectMove(winning, GomokuStone.Black, CancellationToken.None);
        var blockingMove = strategy.SelectMove(blocking, GomokuStone.Black, CancellationToken.None);

        Assert.Contains(winningMove, new GomokuPosition?[] { new(7, 4), new(7, 9) });
        Assert.Contains(blockingMove, new GomokuPosition?[] { new(7, 4), new(7, 9) });
    }

    [Fact]
    public void 三级策略在禁手规则下均只返回合法点()
    {
        var snapshot = GomokuRules.CreateInitialSnapshot(GomokuRuleSet.Forbidden);
        IGomokuMoveStrategy[] strategies =
        [
            new RandomGomokuMoveStrategy(new Random(1)),
            new StableGomokuMoveStrategy(),
            new HardGomokuMoveStrategy(TimeSpan.FromSeconds(1), maximumDepth: 1, nodeLimit: 100),
        ];

        foreach (var strategy in strategies)
        {
            var move = Assert.IsType<GomokuPosition>(
                strategy.SelectMove(snapshot, GomokuStone.Black, CancellationToken.None));
            Assert.True(GomokuRules.ValidateMove(snapshot, GomokuStone.Black, move).IsLegal);
        }
    }

    [Fact]
    public void 困难策略响应取消并在确定节点预算内保留合法结果()
    {
        var snapshot = GomokuRules.CreateInitialSnapshot(GomokuRuleSet.Freestyle);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            new HardGomokuMoveStrategy().SelectMove(snapshot, GomokuStone.Black, cancellation.Token));

        var move = new HardGomokuMoveStrategy(
            TimeSpan.FromSeconds(5),
            maximumDepth: 4,
            nodeLimit: 5).SelectMove(snapshot, GomokuStone.Black, CancellationToken.None);

        Assert.Equal(new GomokuPosition(7, 7), move);
    }

    [Fact]
    public void 困难策略在浅层确定搜索中选择立即胜着()
    {
        var snapshot = SnapshotWithLine(GomokuStone.Black, GomokuStone.Black);
        var strategy = new HardGomokuMoveStrategy(
            TimeSpan.FromSeconds(2),
            maximumDepth: 2,
            nodeLimit: 2_000);

        var move = strategy.SelectMove(snapshot, GomokuStone.Black, CancellationToken.None);

        Assert.Contains(move, new GomokuPosition?[] { new(7, 4), new(7, 9) });
    }

    private static GomokuGameSnapshot SnapshotWithLine(GomokuStone lineStone, GomokuStone current)
    {
        var board = new GomokuStone?[GomokuRules.CellCount];
        for (var column = 5; column <= 8; column++)
        {
            board[(7 * GomokuRules.BoardSize) + column] = lineStone;
        }

        return new GomokuGameSnapshot(
            board,
            GomokuRuleSet.Freestyle,
            current,
            GomokuGameState.Running,
            4,
            null,
            null);
    }
}
