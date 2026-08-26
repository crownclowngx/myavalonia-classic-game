namespace ClassicGamePlugin.Features.SpiderSolitaire.Domain;

/// <summary>
/// 蜘蛛纸牌纯领域引擎。它只负责编排牌局规则、不可变撤销快照与提示枚举，
/// 不依赖 Avalonia、计时器、命令或 Plugin SDK。
/// </summary>
internal sealed class SpiderSolitaireGame
{
    private const int ColumnCountValue = 10;
    private const int CompletedRunLength = 13;
    private readonly ISpiderCardShuffler _shuffler;
    private readonly List<List<SpiderCard>> _columns = [];
    private readonly List<SpiderCard> _stock = [];
    private readonly List<List<SpiderCard>> _completedRuns = [];
    private readonly Stack<SpiderGameSnapshot> _history = [];
    private SpiderCardDefinition[] _initialShuffledDeck = [];

    internal SpiderSolitaireGame(
        SpiderSolitaireDifficulty difficulty,
        ISpiderCardShuffler shuffler)
    {
        _shuffler = shuffler ?? throw new ArgumentNullException(nameof(shuffler));
        StartNewGame(difficulty);
    }

    internal SpiderSolitaireDifficulty Difficulty { get; private set; }
    internal SpiderGameState State { get; private set; }
    internal IReadOnlyList<IReadOnlyList<SpiderCard>> Columns => _columns;
    internal IReadOnlyList<SpiderCard> Stock => _stock;
    internal IReadOnlyList<IReadOnlyList<SpiderCard>> CompletedRuns => _completedRuns;
    internal int StockDealCount => _stock.Count / ColumnCountValue;
    internal int CompletedRunCount => _completedRuns.Count;
    internal int ActionCount { get; private set; }
    internal int Score => 500 - ActionCount + (CompletedRunCount * 100);
    internal bool CanUndo => _history.Count > 0;
    internal bool CanDeal => _stock.Count >= ColumnCountValue && _columns.All(column => column.Count > 0);

    /// <summary>生成该难度的新随机局，并把随机结果保存为“同局重开”的稳定牌序。</summary>
    internal void StartNewGame(SpiderSolitaireDifficulty difficulty)
    {
        ValidateDifficulty(difficulty);
        var originalDeck = CreateDeck(difficulty);
        var shuffledDeck = _shuffler.Shuffle(originalDeck);
        ValidateShuffledDeck(originalDeck, shuffledDeck);

        Difficulty = difficulty;
        _initialShuffledDeck = shuffledDeck.ToArray();
        InitializeFromDeck(_initialShuffledDeck);
    }

    /// <summary>复用本局最初的完整洗牌结果，重置领域状态、历史和计分。</summary>
    internal void ReplaySameDeal() => InitializeFromDeck(_initialShuffledDeck);

    /// <summary>判断从指定牌开始的尾部是否构成可整体移动的同花色连续降序牌组。</summary>
    internal bool CanSelectSequence(int columnIndex, int cardIndex)
    {
        if (!IsValidColumn(columnIndex))
        {
            return false;
        }

        var column = _columns[columnIndex];
        if (cardIndex < 0 || cardIndex >= column.Count || !column[cardIndex].IsFaceUp)
        {
            return false;
        }

        for (var index = cardIndex; index < column.Count - 1; index++)
        {
            var lower = column[index];
            var upper = column[index + 1];
            if (!upper.IsFaceUp || lower.Suit != upper.Suit || lower.Rank != upper.Rank + 1)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 判断牌组能否移动。目标只要求比牌组首牌大一级，花色可以不同；
    /// “跨花色允许压牌、跨花色不允许整体移动”是蜘蛛纸牌最容易混淆的两条规则。
    /// </summary>
    internal bool CanMove(int sourceColumn, int sourceIndex, int destinationColumn)
    {
        if (!IsValidColumn(destinationColumn) || sourceColumn == destinationColumn ||
            !CanSelectSequence(sourceColumn, sourceIndex))
        {
            return false;
        }

        var destination = _columns[destinationColumn];
        return destination.Count == 0 || destination[^1].Rank == _columns[sourceColumn][sourceIndex].Rank + 1;
    }

    /// <summary>执行一次合法牌组移动，并把自动翻牌和自动收组包含在同一个领域事务中。</summary>
    internal SpiderGameTransition? Move(int sourceColumn, int sourceIndex, int destinationColumn)
    {
        if (State == SpiderGameState.Won || !CanMove(sourceColumn, sourceIndex, destinationColumn))
        {
            return null;
        }

        var before = CreateSnapshot();
        var source = _columns[sourceColumn];
        var movedCards = source.GetRange(sourceIndex, source.Count - sourceIndex);
        source.RemoveRange(sourceIndex, source.Count - sourceIndex);
        _columns[destinationColumn].AddRange(movedCards);

        var flipped = new List<int>();
        FlipTopCardIfNeeded(source, flipped);
        var completed = RemoveCompletedRuns(flipped);
        CompleteAction();
        _history.Push(before);

        return new SpiderGameTransition(
            SpiderActionKind.Move,
            before,
            CreateSnapshot(),
            flipped,
            completed);
    }

    /// <summary>从库存向每列发一张牌。存在空列或库存不足时拒绝，且不产生历史记录。</summary>
    internal SpiderGameTransition? Deal()
    {
        if (State == SpiderGameState.Won || !CanDeal)
        {
            return null;
        }

        var before = CreateSnapshot();
        for (var columnIndex = 0; columnIndex < ColumnCountValue; columnIndex++)
        {
            var card = _stock[0];
            _stock.RemoveAt(0);
            card.IsFaceUp = true;
            _columns[columnIndex].Add(card);
        }

        var flipped = new List<int>();
        var completed = RemoveCompletedRuns(flipped);
        CompleteAction();
        _history.Push(before);

        return new SpiderGameTransition(
            SpiderActionKind.Deal,
            before,
            CreateSnapshot(),
            flipped,
            completed);
    }

    /// <summary>
    /// 恢复最近一次成功动作之前的棋局。累计操作数有意不在快照中，
    /// 因为经典规则把撤销本身也视为一次扣分操作。
    /// </summary>
    internal SpiderGameTransition? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        var before = CreateSnapshot();
        var target = _history.Pop();
        RestoreSnapshot(target);
        ActionCount++;
        return new SpiderGameTransition(
            SpiderActionKind.Undo,
            before,
            CreateSnapshot(),
            [],
            []);
    }

    /// <summary>按稳定启发式优先级推荐一步，不修改状态，也不保证能够最终解出整局。</summary>
    internal SpiderHint? FindHint()
    {
        var candidates = new List<(int Priority, SpiderHint Hint)>();
        for (var sourceColumn = 0; sourceColumn < ColumnCountValue; sourceColumn++)
        {
            var source = _columns[sourceColumn];
            for (var sourceIndex = 0; sourceIndex < source.Count; sourceIndex++)
            {
                if (!CanSelectSequence(sourceColumn, sourceIndex))
                {
                    continue;
                }

                for (var destinationColumn = 0; destinationColumn < ColumnCountValue; destinationColumn++)
                {
                    if (!CanMove(sourceColumn, sourceIndex, destinationColumn))
                    {
                        continue;
                    }

                    var destination = _columns[destinationColumn];
                    // 把完整一列原样平移到空列不会揭牌、接龙或释放空间，只会制造循环提示。
                    if (sourceIndex == 0 && destination.Count == 0)
                    {
                        continue;
                    }

                    var priority = GetHintPriority(sourceColumn, sourceIndex, destinationColumn);
                    candidates.Add((priority, new SpiderHint(
                        SpiderHintKind.Move,
                        sourceColumn,
                        sourceIndex,
                        destinationColumn)));
                }
            }
        }

        var bestMove = candidates
            .OrderBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Hint.SourceColumn)
            .ThenBy(candidate => candidate.Hint.SourceIndex)
            .ThenBy(candidate => candidate.Hint.DestinationColumn)
            .Select(candidate => (SpiderHint?)candidate.Hint)
            .FirstOrDefault();
        if (bestMove is not null)
        {
            return bestMove;
        }

        return CanDeal ? new SpiderHint(SpiderHintKind.Deal, -1, -1, -1) : null;
    }

    internal SpiderGameSnapshot CreateSnapshot() =>
        new(
            _columns.Select(column => column.Select(ToState)),
            _stock.Select(ToState),
            _completedRuns.Select(run => run.Select(ToState)),
            State);

    private void InitializeFromDeck(IReadOnlyList<SpiderCardDefinition> deck)
    {
        _columns.Clear();
        for (var index = 0; index < ColumnCountValue; index++)
        {
            _columns.Add([]);
        }

        _stock.Clear();
        _completedRuns.Clear();
        _history.Clear();
        ActionCount = 0;
        State = SpiderGameState.Ready;

        var deckIndex = 0;
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < ColumnCountValue; column++)
            {
                _columns[column].Add(new SpiderCard(deck[deckIndex++]));
            }
        }

        for (var column = 0; column < 4; column++)
        {
            _columns[column].Add(new SpiderCard(deck[deckIndex++]));
        }

        foreach (var column in _columns)
        {
            column[^1].IsFaceUp = true;
        }

        while (deckIndex < deck.Count)
        {
            _stock.Add(new SpiderCard(deck[deckIndex++]));
        }
    }

    private void CompleteAction()
    {
        ActionCount++;
        State = _completedRuns.Count == 8 ? SpiderGameState.Won : SpiderGameState.Running;
    }

    private IReadOnlyList<int> RemoveCompletedRuns(List<int> flipped)
    {
        var completedIds = new List<int>();
        var removedAny = true;
        while (removedAny)
        {
            removedAny = false;
            for (var columnIndex = 0; columnIndex < _columns.Count; columnIndex++)
            {
                var column = _columns[columnIndex];
                if (!HasCompletedRun(column))
                {
                    continue;
                }

                var start = column.Count - CompletedRunLength;
                var run = column.GetRange(start, CompletedRunLength);
                column.RemoveRange(start, CompletedRunLength);
                _completedRuns.Add(run);
                completedIds.AddRange(run.Select(card => card.Id));
                FlipTopCardIfNeeded(column, flipped);
                removedAny = true;
            }
        }

        return completedIds;
    }

    private static bool HasCompletedRun(IReadOnlyList<SpiderCard> column)
    {
        if (column.Count < CompletedRunLength)
        {
            return false;
        }

        var start = column.Count - CompletedRunLength;
        var suit = column[start].Suit;
        for (var offset = 0; offset < CompletedRunLength; offset++)
        {
            var card = column[start + offset];
            if (!card.IsFaceUp || card.Suit != suit || card.Rank != 13 - offset)
            {
                return false;
            }
        }

        return true;
    }

    private static void FlipTopCardIfNeeded(List<SpiderCard> column, ICollection<int> flipped)
    {
        if (column.Count == 0 || column[^1].IsFaceUp)
        {
            return;
        }

        column[^1].IsFaceUp = true;
        flipped.Add(column[^1].Id);
    }

    private int GetHintPriority(int sourceColumn, int sourceIndex, int destinationColumn)
    {
        if (WouldCompleteRun(sourceColumn, sourceIndex, destinationColumn))
        {
            return 0;
        }

        var source = _columns[sourceColumn];
        if (sourceIndex > 0 && !source[sourceIndex - 1].IsFaceUp)
        {
            return 1;
        }

        var destination = _columns[destinationColumn];
        if (destination.Count > 0 && destination[^1].Suit == source[sourceIndex].Suit)
        {
            return 2;
        }

        return sourceIndex == 0 ? 3 : 4;
    }

    private bool WouldCompleteRun(int sourceColumn, int sourceIndex, int destinationColumn)
    {
        var combined = _columns[destinationColumn]
            .Concat(_columns[sourceColumn].Skip(sourceIndex))
            .ToArray();
        return HasCompletedRun(combined);
    }

    private void RestoreSnapshot(SpiderGameSnapshot snapshot)
    {
        _columns.Clear();
        _columns.AddRange(snapshot.Columns.Select(column => column.Select(card => card.ToMutableCard()).ToList()));
        _stock.Clear();
        _stock.AddRange(snapshot.Stock.Select(card => card.ToMutableCard()));
        _completedRuns.Clear();
        _completedRuns.AddRange(snapshot.CompletedRuns.Select(run => run.Select(card => card.ToMutableCard()).ToList()));
        State = snapshot.State;
    }

    private static SpiderCardState ToState(SpiderCard card) =>
        new(card.Id, card.Suit, card.Rank, card.IsFaceUp);

    private static SpiderCardDefinition[] CreateDeck(SpiderSolitaireDifficulty difficulty)
    {
        var suits = difficulty switch
        {
            SpiderSolitaireDifficulty.OneSuit => new[] { SpiderCardSuit.Spades },
            SpiderSolitaireDifficulty.TwoSuits => new[] { SpiderCardSuit.Spades, SpiderCardSuit.Hearts },
            SpiderSolitaireDifficulty.FourSuits => Enum.GetValues<SpiderCardSuit>(),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
        };
        var copiesPerSuit = 8 / suits.Length;
        var cards = new List<SpiderCardDefinition>(104);
        var id = 0;
        foreach (var suit in suits)
        {
            for (var copy = 0; copy < copiesPerSuit; copy++)
            {
                for (var rank = 1; rank <= 13; rank++)
                {
                    cards.Add(new SpiderCardDefinition(id++, suit, rank));
                }
            }
        }

        return cards.ToArray();
    }

    private static void ValidateShuffledDeck(
        IReadOnlyList<SpiderCardDefinition> original,
        IReadOnlyList<SpiderCardDefinition> shuffled)
    {
        if (shuffled is null || shuffled.Count != 104 || original.Count != shuffled.Count)
        {
            throw new InvalidOperationException("洗牌策略必须返回完整的 104 张牌。");
        }

        var expected = original.ToDictionary(card => card.Id);
        var seenIds = new HashSet<int>();
        foreach (var card in shuffled)
        {
            if (!seenIds.Add(card.Id) || !expected.TryGetValue(card.Id, out var definition) || definition != card)
            {
                throw new InvalidOperationException("洗牌策略返回了重复、缺失或被篡改的牌。");
            }
        }
    }

    private static void ValidateDifficulty(SpiderSolitaireDifficulty difficulty)
    {
        if (difficulty is not (SpiderSolitaireDifficulty.OneSuit or
            SpiderSolitaireDifficulty.TwoSuits or SpiderSolitaireDifficulty.FourSuits))
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        }
    }

    private bool IsValidColumn(int columnIndex) =>
        columnIndex >= 0 && columnIndex < ColumnCountValue;
}
