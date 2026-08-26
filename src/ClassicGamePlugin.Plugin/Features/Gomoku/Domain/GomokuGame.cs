namespace ClassicGamePlugin.Features.Gomoku.Domain;

/// <summary>
/// 持有当前不可变快照并管理单步撤销历史。模式、人机暂停、计时和文案属于页面编排，
/// 因而不会进入领域对象。
/// </summary>
internal sealed class GomokuGame
{
    private readonly Stack<GomokuGameSnapshot> _history = new();
    private GomokuGameSnapshot _current;

    internal GomokuGame(GomokuRuleSet ruleSet = GomokuRuleSet.Freestyle) =>
        _current = GomokuRules.CreateInitialSnapshot(ruleSet);

    internal GomokuGame(GomokuGameSnapshot snapshot) =>
        _current = snapshot?.Clone() ?? throw new ArgumentNullException(nameof(snapshot));

    internal GomokuRuleSet RuleSet => _current.RuleSet;
    internal GomokuStone CurrentPlayer => _current.CurrentPlayer;
    internal GomokuGameState State => _current.State;
    internal GomokuStone? Winner => _current.Winner;
    internal int MoveCount => _current.MoveCount;
    internal int BlackCount => _current.BlackCount;
    internal int WhiteCount => _current.WhiteCount;
    internal GomokuPosition? LastMove => _current.LastMove;
    internal IReadOnlyList<IReadOnlyList<GomokuPosition>> WinningLines => _current.WinningLines;
    internal bool CanUndo => _history.Count > 0;

    internal GomokuStone? GetStone(GomokuPosition position) => _current.GetStone(position);
    internal GomokuGameSnapshot CreateSnapshot() => _current.Clone();
    internal GomokuMoveValidation ValidateMove(GomokuPosition position) =>
        GomokuRules.ValidateMove(_current, CurrentPlayer, position);

    internal GomokuMoveResult? PlaceStone(GomokuPosition position)
    {
        var result = GomokuRules.TryApplyMove(_current, CurrentPlayer, position);
        if (result is null)
        {
            return null;
        }

        _history.Push(result.Before);
        _current = result.After;
        return result;
    }

    internal GomokuGameSnapshot? Undo()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        _current = _history.Pop();
        return _current.Clone();
    }

    internal void StartNewGame(GomokuRuleSet ruleSet)
    {
        _history.Clear();
        _current = GomokuRules.CreateInitialSnapshot(ruleSet);
    }
}
