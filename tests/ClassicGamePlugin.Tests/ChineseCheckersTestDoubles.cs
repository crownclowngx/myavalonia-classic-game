using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

internal sealed class FirstLegalChineseCheckersMoveStrategy : IChineseCheckersMoveStrategy
{
    public ChineseCheckersMove? SelectMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var view = snapshot.CurrentSide == side ? snapshot : ChineseCheckersRules.WithCurrentSide(snapshot, side);
        return ChineseCheckersRules.GetLegalMoves(view).FirstOrDefault();
    }
}

internal sealed class BlockingChineseCheckersMoveStrategy : IChineseCheckersMoveStrategy, IDisposable
{
    private readonly ManualResetEventSlim _release = new(initialState: false);
    internal ManualResetEventSlim Started { get; } = new(initialState: false);

    public ChineseCheckersMove? SelectMove(
        ChineseCheckersGameSnapshot snapshot,
        ChineseCheckersSide side,
        CancellationToken cancellationToken)
    {
        Started.Set();
        _release.Wait(cancellationToken);
        return ChineseCheckersRules.GetLegalMoves(snapshot).FirstOrDefault();
    }

    internal void Release() => _release.Set();

    public void Dispose()
    {
        _release.Dispose();
        Started.Dispose();
    }
}

internal static class ChineseCheckersTestData
{
    internal static IReadOnlyDictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy> FirstLegalStrategies()
    {
        var strategy = new FirstLegalChineseCheckersMoveStrategy();
        return new Dictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy>
        {
            [ChineseCheckersAiDifficulty.Easy] = strategy,
            [ChineseCheckersAiDifficulty.Medium] = strategy,
            [ChineseCheckersAiDifficulty.Hard] = strategy,
        };
    }

    internal static IReadOnlyDictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy> All(
        IChineseCheckersMoveStrategy strategy) =>
        new Dictionary<ChineseCheckersAiDifficulty, IChineseCheckersMoveStrategy>
        {
            [ChineseCheckersAiDifficulty.Easy] = strategy,
            [ChineseCheckersAiDifficulty.Medium] = strategy,
            [ChineseCheckersAiDifficulty.Hard] = strategy,
        };

    internal static ChineseCheckersGameSnapshot Snapshot(
        ChineseCheckersSide currentSide,
        params (ChineseCheckersPosition Position, ChineseCheckersSide Side)[] pieces)
    {
        var board = new ChineseCheckersSide?[ChineseCheckersRules.CellCount];
        foreach (var piece in pieces)
        {
            Assert.True(ChineseCheckersRules.TryGetIndex(piece.Position, out var index));
            board[index] = piece.Side;
        }

        return new ChineseCheckersGameSnapshot(
            board,
            currentSide,
            ChineseCheckersGameState.Running,
            0,
            null,
            null,
            null);
    }
}
