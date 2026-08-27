namespace ClassicGamePlugin.Features.Go.Domain;

/// <summary>
/// 持有当前围棋快照、当前分支见过的棋盘键和不限步撤销历史。规则与计分由纯领域服务负责；
/// 本类型只保证每个有效操作连同全局同形历史一起原子提交。
/// </summary>
internal sealed class GoGame
{
    private readonly Stack<HistoryFrame> _history = [];
    private HashSet<string> _seenBoardKeys;
    private GoGameSnapshot _snapshot;

    internal GoGame()
    {
        _snapshot = GoRules.CreateInitialSnapshot();
        _seenBoardKeys = [_snapshot.BoardKey];
    }

    internal GoGame(GoGameSnapshot snapshot, IEnumerable<string>? seenBoardKeys = null)
    {
        _snapshot = snapshot?.Clone() ?? throw new ArgumentNullException(nameof(snapshot));
        _seenBoardKeys = seenBoardKeys?.ToHashSet(StringComparer.Ordinal) ?? [_snapshot.BoardKey];
        _seenBoardKeys.Add(_snapshot.BoardKey);
    }

    internal GoGameState State => _snapshot.State;
    internal GoStone CurrentPlayer => _snapshot.CurrentPlayer;
    internal int MoveCount => _snapshot.MoveCount;
    internal int ActionCount => _snapshot.ActionCount;
    internal int ConsecutivePasses => _snapshot.ConsecutivePasses;
    internal int BlackCaptures => _snapshot.BlackCaptures;
    internal int WhiteCaptures => _snapshot.WhiteCaptures;
    internal GoPosition? LastMove => _snapshot.LastMove;
    internal GoStone? Winner => _snapshot.Winner;
    internal GoFinishReason FinishReason => _snapshot.FinishReason;
    internal GoScoreResult? Score => _snapshot.Score;
    internal bool CanUndo => _history.Count > 0;

    internal GoStone? GetStone(GoPosition position) => _snapshot.GetStone(position);
    internal GoGameSnapshot CreateSnapshot() => _snapshot.Clone();
    internal GoMoveValidation ValidateMove(GoPosition position) =>
        GoRules.ValidateMove(_snapshot, position, _seenBoardKeys);

    internal GoMoveResult? PlaceStone(GoPosition position)
    {
        var result = GoRules.TryApplyMove(_snapshot, position, _seenBoardKeys);
        if (result is null)
        {
            return null;
        }

        SaveHistory();
        _snapshot = result.After;
        _seenBoardKeys.Add(_snapshot.BoardKey);
        return result;
    }

    /// <summary>停一手不检查重复棋盘；第二次连续停手会切换到死子标记与数子阶段。</summary>
    internal bool Pass()
    {
        if (_snapshot.State is not (GoGameState.Ready or GoGameState.Playing))
        {
            return false;
        }

        SaveHistory();
        var passes = _snapshot.ConsecutivePasses + 1;
        _snapshot = new GoGameSnapshot(
            _snapshot.CopyBoard(),
            GoRules.OpponentOf(_snapshot.CurrentPlayer),
            passes >= 2 ? GoGameState.Scoring : GoGameState.Playing,
            _snapshot.MoveCount,
            _snapshot.ActionCount + 1,
            passes,
            _snapshot.BlackCaptures,
            _snapshot.WhiteCaptures,
            _snapshot.LastMove);
        return true;
    }

    /// <summary>数子阶段点击任一棋子会整体切换其正交连接棋组的死子标记。</summary>
    internal bool ToggleDeadGroup(GoPosition position)
    {
        if (_snapshot.State != GoGameState.Scoring || !GoRules.IsInside(position.Row, position.Column) ||
            _snapshot.GetStone(position) is null)
        {
            return false;
        }

        var group = GoRules.GetGroup(_snapshot, position);
        var shouldMark = !_snapshot.IsMarkedDead(position);
        var marked = _snapshot.GetDeadStones().ToHashSet();
        foreach (var member in group)
        {
            if (shouldMark)
            {
                marked.Add(member);
            }
            else
            {
                marked.Remove(member);
            }
        }

        SaveHistory();
        _snapshot = CopySnapshot(
            _snapshot,
            actionCount: _snapshot.ActionCount + 1,
            deadStones: marked);
        return true;
    }

    internal bool ResumePlay()
    {
        if (_snapshot.State != GoGameState.Scoring)
        {
            return false;
        }

        SaveHistory();
        _snapshot = new GoGameSnapshot(
            _snapshot.CopyBoard(),
            _snapshot.CurrentPlayer,
            GoGameState.Playing,
            _snapshot.MoveCount,
            _snapshot.ActionCount + 1,
            consecutivePasses: 0,
            _snapshot.BlackCaptures,
            _snapshot.WhiteCaptures,
            _snapshot.LastMove);
        return true;
    }

    internal bool ConfirmScore()
    {
        if (_snapshot.State != GoGameState.Scoring)
        {
            return false;
        }

        var score = GoScorer.Calculate(_snapshot);
        SaveHistory();
        _snapshot = new GoGameSnapshot(
            _snapshot.CopyBoard(),
            _snapshot.CurrentPlayer,
            GoGameState.Finished,
            _snapshot.MoveCount,
            _snapshot.ActionCount + 1,
            _snapshot.ConsecutivePasses,
            _snapshot.BlackCaptures,
            _snapshot.WhiteCaptures,
            _snapshot.LastMove,
            _snapshot.GetDeadStones(),
            score.Winner,
            GoFinishReason.Score,
            score);
        return true;
    }

    internal bool Resign()
    {
        if (_snapshot.State is not (GoGameState.Ready or GoGameState.Playing))
        {
            return false;
        }

        SaveHistory();
        _snapshot = new GoGameSnapshot(
            _snapshot.CopyBoard(),
            _snapshot.CurrentPlayer,
            GoGameState.Finished,
            _snapshot.MoveCount,
            _snapshot.ActionCount + 1,
            _snapshot.ConsecutivePasses,
            _snapshot.BlackCaptures,
            _snapshot.WhiteCaptures,
            _snapshot.LastMove,
            winner: GoRules.OpponentOf(_snapshot.CurrentPlayer),
            finishReason: GoFinishReason.Resignation);
        return true;
    }

    internal bool Undo()
    {
        if (!_history.TryPop(out var frame))
        {
            return false;
        }

        _snapshot = frame.Snapshot.Clone();
        _seenBoardKeys = new HashSet<string>(frame.SeenBoardKeys, StringComparer.Ordinal);
        return true;
    }

    internal void StartNewGame()
    {
        _history.Clear();
        _snapshot = GoRules.CreateInitialSnapshot();
        _seenBoardKeys = [_snapshot.BoardKey];
    }

    private void SaveHistory() =>
        _history.Push(new HistoryFrame(_snapshot.Clone(), _seenBoardKeys.ToArray()));

    private static GoGameSnapshot CopySnapshot(
        GoGameSnapshot source,
        int actionCount,
        IEnumerable<GoPosition> deadStones) =>
        new(
            source.CopyBoard(),
            source.CurrentPlayer,
            source.State,
            source.MoveCount,
            actionCount,
            source.ConsecutivePasses,
            source.BlackCaptures,
            source.WhiteCaptures,
            source.LastMove,
            deadStones,
            source.Winner,
            source.FinishReason,
            source.Score);

    private sealed record HistoryFrame(GoGameSnapshot Snapshot, IReadOnlyCollection<string> SeenBoardKeys);
}
