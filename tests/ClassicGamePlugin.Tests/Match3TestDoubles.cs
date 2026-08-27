using ClassicGamePlugin.Features.Match3.Domain;

namespace ClassicGamePlugin.Tests;

internal sealed class CyclingMatch3Random(int initialValue = 0) : IMatch3RandomSource
{
    private int _next = initialValue;

    public int Next(int exclusiveMaximum)
    {
        var value = Math.Abs(_next % exclusiveMaximum);
        _next++;
        return value;
    }
}

internal sealed class ConstantMatch3Random(int value) : IMatch3RandomSource
{
    public int Next(int exclusiveMaximum) => value;
}

internal sealed class ThrowingMatch3Random(int allowedCalls) : IMatch3RandomSource
{
    private int _remaining = allowedCalls;

    public int Next(int exclusiveMaximum)
    {
        if (_remaining-- <= 0)
        {
            throw new InvalidOperationException("测试随机源按计划失败。");
        }

        return 0;
    }
}

internal sealed class QueuedThenCyclingMatch3Random(IEnumerable<int> values) : IMatch3RandomSource
{
    private readonly Queue<int> _values = new(values);
    private int _fallback;

    public int Next(int exclusiveMaximum)
    {
        var value = _values.Count > 0 ? _values.Dequeue() : _fallback++ % exclusiveMaximum;
        if (value < 0 || value >= exclusiveMaximum)
        {
            throw new InvalidOperationException("测试随机序列超出了本次范围。");
        }

        return value;
    }
}

internal static class Match3Boards
{
    internal static Match3Tile?[] Stable()
    {
        var board = new Match3Tile?[Match3Rules.CellCount];
        for (var row = 0; row < Match3Rules.BoardSize; row++)
        {
            for (var column = 0; column < Match3Rules.BoardSize; column++)
            {
                board[Match3Rules.ToIndex(new Match3Position(row, column))] =
                    Match3Tile.Normal((Match3GemKind)((row + column) % 6));
            }
        }

        board[Match3Rules.ToIndex(new Match3Position(0, 2))] = Match3Tile.Normal(Match3GemKind.Ruby);
        board[Match3Rules.ToIndex(new Match3Position(1, 1))] = Match3Tile.Normal(Match3GemKind.Ruby);
        return board;
    }

    internal static void Set(
        Match3Tile?[] board,
        int row,
        int column,
        Match3GemKind? kind,
        Match3SpecialKind special = Match3SpecialKind.None) =>
        board[Match3Rules.ToIndex(new Match3Position(row, column))] = new Match3Tile(kind, special);
}
