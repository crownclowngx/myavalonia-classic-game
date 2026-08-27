using ClassicGamePlugin.Features.FreeCell.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class FreeCellRulesTests
{
    [Fact]
    public void 编号候选包含完整唯一牌组且初始按七六张轮流发牌()
    {
        var deal = FreeCellDealProvider.CreateCandidate(20260827, 0);
        var snapshot = FreeCellRules.CreateInitialSnapshot(deal, autoCollect: false);

        Assert.Equal(52, deal.Deck.Count);
        Assert.Equal(52, deal.Deck.Select(card => card.Id).Distinct().Count());
        Assert.All(deal.Deck.GroupBy(card => (card.Suit, card.Rank)), group => Assert.Single(group));
        Assert.Equal([7, 7, 7, 7, 6, 6, 6, 6], snapshot.Tableaus.Select(column => column.Count));
        Assert.All(snapshot.FreeCells, Assert.Null);
        Assert.Equal([0, 0, 0, 0], snapshot.Foundations);
    }

    [Fact]
    public void 同一编号候选稳定而候选序号会产生不同牌序()
    {
        var first = FreeCellDealProvider.CreateCandidate(88, 3);
        var same = FreeCellDealProvider.CreateCandidate(88, 3);
        var other = FreeCellDealProvider.CreateCandidate(88, 4);

        Assert.Equal(first.Deck, same.Deck);
        Assert.NotEqual(first.Deck.Select(card => card.Id), other.Deck.Select(card => card.Id));
    }

    [Fact]
    public void 交替降序牌组可移动且同色或非连续牌组拒绝()
    {
        var blackSeven = FreeCellTestData.Card(1, 7);
        var redSix = FreeCellTestData.Card(2, 6, FreeCellSuit.Hearts);
        var blackFive = FreeCellTestData.Card(3, 5, FreeCellSuit.Clubs);
        var redEight = FreeCellTestData.Card(4, 8, FreeCellSuit.Diamonds);
        var snapshot = FreeCellTestData.Snapshot([[blackSeven, redSix, blackFive], [redEight]]);
        var move = new FreeCellMove(FreeCellLocation.Tableau(0), 0, FreeCellLocation.Tableau(1));

        Assert.True(FreeCellRules.CanMove(snapshot, move));
        Assert.False(FreeCellRules.IsDescendingAlternating([blackSeven, FreeCellTestData.Card(5, 6)], 0));
        Assert.False(FreeCellRules.IsDescendingAlternating([blackSeven, FreeCellTestData.Card(6, 5, FreeCellSuit.Hearts)], 0));
    }

    [Fact]
    public void 批量容量使用空闲单元和空列且目标空列不计入中转()
    {
        var occupied = FreeCellTestData.Card(10, 9);
        var snapshot = FreeCellTestData.Snapshot(
            [[occupied], [occupied], [occupied], [occupied], [occupied], [occupied]],
            [occupied, null, null, null]);

        Assert.Equal(16, FreeCellRules.GetMovableSequenceCapacity(snapshot, 0));
        Assert.Equal(8, FreeCellRules.GetMovableSequenceCapacity(snapshot, 6));
    }

    [Fact]
    public void 单牌可在牌列空闲单元和对应基础区之间前进但基础区不可取回()
    {
        var ace = FreeCellTestData.Card(1, 1, FreeCellSuit.Hearts);
        var blackTwo = FreeCellTestData.Card(2, 2, FreeCellSuit.Clubs);
        var snapshot = FreeCellTestData.Snapshot([[ace], [blackTwo]]);

        Assert.True(FreeCellRules.CanMove(snapshot,
            new FreeCellMove(FreeCellLocation.Tableau(0), 0, FreeCellLocation.Cell(0))));
        Assert.True(FreeCellRules.CanMove(snapshot,
            new FreeCellMove(FreeCellLocation.Tableau(0), 0, FreeCellLocation.Foundation(FreeCellSuit.Hearts))));
        Assert.False(FreeCellRules.CanMove(snapshot,
            new FreeCellMove(FreeCellLocation.Foundation(FreeCellSuit.Hearts), 0, FreeCellLocation.Tableau(1))));
    }

    [Fact]
    public void 安全自动收牌先收A再在两种相反颜色到位后收二()
    {
        var snapshot = FreeCellTestData.Snapshot([
            [FreeCellTestData.Card(4, 2), FreeCellTestData.Card(0, 1)],
            [FreeCellTestData.Card(17, 2, FreeCellSuit.Hearts), FreeCellTestData.Card(13, 1, FreeCellSuit.Hearts)],
            [FreeCellTestData.Card(30, 2, FreeCellSuit.Clubs), FreeCellTestData.Card(26, 1, FreeCellSuit.Clubs)],
            [FreeCellTestData.Card(43, 2, FreeCellSuit.Diamonds), FreeCellTestData.Card(39, 1, FreeCellSuit.Diamonds)],
        ]);

        var result = FreeCellRules.CollectSafeCards(snapshot, incrementMove: false);

        Assert.Equal(8, result.CardIds.Count);
        Assert.Equal([2, 2, 2, 2], result.Snapshot.Foundations);
        Assert.Equal(0, result.Snapshot.MoveCount);
    }

    [Fact]
    public void 非法移动返回空且不会改变输入快照()
    {
        var snapshot = FreeCellTestData.Snapshot([[FreeCellTestData.Card(1, 7)], [FreeCellTestData.Card(2, 7, FreeCellSuit.Hearts)]]);

        var result = FreeCellRules.TryApplyMove(
            snapshot,
            new FreeCellMove(FreeCellLocation.Tableau(0), 0, FreeCellLocation.Tableau(1)),
            autoCollect: true);

        Assert.Null(result);
        Assert.Single(snapshot.Tableaus[0]);
        Assert.Single(snapshot.Tableaus[1]);
        Assert.Equal(0, snapshot.MoveCount);
    }
}
