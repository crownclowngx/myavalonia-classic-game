using ClassicGamePlugin.Features.RichTown.Domain;

namespace ClassicGamePlugin.Features.RichTown.Presentation;

/// <summary>小镇中文投影与操作可用性；金额公式仍来自 G2 配置，页面不维护第二套经济规则。</summary>
internal static class RichTownPresentation
{
    private static readonly string[] Names = ["你 · 1 号车", "小蓝 · 2 号车", "小橙 · 3 号车"];
    public static string Player(int id) => Names[id];
    public static string Cell(int index) => RichTownBoard.Cells[index].Kind switch
    {
        RichTownCellKind.Start => "起点", RichTownCellKind.Chance => "机会", RichTownCellKind.Tax => "税务站",
        RichTownCellKind.Rest => "休息区", _ => $"{index:00} 号街区"
    };

    public static string Status(RichTownPlayController play)
    {
        if (play.IsDisposed) return "对局已关闭";
        if (play.IsPaused) return "已暂停 · 点击继续游戏";
        if (play.IsAnimating) return "正在回放 · 可跳过动画";
        var state = play.Snapshot;
        if (state.Phase == RichTownPhase.Finished)
            return "对局结束 · " + string.Join("、", state.GetStandings().Where(item => item.IsWinner).Select(item => Player(item.PlayerId))) + " 获胜";
        if (!play.IsActive) return "正在准备棋盘…";
        if (state.Players[state.CurrentPlayerId].IsComputer) return $"{Player(state.CurrentPlayerId)} 正在行动…";
        return state.Phase switch
        {
            RichTownPhase.AwaitingRoll => "轮到你了 · 掷骰出发",
            RichTownPhase.AwaitingPurchase => $"可以买下 {Cell(state.Players[state.CurrentPlayerId].Position)}，也可以放弃",
            RichTownPhase.AwaitingDebt => $"需要支付 {state.Debt!.Amount} · 请选择地产出售偿债",
            _ => "可升级一次或出售地产，也可以结束回合"
        };
    }

    public static bool Can(RichTownPlayController play, RichTownCommand command)
    {
        if (!play.CanInteract) return false;
        // 使用纯规则探测合法性，结果不提交也不保存；按钮判断与实际规则入口保持相同语义。
        // Roll 不在此处求值，避免为启用一个按钮提前计算随机结果。
        if (command.Kind == RichTownCommandKind.Roll) return command.PropertyIndex is null && play.Snapshot.Phase == RichTownPhase.AwaitingRoll;
        return RichTownRules.Apply(play.Snapshot, new(play.Snapshot.Revision, play.Snapshot.CurrentPlayerId, command)).Accepted;
    }

    public static string Details(RichTownSnapshot state, int index)
    {
        var cell = RichTownBoard.Cells[index];
        var property = state.Properties.FirstOrDefault(item => item.Index == index);
        if (property is null) return cell.Kind switch
        {
            RichTownCellKind.Start => "经过或落在起点，获得 200。",
            RichTownCellKind.Chance => "抽取机会：获得或支付 100 / 200。",
            RichTownCellKind.Tax => "向银行支付 100。", _ => "休息一会儿，本回合没有额外费用。"
        };
        var owner = property.OwnerId is { } id ? Player(id) : "银行 · 可购买";
        return $"{owner}\n地价 {cell.Price}  ·  等级 {property.Level}/2\n租金 {RichTownBoard.Rent(property)}  ·  升级 {cell.Price}\n整块出售可得 {RichTownBoard.SaleValue(property)}";
    }

    public static string? EventText(RichTownEvent item) => item switch
    {
        RichTownRolled roll => $"{Player(roll.PlayerId)} 掷出 {roll.Steps} 点",
        RichTownMoved move => $"抵达 {Cell(move.To)}",
        RichTownMoneyTransferred money when money.Reason is RichTownPaymentReason.StartReward or RichTownPaymentReason.ChanceReward => $"{Player(money.To!.Value)} 获得 {money.Amount}",
        RichTownMoneyTransferred money when money.Reason is RichTownPaymentReason.Rent or RichTownPaymentReason.Tax or RichTownPaymentReason.ChanceFee =>
            $"{Player(money.From!.Value)} 支付 {money.Amount} → {(money.To is { } id ? Player(id) : "银行")}",
        RichTownPropertyChanged property => $"{Cell(property.PropertyIndex)} · " + (property.Change switch
        {
            RichTownPropertyChange.Purchased => "购入", RichTownPropertyChange.Upgraded => $"升级至 {property.Level} 级",
            RichTownPropertyChange.Sold => "售回银行", _ => "破产清算"
        }),
        RichTownDebtOpened debt => $"待偿债 {debt.Debt.Amount}，只能出售资产",
        RichTownDebtSettled debt => debt.Waived > 0 ? $"清算支付 {debt.Paid}，免除 {debt.Waived}" : "债务已偿清",
        RichTownBankrupt bankrupt => $"{Player(bankrupt.PlayerId)} 破产出局",
        RichTownGameFinished => "对局结束，查看最终排名",
        _ => null
    };
}
