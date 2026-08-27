namespace ClassicGamePlugin.Features.Tetris.Domain;

public enum TetrominoType
{
    I,
    J,
    L,
    O,
    S,
    T,
    Z,
}

public enum TetrisRotation
{
    Spawn,
    Right,
    Reverse,
    Left,
}

public enum TetrisGameState
{
    Playing,
    Paused,
    GameOver,
}

public enum TetrisSpinKind
{
    None,
    Mini,
    Full,
}

public readonly record struct TetrisPosition(int Row, int Column);

public readonly record struct TetrisPiece(
    TetrominoType Type,
    TetrisRotation Rotation,
    int Row,
    int Column);

/// <summary>
/// 保存一次锁定事务的只读结果。领域状态在结果返回前已经完整提交；View 只能使用这些快照回放硬降和消行动画，
/// 不能把动画中间帧写回真实棋盘，因此取消动画不会留下半格方块或只消除一半的行。
/// </summary>
internal sealed record TetrisTransition(
    TetrisPiece LockedPiece,
    int DropStartRow,
    IReadOnlyList<int> ClearedRows,
    TetrominoType?[] BeforeCells,
    TetrominoType?[] AfterCells,
    TetrisSpinKind Spin,
    bool IsPerfectClear,
    bool IsBackToBack,
    int ScoreGained);

