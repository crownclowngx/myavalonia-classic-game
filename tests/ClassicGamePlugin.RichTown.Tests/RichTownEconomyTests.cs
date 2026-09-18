using ClassicGamePlugin.Features.RichTown.Domain;
using Xunit;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownEconomyTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(8, 150)]
    [InlineData(16, 200)]
    public void 购买恰好够钱成功而不足一元不产生债务(int index, int price)
    {
        var before = At(index, price, phase: RichTownPhase.AwaitingPurchase);
        var after = before.Act(RichTownCommandKind.Buy).Accepted();
        Assert.Equal(0, after.Players[0].Cash);
        Assert.Equal(0, after.Properties.Single(property => property.Index == index).OwnerId);
        Assert.Equal(RichTownPhase.TurnActions, after.Phase);
        Assert.Null(after.Debt);
        Assert.Equal(price, before.Players[0].Cash);
        var insufficient = At(index, price - 1, phase: RichTownPhase.AwaitingPurchase);
        Rejected(insufficient, insufficient.Act(RichTownCommandKind.Buy), RichTownRejection.InsufficientCash);
        Rejected(after, after.Act(RichTownCommandKind.Buy), RichTownRejection.WrongPhase);
    }

    [Fact]
    public void 放弃购买不扣款不拍卖并可结束回合()
    {
        var result = At(1, 0, phase: RichTownPhase.AwaitingPurchase).Act(RichTownCommandKind.Decline);
        var after = result.Accepted();
        Assert.Equal(0, after.Players[0].Cash);
        Assert.Null(after.Properties[0].OwnerId);
        Assert.IsType<RichTownPurchaseDeclined>(Assert.Single(result.Events));
        Assert.Equal(1, after.Act(RichTownCommandKind.EndTurn).Accepted().CurrentPlayerId);
    }

    [Theory]
    [InlineData(1, 0, 20)]
    [InlineData(1, 1, 50)]
    [InlineData(1, 2, 100)]
    [InlineData(8, 0, 30)]
    [InlineData(8, 1, 75)]
    [InlineData(8, 2, 150)]
    [InlineData(16, 0, 40)]
    [InlineData(16, 1, 100)]
    [InlineData(16, 2, 200)]
    public void 各档租金转给房主一次且资金守恒(int index, int level, int rent)
    {
        var before = At(index - 1).Own(index, 1, level);
        var result = before.Act(RichTownCommandKind.Roll);
        var after = result.Accepted();
        Assert.Equal(1500 - rent, after.Players[0].Cash);
        Assert.Equal(1500 + rent, after.Players[1].Cash);
        Assert.Equal(4500, after.Players.Sum(player => player.Cash));
        var payment = Assert.Single(result.Events.OfType<RichTownMoneyTransferred>());
        Assert.Equal((0, 1, rent, RichTownPaymentReason.Rent), (payment.From, payment.To, payment.Amount, payment.Reason));
    }

    [Fact]
    public void 自己的地产不收租()
    {
        var result = At().Own(1, level: 2).Act(RichTownCommandKind.Roll);
        Assert.Equal(1500, result.Accepted().Players[0].Cash);
        Assert.Empty(result.Events.OfType<RichTownMoneyTransferred>());
        Assert.Equal(RichTownPhase.TurnActions, result.Snapshot.Phase);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(8, 150)]
    [InlineData(16, 200)]
    public void 升级不要求停在该地产且每回合最多一次并限制两级(int index, int price)
    {
        var before = At(0, price, phase: RichTownPhase.TurnActions).Own(index).Own(index + 1);
        var after = before.Act(RichTownCommandKind.Upgrade, index).Accepted();
        Assert.Equal(0, after.Players[0].Cash);
        Assert.Equal(1, after.Properties.Single(property => property.Index == index).Level);
        Assert.True(after.HasUpgraded);
        Rejected(after, after.Act(RichTownCommandKind.Upgrade, index + 1), RichTownRejection.UpgradeLimit);
        var nextTurn = after with { HasUpgraded = false, Players = after.Players.SetItem(0, after.Players[0] with { Cash = price }) };
        var twice = nextTurn.Act(RichTownCommandKind.Upgrade, index).Accepted();
        Assert.Equal(2, twice.Properties.Single(property => property.Index == index).Level);
        var maximum = twice with { HasUpgraded = false };
        Rejected(maximum, maximum.Act(RichTownCommandKind.Upgrade, index), RichTownRejection.UpgradeLimit);
        var insufficient = before.Player(0, player => player with { Cash = price - 1 });
        Rejected(insufficient, insufficient.Act(RichTownCommandKind.Upgrade, index), RichTownRejection.InsufficientCash);
    }

    [Theory]
    [InlineData(1, 0, 50)]
    [InlineData(1, 1, 100)]
    [InlineData(1, 2, 150)]
    [InlineData(8, 0, 75)]
    [InlineData(8, 1, 150)]
    [InlineData(8, 2, 225)]
    [InlineData(16, 0, 100)]
    [InlineData(16, 1, 200)]
    [InlineData(16, 2, 300)]
    public void 出售连同建筑半价回收且不能重复出售(int index, int level, int sale)
    {
        var after = At(phase: RichTownPhase.TurnActions).Own(index, level: level).Act(RichTownCommandKind.Sell, index).Accepted();
        Assert.Equal(1500 + sale, after.Players[0].Cash);
        var property = after.Properties.Single(property => property.Index == index);
        Assert.Null(property.OwnerId);
        Assert.Equal(0, property.Level);
        Rejected(after, after.Act(RichTownCommandKind.Sell, index), RichTownRejection.NotOwner);
    }

    [Fact]
    public void 出售已升级地产不会重置本回合升级额度()
    {
        var state = At(phase: RichTownPhase.TurnActions).Own(1).Own(2);
        state = state.Act(RichTownCommandKind.Upgrade, 1).Accepted().Act(RichTownCommandKind.Sell, 1).Accepted();
        Rejected(state, state.Act(RichTownCommandKind.Upgrade, 2), RichTownRejection.UpgradeLimit);
    }

    [Fact]
    public void 银行税务等待出售并只结算一次保留剩余现金()
    {
        var opening = At(11, 40).Own(1, level: 1).Act(RichTownCommandKind.Roll);
        var debt = opening.Accepted();
        Assert.Equal(RichTownPhase.AwaitingDebt, debt.Phase);
        Assert.Equal(new RichTownDebt(0, null, 100, RichTownPaymentReason.Tax), debt.Debt);
        Assert.Equal(40, debt.Players[0].Cash);
        Assert.Empty(opening.Events.OfType<RichTownMoneyTransferred>());
        var settled = debt.Act(RichTownCommandKind.Sell, 1);
        Assert.Equal(40, settled.Accepted().Players[0].Cash);
        Assert.Null(settled.Snapshot.Debt);
        Assert.Equal((100, 0), (Assert.Single(settled.Events.OfType<RichTownDebtSettled>()).Paid, Assert.Single(settled.Events.OfType<RichTownDebtSettled>()).Waived));
        Assert.Equal(new[] { RichTownPaymentReason.Sale, RichTownPaymentReason.Tax }, settled.Events.OfType<RichTownMoneyTransferred>().Select(item => item.Reason));
        var ended = settled.Snapshot.Act(RichTownCommandKind.EndTurn);
        Assert.Empty(ended.Events.OfType<RichTownMoneyTransferred>());
    }

    [Fact]
    public void 多次出售恰好偿清租金后债权人才一次入账()
    {
        var debt = At(15, 0).Own(16, 1, 2).Own(1).Own(8, level: 1).Act(RichTownCommandKind.Roll).Accepted();
        Assert.Equal(new RichTownDebt(0, 1, 200, RichTownPaymentReason.Rent), debt.Debt);
        var one = debt.Act(RichTownCommandKind.Sell, 1);
        var partial = one.Accepted();
        Assert.Equal(RichTownPhase.AwaitingDebt, partial.Phase);
        Assert.Equal((50, 1500), (partial.Players[0].Cash, partial.Players[1].Cash));
        Assert.Empty(one.Events.OfType<RichTownDebtSettled>());
        var two = partial.Act(RichTownCommandKind.Sell, 8);
        var after = two.Accepted();
        Assert.Equal((0, 1700), (after.Players[0].Cash, after.Players[1].Cash));
        Assert.Equal(RichTownPhase.TurnActions, after.Phase);
        Assert.Single(two.Events.OfType<RichTownDebtSettled>());
        Assert.Equal(200, Assert.Single(two.Events.OfType<RichTownMoneyTransferred>(), item => item.Reason == RichTownPaymentReason.Rent).Amount);
    }

    [Fact]
    public void 债务状态仅能出售本人资产且旧命令不能重复还款()
    {
        var debt = At(11, 0).Own(1, level: 2).Act(RichTownCommandKind.Roll).Accepted();
        foreach (var kind in new[] { RichTownCommandKind.Roll, RichTownCommandKind.Buy, RichTownCommandKind.Decline, RichTownCommandKind.Upgrade, RichTownCommandKind.EndTurn })
            Rejected(debt, debt.Act(kind, kind == RichTownCommandKind.Upgrade ? 1 : null), RichTownRejection.WrongPhase);
        Rejected(debt, debt.Act(RichTownCommandKind.Sell, 2), RichTownRejection.NotOwner);
        var after = debt.Act(RichTownCommandKind.Sell, 1).Accepted();
        Rejected(after, RichTownRules.Apply(after, new(debt.Revision, 0, new(RichTownCommandKind.Sell, 1))), RichTownRejection.WrongRevision);
    }

    [Fact]
    public void 无力偿还租金自动清算全部资产支付现款并免除差额()
    {
        var result = At(15, 20).Own(16, 1, 2).Own(1).Own(2).Act(RichTownCommandKind.Roll);
        var after = result.Accepted();
        Assert.True(after.Players[0].IsEliminated);
        Assert.Equal(0, after.Players[0].Cash);
        Assert.Equal(1620, after.Players[1].Cash);
        Assert.DoesNotContain(after.Properties, property => property.OwnerId == 0);
        Assert.Equal(new[] { 1, 2 }, result.Events.OfType<RichTownPropertyChanged>().Select(item => item.PropertyIndex));
        Assert.All(result.Events.OfType<RichTownPropertyChanged>(), item => Assert.Equal(RichTownPropertyChange.Liquidated, item.Change));
        var settlement = Assert.Single(result.Events.OfType<RichTownDebtSettled>());
        Assert.Equal((120, 80), (settlement.Paid, settlement.Waived));
        Assert.Single(result.Events.OfType<RichTownBankrupt>());
        Assert.Equal((1, 1, RichTownPhase.AwaitingRoll), (after.CurrentPlayerId, after.Round, after.Phase));
        Assert.Null(after.Debt);
    }

    [Theory]
    [InlineData(11, 19UL, 100)]
    [InlineData(5, 19UL, 100)]
    [InlineData(0, 1UL, 200)]
    public void 银行债务零资金破产不产生零金额入账(int from, ulong seed, int due)
    {
        var result = At(from, 0, seed).Act(RichTownCommandKind.Roll);
        Assert.True(result.Accepted().Players[0].IsEliminated);
        Assert.Empty(result.Events.OfType<RichTownMoneyTransferred>());
        Assert.Equal((0, due), (Assert.Single(result.Events.OfType<RichTownDebtSettled>()).Paid, Assert.Single(result.Events.OfType<RichTownDebtSettled>()).Waived));
    }

    [Fact]
    public void 倒数第二名破产立即结束最后一名即使零资产也获胜()
    {
        var before = At(11, 0).Player(1, player => player with { Cash = 0, IsEliminated = true }).Player(2, player => player with { Cash = 0 });
        var result = before.Act(RichTownCommandKind.Roll);
        var after = result.Accepted();
        Assert.Equal(RichTownFinishReason.LastSurvivor, after.FinishReason);
        Assert.Equal(2, after.CurrentPlayerId);
        Assert.Equal(2, Assert.Single(after.GetStandings(), standing => standing.IsWinner).PlayerId);
        Assert.Equal(new[] { 1, 2, 2 }, after.GetStandings().Select(standing => standing.Rank));
        Assert.Empty(result.Events.OfType<RichTownTurnStarted>());
    }

    [Fact]
    public void 全部资产恰好够偿债时等待出售而少一元时自动破产()
    {
        var enough = At(15, 150).Own(16, 1, 2).Own(1).Act(RichTownCommandKind.Roll).Accepted();
        Assert.Equal(RichTownPhase.AwaitingDebt, enough.Phase);
        Assert.False(enough.Players[0].IsEliminated);
        var settled = enough.Act(RichTownCommandKind.Sell, 1).Accepted();
        Assert.Equal(0, settled.Players[0].Cash);
        Assert.False(settled.Players[0].IsEliminated);
        var insufficient = At(15, 149).Own(16, 1, 2).Own(1).Act(RichTownCommandKind.Roll);
        Assert.True(insufficient.Accepted().Players[0].IsEliminated);
        Assert.Equal(1699, insufficient.Snapshot.Players[1].Cash);
        Assert.Equal(1, Assert.Single(insufficient.Events.OfType<RichTownDebtSettled>()).Waived);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 2, 1)]
    [InlineData(2, 0, 2)]
    public void 任意座位破产自动推进到正确的下一位与轮次(int actor, int next, int round)
    {
        var before = At().Player(actor, player => player with { Position = 11, Cash = 0 }) with { CurrentPlayerId = actor };
        var after = before.Act(RichTownCommandKind.Roll).Accepted();
        Assert.True(after.Players[actor].IsEliminated);
        Assert.Equal((next, round, RichTownPhase.AwaitingRoll), (after.CurrentPlayerId, after.Round, after.Phase));
    }

    [Fact]
    public void 第三十轮末位破产按轮次结束而非多给下一轮()
    {
        var before = At().Player(2, player => player with { Position = 11, Cash = 0 }) with { CurrentPlayerId = 2, Round = 30 };
        var result = before.Act(RichTownCommandKind.Roll);
        Assert.Equal(RichTownFinishReason.RoundLimit, result.Accepted().FinishReason);
        Assert.True(result.Snapshot.Players[2].IsEliminated);
        Assert.Equal(new[] { 0, 1 }, result.Snapshot.GetStandings().Where(standing => standing.IsWinner).Select(standing => standing.PlayerId));
        Assert.Empty(result.Events.OfType<RichTownTurnStarted>());
    }
}
