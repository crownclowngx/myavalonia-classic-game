using ClassicGamePlugin.Features.Reversi.Domain;

namespace ClassicGamePlugin.Tests;

internal sealed class FirstLegalReversiMoveStrategy : IReversiMoveStrategy
{
    public ReversiPosition? SelectMove(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var moves = ReversiRules.GetLegalMoves(snapshot, player);
        return moves.Count == 0 ? null : moves[0];
    }
}

internal sealed class BlockingReversiMoveStrategy : IReversiMoveStrategy, IDisposable
{
    private readonly ManualResetEventSlim _release = new(initialState: false);
    internal ManualResetEventSlim Started { get; } = new(initialState: false);

    public ReversiPosition? SelectMove(
        ReversiGameSnapshot snapshot,
        ReversiDiscColor player,
        CancellationToken cancellationToken)
    {
        Started.Set();
        _release.Wait(cancellationToken);
        var moves = ReversiRules.GetLegalMoves(snapshot, player);
        return moves.Count == 0 ? null : moves[0];
    }

    internal void Release() => _release.Set();

    public void Dispose()
    {
        _release.Dispose();
        Started.Dispose();
    }
}

internal static class ReversiTestStrategies
{
    internal static IReadOnlyDictionary<ReversiAiDifficulty, IReversiMoveStrategy> CreateFirstLegal()
    {
        var strategy = new FirstLegalReversiMoveStrategy();
        return new Dictionary<ReversiAiDifficulty, IReversiMoveStrategy>
        {
            [ReversiAiDifficulty.Easy] = strategy,
            [ReversiAiDifficulty.Medium] = strategy,
            [ReversiAiDifficulty.Hard] = strategy,
        };
    }
}
