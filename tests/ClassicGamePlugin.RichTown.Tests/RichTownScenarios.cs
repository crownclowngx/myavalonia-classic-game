using System.Text.Json;
using ClassicGamePlugin.Features.RichTown.Domain;
using Xunit;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>仅供小镇测试构造局部场景；不复用其他游戏工具，也不绕过生产入口的完整快照校验。</summary>
internal static class RichTownScenarios
{
    // 独立计算的已知向量：种子 19 的第一次骰子为 1。场景不搜索“碰巧符合”的随机结果。
    public static RichTownSnapshot At(int position = 0, int cash = 1500, ulong seed = 19, RichTownPhase phase = RichTownPhase.AwaitingRoll)
    {
        var state = RichTownSnapshot.Create(seed);
        return state with { Players = state.Players.SetItem(0, state.Players[0] with { Position = position, Cash = cash }), Phase = phase };
    }

    public static RichTownSnapshot Own(this RichTownSnapshot state, int index, int owner = 0, int level = 0)
    {
        var property = state.Properties.Single(property => property.Index == index);
        return state with { Properties = state.Properties.Replace(property, property with { OwnerId = owner, Level = level }) };
    }

    public static RichTownSnapshot Player(this RichTownSnapshot state, int id, Func<RichTownPlayer, RichTownPlayer> change) =>
        state with { Players = state.Players.SetItem(id, change(state.Players[id])) };

    public static RichTownTransition Act(this RichTownSnapshot state, RichTownCommandKind kind, int? index = null) =>
        RichTownRules.Apply(state, new(state.Revision, state.CurrentPlayerId, new(kind, index)));

    public static RichTownSnapshot Accepted(this RichTownTransition result)
    {
        Assert.True(result.Accepted, $"意外拒绝：{result.Rejection}");
        Assert.NotEmpty(result.Events);
        RichTownSnapshotValidator.Validate(result.Snapshot);
        return result.Snapshot;
    }

    public static void Rejected(RichTownSnapshot before, RichTownTransition result, RichTownRejection reason)
    {
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Rejection);
        Assert.Same(before, result.Snapshot);
        Assert.Empty(result.Events);
    }

    public static string StateText(RichTownSnapshot state) => JsonSerializer.Serialize(state);
    public static string EventText(IEnumerable<RichTownEvent> events) => string.Join('\n', events.Select(item => JsonSerializer.Serialize(item, item.GetType())));
}
