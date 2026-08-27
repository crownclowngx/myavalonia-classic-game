namespace ClassicGamePlugin.Features.Match3.Domain;

/// <summary>生成没有初始匹配且至少存在一步合法交换的普通棋盘。</summary>
internal sealed class Match3BoardGenerator
{
    private const int MaximumAttempts = 256;
    private static readonly Match3GemKind[] Kinds = Enum.GetValues<Match3GemKind>();

    internal Match3Tile?[] Create(IMatch3RandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var board = FillWithoutImmediateMatches(randomSource);
            if (Match3Rules.TryFindFirstLegalSwap(board, out _, out _))
            {
                return board;
            }
        }

        // 即使测试随机源始终返回同一个值，也必须有界结束。该模板无三连，交换首行中间格后可形成三连。
        return CreateFallbackBoard();
    }

    private static Match3Tile?[] FillWithoutImmediateMatches(IMatch3RandomSource randomSource)
    {
        var board = new Match3Tile?[Match3Rules.CellCount];
        for (var row = 0; row < Match3Rules.BoardSize; row++)
        {
            for (var column = 0; column < Match3Rules.BoardSize; column++)
            {
                var allowed = Kinds.Where(kind =>
                    !WouldCreateHorizontalTriple(board, row, column, kind) &&
                    !WouldCreateVerticalTriple(board, row, column, kind)).ToArray();
                var selected = NextValidated(randomSource, allowed.Length);
                board[Match3Rules.ToIndex(new Match3Position(row, column))] = Match3Tile.Normal(allowed[selected]);
            }
        }

        return board;
    }

    internal static int NextValidated(IMatch3RandomSource randomSource, int exclusiveMaximum)
    {
        var value = randomSource.Next(exclusiveMaximum);
        if (value < 0 || value >= exclusiveMaximum)
        {
            throw new InvalidOperationException("消消乐随机源返回了范围外的值。");
        }

        return value;
    }

    private static bool WouldCreateHorizontalTriple(
        IReadOnlyList<Match3Tile?> board,
        int row,
        int column,
        Match3GemKind kind) =>
        column >= 2 &&
        board[Match3Rules.ToIndex(new Match3Position(row, column - 1))]?.Kind == kind &&
        board[Match3Rules.ToIndex(new Match3Position(row, column - 2))]?.Kind == kind;

    private static bool WouldCreateVerticalTriple(
        IReadOnlyList<Match3Tile?> board,
        int row,
        int column,
        Match3GemKind kind) =>
        row >= 2 &&
        board[Match3Rules.ToIndex(new Match3Position(row - 1, column))]?.Kind == kind &&
        board[Match3Rules.ToIndex(new Match3Position(row - 2, column))]?.Kind == kind;

    private static Match3Tile?[] CreateFallbackBoard()
    {
        var board = new Match3Tile?[Match3Rules.CellCount];
        for (var row = 0; row < Match3Rules.BoardSize; row++)
        {
            for (var column = 0; column < Match3Rules.BoardSize; column++)
            {
                var kind = (Match3GemKind)((row + column) % Kinds.Length);
                board[Match3Rules.ToIndex(new Match3Position(row, column))] = Match3Tile.Normal(kind);
            }
        }

        board[Match3Rules.ToIndex(new Match3Position(0, 2))] = Match3Tile.Normal(Match3GemKind.Ruby);
        board[Match3Rules.ToIndex(new Match3Position(1, 1))] = Match3Tile.Normal(Match3GemKind.Ruby);
        return board;
    }
}
