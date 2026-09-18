using System.Collections.Immutable;

namespace ClassicGamePlugin.Features.RichTown.Domain;

/// <summary>阶段直接决定允许的命令；动画不是领域阶段，不能再次触发经济结算。</summary>
internal enum RichTownPhase { AwaitingRoll, AwaitingPurchase, AwaitingDebt, TurnActions, Finished }
internal enum RichTownFinishReason { LastSurvivor, RoundLimit }
internal enum RichTownPaymentReason { StartReward, ChanceReward, Purchase, Upgrade, Sale, Rent, Tax, ChanceFee }

/// <summary>座位编号固定为 0–2；位置单位为格子，现金为非负整数。出局玩家保留身份用于最终排名。</summary>
internal sealed record RichTownPlayer(int Id, int Position, int Cash, bool IsComputer, bool IsEliminated = false);

/// <summary>仅地产有此状态；银行持有时 OwnerId 为空且 Level 为零，出售时两项一起清除。</summary>
internal sealed record RichTownProperty(int Index, int? OwnerId = null, int Level = 0);

/// <summary>一笔已经确定的债务；空债权人代表银行。偿清前不扣部分现金，防止恢复或动画重复入账。</summary>
internal sealed record RichTownDebt(int DebtorId, int? CreditorId, int Amount, RichTownPaymentReason Reason);

/// <summary>
/// 对局的全部规则状态。不可变记录与不可变数组允许读者持有历史快照，无需复制或持锁绘制。
/// Random 保存算法版本及当前内部状态；Revision 是成功命令数，EventSequence 是最后一个已提交事件序号。
/// 这不是 SDK 存档 DTO；G4 仍需独立的格式、迁移、加载上限和保存握手。
/// </summary>
internal sealed record RichTownSnapshot(
    int RulesVersion,
    ImmutableArray<RichTownPlayer> Players,
    ImmutableArray<RichTownProperty> Properties,
    RichTownRandomState Random,
    int Round = 1,
    int CurrentPlayerId = 0,
    RichTownPhase Phase = RichTownPhase.AwaitingRoll,
    bool HasUpgraded = false,
    RichTownDebt? Debt = null,
    RichTownFinishReason? FinishReason = null,
    long Revision = 0,
    long EventSequence = 0)
{
    /// <summary>创建标准三人局；allComputer 只用于无图形模拟，默认一真人两电脑。任意 64 位种子均合法。</summary>
    public static RichTownSnapshot Create(ulong seed, bool allComputer = false) => new(
        RichTownBoard.RulesVersion,
        Enumerable.Range(0, RichTownBoard.PlayerCount)
            .Select(id => new RichTownPlayer(id, 0, RichTownBoard.InitialCash, allComputer || id != 0)).ToImmutableArray(),
        RichTownBoard.Cells.Where(cell => cell.Kind == RichTownCellKind.Property)
            .Select(cell => new RichTownProperty(cell.Index)).ToImmutableArray(),
        new RichTownRandomState(RichTownRandom.AlgorithmVersion, seed));

    /// <summary>
    /// 以账面总值降序排名，同值使用竞赛名次（例如 1、1、3），座位号仅稳定显示顺序。
    /// 最后一名存活者优先获胜；30 轮终局按总值排名。使用 long 求和避免合法 int 现金求总值时溢出。
    /// </summary>
    public ImmutableArray<RichTownStanding> GetStandings()
    {
        var scores = Players.Select(player => new
        {
            player.Id,
            player.IsEliminated,
            Score = (long)player.Cash + Properties.Where(property => property.OwnerId == player.Id).Sum(RichTownBoard.BookValue)
        }).OrderBy(player => FinishReason == RichTownFinishReason.LastSurvivor && player.IsEliminated)
            .ThenByDescending(player => player.Score).ThenBy(player => player.Id).ToArray();
        var result = ImmutableArray.CreateBuilder<RichTownStanding>(scores.Length);
        var rank = 1;
        for (var i = 0; i < scores.Length; i++)
        {
            if (i > 0 && (scores[i].Score != scores[i - 1].Score ||
                (FinishReason == RichTownFinishReason.LastSurvivor && scores[i].IsEliminated != scores[i - 1].IsEliminated))) rank = i + 1;
            result.Add(new RichTownStanding(scores[i].Id, scores[i].Score, rank,
                Phase == RichTownPhase.Finished && rank == 1 && !scores[i].IsEliminated));
        }
        return result.MoveToImmutable();
    }
}

internal sealed record RichTownStanding(int PlayerId, long Score, int Rank, bool IsWinner);
