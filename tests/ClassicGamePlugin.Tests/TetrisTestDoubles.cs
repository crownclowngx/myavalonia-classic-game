using ClassicGamePlugin.Features.Tetris.Domain;

namespace ClassicGamePlugin.Tests;

internal sealed class SequenceTetrominoSource(params TetrominoType[] sequence) : ITetrominoSource
{
    private readonly TetrominoType[] _sequence = sequence.Length == 0
        ? Enum.GetValues<TetrominoType>()
        : sequence;
    private int _index;

    public TetrominoType Next()
    {
        var result = _sequence[_index % _sequence.Length];
        _index++;
        return result;
    }
}

internal static class TetrisTestBoard
{
    internal static TetrominoType?[] Empty() =>
        new TetrominoType?[TetrisRules.BoardWidth * TetrisRules.BoardHeight];

    internal static void FillRowExcept(
        TetrominoType?[] cells,
        int row,
        params int[] emptyColumns)
    {
        var empty = emptyColumns.ToHashSet();
        for (var column = 0; column < TetrisRules.BoardWidth; column++)
        {
            if (!empty.Contains(column))
            {
                cells[TetrisRules.ToIndex(row, column)] = TetrominoType.J;
            }
        }
    }
}

