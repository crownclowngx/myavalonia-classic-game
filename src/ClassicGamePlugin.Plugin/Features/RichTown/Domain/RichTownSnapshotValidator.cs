namespace ClassicGamePlugin.Features.RichTown.Domain;

/// <summary>
/// 校验规则快照的结构与可继续执行的不变量；供会话恢复和纯规则入口共同使用。
/// 它不证明快照来自某条真实历史，也不替代 G4 的不可信文件解析和格式迁移。失败抛出参数异常，不修改现有会话。
/// </summary>
internal static class RichTownSnapshotValidator
{
    public static void Validate(RichTownSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Require(snapshot.RulesVersion == RichTownBoard.RulesVersion, "规则版本不受支持。");
        Require(snapshot.Random.Version == RichTownRandom.AlgorithmVersion, "随机算法版本不受支持。");
        Require(snapshot.Round is >= 1 and <= RichTownBoard.MaxRounds, "轮次越界。");
        Require(Enum.IsDefined(snapshot.Phase), "阶段无效。");
        Require(snapshot.CurrentPlayerId is >= 0 and < RichTownBoard.PlayerCount, "当前玩家越界。");
        Require(snapshot.Revision >= 0 && snapshot.EventSequence >= snapshot.Revision, "修订或事件序号无效。");
        Require(!snapshot.Players.IsDefault && snapshot.Players.Length == RichTownBoard.PlayerCount, "必须有三个固定座位。");
        for (var id = 0; id < snapshot.Players.Length; id++)
        {
            var player = snapshot.Players[id];
            Require(player is not null, "玩家不能为空。");
            Require(player!.Id == id, "玩家编号必须按座位唯一排列。");
            Require(player.Position is >= 0 and < RichTownBoard.CellCount && player.Cash >= 0, "玩家位置或现金越界。");
            Require(!player.IsEliminated || player.Cash == 0, "出局玩家不能保留现金。");
        }

        var propertyCells = RichTownBoard.Cells.Where(cell => cell.Kind == RichTownCellKind.Property).ToArray();
        Require(!snapshot.Properties.IsDefault && snapshot.Properties.Length == propertyCells.Length, "地产数量错误。");
        for (var i = 0; i < propertyCells.Length; i++)
        {
            var property = snapshot.Properties[i];
            Require(property is not null, "地产不能为空。");
            Require(property!.Index == propertyCells[i].Index, "地产索引必须与地图按顺序一一对应。");
            Require(property.Level is >= 0 and <= RichTownBoard.MaxLevel, "建筑等级越界。");
            if (property.OwnerId is { } owner)
            {
                Require(owner is >= 0 and < RichTownBoard.PlayerCount, "地产所有者越界。");
                Require(!snapshot.Players[owner].IsEliminated, "出局玩家不能保留地产。");
            }
            else Require(property.Level == 0, "银行地产必须清除建筑。");
        }

        var alive = snapshot.Players.Count(player => !player.IsEliminated);
        Require(!snapshot.Players[snapshot.CurrentPlayerId].IsEliminated, "当前玩家必须仍在场。");
        if (snapshot.Phase == RichTownPhase.Finished)
        {
            Require(snapshot.FinishReason is RichTownFinishReason.LastSurvivor or RichTownFinishReason.RoundLimit, "终局缺少有效原因。");
            Require(snapshot.FinishReason == RichTownFinishReason.LastSurvivor ? alive == 1 : alive >= 2 && snapshot.Round == RichTownBoard.MaxRounds,
                "终局原因与轮次或在场人数矛盾。");
        }
        else Require(alive >= 2 && snapshot.FinishReason is null, "未结束的对局必须至少有两人在场且没有终局原因。");

        if (snapshot.Phase is RichTownPhase.AwaitingRoll or RichTownPhase.AwaitingPurchase or RichTownPhase.AwaitingDebt)
            Require(!snapshot.HasUpgraded, "落点决策前不能已执行升级。");
        if (snapshot.Phase == RichTownPhase.AwaitingPurchase)
            Require(snapshot.Properties.Any(property => property.Index == snapshot.Players[snapshot.CurrentPlayerId].Position && property.OwnerId is null),
                "待购买位置必须是银行地产。");

        Require((snapshot.Phase == RichTownPhase.AwaitingDebt) == (snapshot.Debt is not null), "债务与阶段必须同时存在或同时为空。");
        if (snapshot.Debt is { } debt)
        {
            var debtor = snapshot.Players[snapshot.CurrentPlayerId];
            Require(debt.DebtorId == debtor.Id && debt.Amount > debtor.Cash, "待偿债玩家或金额错误。");
            var assets = snapshot.Properties.Where(property => property.OwnerId == debtor.Id).Sum(RichTownBoard.SaleValue);
            Require(debt.Amount <= (long)debtor.Cash + assets, "无力清偿的快照应已自动破产。");
            var cell = RichTownBoard.Cells[debtor.Position];
            if (debt.CreditorId is { } creditor)
            {
                Require(creditor is >= 0 and < RichTownBoard.PlayerCount && creditor != debtor.Id, "债权人无效。");
                Require(!snapshot.Players[creditor].IsEliminated, "债权人不能出局。");
                var property = snapshot.Properties.FirstOrDefault(property => property.Index == cell.Index);
                Require(debt.Reason == RichTownPaymentReason.Rent && property?.OwnerId == creditor && RichTownBoard.Rent(property) == debt.Amount,
                    "租金必须对应当前地块和债权人。");
            }
            else Require((debt.Reason == RichTownPaymentReason.Tax && cell.Kind == RichTownCellKind.Tax && debt.Amount == RichTownBoard.Tax) ||
                (debt.Reason == RichTownPaymentReason.ChanceFee && cell.Kind == RichTownCellKind.Chance && RichTownBoard.ChanceAmounts.Contains(-debt.Amount)),
                "银行债务必须对应税费或机会缴费。");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException(message, "snapshot");
    }
}
