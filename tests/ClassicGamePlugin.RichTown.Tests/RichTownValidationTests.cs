using System.Collections.Immutable;
using ClassicGamePlugin.Features.RichTown.Application;
using ClassicGamePlugin.Features.RichTown.Domain;
using Xunit;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownValidationTests
{
    [Fact]
    public void 命令和阶段笛卡尔矩阵仅开放规定的动作()
    {
        foreach (var phase in Enum.GetValues<RichTownPhase>())
            foreach (var kind in Enum.GetValues<RichTownCommandKind>())
            {
                var state = At(2, phase: phase).Own(1);
                if (phase == RichTownPhase.AwaitingDebt) state = At(11, 0).Own(1, level: 2).Act(RichTownCommandKind.Roll).Accepted();
                if (phase == RichTownPhase.Finished) state = state with { Round = 30, FinishReason = RichTownFinishReason.RoundLimit };
                var allowed = phase switch
                {
                    RichTownPhase.AwaitingRoll => kind == RichTownCommandKind.Roll,
                    RichTownPhase.AwaitingPurchase => kind is RichTownCommandKind.Buy or RichTownCommandKind.Decline,
                    RichTownPhase.AwaitingDebt => kind == RichTownCommandKind.Sell,
                    RichTownPhase.TurnActions => kind is RichTownCommandKind.Upgrade or RichTownCommandKind.Sell or RichTownCommandKind.EndTurn,
                    _ => false
                };
                var result = state.Act(kind, kind is RichTownCommandKind.Upgrade or RichTownCommandKind.Sell ? 1 : null);
                if (allowed) result.Accepted();
                else Rejected(state, result, RichTownRejection.WrongPhase);
            }
    }

    [Fact]
    public void 错版本玩家命令和地产参数都拒绝且无副作用()
    {
        var state = At(phase: RichTownPhase.TurnActions).Own(1).Own(2, 1);
        Rejected(state, RichTownRules.Apply(state, new(1, 0, new(RichTownCommandKind.EndTurn))), RichTownRejection.WrongRevision);
        foreach (var actor in new[] { -1, 1, 2, 3, int.MaxValue })
            Rejected(state, RichTownRules.Apply(state, new(0, actor, new(RichTownCommandKind.EndTurn))), RichTownRejection.WrongPlayer);
        foreach (var command in new RichTownCommand[] { new((RichTownCommandKind)99), new(RichTownCommandKind.Roll, 1),
            new(RichTownCommandKind.Upgrade), new(RichTownCommandKind.Sell), null! })
            Rejected(state, RichTownRules.Apply(state, new(0, 0, command)), RichTownRejection.InvalidCommand);
        foreach (var kind in new[] { RichTownCommandKind.Upgrade, RichTownCommandKind.Sell })
        {
            foreach (var index in new[] { -1, 0, 3, 6, 12, 15, 18, 24, int.MaxValue })
                Rejected(state, state.Act(kind, index), RichTownRejection.InvalidProperty);
            Rejected(state, state.Act(kind, 2), RichTownRejection.NotOwner);
            Rejected(state, state.Act(kind, 4), RichTownRejection.NotOwner);
        }
        Assert.Throws<ArgumentNullException>(() => RichTownRules.Apply(state, null!));
    }

    [Fact]
    public void 金额或事件序号溢出回滚整个命令包括随机状态()
    {
        var cases = new[]
        {
            At(23, int.MaxValue),
            At().Own(1, 1).Player(1, player => player with { Cash = int.MaxValue }),
            At(cash: int.MaxValue, phase: RichTownPhase.TurnActions).Own(1),
            At() with { EventSequence = long.MaxValue },
            At() with { EventSequence = long.MaxValue - 1 },
            At() with { Revision = long.MaxValue, EventSequence = long.MaxValue }
        };
        foreach (var state in cases)
        {
            var session = new RichTownSession(state);
            var command = state.Phase == RichTownPhase.TurnActions ? new RichTownCommand(RichTownCommandKind.Sell, 1) : new(RichTownCommandKind.Roll);
            Rejected(state, session.Submit(new(state.Revision, 0, command)), RichTownRejection.NumericLimit);
            Assert.Same(state, session.Snapshot);
        }
    }

    [Fact]
    public void 坏快照拒绝构造会话与直接规则入口()
    {
        var valid = At();
        var debt = At(11, 0).Own(1, level: 2).Act(RichTownCommandKind.Roll).Accepted();
        var rental = At(15, 0).Own(16, 1, 2).Own(1, level: 2).Own(2).Act(RichTownCommandKind.Roll).Accepted();
        var cases = new Dictionary<string, RichTownSnapshot>
        {
            ["规则版本"] = valid with { RulesVersion = 99 },
            ["随机版本"] = valid with { Random = new(0, 0) },
            ["零轮次"] = valid with { Round = 0 },
            ["超出轮次"] = valid with { Round = 31 },
            ["未知阶段"] = valid with { Phase = (RichTownPhase)99 },
            ["当前玩家负数"] = valid with { CurrentPlayerId = -1 },
            ["当前玩家越界"] = valid with { CurrentPlayerId = 3 },
            ["负版本"] = valid with { Revision = -1 },
            ["事件落后"] = valid with { Revision = 1, EventSequence = 0 },
            ["缺省玩家数组"] = valid with { Players = default },
            ["错误玩家数量"] = valid with { Players = [] },
            ["空玩家"] = valid with { Players = valid.Players.SetItem(0, null!) },
            ["重复座位"] = valid.Player(1, player => player with { Id = 0 }),
            ["负位置"] = valid.Player(0, player => player with { Position = -1 }),
            ["位置越界"] = valid.Player(0, player => player with { Position = 24 }),
            ["现金负数"] = valid.Player(0, player => player with { Cash = -1 }),
            ["出局仍有现金"] = valid.Player(1, player => player with { IsEliminated = true }),
            ["缺省地产数组"] = valid with { Properties = default },
            ["缺少地产"] = valid with { Properties = [] },
            ["空地产"] = valid with { Properties = valid.Properties.SetItem(0, null!) },
            ["错序地产"] = valid with { Properties = valid.Properties.Reverse().ToImmutableArray() },
            ["等级负数"] = valid.Own(1, level: -1),
            ["等级越界"] = valid.Own(1, level: 3),
            ["业主越界"] = valid.Own(1, 3),
            ["业主负数"] = valid.Own(1, -1),
            ["出局有地产"] = valid.Own(1, 1).Player(1, player => player with { Cash = 0, IsEliminated = true }),
            ["银行有建筑"] = valid with { Properties = valid.Properties.SetItem(0, valid.Properties[0] with { Level = 1 }) },
            ["当前玩家出局"] = valid.Player(0, player => player with { Cash = 0, IsEliminated = true }),
            ["仅剩一人未结束"] = valid.Player(1, player => player with { Cash = 0, IsEliminated = true }).Player(2, player => player with { Cash = 0, IsEliminated = true }),
            ["结束缺原因"] = valid with { Phase = RichTownPhase.Finished },
            ["未知终局原因"] = valid with { Phase = RichTownPhase.Finished, FinishReason = (RichTownFinishReason)99 },
            ["末人胜利仍有三人"] = valid with { Phase = RichTownPhase.Finished, FinishReason = RichTownFinishReason.LastSurvivor },
            ["未满轮数结束"] = valid with { Phase = RichTownPhase.Finished, FinishReason = RichTownFinishReason.RoundLimit },
            ["末人胜利误标轮次终局"] = valid.Player(1, player => player with { Cash = 0, IsEliminated = true }).Player(2, player => player with { Cash = 0, IsEliminated = true }) with { Round = 30, Phase = RichTownPhase.Finished, FinishReason = RichTownFinishReason.RoundLimit },
            ["未结束残留原因"] = valid with { FinishReason = RichTownFinishReason.RoundLimit },
            ["掷骰前已升级"] = valid with { HasUpgraded = true },
            ["购买位置非地产"] = valid with { Phase = RichTownPhase.AwaitingPurchase },
            ["购买他人地产"] = At(1, phase: RichTownPhase.AwaitingPurchase).Own(1, 1),
            ["债务缺内容"] = valid with { Phase = RichTownPhase.AwaitingDebt },
            ["其他阶段有债务"] = valid with { Debt = debt.Debt },
            ["债务不是当前玩家"] = debt with { Debt = debt.Debt! with { DebtorId = 1 } },
            ["债务金额非正"] = debt with { Debt = debt.Debt! with { Amount = 0 } },
            ["有现钱却等待偿债"] = debt.Player(0, player => player with { Cash = 100 }),
            ["无力清偿未破产"] = debt with { Debt = debt.Debt! with { Amount = 151 } },
            ["税金额不匹配"] = debt with { Debt = debt.Debt! with { Amount = 99 } },
            ["银行原因不匹配"] = debt with { Debt = debt.Debt! with { Reason = RichTownPaymentReason.Rent } },
            ["银行地块不匹配"] = debt.Player(0, player => player with { Position = 0 }),
            ["银行机会金额不匹配"] = debt.Player(0, player => player with { Position = 6 }) with { Debt = debt.Debt! with { Reason = RichTownPaymentReason.ChanceFee, Amount = 99 } },
            ["债权人越界"] = rental with { Debt = rental.Debt! with { CreditorId = 3 } },
            ["自己欠自己"] = rental with { Debt = rental.Debt! with { CreditorId = 0 } },
            ["债权人出局"] = debt.Player(1, player => player with { Cash = 0, IsEliminated = true }) with { Debt = debt.Debt! with { CreditorId = 1 } },
            ["租金金额不匹配"] = rental with { Debt = rental.Debt! with { Amount = 199 } },
            ["租金债权人不匹配"] = rental with { Debt = rental.Debt! with { CreditorId = 2 } },
            ["租金落点不匹配"] = rental.Player(0, player => player with { Position = 3 }),
            ["租金原因不匹配"] = rental with { Debt = rental.Debt! with { Reason = RichTownPaymentReason.Tax } }
        };
        foreach (var (name, state) in cases)
        {
            var exception = Record.Exception(() => new RichTownSession(state));
            Assert.True(exception is ArgumentException, $"{name} 应返回参数错误，实际：{exception}");
            Assert.ThrowsAny<ArgumentException>(() => state.Act(RichTownCommandKind.Roll));
        }
        Assert.Throws<ArgumentNullException>(() => new RichTownSession(null!));
    }

    [Fact]
    public void 总资产计算容纳最大整数现金但等级无效不能算租金()
    {
        Assert.Equal((long)int.MaxValue + 300, At(cash: int.MaxValue).Own(1, level: 2).GetStandings()[0].Score);
        Assert.Throws<ArgumentOutOfRangeException>(() => RichTownBoard.Rent(new(1, 0, 3)));
    }
}
