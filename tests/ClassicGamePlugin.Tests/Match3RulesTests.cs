using ClassicGamePlugin.Features.Match3.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Match3RulesTests
{
    [Fact]
    public void 棋盘生成没有初始匹配且至少存在一步合法交换()
    {
        var board = new Match3BoardGenerator().Create(new CyclingMatch3Random());

        Assert.Equal(64, board.Length);
        Assert.DoesNotContain(board, tile => tile is null);
        Assert.False(Match3Rules.HasAnyMatch(board));
        Assert.True(Match3Rules.TryFindFirstLegalSwap(board, out _, out _));
        Assert.Equal(6, board.Select(tile => tile!.Value.Kind).Distinct().Count());
    }

    [Fact]
    public void 病态随机源使用有界后备棋盘而不无限循环()
    {
        var board = new Match3BoardGenerator().Create(new ConstantMatch3Random(0));

        Assert.False(Match3Rules.HasAnyMatch(board));
        Assert.True(Match3Rules.TryFindFirstLegalSwap(board, out var source, out var target));
        Assert.True(Match3Rules.AreAdjacent(source, target));
    }

    [Fact]
    public void 横纵三连会被识别而彩虹球不会参与颜色匹配()
    {
        var board = Match3Boards.Stable();
        Match3Boards.Set(board, 4, 1, Match3GemKind.Emerald);
        Match3Boards.Set(board, 4, 2, Match3GemKind.Emerald);
        Match3Boards.Set(board, 4, 3, Match3GemKind.Emerald);
        Match3Boards.Set(board, 4, 4, Match3GemKind.Amber);
        Match3Boards.Set(board, 1, 6, Match3GemKind.Amethyst);
        Match3Boards.Set(board, 2, 6, Match3GemKind.Amethyst);
        Match3Boards.Set(board, 3, 6, Match3GemKind.Amethyst);
        Match3Boards.Set(board, 4, 6, null, Match3SpecialKind.Rainbow);

        var runs = Match3Rules.FindRuns(board);

        Assert.Contains(runs, run => run.IsHorizontal && run.Positions.Count == 3 && run.Positions[0].Row == 4);
        Assert.Contains(runs, run => !run.IsHorizontal && run.Positions.Count == 3 && run.Positions[0].Column == 6);
        Assert.DoesNotContain(runs.SelectMany(run => run.Positions), position => position == new Match3Position(4, 6));
    }

    [Fact]
    public void 只有相邻且造成匹配的普通交换才合法()
    {
        var board = Match3Boards.Stable();

        Assert.True(Match3Rules.IsLegalSwap(board, new Match3Position(0, 1), new Match3Position(1, 1)));
        Assert.False(Match3Rules.IsLegalSwap(board, new Match3Position(7, 6), new Match3Position(7, 7)));
        Assert.False(Match3Rules.IsLegalSwap(board, new Match3Position(0, 0), new Match3Position(2, 0)));
    }

    [Fact]
    public void 彩虹球与普通棋子以及任意两个特殊棋子可以直接交换()
    {
        var board = Match3Boards.Stable();
        Match3Boards.Set(board, 5, 4, null, Match3SpecialKind.Rainbow);
        Match3Boards.Set(board, 6, 5, Match3GemKind.Amber, Match3SpecialKind.AreaBomb);
        Match3Boards.Set(board, 6, 6, Match3GemKind.Sapphire, Match3SpecialKind.RowClear);

        Assert.True(Match3Rules.IsLegalSwap(board, new Match3Position(5, 4), new Match3Position(5, 5)));
        Assert.True(Match3Rules.IsLegalSwap(board, new Match3Position(6, 5), new Match3Position(6, 6)));
    }
}
