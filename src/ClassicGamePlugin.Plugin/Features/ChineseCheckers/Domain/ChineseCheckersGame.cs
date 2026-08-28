namespace ClassicGamePlugin.Features.ChineseCheckers.Domain;

/// <summary>持有一局中国跳棋及完整快照历史；模式、AI、计时和展示均由上层编排。</summary>
internal sealed class ChineseCheckersGame
{
    private readonly Stack<ChineseCheckersGameSnapshot> _history = [];
    private ChineseCheckersGameSnapshot _snapshot;

    internal ChineseCheckersGame()
        : this(ChineseCheckersRules.CreateInitialSnapshot())
    {
    }

    internal ChineseCheckersGame(ChineseCheckersGameSnapshot snapshot) =>
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

    internal ChineseCheckersGameSnapshot Snapshot => _snapshot;
    internal ChineseCheckersSide CurrentSide => _snapshot.CurrentSide;
    internal ChineseCheckersGameState State => _snapshot.State;
    internal int MoveCount => _snapshot.MoveCount;
    internal bool CanUndo => _history.Count > 0;

    internal IReadOnlyList<ChineseCheckersMove> GetLegalMoves() => ChineseCheckersRules.GetLegalMoves(_snapshot);

    internal ChineseCheckersMoveResult? Move(ChineseCheckersPosition from, ChineseCheckersPosition to)
    {
        var result = ChineseCheckersRules.TryApplyMove(_snapshot, from, to);
        if (result is null)
        {
            return null;
        }

        _history.Push(_snapshot);
        _snapshot = result.After;
        return result;
    }

    internal ChineseCheckersGameSnapshot? Undo()
    {
        if (!_history.TryPop(out var previous))
        {
            return null;
        }

        _snapshot = previous;
        return previous;
    }

    internal bool HasMoveBy(ChineseCheckersSide side) =>
        _history.Any(snapshot => snapshot.CurrentSide == side);
}
