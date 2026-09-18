using System.Collections.Immutable;

namespace ClassicGamePlugin.Features.RichTown.Domain;

internal enum RichTownCommandKind { Roll, Buy, Decline, Upgrade, Sell, EndTurn }
internal enum RichTownRejection { WrongRevision, WrongPlayer, WrongPhase, InvalidCommand, InvalidProperty, NotOwner, InsufficientCash, UpgradeLimit, NumericLimit }

/// <summary>真人与电脑共用的命令值；只有 Upgrade/Sell 携带地块索引，其他命令不得夹带多余参数。</summary>
internal sealed record RichTownCommand(RichTownCommandKind Kind, int? PropertyIndex = null);

/// <summary>以修订和玩家身份防止重复点击及旧状态提交；重开会话代数由 Document 拥有的展示控制器负责。</summary>
internal sealed record RichTownRequest(long ExpectedRevision, int PlayerId, RichTownCommand Command);

/// <summary>拒绝时返回原快照引用和空事件；调用方不需要通过比较差异猜测是否成功。</summary>
internal sealed record RichTownTransition(RichTownSnapshot Snapshot, ImmutableArray<RichTownEvent> Events, RichTownRejection? Rejection = null)
{
    public bool Accepted => Rejection is null;
}

/// <summary>仅用于当前小镇对局的有序展示结果，不是跨游戏消息总线或可回放的写入接口。</summary>
internal abstract record RichTownEvent(long Sequence);
internal sealed record RichTownRolled(long Sequence, int PlayerId, int Steps) : RichTownEvent(Sequence);
internal sealed record RichTownMoved(long Sequence, int PlayerId, int From, int To, ImmutableArray<int> Path) : RichTownEvent(Sequence);
internal sealed record RichTownMoneyTransferred(long Sequence, int? From, int? To, int Amount, RichTownPaymentReason Reason) : RichTownEvent(Sequence);
internal enum RichTownPropertyChange { Purchased, Upgraded, Sold, Liquidated }
internal sealed record RichTownPropertyChanged(long Sequence, int PropertyIndex, int PlayerId, int Level, RichTownPropertyChange Change) : RichTownEvent(Sequence);
internal sealed record RichTownPurchaseDeclined(long Sequence, int PlayerId, int PropertyIndex) : RichTownEvent(Sequence);
internal sealed record RichTownChanceDrawn(long Sequence, int PlayerId, int Card) : RichTownEvent(Sequence);
internal sealed record RichTownDebtOpened(long Sequence, RichTownDebt Debt) : RichTownEvent(Sequence);
internal sealed record RichTownDebtSettled(long Sequence, RichTownDebt Debt, int Paid, int Waived) : RichTownEvent(Sequence);
internal sealed record RichTownBankrupt(long Sequence, int PlayerId) : RichTownEvent(Sequence);
internal sealed record RichTownTurnStarted(long Sequence, int PlayerId, int Round) : RichTownEvent(Sequence);
internal sealed record RichTownGameFinished(long Sequence, RichTownFinishReason Reason, ImmutableArray<RichTownStanding> Standings) : RichTownEvent(Sequence);
