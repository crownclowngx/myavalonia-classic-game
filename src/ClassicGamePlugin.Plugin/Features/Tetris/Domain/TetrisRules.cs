namespace ClassicGamePlugin.Features.Tetris.Domain;

/// <summary>
/// 集中保存俄罗斯方块的纯几何规则。坐标以棋盘左上角为原点、行向下递增；SRS 表仍按规范的 X 向右、Y 向上书写，
/// 应用踢墙时统一转换为“列加 X、行减 Y”，避免在每张表中混用两套符号。
/// </summary>
internal static class TetrisRules
{
    internal const int BoardWidth = 10;
    internal const int VisibleHeight = 20;
    internal const int HiddenRows = 4;
    internal const int BoardHeight = VisibleHeight + HiddenRows;
    internal const int SpawnRow = 2;
    internal const int SpawnColumn = 3;

    private static readonly IReadOnlyDictionary<(TetrisRotation From, TetrisRotation To), (int X, int Y)[]> JlstzKicks =
        new Dictionary<(TetrisRotation, TetrisRotation), (int, int)[]>
        {
            [(TetrisRotation.Spawn, TetrisRotation.Right)] = [(0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2)],
            [(TetrisRotation.Right, TetrisRotation.Spawn)] = [(0, 0), (1, 0), (1, -1), (0, 2), (1, 2)],
            [(TetrisRotation.Right, TetrisRotation.Reverse)] = [(0, 0), (1, 0), (1, -1), (0, 2), (1, 2)],
            [(TetrisRotation.Reverse, TetrisRotation.Right)] = [(0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2)],
            [(TetrisRotation.Reverse, TetrisRotation.Left)] = [(0, 0), (1, 0), (1, 1), (0, -2), (1, -2)],
            [(TetrisRotation.Left, TetrisRotation.Reverse)] = [(0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2)],
            [(TetrisRotation.Left, TetrisRotation.Spawn)] = [(0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2)],
            [(TetrisRotation.Spawn, TetrisRotation.Left)] = [(0, 0), (1, 0), (1, 1), (0, -2), (1, -2)],
        };

    private static readonly IReadOnlyDictionary<(TetrisRotation From, TetrisRotation To), (int X, int Y)[]> IKicks =
        new Dictionary<(TetrisRotation, TetrisRotation), (int, int)[]>
        {
            [(TetrisRotation.Spawn, TetrisRotation.Right)] = [(0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2)],
            [(TetrisRotation.Right, TetrisRotation.Spawn)] = [(0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2)],
            [(TetrisRotation.Right, TetrisRotation.Reverse)] = [(0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1)],
            [(TetrisRotation.Reverse, TetrisRotation.Right)] = [(0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1)],
            [(TetrisRotation.Reverse, TetrisRotation.Left)] = [(0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2)],
            [(TetrisRotation.Left, TetrisRotation.Reverse)] = [(0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2)],
            [(TetrisRotation.Left, TetrisRotation.Spawn)] = [(0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1)],
            [(TetrisRotation.Spawn, TetrisRotation.Left)] = [(0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1)],
        };

    internal static TetrisPiece CreateSpawnPiece(TetrominoType type) =>
        new(type, TetrisRotation.Spawn, SpawnRow, SpawnColumn);

    internal static IReadOnlyList<TetrisPosition> GetCells(TetrisPiece piece) =>
        GetOffsets(piece.Type, piece.Rotation)
            .Select(offset => new TetrisPosition(piece.Row + offset.Row, piece.Column + offset.Column))
            .ToArray();

    internal static IReadOnlyList<(int X, int Y)> GetKickTests(
        TetrominoType type,
        TetrisRotation from,
        TetrisRotation to)
    {
        if (type == TetrominoType.O)
        {
            return [(0, 0)];
        }

        var table = type == TetrominoType.I ? IKicks : JlstzKicks;
        return table[(from, to)];
    }

    internal static TetrisRotation Rotate(TetrisRotation rotation, bool clockwise) =>
        (TetrisRotation)(((int)rotation + (clockwise ? 1 : 3)) % 4);

    internal static int ToIndex(int row, int column) => (row * BoardWidth) + column;

    private static IReadOnlyList<TetrisPosition> GetOffsets(TetrominoType type, TetrisRotation rotation) =>
        (type, rotation) switch
        {
            (TetrominoType.I, TetrisRotation.Spawn) => P((1, 0), (1, 1), (1, 2), (1, 3)),
            (TetrominoType.I, TetrisRotation.Right) => P((0, 2), (1, 2), (2, 2), (3, 2)),
            (TetrominoType.I, TetrisRotation.Reverse) => P((2, 0), (2, 1), (2, 2), (2, 3)),
            (TetrominoType.I, TetrisRotation.Left) => P((0, 1), (1, 1), (2, 1), (3, 1)),
            (TetrominoType.O, _) => P((0, 1), (0, 2), (1, 1), (1, 2)),
            (TetrominoType.T, TetrisRotation.Spawn) => P((0, 1), (1, 0), (1, 1), (1, 2)),
            (TetrominoType.T, TetrisRotation.Right) => P((0, 1), (1, 1), (1, 2), (2, 1)),
            (TetrominoType.T, TetrisRotation.Reverse) => P((1, 0), (1, 1), (1, 2), (2, 1)),
            (TetrominoType.T, TetrisRotation.Left) => P((0, 1), (1, 0), (1, 1), (2, 1)),
            (TetrominoType.J, TetrisRotation.Spawn) => P((0, 0), (1, 0), (1, 1), (1, 2)),
            (TetrominoType.J, TetrisRotation.Right) => P((0, 1), (0, 2), (1, 1), (2, 1)),
            (TetrominoType.J, TetrisRotation.Reverse) => P((1, 0), (1, 1), (1, 2), (2, 2)),
            (TetrominoType.J, TetrisRotation.Left) => P((0, 1), (1, 1), (2, 0), (2, 1)),
            (TetrominoType.L, TetrisRotation.Spawn) => P((0, 2), (1, 0), (1, 1), (1, 2)),
            (TetrominoType.L, TetrisRotation.Right) => P((0, 1), (1, 1), (2, 1), (2, 2)),
            (TetrominoType.L, TetrisRotation.Reverse) => P((1, 0), (1, 1), (1, 2), (2, 0)),
            (TetrominoType.L, TetrisRotation.Left) => P((0, 0), (0, 1), (1, 1), (2, 1)),
            (TetrominoType.S, TetrisRotation.Spawn) => P((0, 1), (0, 2), (1, 0), (1, 1)),
            (TetrominoType.S, TetrisRotation.Right) => P((0, 1), (1, 1), (1, 2), (2, 2)),
            (TetrominoType.S, TetrisRotation.Reverse) => P((1, 1), (1, 2), (2, 0), (2, 1)),
            (TetrominoType.S, TetrisRotation.Left) => P((0, 0), (1, 0), (1, 1), (2, 1)),
            (TetrominoType.Z, TetrisRotation.Spawn) => P((0, 0), (0, 1), (1, 1), (1, 2)),
            (TetrominoType.Z, TetrisRotation.Right) => P((0, 2), (1, 1), (1, 2), (2, 1)),
            (TetrominoType.Z, TetrisRotation.Reverse) => P((1, 0), (1, 1), (2, 1), (2, 2)),
            (TetrominoType.Z, TetrisRotation.Left) => P((0, 1), (1, 0), (1, 1), (2, 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private static TetrisPosition[] P(params (int Row, int Column)[] values) =>
        values.Select(value => new TetrisPosition(value.Row, value.Column)).ToArray();
}
