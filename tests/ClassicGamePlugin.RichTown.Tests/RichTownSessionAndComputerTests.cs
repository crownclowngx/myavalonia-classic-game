using ClassicGamePlugin.Features.RichTown.Application;
using ClassicGamePlugin.Features.RichTown.Domain;
using Xunit;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownSessionAndComputerTests
{
    [Fact]
    public void 三十二个同修订并发命令只有一个成功且历史快照不变()
    {
        var initial = At();
        var session = new RichTownSession(initial);
        var request = new RichTownRequest(0, 0, new(RichTownCommandKind.Roll));
        var results = new RichTownTransition[32];
        Parallel.For(0, results.Length, index => results[index] = session.Submit(request));
        Assert.Single(results, result => result.Accepted);
        Assert.Equal(31, results.Count(result => result.Rejection == RichTownRejection.WrongRevision));
        Assert.All(results.Where(result => !result.Accepted), result => { Assert.Empty(result.Events); Assert.Same(session.Snapshot, result.Snapshot); });
        Assert.Equal(1, session.Snapshot.Revision);
        Assert.Equal(0, initial.Revision);
        Assert.Equal(0, initial.Players[0].Position);
        var independent = new RichTownSession(initial);
        Assert.Same(initial, independent.Snapshot);
        Assert.Equal(StateText(session.Snapshot), StateText(independent.Submit(request).Accepted()));
    }

    [Theory]
    [InlineData(1, 400, true)]
    [InlineData(1, 399, false)]
    [InlineData(8, 450, true)]
    [InlineData(8, 449, false)]
    [InlineData(16, 500, true)]
    [InlineData(16, 499, false)]
    public void 电脑购买后保留至少三百现金(int index, int cash, bool buys)
    {
        var state = At(index, cash, phase: RichTownPhase.AwaitingPurchase).Player(0, player => player with { IsComputer = true });
        var request = Assert.IsType<RichTownRequest>(RichTownComputerPlayer.Decide(state));
        Assert.Equal(buys ? RichTownCommandKind.Buy : RichTownCommandKind.Decline, request.Command.Kind);
        var after = new RichTownSession(state).Submit(request).Accepted();
        Assert.True(after.Players[0].Cash >= 300);
    }

    [Fact]
    public void 电脑升级挑选编号最小的可负担地产并遵守本回合额度()
    {
        var state = At(cash: 400, phase: RichTownPhase.TurnActions).Own(1, level: 2).Own(2).Own(4).Own(8)
            .Player(0, player => player with { IsComputer = true });
        var request = RichTownComputerPlayer.Decide(state)!;
        Assert.Equal(new RichTownCommand(RichTownCommandKind.Upgrade, 2), request.Command);
        var after = new RichTownSession(state).Submit(request).Accepted();
        Assert.Equal(300, after.Players[0].Cash);
        Assert.Equal(RichTownCommandKind.EndTurn, RichTownComputerPlayer.Decide(after)!.Command.Kind);
        var rich = after.Player(0, player => player with { Cash = 1000 });
        Assert.Equal(RichTownCommandKind.EndTurn, RichTownComputerPlayer.Decide(rich)!.Command.Kind);
        var poor = state.Player(0, player => player with { Cash = 399 });
        Assert.Equal(RichTownCommandKind.EndTurn, RichTownComputerPlayer.Decide(poor)!.Command.Kind);
    }

    [Fact]
    public void 电脑偿债按折价及编号排序并在足额后停止出售()
    {
        var state = At(15, 90).Own(16, 1, 2).Own(1, level: 2).Own(2).Own(4).Own(8)
            .Player(0, player => player with { IsComputer = true }).Act(RichTownCommandKind.Roll).Accepted();
        var session = new RichTownSession(state);
        foreach (var expected in new[] { 2, 4, 8 })
        {
            var request = RichTownComputerPlayer.Decide(session.Snapshot)!;
            Assert.Equal(new RichTownCommand(RichTownCommandKind.Sell, expected), request.Command);
            session.Submit(request).Accepted();
        }
        Assert.Null(session.Snapshot.Debt);
        Assert.Equal(65, session.Snapshot.Players[0].Cash);
        Assert.Equal(0, session.Snapshot.Properties.Single(property => property.Index == 1).OwnerId);
        Assert.Equal(RichTownCommandKind.EndTurn, RichTownComputerPlayer.Decide(session.Snapshot)!.Command.Kind);
    }

    [Fact]
    public void 电脑不接管真人不窥视随机状态且不直接推进会话()
    {
        Assert.Null(RichTownComputerPlayer.Decide(At()));
        var state = RichTownSnapshot.Create(0, allComputer: true);
        var session = new RichTownSession(state);
        var before = StateText(state);
        var request = RichTownComputerPlayer.Decide(state);
        Assert.Equal(RichTownCommandKind.Roll, request!.Command.Kind);
        Assert.Equal(request, RichTownComputerPlayer.Decide(state with { Random = new(1, ulong.MaxValue) }));
        Assert.Same(state, session.Snapshot);
        Assert.Equal(before, StateText(state));
        Assert.Null(RichTownComputerPlayer.Decide(state with { Round = 30, Phase = RichTownPhase.Finished, FinishReason = RichTownFinishReason.RoundLimit }));
    }

    [Fact]
    public void 购买和偿债决策点恢复后的命令事件及快照一致()
    {
        var states = new[]
        {
            At(1, phase: RichTownPhase.AwaitingPurchase),
            At(11, 0).Own(1, level: 2).Act(RichTownCommandKind.Roll).Accepted(),
            At(15, 0).Own(16, 1, 2).Own(1).Own(8, level: 1).Act(RichTownCommandKind.Roll).Accepted().Act(RichTownCommandKind.Sell, 1).Accepted()
        };
        foreach (var candidate in states)
        {
            var state = candidate.Player(0, player => player with { IsComputer = true });
            var original = new RichTownSession(state);
            // 重新构造集合与随机状态，验证续序不依赖原对象身份；文件解析与 SDK 保存不属于 G2。
            var restored = new RichTownSession(state with { Players = [.. state.Players], Properties = [.. state.Properties], Random = new(state.Random.Version, state.Random.State) });
            for (var i = 0; i < 50 && original.Snapshot.Phase != RichTownPhase.Finished; i++)
            {
                var request = RichTownComputerPlayer.Decide(original.Snapshot)!;
                Assert.Equal(request, RichTownComputerPlayer.Decide(restored.Snapshot));
                var left = original.Submit(request);
                var right = restored.Submit(request);
                Assert.Equal(StateText(left.Accepted()), StateText(right.Accepted()));
                Assert.Equal(EventText(left.Events), EventText(right.Events));
            }
        }
    }
}
