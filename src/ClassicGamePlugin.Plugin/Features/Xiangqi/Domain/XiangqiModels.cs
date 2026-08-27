namespace ClassicGamePlugin.Features.Xiangqi.Domain;

internal enum XiangqiSide
{
    Red,
    Black,
}

internal enum XiangqiPieceType
{
    General,
    Advisor,
    Elephant,
    Horse,
    Chariot,
    Cannon,
    Soldier,
}

internal enum XiangqiGameState
{
    Ready,
    Running,
    Finished,
}

internal enum XiangqiTerminationReason
{
    Checkmate,
    Stalemate,
    Resignation,
    ThreefoldRepetition,
    NoCaptureLimit,
}

internal enum XiangqiMoveError
{
    None,
    GameFinished,
    OutOfBounds,
    EmptyOrigin,
    WrongSide,
    FriendlyDestination,
    PieceMovement,
    PathBlocked,
    HorseLegBlocked,
    ElephantEyeBlocked,
    PalaceRestricted,
    ElephantCrossesRiver,
    CannonScreen,
    SoldierDirection,
    GeneralCaptureNotAllowed,
    ExposesGeneral,
    GeneralsFace,
    PerpetualCheck,
}

internal enum XiangqiAiDifficulty
{
    Easy,
    Medium,
    Hard,
}

internal readonly record struct XiangqiPiece(XiangqiSide Side, XiangqiPieceType Type)
{
    internal string DisplayName => Type switch
    {
        XiangqiPieceType.General => Side == XiangqiSide.Red ? "帅" : "将",
        XiangqiPieceType.Advisor => Side == XiangqiSide.Red ? "仕" : "士",
        XiangqiPieceType.Elephant => Side == XiangqiSide.Red ? "相" : "象",
        XiangqiPieceType.Horse => "马",
        XiangqiPieceType.Chariot => "车",
        XiangqiPieceType.Cannon => "炮",
        XiangqiPieceType.Soldier => Side == XiangqiSide.Red ? "兵" : "卒",
        _ => throw new ArgumentOutOfRangeException(),
    };
}

internal readonly record struct XiangqiPosition(int Row, int Column)
{
    internal string DisplayName => $"({Row + 1},{Column + 1})";
}

internal readonly record struct XiangqiMove(XiangqiPosition From, XiangqiPosition To);

internal sealed record XiangqiMoveValidation(XiangqiMoveError Error)
{
    internal static XiangqiMoveValidation Legal { get; } = new(XiangqiMoveError.None);
    internal bool IsLegal => Error == XiangqiMoveError.None;
}

/// <summary>
/// 记录一次走棋完成后的局面身份与走棋性质。重复裁定既需要比较“棋盘＋轮到谁走”，也需要知道
/// 重复周期内双方是否每一着都在将军；把两类信息放在同一条不可变记录中，可让撤销和 AI 快照自然恢复。
/// </summary>
internal sealed record XiangqiPositionRecord(
    ulong Key,
    string Signature,
    XiangqiSide SideToMove,
    XiangqiSide? Mover,
    bool GaveCheck);

/// <summary>
/// 中国象棋领域的不可变快照。构造时复制棋盘和历史，调用方只能通过复制方法取得新数组，避免真实棋局、
/// 撤销栈和后台 AI 共享可变引用。
/// </summary>
internal sealed class XiangqiGameSnapshot
{
    private readonly XiangqiPiece?[] _board;
    private readonly XiangqiPositionRecord[] _positionHistory;

    internal XiangqiGameSnapshot(
        IEnumerable<XiangqiPiece?> board,
        XiangqiSide currentSide,
        XiangqiGameState state,
        int moveCount,
        XiangqiMove? lastMove,
        XiangqiSide? winner,
        XiangqiTerminationReason? terminationReason,
        int noCapturePlyCount,
        IEnumerable<XiangqiPositionRecord> positionHistory)
    {
        _board = board?.ToArray() ?? throw new ArgumentNullException(nameof(board));
        if (_board.Length != XiangqiRules.CellCount)
        {
            throw new ArgumentException("中国象棋棋盘必须恰好包含 90 个交叉点。", nameof(board));
        }

        _positionHistory = positionHistory?.ToArray() ?? throw new ArgumentNullException(nameof(positionHistory));
        if (_positionHistory.Length == 0)
        {
            throw new ArgumentException("局面历史至少应包含初始局面。", nameof(positionHistory));
        }

        CurrentSide = currentSide;
        State = state;
        MoveCount = moveCount;
        LastMove = lastMove;
        Winner = winner;
        TerminationReason = terminationReason;
        NoCapturePlyCount = noCapturePlyCount;
    }

    internal XiangqiSide CurrentSide { get; }
    internal XiangqiGameState State { get; }
    internal int MoveCount { get; }
    internal XiangqiMove? LastMove { get; }
    internal XiangqiSide? Winner { get; }
    internal XiangqiTerminationReason? TerminationReason { get; }
    internal int NoCapturePlyCount { get; }
    internal IReadOnlyList<XiangqiPositionRecord> PositionHistory => _positionHistory;

    internal XiangqiPiece? GetPiece(XiangqiPosition position)
    {
        if (!XiangqiRules.IsInside(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return _board[(position.Row * XiangqiRules.ColumnCount) + position.Column];
    }

    internal XiangqiPiece?[] CopyBoard() => (XiangqiPiece?[])_board.Clone();
    internal XiangqiPositionRecord[] CopyPositionHistory() =>
        (XiangqiPositionRecord[])_positionHistory.Clone();
}

internal sealed record XiangqiMoveResult(
    XiangqiGameSnapshot Before,
    XiangqiGameSnapshot After,
    XiangqiMove Move,
    XiangqiPiece MovingPiece,
    XiangqiPiece? CapturedPiece,
    bool GaveCheck,
    string Notation);
