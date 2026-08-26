using ClassicGamePlugin.Features.SpiderSolitaire.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SpiderSolitaireGameTests
{
    [Theory]
    [InlineData(SpiderSolitaireDifficulty.OneSuit, 1, 8)]
    [InlineData(SpiderSolitaireDifficulty.TwoSuits, 2, 4)]
    [InlineData(SpiderSolitaireDifficulty.FourSuits, 4, 2)]
    internal void 三档难度生成完整且符合套数的牌组(
        SpiderSolitaireDifficulty difficulty,
        int suitCount,
        int copiesPerSuit)
    {
        var game = CreateGame(difficulty);
        var cards = game.CreateSnapshot().Columns.SelectMany(column => column)
            .Concat(game.CreateSnapshot().Stock)
            .ToArray();

        Assert.Equal(104, cards.Length);
        Assert.Equal(104, cards.Select(card => card.Id).Distinct().Count());
        Assert.Equal(suitCount, cards.Select(card => card.Suit).Distinct().Count());
        Assert.All(cards.GroupBy(card => (card.Suit, card.Rank)),
            group => Assert.Equal(copiesPerSuit, group.Count()));
    }

    [Fact]
    public void 初始牌局符合五十四张分列与五轮库存()
    {
        var game = CreateGame();

        Assert.Equal([6, 6, 6, 6, 5, 5, 5, 5, 5, 5],
            game.Columns.Select(column => column.Count));
        Assert.Equal(50, game.Stock.Count);
        Assert.Equal(5, game.StockDealCount);
        Assert.All(game.Columns, column =>
        {
            Assert.True(column[^1].IsFaceUp);
            Assert.All(column.Take(column.Count - 1), card => Assert.False(card.IsFaceUp));
        });
    }

    [Fact]
    public void 引擎拒绝洗牌策略返回重复或缺失牌()
    {
        var shuffler = new DelegateSpiderCardShuffler(cards =>
            Enumerable.Repeat(cards[0], 104).ToArray());

        Assert.Throws<InvalidOperationException>(() =>
            new SpiderSolitaireGame(SpiderSolitaireDifficulty.OneSuit, shuffler));
    }

    [Fact]
    public void 同花色连续牌组可整体移动而混花牌组不可整体移动()
    {
        var game = CreateEmptyGame();
        var columns = SpiderTestBoard.MutableColumns(game);
        columns[0].AddRange([
            SpiderTestBoard.Card(0, 7, SpiderCardSuit.Spades),
            SpiderTestBoard.Card(1, 6, SpiderCardSuit.Spades),
            SpiderTestBoard.Card(2, 5, SpiderCardSuit.Spades),
        ]);
        columns[1].Add(SpiderTestBoard.Card(3, 8, SpiderCardSuit.Hearts));

        Assert.True(game.CanSelectSequence(0, 0));
        Assert.True(game.CanMove(0, 0, 1));

        columns[0][1] = SpiderTestBoard.Card(4, 6, SpiderCardSuit.Hearts);

        Assert.False(game.CanSelectSequence(0, 0));
        Assert.False(game.CanMove(0, 0, 1));
        Assert.True(game.CanSelectSequence(0, 2));
    }

    [Fact]
    public void 单张允许跨花色压牌且移动后翻开源列顶牌()
    {
        var game = CreateEmptyGame();
        var columns = SpiderTestBoard.MutableColumns(game);
        columns[0].Add(SpiderTestBoard.Card(0, 10, faceUp: false));
        columns[0].Add(SpiderTestBoard.Card(1, 7, SpiderCardSuit.Hearts));
        columns[1].Add(SpiderTestBoard.Card(2, 8, SpiderCardSuit.Spades));

        var transition = Assert.IsType<SpiderGameTransition>(game.Move(0, 1, 1));

        Assert.True(columns[0][0].IsFaceUp);
        Assert.Equal([0], transition.FlippedCardIds);
        Assert.Equal(SpiderGameState.Running, game.State);
        Assert.Equal(1, game.ActionCount);
        Assert.Equal(499, game.Score);
    }

    [Fact]
    public void 空列接受任意合法牌组但存在空列时禁止发库存()
    {
        var game = CreateEmptyGame();
        var columns = SpiderTestBoard.MutableColumns(game);
        columns[0].Add(SpiderTestBoard.Card(0, 13));
        SpiderTestBoard.MutableStock(game).AddRange(
            Enumerable.Range(10, 10).Select(id => SpiderTestBoard.Card(id, 5, faceUp: false)));

        Assert.True(game.CanMove(0, 0, 1));
        Assert.False(game.CanDeal);
        Assert.Null(game.Deal());
        Assert.Equal(0, game.ActionCount);
    }

    [Fact]
    public void 发库存会向十列各加一张正面牌且只计一步()
    {
        var game = CreateEmptyGame();
        var columns = SpiderTestBoard.MutableColumns(game);
        for (var column = 0; column < 10; column++)
        {
            columns[column].Add(SpiderTestBoard.Card(column, 13));
        }

        SpiderTestBoard.MutableStock(game).AddRange(
            Enumerable.Range(10, 20).Select(id => SpiderTestBoard.Card(id, 5, faceUp: false)));

        var transition = Assert.IsType<SpiderGameTransition>(game.Deal());

        Assert.All(columns, column =>
        {
            Assert.Equal(2, column.Count);
            Assert.True(column[^1].IsFaceUp);
        });
        Assert.Equal(10, game.Stock.Count);
        Assert.Equal(SpiderActionKind.Deal, transition.Kind);
        Assert.Equal(1, game.ActionCount);
    }

    [Fact]
    public void 完成八组同花色序列后获胜且每组增加一百分()
    {
        var game = CreateEmptyGame();
        var columns = SpiderTestBoard.MutableColumns(game);
        for (var column = 0; column < 8; column++)
        {
            columns[column].AddRange(SpiderTestBoard.Run(column * 13));
        }

        columns[8].Add(SpiderTestBoard.Card(200, 5));
        columns[9].Add(SpiderTestBoard.Card(201, 6));

        Assert.NotNull(game.Move(8, 0, 9));

        Assert.Equal(8, game.CompletedRunCount);
        Assert.Equal(SpiderGameState.Won, game.State);
        Assert.Equal(1299, game.Score);
    }

    [Fact]
    public void 撤销恢复移动与翻牌但撤销本身继续扣分()
    {
        var game = CreateEmptyGame();
        var columns = SpiderTestBoard.MutableColumns(game);
        columns[0].Add(SpiderTestBoard.Card(0, 10, faceUp: false));
        columns[0].Add(SpiderTestBoard.Card(1, 7));
        columns[1].Add(SpiderTestBoard.Card(2, 8));
        game.Move(0, 1, 1);

        var undo = Assert.IsType<SpiderGameTransition>(game.Undo());

        Assert.Equal(SpiderActionKind.Undo, undo.Kind);
        Assert.Equal(2, game.ActionCount);
        Assert.Equal(498, game.Score);
        Assert.False(game.Columns[0][0].IsFaceUp);
        Assert.Equal(2, game.Columns[0].Count);
        Assert.Single(game.Columns[1]);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void 提示优先选择可以翻开背面牌的移动且不修改棋局()
    {
        var game = CreateEmptyGame();
        var columns = SpiderTestBoard.MutableColumns(game);
        columns[0].Add(SpiderTestBoard.Card(0, 10, faceUp: false));
        columns[0].Add(SpiderTestBoard.Card(1, 7));
        columns[1].Add(SpiderTestBoard.Card(2, 8, SpiderCardSuit.Hearts));
        columns[2].Add(SpiderTestBoard.Card(3, 5));
        columns[3].Add(SpiderTestBoard.Card(4, 6));
        var before = game.CreateSnapshot();

        var hint = Assert.IsType<SpiderHint>(game.FindHint());

        Assert.Equal(new SpiderHint(SpiderHintKind.Move, 0, 1, 1), hint);
        Assert.Equal(before.Stock.Count, game.Stock.Count);
        Assert.Equal(0, game.ActionCount);
    }

    [Fact]
    public void 同局重开恢复最初牌序并清空历史与计分()
    {
        var game = CreateGame();
        var initial = game.CreateSnapshot();
        var move = FindFirstLegalMove(game);
        Assert.NotNull(game.Move(move.Source, move.Index, move.Destination));

        game.ReplaySameDeal();

        Assert.Equal(initial.Columns.SelectMany(column => column).Select(card => card.Id),
            game.CreateSnapshot().Columns.SelectMany(column => column).Select(card => card.Id));
        Assert.Equal(0, game.ActionCount);
        Assert.Equal(500, game.Score);
        Assert.False(game.CanUndo);
    }

    private static SpiderSolitaireGame CreateGame(
        SpiderSolitaireDifficulty difficulty = SpiderSolitaireDifficulty.OneSuit) =>
        new(difficulty, new IdentitySpiderCardShuffler());

    private static SpiderSolitaireGame CreateEmptyGame()
    {
        var game = CreateGame();
        SpiderTestBoard.Clear(game);
        return game;
    }

    internal static (int Source, int Index, int Destination) FindFirstLegalMove(SpiderSolitaireGame game)
    {
        for (var source = 0; source < 10; source++)
        {
            for (var index = 0; index < game.Columns[source].Count; index++)
            {
                for (var destination = 0; destination < 10; destination++)
                {
                    if (game.CanMove(source, index, destination))
                    {
                        return (source, index, destination);
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("测试牌序中没有合法移动。");
    }
}
