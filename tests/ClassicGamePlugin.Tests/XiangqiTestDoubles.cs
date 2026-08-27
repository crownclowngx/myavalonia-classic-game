using ClassicGamePlugin.Features.Xiangqi.Domain;

namespace ClassicGamePlugin.Tests;

internal static class XiangqiTestFactory
{
    internal static XiangqiPiece?[] EmptyBoardWithGenerals(bool blockCenter = true)
    {
        var board = new XiangqiPiece?[XiangqiRules.CellCount];
        Set(board, 9, 4, XiangqiSide.Red, XiangqiPieceType.General);
        Set(board, 0, 4, XiangqiSide.Black, XiangqiPieceType.General);
        if (blockCenter)
        {
            Set(board, 5, 4, XiangqiSide.Red, XiangqiPieceType.Soldier);
        }

        return board;
    }

    internal static XiangqiGameSnapshot Snapshot(
        XiangqiPiece?[] board,
        XiangqiSide currentSide,
        int moveCount = 0,
        int noCapturePlyCount = 0,
        IEnumerable<XiangqiPositionRecord>? history = null)
    {
        var actualHistory = history?.ToArray();
        if (actualHistory is null)
        {
            actualHistory =
            [
                new XiangqiPositionRecord(
                    XiangqiRules.ComputePositionKey(board, currentSide),
                    XiangqiRules.CreatePositionSignature(board, currentSide),
                    currentSide,
                    null,
                    false),
            ];
        }

        return new XiangqiGameSnapshot(
            board,
            currentSide,
            moveCount == 0 ? XiangqiGameState.Ready : XiangqiGameState.Running,
            moveCount,
            null,
            null,
            null,
            noCapturePlyCount,
            actualHistory);
    }

    internal static void Set(
        XiangqiPiece?[] board,
        int row,
        int column,
        XiangqiSide side,
        XiangqiPieceType type) =>
        board[(row * XiangqiRules.ColumnCount) + column] = new XiangqiPiece(side, type);

    internal static IReadOnlyDictionary<XiangqiAiDifficulty, IXiangqiMoveStrategy> FirstLegalStrategies() =>
        Enum.GetValues<XiangqiAiDifficulty>()
            .ToDictionary(difficulty => difficulty, _ => (IXiangqiMoveStrategy)new FirstLegalXiangqiStrategy());
}

internal sealed class FirstLegalXiangqiStrategy : IXiangqiMoveStrategy
{
    public XiangqiMove? SelectMove(
        XiangqiGameSnapshot snapshot,
        XiangqiSide side,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var moves = XiangqiRules.GetLegalMoves(snapshot, side);
        return moves.Count == 0 ? null : moves[0];
    }
}

internal sealed class BlockingXiangqiMoveStrategy : IXiangqiMoveStrategy, IDisposable
{
    private readonly ManualResetEventSlim _release = new(false);
    internal ManualResetEventSlim Started { get; } = new(false);

    public XiangqiMove? SelectMove(
        XiangqiGameSnapshot snapshot,
        XiangqiSide side,
        CancellationToken cancellationToken)
    {
        Started.Set();
        _release.Wait(cancellationToken);
        var moves = XiangqiRules.GetLegalMoves(snapshot, side);
        return moves.Count == 0 ? null : moves[0];
    }

    public void Dispose()
    {
        _release.Set();
        _release.Dispose();
        Started.Dispose();
    }
}
