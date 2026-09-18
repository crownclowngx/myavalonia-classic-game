using ClassicGamePlugin.Features.RichTown.Domain;
using Xunit;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownRulesTests
{
    [Fact]
    public void 标准开局与二十四格地图保持唯一配置()
    {
        var state = RichTownSnapshot.Create(42);
        RichTownSnapshotValidator.Validate(state);
        Assert.Equal(Enumerable.Range(0, 24), RichTownBoard.Cells.Select(cell => cell.Index));
        Assert.Equal(new[] { 0, 3, 6, 12, 15, 18 }, RichTownBoard.Cells.Where(cell => cell.Kind != RichTownCellKind.Property).Select(cell => cell.Index));
        Assert.Equal(RichTownCellKind.Start, RichTownBoard.Cells[0].Kind);
        Assert.Equal(RichTownCellKind.Rest, RichTownBoard.Cells[3].Kind);
        Assert.Equal(RichTownCellKind.Rest, RichTownBoard.Cells[15].Kind);
        Assert.Equal(RichTownCellKind.Chance, RichTownBoard.Cells[6].Kind);
        Assert.Equal(RichTownCellKind.Chance, RichTownBoard.Cells[18].Kind);
        Assert.Equal(RichTownCellKind.Tax, RichTownBoard.Cells[12].Kind);
        Assert.Equal(18, state.Properties.Length);
        Assert.All(state.Properties, property => { Assert.Null(property.OwnerId); Assert.Equal(0, property.Level); });
        Assert.All(state.Players, player => { Assert.Equal(1500, player.Cash); Assert.Equal(0, player.Position); Assert.False(player.IsEliminated); });
        Assert.Equal(new[] { false, true, true }, state.Players.Select(player => player.IsComputer));
        Assert.Equal((1, 0, RichTownPhase.AwaitingRoll, 0L, 0L), (state.Round, state.CurrentPlayerId, state.Phase, state.Revision, state.EventSequence));
        Assert.All(RichTownBoard.Cells.Where(cell => cell.Kind == RichTownCellKind.Property), cell =>
            Assert.Equal(cell.Index < 8 ? 100 : cell.Index < 16 ? 150 : 200, cell.Price));
        Assert.All(RichTownBoard.Cells.Where(cell => cell.Kind != RichTownCellKind.Property), cell => Assert.Equal(0, cell.Price));
    }

    [Theory]
    [InlineData(0, 19UL, 1, 1500)]
    [InlineData(23, 19UL, 0, 1700)]
    [InlineData(23, 0UL, 1, 1700)]
    [InlineData(20, 1UL, 2, 1700)]
    public void 移动路径与起点奖励一次结算(int position, ulong seed, int target, int cash)
    {
        var before = At(position, seed: seed);
        var result = before.Act(RichTownCommandKind.Roll);
        var after = result.Accepted();
        Assert.Equal(target, after.Players[0].Position);
        Assert.Equal(cash, after.Players[0].Cash);
        var roll = Assert.IsType<RichTownRolled>(result.Events[0]);
        var move = Assert.IsType<RichTownMoved>(result.Events[1]);
        Assert.Equal(roll.Steps, move.Path.Length);
        Assert.Equal(Enumerable.Range(1, roll.Steps).Select(step => (position + step) % 24), move.Path);
        Assert.Equal((position, target), (move.From, move.To));
        Assert.Equal(cash > 1500 ? 1 : 0, result.Events.OfType<RichTownMoneyTransferred>().Count());
        Assert.Equal(0, after.CurrentPlayerId);
        Assert.Equal(1, after.Revision);
        Assert.Equal(position, before.Players[0].Position);
        Assert.Equal(1500, before.Players[0].Cash);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(15)]
    public void 休息格不收费也不跳过下一回合(int cell)
    {
        var result = At(cell - 1).Act(RichTownCommandKind.Roll);
        Assert.Equal(RichTownPhase.TurnActions, result.Accepted().Phase);
        Assert.Equal(1500, result.Snapshot.Players[0].Cash);
        Assert.Equal(2, result.Events.Length);
    }

    [Theory]
    [InlineData(0UL, 4, 0, 1600)]
    [InlineData(3UL, 2, 1, 1700)]
    [InlineData(19UL, 5, 2, 1400)]
    [InlineData(1UL, 0, 3, 1300)]
    public void 四张机会卡只执行一次且消费两个随机样本(ulong seed, int from, int card, int cash)
    {
        var result = At(from, seed: seed).Act(RichTownCommandKind.Roll);
        Assert.Equal(cash, result.Accepted().Players[0].Cash);
        Assert.Equal(6, result.Snapshot.Players[0].Position);
        Assert.Equal(card, Assert.Single(result.Events.OfType<RichTownChanceDrawn>()).Card);
        Assert.Single(result.Events.OfType<RichTownMoneyTransferred>());
        Assert.Equal(RichTownRandom.NextUInt64(RichTownRandom.NextUInt64(new(1, seed)).State).State, result.Snapshot.Random);
    }

    [Fact]
    public void 税费恰好付清不进入债务且没有负余额()
    {
        var result = At(11, 100).Act(RichTownCommandKind.Roll);
        Assert.Equal(0, result.Accepted().Players[0].Cash);
        Assert.Null(result.Snapshot.Debt);
        Assert.Equal(RichTownPhase.TurnActions, result.Snapshot.Phase);
        var transfer = Assert.Single(result.Events.OfType<RichTownMoneyTransferred>());
        Assert.Equal((0, (int?)null, 100, RichTownPaymentReason.Tax), (transfer.From, transfer.To, transfer.Amount, transfer.Reason));
    }

    [Fact]
    public void 同一输入重算一致且非法重复掷骰不推进随机或序号()
    {
        var before = At(seed: 1);
        var one = before.Act(RichTownCommandKind.Roll);
        var two = before.Act(RichTownCommandKind.Roll);
        Assert.Equal(StateText(one.Accepted()), StateText(two.Accepted()));
        Assert.Equal(EventText(one.Events), EventText(two.Events));
        Rejected(one.Snapshot, one.Snapshot.Act(RichTownCommandKind.Roll), RichTownRejection.WrongPhase);
        Assert.Equal(Enumerable.Range(1, one.Events.Length).Select(value => (long)value), one.Events.Select(item => item.Sequence));
    }

    [Fact]
    public void 固定座位轮流行动并在最后一位之后进入下一轮()
    {
        var state = At(phase: RichTownPhase.TurnActions);
        for (var turn = 0; turn < 6; turn++)
        {
            Assert.Equal(turn % 3, state.CurrentPlayerId);
            Assert.Equal(turn / 3 + 1, state.Round);
            var result = state.Act(RichTownCommandKind.EndTurn);
            state = result.Accepted();
            Assert.Equal(RichTownPhase.AwaitingRoll, state.Phase);
            Assert.False(state.HasUpgraded);
            var started = Assert.Single(result.Events.OfType<RichTownTurnStarted>());
            Assert.Equal((state.CurrentPlayerId, state.Round), (started.PlayerId, started.Round));
            state = state with { Phase = RichTownPhase.TurnActions };
        }
    }

    [Theory]
    [InlineData(0, 1, 2, 1)]
    [InlineData(0, 2, 1, 2)]
    [InlineData(1, 0, 2, 1)]
    [InlineData(1, 2, 0, 2)]
    [InlineData(2, 0, 1, 1)]
    [InlineData(2, 1, 0, 2)]
    public void 跳过出局座位不重复或漏算轮次(int eliminated, int actor, int next, int round)
    {
        var state = At(phase: RichTownPhase.TurnActions).Player(eliminated, player => player with { Cash = 0, IsEliminated = true }) with { CurrentPlayerId = actor };
        var after = state.Act(RichTownCommandKind.EndTurn).Accepted();
        Assert.Equal((next, round), (after.CurrentPlayerId, after.Round));
    }

    [Fact]
    public void 第三十轮最后一位结束后按资产总值排名且同分并列()
    {
        var state = At(phase: RichTownPhase.TurnActions).Own(1).Player(0, player => player with { Cash = 1400 }) with { Round = 30, CurrentPlayerId = 2 };
        var result = state.Act(RichTownCommandKind.EndTurn);
        var after = result.Accepted();
        Assert.Equal(RichTownFinishReason.RoundLimit, after.FinishReason);
        Assert.Equal(RichTownPhase.Finished, after.Phase);
        Assert.Equal(30, after.Round);
        Assert.All(after.GetStandings(), standing => { Assert.Equal(1500, standing.Score); Assert.Equal(1, standing.Rank); Assert.True(standing.IsWinner); });
        Assert.Single(result.Events.OfType<RichTownGameFinished>());
        foreach (var kind in Enum.GetValues<RichTownCommandKind>())
            Rejected(after, after.Act(kind, kind is RichTownCommandKind.Upgrade or RichTownCommandKind.Sell ? 1 : null), RichTownRejection.WrongPhase);
    }

    [Fact]
    public void 名次包含升级账面成本且同分后留出名次()
    {
        var state = At().Own(1, level: 2).Player(0, player => player with { Cash = 1300 }).Player(1, player => player with { Cash = 1600 });
        Assert.Equal(new[] { 1, 1, 3 }, state.GetStandings().Select(item => item.Rank));
        Assert.Equal(new long[] { 1600, 1600, 1500 }, state.GetStandings().Select(item => item.Score));
        Assert.All(state.GetStandings(), item => Assert.False(item.IsWinner));
    }
}
