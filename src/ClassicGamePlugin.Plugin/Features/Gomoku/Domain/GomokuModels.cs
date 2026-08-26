namespace ClassicGamePlugin.Features.Gomoku.Domain;

/// <summary>表示五子棋棋盘上的一方棋子。</summary>
internal enum GomokuStone
{
    Black,
    White,
}

/// <summary>区分面向休闲玩家的自由规则与限制黑方先手优势的禁手规则。</summary>
internal enum GomokuRuleSet
{
    Freestyle,
    Forbidden,
}

/// <summary>表示一局五子棋所处的领域生命周期；回退暂停属于页面编排状态，不污染纯棋局状态。</summary>
internal enum GomokuGameState
{
    Ready,
    Running,
    Finished,
}

/// <summary>使用从零开始的行列保存交叉点，并提供 A1-O15 玩家坐标。</summary>
internal readonly record struct GomokuPosition(int Row, int Column)
{
    internal string DisplayName => $"{(char)('A' + Column)}{Row + 1}";
}

/// <summary>黑方禁手原因可以同时出现，使用标志值让界面完整解释被拒绝的落点。</summary>
[Flags]
internal enum GomokuForbiddenReason
{
    None = 0,
    Overline = 1,
    DoubleFour = 2,
    DoubleThree = 4,
}

internal enum GomokuMoveInvalidReason
{
    None,
    Finished,
    WrongPlayer,
    Occupied,
    Forbidden,
}

/// <summary>落子预检结果不修改快照，供领域提交、禁手标记和界面提示共同使用。</summary>
internal readonly record struct GomokuMoveValidation(
    bool IsLegal,
    GomokuMoveInvalidReason InvalidReason,
    GomokuForbiddenReason ForbiddenReasons)
{
    internal static GomokuMoveValidation Legal { get; } =
        new(true, GomokuMoveInvalidReason.None, GomokuForbiddenReason.None);
}

/// <summary>保存一次成功落子的前后快照，调用方不需要重新推导终局和获胜线。</summary>
internal sealed record GomokuMoveResult(
    GomokuStone Player,
    GomokuPosition Position,
    GomokuGameSnapshot Before,
    GomokuGameSnapshot After);

/// <summary>
/// 五子棋不可变快照。棋盘数组和获胜线在构造、复制时均隔离，确保后台 AI、撤销历史与真实棋局
/// 不会通过共享可变集合互相污染。
/// </summary>
internal sealed class GomokuGameSnapshot
{
    private readonly GomokuStone?[] _board;
    private readonly IReadOnlyList<IReadOnlyList<GomokuPosition>> _winningLines;

    internal GomokuGameSnapshot(
        IEnumerable<GomokuStone?> board,
        GomokuRuleSet ruleSet,
        GomokuStone currentPlayer,
        GomokuGameState state,
        int moveCount,
        GomokuPosition? lastMove,
        GomokuStone? winner,
        IEnumerable<IEnumerable<GomokuPosition>>? winningLines = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        _board = board.ToArray();
        if (_board.Length != GomokuRules.CellCount)
        {
            throw new ArgumentException("五子棋快照必须恰好包含 225 个交叉点。", nameof(board));
        }

        RuleSet = ruleSet;
        CurrentPlayer = currentPlayer;
        State = state;
        MoveCount = moveCount;
        LastMove = lastMove;
        Winner = winner;
        _winningLines = (winningLines ?? [])
            .Select(line => (IReadOnlyList<GomokuPosition>)line.ToArray())
            .ToArray();
    }

    internal GomokuRuleSet RuleSet { get; }
    internal GomokuStone CurrentPlayer { get; }
    internal GomokuGameState State { get; }
    internal int MoveCount { get; }
    internal GomokuPosition? LastMove { get; }
    internal GomokuStone? Winner { get; }
    internal IReadOnlyList<IReadOnlyList<GomokuPosition>> WinningLines => _winningLines;
    internal int BlackCount => _board.Count(stone => stone == GomokuStone.Black);
    internal int WhiteCount => _board.Count(stone => stone == GomokuStone.White);
    internal int EmptyCount => _board.Count(stone => stone is null);

    internal GomokuStone? GetStone(GomokuPosition position)
    {
        GomokuRules.ValidatePosition(position);
        return _board[(position.Row * GomokuRules.BoardSize) + position.Column];
    }

    internal GomokuStone?[] CopyBoard() => (GomokuStone?[])_board.Clone();

    internal GomokuGameSnapshot Clone() =>
        new(_board, RuleSet, CurrentPlayer, State, MoveCount, LastMove, Winner, _winningLines);

    /// <summary>只替换模拟回合，供防守评分读取对手合法点；原快照和棋盘内容保持不变。</summary>
    internal GomokuGameSnapshot WithCurrentPlayer(GomokuStone player) =>
        new(_board, RuleSet, player, State, MoveCount, LastMove, Winner, _winningLines);
}
