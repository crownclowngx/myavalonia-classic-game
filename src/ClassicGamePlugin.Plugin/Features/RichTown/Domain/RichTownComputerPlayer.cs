namespace ClassicGamePlugin.Features.RichTown.Domain;

/// <summary>
/// 普通同步决策器，只读快照、返回与真人相同的请求，不调用随机数或提交会话，也不安排延迟。
/// 购买/升级后保留至少 300 现金；升级按地块编号，偿债按折价从低到高、同价按编号出售。
/// 每次只提出一个动作，下一次必须读最新快照，因此一旦还清债务就不会继续卖出多余资产。
/// </summary>
internal static class RichTownComputerPlayer
{
    public static RichTownRequest? Decide(RichTownSnapshot snapshot)
    {
        RichTownSnapshotValidator.Validate(snapshot);
        var player = snapshot.Players[snapshot.CurrentPlayerId];
        if (snapshot.Phase == RichTownPhase.Finished || !player.IsComputer) return null;
        var command = snapshot.Phase switch
        {
            RichTownPhase.AwaitingRoll => new RichTownCommand(RichTownCommandKind.Roll),
            RichTownPhase.AwaitingPurchase => new RichTownCommand(player.Cash >= RichTownBoard.Cells[player.Position].Price + RichTownBoard.ComputerReserve
                ? RichTownCommandKind.Buy : RichTownCommandKind.Decline),
            RichTownPhase.AwaitingDebt => new RichTownCommand(RichTownCommandKind.Sell, snapshot.Properties
                .Where(property => property.OwnerId == player.Id)
                .OrderBy(RichTownBoard.SaleValue).ThenBy(property => property.Index).First().Index),
            _ => ChooseTurnAction(snapshot, player)
        };
        return new RichTownRequest(snapshot.Revision, player.Id, command);
    }

    private static RichTownCommand ChooseTurnAction(RichTownSnapshot snapshot, RichTownPlayer player)
    {
        var property = snapshot.HasUpgraded ? null : snapshot.Properties.FirstOrDefault(property => property.OwnerId == player.Id &&
            property.Level < RichTownBoard.MaxLevel && player.Cash >= RichTownBoard.Cells[property.Index].Price + RichTownBoard.ComputerReserve);
        return property is null ? new(RichTownCommandKind.EndTurn) : new(RichTownCommandKind.Upgrade, property.Index);
    }
}
