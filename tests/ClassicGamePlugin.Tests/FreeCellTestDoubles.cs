using ClassicGamePlugin.Features.FreeCell.Domain;

namespace ClassicGamePlugin.Tests;

internal static class FreeCellTestData
{
    internal static FreeCellCard Card(int id, int rank, FreeCellSuit suit = FreeCellSuit.Spades) =>
        new(id, suit, rank);

    internal static FreeCellSnapshot Snapshot(
        IEnumerable<IEnumerable<FreeCellCard>>? tableaus = null,
        IEnumerable<FreeCellCard?>? cells = null,
        IEnumerable<int>? foundations = null,
        int moveCount = 0,
        FreeCellGameState state = FreeCellGameState.Ready) =>
        new(
            PadColumns(tableaus),
            (cells ?? Array.Empty<FreeCellCard?>()).Concat(new FreeCellCard?[4]).Take(4),
            (foundations ?? Array.Empty<int>()).Concat(new int[4]).Take(4),
            moveCount,
            state,
            42,
            0);

    internal static FreeCellDeal Deal(int number = 42) =>
        FreeCellDealProvider.CreateCandidate(number, 0);

    private static IEnumerable<IEnumerable<FreeCellCard>> PadColumns(
        IEnumerable<IEnumerable<FreeCellCard>>? columns) =>
        (columns ?? Array.Empty<IEnumerable<FreeCellCard>>())
        .Select(column => column.ToArray())
        .Concat(Enumerable.Range(0, 8).Select(_ => Array.Empty<FreeCellCard>()))
        .Take(8);
}

internal sealed class ScriptedFreeCellSolver(params FreeCellSolveStatus[] statuses) : IFreeCellSolver
{
    private readonly Queue<FreeCellSolveStatus> _statuses = new(statuses);
    internal int CallCount { get; private set; }

    public FreeCellSolveResult Solve(
        FreeCellSnapshot snapshot,
        int nodeLimit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        var status = _statuses.Count == 0 ? FreeCellSolveStatus.Solved : _statuses.Dequeue();
        var move = FreeCellRules.EnumerateLegalMoves(snapshot, false).FirstOrDefault();
        return new FreeCellSolveResult(
            status,
            status == FreeCellSolveStatus.Solved && move != default ? [move] : [],
            1);
    }
}

internal sealed class FixedFreeCellDealProvider(FreeCellDeal deal) : IFreeCellDealProvider
{
    internal Exception? Exception { get; init; }
    internal int CallCount { get; private set; }

    public Task<FreeCellDeal> CreateSolvableDealAsync(int number, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        return Exception is null
            ? Task.FromResult(deal with { Number = number })
            : Task.FromException<FreeCellDeal>(Exception);
    }
}

internal sealed class BlockingFreeCellDealProvider : IFreeCellDealProvider
{
    public async Task<FreeCellDeal> CreateSolvableDealAsync(int number, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("取消后不应继续返回牌局。");
    }
}

internal sealed class QueuedFreeCellDealProvider : IFreeCellDealProvider
{
    private readonly List<(int Number, TaskCompletionSource<FreeCellDeal> Completion)> _requests = [];
    internal IReadOnlyList<(int Number, TaskCompletionSource<FreeCellDeal> Completion)> Requests => _requests;

    public Task<FreeCellDeal> CreateSolvableDealAsync(int number, CancellationToken cancellationToken)
    {
        // 故意模拟一个不响应取消的外部协作者，用来证明 ViewModel 的版本校验仍能隔离过期结果。
        var completion = new TaskCompletionSource<FreeCellDeal>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests.Add((number, completion));
        return completion.Task;
    }
}
