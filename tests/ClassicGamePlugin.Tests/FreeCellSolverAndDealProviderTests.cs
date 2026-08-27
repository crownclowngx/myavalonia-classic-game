using ClassicGamePlugin.Features.FreeCell.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class FreeCellSolverAndDealProviderTests
{
    [Fact]
    public void 求解器找到合法胜利路径且对同一状态保持确定性()
    {
        var snapshot = FreeCellTestData.Snapshot(
            cells:
            [
                FreeCellTestData.Card(12, 13, FreeCellSuit.Spades),
                FreeCellTestData.Card(25, 13, FreeCellSuit.Hearts),
                FreeCellTestData.Card(38, 13, FreeCellSuit.Clubs),
                FreeCellTestData.Card(51, 13, FreeCellSuit.Diamonds),
            ],
            foundations: [12, 12, 12, 12],
            state: FreeCellGameState.Running);
        var solver = new FreeCellSolver();

        var first = solver.Solve(snapshot, 100, CancellationToken.None);
        var second = solver.Solve(snapshot, 100, CancellationToken.None);

        Assert.Equal(FreeCellSolveStatus.Solved, first.Status);
        Assert.Equal(first.Moves, second.Moves);
        var current = snapshot;
        foreach (var move in first.Moves)
        {
            current = Assert.IsType<ValueTuple<FreeCellSnapshot, IReadOnlyList<int>, IReadOnlyList<int>>>(
                FreeCellRules.TryApplyMove(current, move, autoCollect: true)).Item1;
        }

        Assert.Equal(FreeCellGameState.Won, current.State);
    }

    [Fact]
    public void 空状态被证明无解且零节点预算参数被拒绝()
    {
        var solver = new FreeCellSolver();
        var snapshot = FreeCellTestData.Snapshot();

        Assert.Equal(FreeCellSolveStatus.Unsolvable,
            solver.Solve(snapshot, 10, CancellationToken.None).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            solver.Solve(snapshot, 0, CancellationToken.None));
    }

    [Fact]
    public void 求解器响应取消且固定小预算返回节点上限()
    {
        var solver = new FreeCellSolver();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            solver.Solve(FreeCellRules.CreateInitialSnapshot(FreeCellTestData.Deal(), false), 10, cancellation.Token));
        Assert.Equal(FreeCellSolveStatus.NodeLimitReached,
            solver.Solve(FreeCellRules.CreateInitialSnapshot(FreeCellTestData.Deal(), false), 1, CancellationToken.None).Status);
    }

    [Fact]
    public async Task 编号供应器按固定顺序跳过超限候选并返回首个已证明可解牌局()
    {
        var solver = new ScriptedFreeCellSolver(
            FreeCellSolveStatus.NodeLimitReached,
            FreeCellSolveStatus.Unsolvable,
            FreeCellSolveStatus.Solved);
        var provider = new FreeCellDealProvider(solver);

        var deal = await provider.CreateSolvableDealAsync(12345, CancellationToken.None);

        Assert.Equal(2, deal.CandidateIndex);
        Assert.Equal(3, solver.CallCount);
        Assert.Equal(FreeCellDealProvider.CreateCandidate(12345, 2).Deck, deal.Deck);
    }

    [Fact]
    public async Task 编号供应器在限定候选全部失败时拒绝未证明牌局()
    {
        var solver = new ScriptedFreeCellSolver(
            Enumerable.Repeat(FreeCellSolveStatus.NodeLimitReached, FreeCellDealProvider.MaximumCandidateCount).ToArray());
        var provider = new FreeCellDealProvider(solver);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateSolvableDealAsync(7, CancellationToken.None));

        Assert.Contains("均未能", exception.Message, StringComparison.Ordinal);
        Assert.Equal(FreeCellDealProvider.MaximumCandidateCount, solver.CallCount);
    }

    [Fact]
    public async Task 默认编号使用真实求解器生成经过证明的稳定牌局()
    {
        var provider = new FreeCellDealProvider();

        var first = await provider.CreateSolvableDealAsync(1, CancellationToken.None);
        var second = await provider.CreateSolvableDealAsync(1, CancellationToken.None);

        Assert.Equal(first.CandidateIndex, second.CandidateIndex);
        Assert.Equal(first.Deck, second.Deck);
    }
}
