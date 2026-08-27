namespace ClassicGamePlugin.Features.FreeCell.Domain;

/// <summary>
/// 空当接龙的纯规则入口。移动预检和纯快照提交共用同一份实现，确保真实棋局与求解器
/// 不会因为分别实现规则而产生细微分歧。
/// </summary>
internal static class FreeCellRules
{
    internal const int TableauCount = 8;
    internal const int FreeCellCount = 4;
    internal const int FoundationCount = 4;

    internal static FreeCellSnapshot CreateInitialSnapshot(FreeCellDeal deal, bool autoCollect)
    {
        ArgumentNullException.ThrowIfNull(deal);
        ValidateDeck(deal.Deck);
        var columns = Enumerable.Range(0, TableauCount).Select(_ => new List<FreeCellCard>()).ToArray();
        for (var index = 0; index < deal.Deck.Count; index++)
        {
            columns[index % TableauCount].Add(deal.Deck[index]);
        }

        var snapshot = new FreeCellSnapshot(
            columns,
            new FreeCellCard?[FreeCellCount],
            new int[FoundationCount],
            0,
            FreeCellGameState.Ready,
            deal.Number,
            deal.CandidateIndex);
        return autoCollect ? CollectSafeCards(snapshot, incrementMove: false).Snapshot : snapshot;
    }

    /// <summary>
    /// 计算一次可等价拆成单张搬运的最大牌组长度。每个空闲单元提供一个临时位置，每个可用空列
    /// 会把容量翻倍；当目标本身为空列时，它已经被占作落点，不能再次充当中转位置。
    /// </summary>
    internal static int GetMovableSequenceCapacity(FreeCellSnapshot snapshot, int destinationTableau)
    {
        var emptyCells = snapshot.FreeCells.Count(card => card is null);
        var emptyColumns = snapshot.Tableaus.Count(column => column.Count == 0);
        if (destinationTableau is >= 0 and < TableauCount && snapshot.Tableaus[destinationTableau].Count == 0)
        {
            emptyColumns--;
        }

        return (emptyCells + 1) * (1 << Math.Max(0, emptyColumns));
    }

    internal static bool IsDescendingAlternating(IReadOnlyList<FreeCellCard> cards, int startIndex)
    {
        if (startIndex < 0 || startIndex >= cards.Count)
        {
            return false;
        }

        for (var index = startIndex; index < cards.Count - 1; index++)
        {
            if (cards[index].Rank != cards[index + 1].Rank + 1 ||
                cards[index].IsRed == cards[index + 1].IsRed)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool CanMove(FreeCellSnapshot snapshot, FreeCellMove move)
    {
        if (!TryGetMovingCards(snapshot, move, out var movingCards) || movingCards.Count == 0)
        {
            return false;
        }

        return move.Destination.Kind switch
        {
            FreeCellLocationKind.Tableau => CanMoveToTableau(snapshot, move, movingCards),
            FreeCellLocationKind.FreeCell => movingCards.Count == 1 &&
                IsIndex(move.Destination.Index, FreeCellCount) &&
                snapshot.FreeCells[move.Destination.Index] is null,
            FreeCellLocationKind.Foundation => movingCards.Count == 1 &&
                IsIndex(move.Destination.Index, FoundationCount) &&
                (int)movingCards[0].Suit == move.Destination.Index &&
                snapshot.Foundations[move.Destination.Index] == movingCards[0].Rank - 1,
            _ => false,
        };
    }

    internal static (FreeCellSnapshot Snapshot, IReadOnlyList<int> PrimaryIds, IReadOnlyList<int> AutoIds)?
        TryApplyMove(FreeCellSnapshot snapshot, FreeCellMove move, bool autoCollect)
    {
        if (!CanMove(snapshot, move) || !TryGetMovingCards(snapshot, move, out var movingCards))
        {
            return null;
        }

        var tableaus = snapshot.Tableaus.Select(column => column.ToList()).ToArray();
        var cells = snapshot.FreeCells.ToArray();
        var foundations = snapshot.Foundations.ToArray();
        RemoveFromSource(tableaus, cells, move, movingCards.Count);
        AddToDestination(tableaus, cells, foundations, move.Destination, movingCards);

        var afterPrimary = CreateSnapshot(
            snapshot,
            tableaus,
            cells,
            foundations,
            snapshot.MoveCount + 1);
        var collected = autoCollect
            ? CollectSafeCards(afterPrimary, incrementMove: false)
            : (afterPrimary, (IReadOnlyList<int>)Array.Empty<int>());
        return (collected.Item1, movingCards.Select(card => card.Id).ToArray(), collected.Item2);
    }

    /// <summary>
    /// 自动收牌采用充分而保守的安全条件：A 不会再承载任何牌；点数 r 大于 1 时，只有两种相反颜色
    /// 的基础区都至少到达 r-1，当前牌才不再承担临时承载这些牌的作用。该规则宁可晚收，也不会因自动化
    /// 剥夺一条原本可用的解路径。
    /// </summary>
    internal static (FreeCellSnapshot Snapshot, IReadOnlyList<int> CardIds) CollectSafeCards(
        FreeCellSnapshot snapshot,
        bool incrementMove)
    {
        var tableaus = snapshot.Tableaus.Select(column => column.ToList()).ToArray();
        var cells = snapshot.FreeCells.ToArray();
        var foundations = snapshot.Foundations.ToArray();
        var movedIds = new List<int>();

        while (true)
        {
            var candidates = new List<(FreeCellCard Card, FreeCellLocation Source)>();
            for (var index = 0; index < cells.Length; index++)
            {
                if (cells[index] is { } card)
                {
                    candidates.Add((card, FreeCellLocation.Cell(index)));
                }
            }

            for (var index = 0; index < tableaus.Length; index++)
            {
                if (tableaus[index].Count > 0)
                {
                    candidates.Add((tableaus[index][^1], FreeCellLocation.Tableau(index)));
                }
            }

            var next = candidates
                .Where(candidate => foundations[(int)candidate.Card.Suit] == candidate.Card.Rank - 1)
                .Where(candidate => IsSafeForFoundation(candidate.Card, foundations))
                .OrderBy(candidate => candidate.Card.Rank)
                .ThenBy(candidate => candidate.Card.Suit)
                .ThenBy(candidate => candidate.Source.Kind)
                .ThenBy(candidate => candidate.Source.Index)
                .Select(candidate => ((FreeCellCard Card, FreeCellLocation Source)?)candidate)
                .FirstOrDefault();
            if (next is null)
            {
                break;
            }

            var value = next.Value;
            if (value.Source.Kind == FreeCellLocationKind.FreeCell)
            {
                cells[value.Source.Index] = null;
            }
            else
            {
                tableaus[value.Source.Index].RemoveAt(tableaus[value.Source.Index].Count - 1);
            }

            foundations[(int)value.Card.Suit] = value.Card.Rank;
            movedIds.Add(value.Card.Id);
        }

        if (movedIds.Count == 0)
        {
            return (snapshot, movedIds);
        }

        return (CreateSnapshot(
            snapshot,
            tableaus,
            cells,
            foundations,
            snapshot.MoveCount + (incrementMove ? 1 : 0)), movedIds);
    }

    internal static IReadOnlyList<FreeCellMove> EnumerateLegalMoves(
        FreeCellSnapshot snapshot,
        bool reduceSymmetricDestinations)
    {
        var result = new List<FreeCellMove>();
        var firstEmptyCell = snapshot.FreeCells
            .Select((card, index) => (card, index))
            .Where(value => value.card is null)
            .Select(value => value.index)
            .DefaultIfEmpty(-1)
            .First();
        var firstEmptyTableau = snapshot.Tableaus
            .Select((column, index) => (column, index))
            .Where(value => value.column.Count == 0)
            .Select(value => value.index)
            .DefaultIfEmpty(-1)
            .First();

        for (var source = 0; source < TableauCount; source++)
        {
            var column = snapshot.Tableaus[source];
            for (var cardIndex = 0; cardIndex < column.Count; cardIndex++)
            {
                if (!IsDescendingAlternating(column, cardIndex))
                {
                    continue;
                }

                AddDestinations(FreeCellLocation.Tableau(source), cardIndex, column.Count - cardIndex);
            }
        }

        for (var source = 0; source < FreeCellCount; source++)
        {
            if (snapshot.FreeCells[source] is not null)
            {
                AddDestinations(FreeCellLocation.Cell(source), 0, 1);
            }
        }

        return result;

        void AddDestinations(FreeCellLocation source, int sourceCardIndex, int count)
        {
            var card = source.Kind == FreeCellLocationKind.Tableau
                ? snapshot.Tableaus[source.Index][sourceCardIndex]
                : snapshot.FreeCells[source.Index]!.Value;
            var foundation = new FreeCellMove(source, sourceCardIndex, FreeCellLocation.Foundation(card.Suit));
            if (count == 1 && CanMove(snapshot, foundation))
            {
                result.Add(foundation);
            }

            if (count == 1)
            {
                for (var cell = 0; cell < FreeCellCount; cell++)
                {
                    if (reduceSymmetricDestinations && cell != firstEmptyCell && snapshot.FreeCells[cell] is null)
                    {
                        continue;
                    }

                    var move = new FreeCellMove(source, sourceCardIndex, FreeCellLocation.Cell(cell));
                    if (CanMove(snapshot, move))
                    {
                        result.Add(move);
                    }
                }
            }

            for (var destination = 0; destination < TableauCount; destination++)
            {
                if (reduceSymmetricDestinations && destination != firstEmptyTableau &&
                    snapshot.Tableaus[destination].Count == 0)
                {
                    continue;
                }

                var move = new FreeCellMove(source, sourceCardIndex, FreeCellLocation.Tableau(destination));
                if (CanMove(snapshot, move))
                {
                    result.Add(move);
                }
            }
        }
    }

    private static bool TryGetMovingCards(
        FreeCellSnapshot snapshot,
        FreeCellMove move,
        out IReadOnlyList<FreeCellCard> cards)
    {
        cards = Array.Empty<FreeCellCard>();
        if (move.Source == move.Destination)
        {
            return false;
        }

        if (move.Source.Kind == FreeCellLocationKind.Tableau &&
            IsIndex(move.Source.Index, TableauCount))
        {
            var column = snapshot.Tableaus[move.Source.Index];
            if (!IsDescendingAlternating(column, move.SourceCardIndex))
            {
                return false;
            }

            cards = column.Skip(move.SourceCardIndex).ToArray();
            return true;
        }

        if (move.Source.Kind == FreeCellLocationKind.FreeCell &&
            IsIndex(move.Source.Index, FreeCellCount) &&
            snapshot.FreeCells[move.Source.Index] is { } card)
        {
            cards = [card];
            return true;
        }

        // 基础区只允许通过撤销恢复，不能主动取回，防止求解规则和玩家规则出现两套语义。
        return false;
    }

    private static bool CanMoveToTableau(
        FreeCellSnapshot snapshot,
        FreeCellMove move,
        IReadOnlyList<FreeCellCard> movingCards)
    {
        if (!IsIndex(move.Destination.Index, TableauCount) ||
            move.Source.Kind == FreeCellLocationKind.Tableau && move.Source.Index == move.Destination.Index ||
            movingCards.Count > GetMovableSequenceCapacity(snapshot, move.Destination.Index))
        {
            return false;
        }

        var destination = snapshot.Tableaus[move.Destination.Index];
        return destination.Count == 0 ||
            destination[^1].Rank == movingCards[0].Rank + 1 &&
            destination[^1].IsRed != movingCards[0].IsRed;
    }

    private static bool IsSafeForFoundation(FreeCellCard card, IReadOnlyList<int> foundations)
    {
        if (card.Rank == 1)
        {
            return true;
        }

        var opposite = card.IsRed
            ? new[] { FreeCellSuit.Spades, FreeCellSuit.Clubs }
            : new[] { FreeCellSuit.Hearts, FreeCellSuit.Diamonds };
        return opposite.All(suit => foundations[(int)suit] >= card.Rank - 1);
    }

    private static FreeCellSnapshot CreateSnapshot(
        FreeCellSnapshot source,
        IEnumerable<IEnumerable<FreeCellCard>> tableaus,
        IEnumerable<FreeCellCard?> cells,
        IEnumerable<int> foundations,
        int moveCount)
    {
        var foundationArray = foundations.ToArray();
        return new FreeCellSnapshot(
            tableaus,
            cells,
            foundationArray,
            moveCount,
            foundationArray.All(rank => rank == 13)
                ? FreeCellGameState.Won
                : moveCount == 0 ? FreeCellGameState.Ready : FreeCellGameState.Running,
            source.DealNumber,
            source.CandidateIndex);
    }

    private static void RemoveFromSource(
        IReadOnlyList<List<FreeCellCard>> tableaus,
        FreeCellCard?[] cells,
        FreeCellMove move,
        int count)
    {
        if (move.Source.Kind == FreeCellLocationKind.Tableau)
        {
            tableaus[move.Source.Index].RemoveRange(move.SourceCardIndex, count);
        }
        else
        {
            cells[move.Source.Index] = null;
        }
    }

    private static void AddToDestination(
        IReadOnlyList<List<FreeCellCard>> tableaus,
        FreeCellCard?[] cells,
        int[] foundations,
        FreeCellLocation destination,
        IReadOnlyList<FreeCellCard> cards)
    {
        switch (destination.Kind)
        {
            case FreeCellLocationKind.Tableau:
                tableaus[destination.Index].AddRange(cards);
                break;
            case FreeCellLocationKind.FreeCell:
                cells[destination.Index] = cards[0];
                break;
            case FreeCellLocationKind.Foundation:
                foundations[destination.Index] = cards[0].Rank;
                break;
            default:
                throw new InvalidOperationException("遇到了未知的空当接龙目标区域。");
        }
    }

    private static void ValidateDeck(IReadOnlyList<FreeCellCard> deck)
    {
        if (deck.Count != 52 || deck.Select(card => card.Id).Distinct().Count() != 52 ||
            deck.Any(card => card.Rank is < 1 or > 13) ||
            deck.GroupBy(card => (card.Suit, card.Rank)).Any(group => group.Count() != 1))
        {
            throw new ArgumentException("空当接龙牌组必须包含四种花色各 A 到 K，并具有 52 个唯一身份。", nameof(deck));
        }
    }

    private static bool IsIndex(int index, int count) => index >= 0 && index < count;
}
