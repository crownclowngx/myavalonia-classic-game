using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ChineseCheckersAiStrategyTests
{
    [Fact]
    public void 简单与中等策略返回合法着法且不修改输入快照()
    {
        var snapshot = ChineseCheckersRules.CreateInitialSnapshot();
        var before = snapshot.CopyBoard();
        IChineseCheckersMoveStrategy[] strategies =
        [
            new RandomChineseCheckersMoveStrategy(new Random(42)),
            new StableChineseCheckersMoveStrategy(),
        ];

        foreach (var strategy in strategies)
        {
            var move = Assert.IsType<ChineseCheckersMove>(
                strategy.SelectMove(snapshot, ChineseCheckersSide.Blue, CancellationToken.None));
            Assert.Contains(ChineseCheckersRules.GetLegalMoves(snapshot), legal =>
                legal.From == move.From && legal.To == move.To);
            Assert.Equal(before, snapshot.CopyBoard());
        }
    }

    [Fact]
    public void 困难策略响应取消并在节点预算到达时保留合法兜底结果()
    {
        var snapshot = ChineseCheckersRules.CreateInitialSnapshot();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            new HardChineseCheckersMoveStrategy().SelectMove(
                snapshot, ChineseCheckersSide.Blue, cancellation.Token));

        var move = Assert.IsType<ChineseCheckersMove>(
            new HardChineseCheckersMoveStrategy(TimeSpan.FromSeconds(1), maximumDepth: 4, nodeLimit: 8)
                .SelectMove(snapshot, ChineseCheckersSide.Blue, CancellationToken.None));

        Assert.Contains(ChineseCheckersRules.GetLegalMoves(snapshot), legal =>
            legal.From == move.From && legal.To == move.To);
    }

    [Fact]
    public void 中等策略在强制撤营局面必定选择撤营着法()
    {
        var home = ChineseCheckersRules.BlueHome;
        var evacuee = home.First(position => ChineseCheckersRules.Directions.Any(direction =>
            ChineseCheckersRules.TryGetIndex(position.Add(direction), out _) && !home.Contains(position.Add(direction))));
        var intruder = home.First(position => position != evacuee);
        var snapshot = ChineseCheckersTestData.Snapshot(
            ChineseCheckersSide.Blue,
            (evacuee, ChineseCheckersSide.Blue),
            (intruder, ChineseCheckersSide.Red));

        var move = Assert.IsType<ChineseCheckersMove>(
            new StableChineseCheckersMoveStrategy().SelectMove(
                snapshot, ChineseCheckersSide.Blue, CancellationToken.None));

        Assert.Contains(move.From, home);
        Assert.DoesNotContain(move.To, home);
    }
}
