using ClassicGamePlugin.Features.Gomoku.Domain;
using ClassicGamePlugin.Features.Gomoku.ViewModels;

namespace ClassicGamePlugin.Tests;

internal sealed class FirstLegalGomokuMoveStrategy : IGomokuMoveStrategy
{
    public GomokuPosition? SelectMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var moves = GomokuRules.GetCandidateMoves(snapshot, player);
        return moves.Count == 0 ? null : moves[0];
    }
}

internal sealed class BlockingGomokuMoveStrategy : IGomokuMoveStrategy, IDisposable
{
    private readonly ManualResetEventSlim _release = new(initialState: false);
    internal ManualResetEventSlim Started { get; } = new(initialState: false);

    public GomokuPosition? SelectMove(
        GomokuGameSnapshot snapshot,
        GomokuStone player,
        CancellationToken cancellationToken)
    {
        Started.Set();
        _release.Wait(cancellationToken);
        var moves = GomokuRules.GetCandidateMoves(snapshot, player);
        return moves.Count == 0 ? null : moves[0];
    }

    public void Dispose()
    {
        _release.Dispose();
        Started.Dispose();
    }
}

internal static class GomokuTestStrategies
{
    internal static IReadOnlyDictionary<GomokuAiDifficulty, IGomokuMoveStrategy> CreateFirstLegal()
    {
        var strategy = new FirstLegalGomokuMoveStrategy();
        return Enum.GetValues<GomokuAiDifficulty>()
            .ToDictionary(difficulty => difficulty, _ => (IGomokuMoveStrategy)strategy);
    }
}
