using ClassicGamePlugin.Features.Match3.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Match3SpecialTests
{
    [Fact]
    public void 四连在交换目标生成同行消除棋子()
    {
        var board = Match3Boards.Stable();
        Match3Boards.Set(board, 0, 0, Match3GemKind.Ruby);
        Match3Boards.Set(board, 0, 1, Match3GemKind.Ruby);
        Match3Boards.Set(board, 0, 2, Match3GemKind.Amber);
        Match3Boards.Set(board, 0, 3, Match3GemKind.Ruby);
        Match3Boards.Set(board, 1, 2, Match3GemKind.Ruby);

        var transition = Resolve(board, new Match3Position(1, 2), new Match3Position(0, 2));

        var created = Assert.Single(transition.Steps[0].CreatedSpecials);
        Assert.Equal(new Match3Position(0, 2), created.Position);
        Assert.Equal(Match3SpecialKind.RowClear, created.Tile.Special);
    }

    [Fact]
    public void 五连优先生成彩虹球()
    {
        var board = Match3Boards.Stable();
        Match3Boards.Set(board, 0, 0, Match3GemKind.Ruby);
        Match3Boards.Set(board, 0, 1, Match3GemKind.Ruby);
        Match3Boards.Set(board, 0, 2, Match3GemKind.Amber);
        Match3Boards.Set(board, 0, 3, Match3GemKind.Ruby);
        Match3Boards.Set(board, 0, 4, Match3GemKind.Ruby);
        Match3Boards.Set(board, 1, 2, Match3GemKind.Ruby);

        var transition = Resolve(board, new Match3Position(1, 2), new Match3Position(0, 2));

        var created = Assert.Single(transition.Steps[0].CreatedSpecials);
        Assert.Equal(Match3SpecialKind.Rainbow, created.Tile.Special);
        Assert.Null(created.Tile.Kind);
    }

    [Fact]
    public void T形匹配优先生成范围炸弹()
    {
        var board = Match3Boards.Stable();
        Match3Boards.Set(board, 0, 2, Match3GemKind.Ruby);
        Match3Boards.Set(board, 1, 2, Match3GemKind.Ruby);
        Match3Boards.Set(board, 2, 1, Match3GemKind.Ruby);
        Match3Boards.Set(board, 2, 2, Match3GemKind.Amber);
        Match3Boards.Set(board, 2, 3, Match3GemKind.Ruby);
        Match3Boards.Set(board, 3, 2, Match3GemKind.Ruby);

        var transition = Resolve(board, new Match3Position(3, 2), new Match3Position(2, 2));

        var created = Assert.Single(transition.Steps[0].CreatedSpecials);
        Assert.Equal(Match3SpecialKind.AreaBomb, created.Tile.Special);
        Assert.Equal(new Match3Position(2, 2), created.Position);
    }

    [Theory]
    [InlineData((int)Match3SpecialKind.RowClear, (int)Match3SpecialKind.RowClear, 16)]
    [InlineData((int)Match3SpecialKind.ColumnClear, (int)Match3SpecialKind.ColumnClear, 16)]
    [InlineData((int)Match3SpecialKind.RowClear, (int)Match3SpecialKind.ColumnClear, 15)]
    [InlineData((int)Match3SpecialKind.RowClear, (int)Match3SpecialKind.AreaBomb, 39)]
    [InlineData((int)Match3SpecialKind.AreaBomb, (int)Match3SpecialKind.AreaBomb, 25)]
    public void 五类几何特殊组合清除固定范围(
        int firstValue,
        int secondValue,
        int expectedCount)
    {
        var first = (Match3SpecialKind)firstValue;
        var second = (Match3SpecialKind)secondValue;
        var board = Match3Boards.Stable();
        var source = first == Match3SpecialKind.RowClear && second == Match3SpecialKind.RowClear
            ? new Match3Position(2, 3)
            : new Match3Position(3, 2);
        var target = new Match3Position(3, 3);
        Match3Boards.Set(board, source.Row, source.Column, Match3GemKind.Ruby, first);
        Match3Boards.Set(board, target.Row, target.Column, Match3GemKind.Amber, second);

        var transition = Resolve(board, source, target);

        Assert.Equal(expectedCount, transition.Steps[0].ClearedPositions.Count);
    }

    [Fact]
    public void 彩虹球加普通棋子清除目标颜色()
    {
        var board = Match3Boards.Stable();
        var source = new Match3Position(4, 3);
        var target = new Match3Position(4, 4);
        Match3Boards.Set(board, source.Row, source.Column, null, Match3SpecialKind.Rainbow);
        Match3Boards.Set(board, target.Row, target.Column, Match3GemKind.Emerald);
        var expectedColorCount = board.Count(tile => tile?.Kind == Match3GemKind.Emerald);

        var transition = Resolve(board, source, target);

        Assert.Equal(expectedColorCount + 1, transition.Steps[0].ClearedPositions.Count);
    }

    [Theory]
    [InlineData((int)Match3SpecialKind.RowClear)]
    [InlineData((int)Match3SpecialKind.AreaBomb)]
    public void 彩虹球会把目标颜色转换为特殊棋子并连锁触发(int targetSpecialValue)
    {
        var targetSpecial = (Match3SpecialKind)targetSpecialValue;
        var board = Match3Boards.Stable();
        var source = new Match3Position(4, 3);
        var target = new Match3Position(4, 4);
        Match3Boards.Set(board, source.Row, source.Column, null, Match3SpecialKind.Rainbow);
        Match3Boards.Set(board, target.Row, target.Column, Match3GemKind.Emerald, targetSpecial);
        var colorCount = board.Count(tile => tile?.Kind == Match3GemKind.Emerald);

        var transition = Resolve(board, source, target);

        Assert.True(transition.Steps[0].ClearedPositions.Count > colorCount);
    }

    [Fact]
    public void 双彩虹球清除整个棋盘()
    {
        var board = Match3Boards.Stable();
        Match3Boards.Set(board, 4, 3, null, Match3SpecialKind.Rainbow);
        Match3Boards.Set(board, 4, 4, null, Match3SpecialKind.Rainbow);

        var transition = Resolve(board, new Match3Position(4, 3), new Match3Position(4, 4));

        Assert.Equal(64, transition.Steps[0].ClearedPositions.Count);
    }

    [Fact]
    public void 双彩虹清场后形成无解稳定局面会免费重建棋盘()
    {
        var board = Match3Boards.Stable();
        Match3Boards.Set(board, 4, 3, null, Match3SpecialKind.Rainbow);
        Match3Boards.Set(board, 4, 4, null, Match3SpecialKind.Rainbow);
        var latinFillInColumnGravityOrder =
            from column in Enumerable.Range(0, Match3Rules.BoardSize)
            from row in Enumerable.Range(0, Match3Rules.BoardSize).Reverse()
            select (row + column) % 6;
        var random = new QueuedThenCyclingMatch3Random(latinFillInColumnGravityOrder);

        var transition = new Match3TurnResolver(new Match3BoardGenerator()).Resolve(
            board, new Match3Position(4, 3), new Match3Position(4, 4), random);

        Assert.True(transition.WasShuffled);
        Assert.False(Match3Rules.HasAnyMatch(transition.After));
        Assert.True(Match3Rules.TryFindFirstLegalSwap(transition.After, out _, out _));
    }

    private static Match3TurnTransition Resolve(
        Match3Tile?[] board,
        Match3Position source,
        Match3Position target) =>
        new Match3TurnResolver(new Match3BoardGenerator())
            .Resolve(board, source, target, new CyclingMatch3Random());
}
