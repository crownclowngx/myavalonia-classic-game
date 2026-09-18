using System.Collections.Immutable;

namespace ClassicGamePlugin.Features.RichTown.Domain;

/// <summary>
/// 确定性命令解释器：同一快照和请求得到相同的下一快照与事件；不访问 UI、时钟、磁盘或任何共享随机源。
/// 先完整检查命令，再在命令私有的计算对象中结算。旧快照始终不变，溢出或拒绝不会泄露部分转账、随机状态或事件。
/// </summary>
internal static class RichTownRules
{
    public static RichTownTransition Apply(RichTownSnapshot snapshot, RichTownRequest request)
    {
        RichTownSnapshotValidator.Validate(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        var rejection = ValidateCommand(snapshot, request);
        if (rejection is not null) return Reject(snapshot, rejection.Value);
        try
        {
            var turn = new Turn(snapshot);
            turn.Execute(request.Command);
            var result = turn.Complete();
            RichTownSnapshotValidator.Validate(result.Snapshot);
            return result;
        }
        catch (OverflowException)
        {
            // 即使来自边界快照的收款或序号溢出，也只能拒绝整个命令，不能提交已执行的一半操作。
            return Reject(snapshot, RichTownRejection.NumericLimit);
        }
    }

    private static RichTownTransition Reject(RichTownSnapshot snapshot, RichTownRejection reason) => new(snapshot, [], reason);

    private static RichTownRejection? ValidateCommand(RichTownSnapshot state, RichTownRequest request)
    {
        if (request.ExpectedRevision != state.Revision) return RichTownRejection.WrongRevision;
        if (request.PlayerId != state.CurrentPlayerId) return RichTownRejection.WrongPlayer;
        var command = request.Command;
        if (command is null || !Enum.IsDefined(command.Kind)) return RichTownRejection.InvalidCommand;
        var needsProperty = command.Kind is RichTownCommandKind.Upgrade or RichTownCommandKind.Sell;
        if (needsProperty != command.PropertyIndex.HasValue) return RichTownRejection.InvalidCommand;
        var phaseAllowed = command.Kind switch
        {
            RichTownCommandKind.Roll => state.Phase == RichTownPhase.AwaitingRoll,
            RichTownCommandKind.Buy or RichTownCommandKind.Decline => state.Phase == RichTownPhase.AwaitingPurchase,
            RichTownCommandKind.Upgrade or RichTownCommandKind.EndTurn => state.Phase == RichTownPhase.TurnActions,
            RichTownCommandKind.Sell => state.Phase is RichTownPhase.TurnActions or RichTownPhase.AwaitingDebt,
            _ => false
        };
        if (!phaseAllowed) return RichTownRejection.WrongPhase;
        var player = state.Players[state.CurrentPlayerId];
        if (command.Kind == RichTownCommandKind.Buy && player.Cash < RichTownBoard.Cells[player.Position].Price)
            return RichTownRejection.InsufficientCash;
        if (!needsProperty) return null;
        var property = state.Properties.FirstOrDefault(property => property.Index == command.PropertyIndex);
        if (property is null) return RichTownRejection.InvalidProperty;
        if (property.OwnerId != player.Id) return RichTownRejection.NotOwner;
        if (command.Kind == RichTownCommandKind.Upgrade)
        {
            if (state.HasUpgraded || property.Level >= RichTownBoard.MaxLevel) return RichTownRejection.UpgradeLimit;
            if (player.Cash < RichTownBoard.Cells[property.Index].Price) return RichTownRejection.InsufficientCash;
        }
        return null;
    }

    /// <summary>
    /// 仅存活于一次 Apply 的局部工作区，不是持久会话或可共享服务。把移动、交易和结算步骤组合在一起，
    /// 保证一次掷骰中的绕圈奖励、机会、债务乃至破产都作为同一事务发布，外部不能观察中间状态。
    /// </summary>
    private sealed class Turn(RichTownSnapshot initial)
    {
        private RichTownSnapshot _state = initial;
        private readonly ImmutableArray<RichTownEvent>.Builder _events = ImmutableArray.CreateBuilder<RichTownEvent>();
        private int Actor => _state.CurrentPlayerId;
        private RichTownPlayer Player => _state.Players[Actor];
        private long NextSequence()
        {
            var sequence = checked(_state.EventSequence + 1);
            _state = _state with { EventSequence = sequence };
            return sequence;
        }

        public RichTownTransition Complete() => new(_state with { Revision = checked(_state.Revision + 1) }, _events.ToImmutable());

        public void Execute(RichTownCommand command)
        {
            switch (command.Kind)
            {
                case RichTownCommandKind.Roll: Roll(); break;
                case RichTownCommandKind.Buy: Buy(); break;
                case RichTownCommandKind.Decline:
                    _events.Add(new RichTownPurchaseDeclined(NextSequence(), Actor, Player.Position));
                    _state = _state with { Phase = RichTownPhase.TurnActions };
                    break;
                case RichTownCommandKind.Upgrade: Upgrade(Property(command.PropertyIndex!.Value)); break;
                case RichTownCommandKind.Sell:
                    Sell(Property(command.PropertyIndex!.Value), false);
                    if (_state.Debt is { } debt && Player.Cash >= debt.Amount) Settle(debt, debt.Amount);
                    break;
                case RichTownCommandKind.EndTurn: AdvanceTurn(); break;
            }
        }

        private RichTownProperty Property(int index) => _state.Properties.First(property => property.Index == index);
        private void SetProperty(RichTownProperty property)
        {
            var offset = _state.Properties.IndexOf(Property(property.Index));
            _state = _state with { Properties = _state.Properties.SetItem(offset, property) };
        }

        /// <summary>银行用空身份表示；零金额不生成资金事件，所有实际扣款都在余额足够时执行。</summary>
        private void Transfer(int? from, int? to, int amount, RichTownPaymentReason reason)
        {
            if (amount == 0) return;
            var players = _state.Players;
            if (from is { } payer) players = players.SetItem(payer, players[payer] with { Cash = checked(players[payer].Cash - amount) });
            if (to is { } payee) players = players.SetItem(payee, players[payee] with { Cash = checked(players[payee].Cash + amount) });
            _state = _state with { Players = players };
            _events.Add(new RichTownMoneyTransferred(NextSequence(), from, to, amount, reason));
        }

        private int Sample(int maximum)
        {
            var sample = RichTownRandom.Next(_state.Random, maximum);
            _state = _state with { Random = sample.State };
            return sample.Value;
        }

        private void Roll()
        {
            var steps = Sample(6) + 1;
            var from = Player.Position;
            var path = Enumerable.Range(1, steps).Select(step => (from + step) % RichTownBoard.CellCount).ToImmutableArray();
            _events.Add(new RichTownRolled(NextSequence(), Actor, steps));
            _state = _state with
            {
                Players = _state.Players.SetItem(Actor, Player with { Position = path[^1] }),
                Phase = RichTownPhase.TurnActions
            };
            _events.Add(new RichTownMoved(NextSequence(), Actor, from, path[^1], path));
            if (from + steps >= RichTownBoard.CellCount) Transfer(null, Actor, RichTownBoard.StartReward, RichTownPaymentReason.StartReward);
            var cell = RichTownBoard.Cells[path[^1]];
            switch (cell.Kind)
            {
                case RichTownCellKind.Property:
                    var property = Property(cell.Index);
                    if (property.OwnerId is null) _state = _state with { Phase = RichTownPhase.AwaitingPurchase };
                    else if (property.OwnerId != Actor) Charge(property.OwnerId, RichTownBoard.Rent(property), RichTownPaymentReason.Rent);
                    break;
                case RichTownCellKind.Tax: Charge(null, RichTownBoard.Tax, RichTownPaymentReason.Tax); break;
                case RichTownCellKind.Chance:
                    var card = Sample(RichTownBoard.ChanceAmounts.Length);
                    _events.Add(new RichTownChanceDrawn(NextSequence(), Actor, card));
                    // 卡牌编号属于规则版本：0/1 得到 100/200，2/3 支付 100/200，不发生额外移动。
                    var amount = RichTownBoard.ChanceAmounts[card];
                    if (amount > 0) Transfer(null, Actor, amount, RichTownPaymentReason.ChanceReward);
                    else Charge(null, -amount, RichTownPaymentReason.ChanceFee);
                    break;
            }
        }

        private void Buy()
        {
            var property = Property(Player.Position) with { OwnerId = Actor };
            Transfer(Actor, null, RichTownBoard.Cells[property.Index].Price, RichTownPaymentReason.Purchase);
            SetProperty(property);
            _state = _state with { Phase = RichTownPhase.TurnActions };
            _events.Add(new RichTownPropertyChanged(NextSequence(), property.Index, Actor, 0, RichTownPropertyChange.Purchased));
        }

        private void Upgrade(RichTownProperty property)
        {
            Transfer(Actor, null, RichTownBoard.Cells[property.Index].Price, RichTownPaymentReason.Upgrade);
            property = property with { Level = property.Level + 1 };
            SetProperty(property);
            _state = _state with { HasUpgraded = true };
            _events.Add(new RichTownPropertyChanged(NextSequence(), property.Index, Actor, property.Level, RichTownPropertyChange.Upgraded));
        }

        private void Sell(RichTownProperty property, bool liquidation)
        {
            Transfer(null, Actor, RichTownBoard.SaleValue(property), RichTownPaymentReason.Sale);
            SetProperty(property with { OwnerId = null, Level = 0 });
            _events.Add(new RichTownPropertyChanged(NextSequence(), property.Index, Actor, 0,
                liquidation ? RichTownPropertyChange.Liquidated : RichTownPropertyChange.Sold));
        }

        private void Charge(int? creditor, int amount, RichTownPaymentReason reason)
        {
            if (Player.Cash >= amount)
            {
                Transfer(Actor, creditor, amount, reason);
                return;
            }
            var debt = new RichTownDebt(Actor, creditor, amount, reason);
            _state = _state with { Phase = RichTownPhase.AwaitingDebt, Debt = debt };
            _events.Add(new RichTownDebtOpened(NextSequence(), debt));
            var properties = _state.Properties.Where(property => property.OwnerId == Actor).ToArray();
            if ((long)Player.Cash + properties.Sum(RichTownBoard.SaleValue) >= amount) return;

            // 无法清偿时整笔命令内自动清算，按地图顺序回收所有资产，只支付现有资金，剩余债务一次免除。
            foreach (var property in properties) Sell(property, true);
            Settle(debt, Math.Min(amount, Player.Cash));
            var eliminated = Actor;
            _state = _state with { Players = _state.Players.SetItem(Actor, Player with { IsEliminated = true }) };
            _events.Add(new RichTownBankrupt(NextSequence(), eliminated));
            AdvanceTurn();
        }

        private void Settle(RichTownDebt debt, int paid)
        {
            Transfer(debt.DebtorId, debt.CreditorId, paid, debt.Reason);
            _state = _state with { Debt = null, Phase = RichTownPhase.TurnActions };
            _events.Add(new RichTownDebtSettled(NextSequence(), debt, paid, debt.Amount - paid));
        }

        private void AdvanceTurn()
        {
            var alive = _state.Players.Where(player => !player.IsEliminated).Select(player => player.Id).ToArray();
            if (alive.Length == 1)
            {
                _state = _state with { CurrentPlayerId = alive[0] };
                Finish(RichTownFinishReason.LastSurvivor);
                return;
            }
            var next = alive.FirstOrDefault(id => id > Actor, -1);
            var wrapped = next < 0;
            if (wrapped) next = alive[0];
            _state = _state with { CurrentPlayerId = next, HasUpgraded = false };
            // 固定座位只允许淘汰、不允许加入；越过最后一个在场座位恰好意味着本轮在场者都已行动。
            // 因此无需可变的“本轮名单”，即使首位/末位中途破产也不会重复行动或多算一轮。
            if (wrapped && _state.Round == RichTownBoard.MaxRounds) { Finish(RichTownFinishReason.RoundLimit); return; }
            _state = _state with { Round = _state.Round + (wrapped ? 1 : 0), Phase = RichTownPhase.AwaitingRoll };
            _events.Add(new RichTownTurnStarted(NextSequence(), next, _state.Round));
        }

        private void Finish(RichTownFinishReason reason)
        {
            _state = _state with { Phase = RichTownPhase.Finished, FinishReason = reason };
            _events.Add(new RichTownGameFinished(NextSequence(), reason, _state.GetStandings()));
        }
    }
}
