namespace ClassicGamePlugin.Features.Go.Domain;

/// <summary>表示围棋棋盘上的一方棋子。</summary>
internal enum GoStone
{
    Black,
    White,
}

/// <summary>表示围棋对局从准备、行棋、数子到结束的四个明确阶段。</summary>
internal enum GoGameState
{
    Ready,
    Playing,
    Scoring,
    Finished,
}

/// <summary>记录终局产生方式，避免 UI 根据分数或胜者反向猜测领域状态。</summary>
internal enum GoFinishReason
{
    None,
    Score,
    Resignation,
}

/// <summary>说明一次落子为何被拒绝，供 ViewModel 生成准确的中文反馈。</summary>
internal enum GoMoveInvalidReason
{
    None,
    OutsideBoard,
    WrongPhase,
    Occupied,
    Suicide,
    Superko,
}

/// <summary>
/// 使用从零开始的行列保存位置。显示坐标遵循围棋常用记法：列字母跳过 I，
/// 行号从棋盘底部向上递增，因此内部第 0 行显示为第 19 行。
/// </summary>
internal readonly record struct GoPosition(int Row, int Column)
{
    private const string ColumnNames = "ABCDEFGHJKLMNOPQRST";

    internal string DisplayName => GoRules.IsInside(Row, Column)
        ? $"{ColumnNames[Column]}{GoRules.BoardSize - Row}"
        : $"({Row},{Column})";
}

/// <summary>一次落子验证的稳定结果；合法时原因固定为 <see cref="GoMoveInvalidReason.None"/>。</summary>
internal readonly record struct GoMoveValidation(bool IsLegal, GoMoveInvalidReason Reason)
{
    internal static readonly GoMoveValidation Legal = new(true, GoMoveInvalidReason.None);
}

/// <summary>
/// 保存中国数子法的完整结果。领地映射只包含空交叉点；活棋本身的区域分可直接从棋盘读取。
/// 白方最终得分已经包含固定 7.5 目贴目。
/// </summary>
internal sealed class GoScoreResult
{
    private readonly Dictionary<GoPosition, GoStone> _territoryOwners;

    internal GoScoreResult(
        int blackStones,
        int whiteStones,
        int blackTerritory,
        int whiteTerritory,
        int neutralPoints,
        IReadOnlyDictionary<GoPosition, GoStone> territoryOwners)
    {
        ArgumentNullException.ThrowIfNull(territoryOwners);
        BlackStones = blackStones;
        WhiteStones = whiteStones;
        BlackTerritory = blackTerritory;
        WhiteTerritory = whiteTerritory;
        NeutralPoints = neutralPoints;
        _territoryOwners = new Dictionary<GoPosition, GoStone>(territoryOwners);
    }

    internal const double Komi = 7.5;
    internal int BlackStones { get; }
    internal int WhiteStones { get; }
    internal int BlackTerritory { get; }
    internal int WhiteTerritory { get; }
    internal int NeutralPoints { get; }
    internal double BlackScore => BlackStones + BlackTerritory;
    internal double WhiteScore => WhiteStones + WhiteTerritory + Komi;
    internal GoStone Winner => BlackScore > WhiteScore ? GoStone.Black : GoStone.White;
    internal double Margin => Math.Abs(BlackScore - WhiteScore);
    internal IReadOnlyDictionary<GoPosition, GoStone> TerritoryOwners => _territoryOwners;

    internal GoScoreResult Clone() =>
        new(BlackStones, WhiteStones, BlackTerritory, WhiteTerritory, NeutralPoints, _territoryOwners);
}

/// <summary>
/// 围棋不可变快照。棋盘和死子标记在构造、复制时都进行深拷贝，撤销历史、动画与当前棋局
/// 不会通过数组引用互相污染。
/// </summary>
internal sealed class GoGameSnapshot
{
    private readonly GoStone?[] _board;
    private readonly bool[] _deadStones;

    internal GoGameSnapshot(
        IEnumerable<GoStone?> board,
        GoStone currentPlayer,
        GoGameState state,
        int moveCount,
        int actionCount,
        int consecutivePasses,
        int blackCaptures,
        int whiteCaptures,
        GoPosition? lastMove,
        IEnumerable<GoPosition>? deadStones = null,
        GoStone? winner = null,
        GoFinishReason finishReason = GoFinishReason.None,
        GoScoreResult? score = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        _board = board.ToArray();
        if (_board.Length != GoRules.CellCount)
        {
            throw new ArgumentException("围棋快照必须恰好包含 361 个交叉点。", nameof(board));
        }

        _deadStones = new bool[GoRules.CellCount];
        if (deadStones is not null)
        {
            foreach (var position in deadStones)
            {
                GoRules.ValidatePosition(position);
                _deadStones[GoRules.IndexOf(position)] = true;
            }
        }

        CurrentPlayer = currentPlayer;
        State = state;
        MoveCount = moveCount;
        ActionCount = actionCount;
        ConsecutivePasses = consecutivePasses;
        BlackCaptures = blackCaptures;
        WhiteCaptures = whiteCaptures;
        LastMove = lastMove;
        Winner = winner;
        FinishReason = finishReason;
        Score = score?.Clone();
    }

    internal GoStone CurrentPlayer { get; }
    internal GoGameState State { get; }
    internal int MoveCount { get; }
    internal int ActionCount { get; }
    internal int ConsecutivePasses { get; }
    internal int BlackCaptures { get; }
    internal int WhiteCaptures { get; }
    internal GoPosition? LastMove { get; }
    internal GoStone? Winner { get; }
    internal GoFinishReason FinishReason { get; }
    internal GoScoreResult? Score { get; }
    internal int BlackStoneCount => _board.Count(stone => stone == GoStone.Black);
    internal int WhiteStoneCount => _board.Count(stone => stone == GoStone.White);

    internal GoStone? GetStone(GoPosition position)
    {
        GoRules.ValidatePosition(position);
        return _board[GoRules.IndexOf(position)];
    }

    internal bool IsMarkedDead(GoPosition position)
    {
        GoRules.ValidatePosition(position);
        return _deadStones[GoRules.IndexOf(position)];
    }

    internal GoStone?[] CopyBoard() => (GoStone?[])_board.Clone();

    internal IReadOnlyList<GoPosition> GetDeadStones()
    {
        var positions = new List<GoPosition>();
        for (var index = 0; index < _deadStones.Length; index++)
        {
            if (_deadStones[index])
            {
                positions.Add(GoRules.PositionOf(index));
            }
        }

        return positions;
    }

    internal string BoardKey => GoRules.CreateBoardKey(_board);

    internal GoGameSnapshot Clone() =>
        new(
            _board,
            CurrentPlayer,
            State,
            MoveCount,
            ActionCount,
            ConsecutivePasses,
            BlackCaptures,
            WhiteCaptures,
            LastMove,
            GetDeadStones(),
            Winner,
            FinishReason,
            Score);
}

/// <summary>领域已经原子提交的一次落子，供记录和纯视觉动画消费。</summary>
internal sealed record GoMoveResult(
    GoStone Player,
    GoPosition Position,
    IReadOnlyList<GoPosition> CapturedPositions,
    GoGameSnapshot Before,
    GoGameSnapshot After);
