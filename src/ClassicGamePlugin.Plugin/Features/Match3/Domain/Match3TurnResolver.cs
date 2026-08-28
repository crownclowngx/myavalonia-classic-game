namespace ClassicGamePlugin.Features.Match3.Domain;

/// <summary>
/// 在候选棋盘中完成一次交换的全部消除、特殊连锁、下落和补位。调用方只有拿到完整结果后才提交状态，
/// 因此随机源异常或病态连锁不会污染真实对局。
/// </summary>
internal sealed class Match3TurnResolver
{
    internal const int MaximumCascadeLevels = 64;
    private const int PointsPerTile = 10;
    private static readonly Match3GemKind[] Kinds = Enum.GetValues<Match3GemKind>();
    private readonly Match3BoardGenerator _boardGenerator;

    internal Match3TurnResolver(Match3BoardGenerator boardGenerator) =>
        _boardGenerator = boardGenerator ?? throw new ArgumentNullException(nameof(boardGenerator));

    internal Match3TurnTransition Resolve(
        IReadOnlyList<Match3Tile?> original,
        Match3Position source,
        Match3Position target,
        IMatch3RandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        Match3Rules.ValidateBoard(original);
        var before = original.ToArray();
        if (!Match3Rules.IsLegalSwap(before, source, target))
        {
            return new Match3TurnTransition(source, target, false, before, before);
        }

        var first = before[Match3Rules.ToIndex(source)]!.Value;
        var second = before[Match3Rules.ToIndex(target)]!.Value;
        var candidate = before.ToArray();
        Match3Rules.Swap(candidate, source, target);

        var steps = new List<Match3ResolutionStep>();
        var totalScore = 0;
        var cascadeLevel = 1;
        if (Match3Rules.IsDirectSpecialSwap(first, second))
        {
            var comboStep = ResolveSpecialCombination(
                candidate, source, target, first, second, cascadeLevel, randomSource);
            steps.Add(comboStep);
            totalScore = checked(totalScore + comboStep.ScoreDelta);
            cascadeLevel++;
        }

        while (true)
        {
            var runs = Match3Rules.FindRuns(candidate);
            if (runs.Count == 0)
            {
                break;
            }

            if (cascadeLevel > MaximumCascadeLevels)
            {
                throw new InvalidOperationException("消消乐连锁超过安全上限，真实棋盘未提交本次交换。");
            }

            var step = ResolveMatchedWave(
                candidate, runs, source, target, cascadeLevel, randomSource,
                preferSwappedAnchor: cascadeLevel == 1);
            steps.Add(step);
            totalScore = checked(totalScore + step.ScoreDelta);
            cascadeLevel++;
        }

        var wasShuffled = false;
        if (!Match3Rules.TryFindFirstLegalSwap(candidate, out _, out _))
        {
            candidate = _boardGenerator.Create(randomSource);
            wasShuffled = true;
        }

        return new Match3TurnTransition(
            source,
            target,
            true,
            before,
            candidate,
            steps,
            totalScore,
            wasShuffled);
    }

    private static Match3ResolutionStep ResolveMatchedWave(
        Match3Tile?[] board,
        IReadOnlyList<Match3MatchRun> runs,
        Match3Position source,
        Match3Position target,
        int cascadeLevel,
        IMatch3RandomSource randomSource,
        bool preferSwappedAnchor)
    {
        var matchedPositions = runs.SelectMany(run => run.Positions).ToHashSet();
        var clearPositions = matchedPositions.ToHashSet();
        var createdSpecials = new List<Match3CreatedSpecial>();
        foreach (var component in BuildComponents(runs))
        {
            var special = DetermineCreatedSpecial(component);
            if (special == Match3SpecialKind.None)
            {
                continue;
            }

            var anchor = ChooseAnchor(board, component, source, target, preferSwappedAnchor);
            if (anchor is null)
            {
                continue;
            }

            var anchorIndex = Match3Rules.ToIndex(anchor.Value);
            var oldTile = board[anchorIndex]!.Value;
            var createdTile = special == Match3SpecialKind.Rainbow
                ? new Match3Tile(null, Match3SpecialKind.Rainbow)
                : new Match3Tile(oldTile.Kind, special);
            board[anchorIndex] = createdTile;
            clearPositions.Remove(anchor.Value);
            createdSpecials.Add(new Match3CreatedSpecial(anchor.Value, createdTile));
        }

        var beforeClear = board.ToArray();
        ExpandTriggeredSpecials(board, clearPositions, createdSpecials.Select(item => item.Position));
        var scoredPositions = matchedPositions.Concat(clearPositions).Distinct().ToArray();
        var scoreDelta = checked(scoredPositions.Length * PointsPerTile * cascadeLevel);
        ClearAndRefill(board, clearPositions, randomSource);
        return new Match3ResolutionStep(
            cascadeLevel,
            beforeClear,
            clearPositions,
            createdSpecials,
            board,
            scoreDelta);
    }

    private static Match3ResolutionStep ResolveSpecialCombination(
        Match3Tile?[] board,
        Match3Position source,
        Match3Position target,
        Match3Tile firstBeforeSwap,
        Match3Tile secondBeforeSwap,
        int cascadeLevel,
        IMatch3RandomSource randomSource)
    {
        var clear = new HashSet<Match3Position>();
        var firstSpecial = firstBeforeSwap.Special;
        var secondSpecial = secondBeforeSwap.Special;
        // 交换后 first 位于 target、second 位于 source。彩虹组合需要让另一枚被转换后的特殊棋子正常触发，
        // 因此只预先标记彩虹球；其他组合已包含双方的增强效果，预先标记两格可避免重复按单体效果展开。
        var alreadyActivated = firstSpecial == Match3SpecialKind.Rainbow ||
                               secondSpecial == Match3SpecialKind.Rainbow
            ? new HashSet<Match3Position>(
                firstSpecial == Match3SpecialKind.Rainbow && secondSpecial == Match3SpecialKind.Rainbow
                    ? [source, target]
                    : [firstSpecial == Match3SpecialKind.Rainbow ? target : source])
            : [source, target];

        if (firstSpecial == Match3SpecialKind.Rainbow || secondSpecial == Match3SpecialKind.Rainbow)
        {
            ResolveRainbowCombination(
                board, source, target, firstBeforeSwap, secondBeforeSwap, clear);
        }
        else if (IsLine(firstSpecial) && IsLine(secondSpecial))
        {
            ResolveLinePair(source, target, firstSpecial, secondSpecial, clear);
        }
        else if (IsLine(firstSpecial) && secondSpecial == Match3SpecialKind.AreaBomb ||
                 IsLine(secondSpecial) && firstSpecial == Match3SpecialKind.AreaBomb)
        {
            AddRows(board, clear, target.Row - 1, target.Row, target.Row + 1);
            AddColumns(board, clear, target.Column - 1, target.Column, target.Column + 1);
        }
        else if (firstSpecial == Match3SpecialKind.AreaBomb && secondSpecial == Match3SpecialKind.AreaBomb)
        {
            AddRectangle(board, clear, target, radius: 2);
        }
        else
        {
            throw new InvalidOperationException("遇到了未定义的消消乐特殊棋子组合。");
        }

        clear.Add(source);
        clear.Add(target);
        var beforeClear = board.ToArray();
        ExpandTriggeredSpecials(board, clear, alreadyActivated);
        var scoreDelta = checked(clear.Count * PointsPerTile * cascadeLevel);
        ClearAndRefill(board, clear, randomSource);
        return new Match3ResolutionStep(
            cascadeLevel,
            beforeClear,
            clear,
            [],
            board,
            scoreDelta);
    }

    private static void ResolveRainbowCombination(
        Match3Tile?[] board,
        Match3Position source,
        Match3Position target,
        Match3Tile first,
        Match3Tile second,
        HashSet<Match3Position> clear)
    {
        if (first.Special == Match3SpecialKind.Rainbow && second.Special == Match3SpecialKind.Rainbow)
        {
            AddEntireBoard(board, clear);
            return;
        }

        var other = first.Special == Match3SpecialKind.Rainbow ? second : first;
        if (other.Kind is not { } color)
        {
            throw new InvalidOperationException("彩虹球组合缺少目标颜色。");
        }

        var matching = Enumerable.Range(0, Match3Rules.CellCount)
            .Where(index => board[index]?.Kind == color)
            .Select(Match3Rules.ToPosition)
            .ToArray();
        switch (other.Special)
        {
            case Match3SpecialKind.None:
                clear.UnionWith(matching);
                break;
            case Match3SpecialKind.RowClear:
            case Match3SpecialKind.ColumnClear:
                for (var index = 0; index < matching.Length; index++)
                {
                    var position = matching[index];
                    board[Match3Rules.ToIndex(position)] = new Match3Tile(
                        color,
                        index % 2 == 0 ? Match3SpecialKind.RowClear : Match3SpecialKind.ColumnClear);
                    clear.Add(position);
                }
                break;
            case Match3SpecialKind.AreaBomb:
                foreach (var position in matching)
                {
                    board[Match3Rules.ToIndex(position)] = new Match3Tile(color, Match3SpecialKind.AreaBomb);
                    clear.Add(position);
                }
                break;
            default:
                throw new InvalidOperationException("遇到了不支持的彩虹球组合目标。");
        }

        clear.Add(source);
        clear.Add(target);
    }

    private static void ResolveLinePair(
        Match3Position source,
        Match3Position target,
        Match3SpecialKind first,
        Match3SpecialKind second,
        HashSet<Match3Position> clear)
    {
        if (first == Match3SpecialKind.RowClear && second == Match3SpecialKind.RowClear)
        {
            AddRows(null, clear, source.Row, target.Row);
        }
        else if (first == Match3SpecialKind.ColumnClear && second == Match3SpecialKind.ColumnClear)
        {
            AddColumns(null, clear, source.Column, target.Column);
        }
        else
        {
            AddRows(null, clear, target.Row);
            AddColumns(null, clear, target.Column);
        }
    }

    private static IReadOnlyList<IReadOnlyList<Match3MatchRun>> BuildComponents(
        IReadOnlyList<Match3MatchRun> runs)
    {
        var remaining = runs.ToList();
        var components = new List<IReadOnlyList<Match3MatchRun>>();
        while (remaining.Count > 0)
        {
            var component = new List<Match3MatchRun> { remaining[0] };
            remaining.RemoveAt(0);
            var positions = component[0].Positions.ToHashSet();
            var added = true;
            while (added)
            {
                added = false;
                for (var index = remaining.Count - 1; index >= 0; index--)
                {
                    if (!remaining[index].Positions.Any(positions.Contains))
                    {
                        continue;
                    }

                    component.Add(remaining[index]);
                    positions.UnionWith(remaining[index].Positions);
                    remaining.RemoveAt(index);
                    added = true;
                }
            }

            components.Add(component);
        }

        return components;
    }

    private static Match3SpecialKind DetermineCreatedSpecial(IReadOnlyList<Match3MatchRun> component)
    {
        if (component.Any(run => run.Positions.Count >= 5))
        {
            return Match3SpecialKind.Rainbow;
        }

        if (component.Any(run => run.IsHorizontal) && component.Any(run => !run.IsHorizontal))
        {
            return Match3SpecialKind.AreaBomb;
        }

        var four = component.FirstOrDefault(run => run.Positions.Count == 4);
        return four is null
            ? Match3SpecialKind.None
            : four.IsHorizontal ? Match3SpecialKind.RowClear : Match3SpecialKind.ColumnClear;
    }

    private static Match3Position? ChooseAnchor(
        IReadOnlyList<Match3Tile?> board,
        IReadOnlyList<Match3MatchRun> component,
        Match3Position source,
        Match3Position target,
        bool preferSwappedAnchor)
    {
        var positions = component.SelectMany(run => run.Positions).Distinct().ToHashSet();
        var candidates = new List<Match3Position>();
        if (preferSwappedAnchor)
        {
            candidates.Add(target);
            candidates.Add(source);
        }

        var horizontal = component.FirstOrDefault(run => run.IsHorizontal);
        var vertical = component.FirstOrDefault(run => !run.IsHorizontal);
        if (horizontal is not null && vertical is not null)
        {
            candidates.AddRange(horizontal.Positions.Intersect(vertical.Positions));
        }

        var longest = component.OrderByDescending(run => run.Positions.Count).First();
        candidates.Add(longest.Positions[(longest.Positions.Count - 1) / 2]);
        candidates.AddRange(positions.OrderBy(position => position.Row).ThenBy(position => position.Column));
        return candidates
            .Where(positions.Contains)
            .Distinct()
            .Where(position => board[Match3Rules.ToIndex(position)]?.Special == Match3SpecialKind.None)
            .Select(position => (Match3Position?)position)
            .FirstOrDefault();
    }

    private static void ExpandTriggeredSpecials(
        IReadOnlyList<Match3Tile?> board,
        HashSet<Match3Position> clear,
        IEnumerable<Match3Position> initiallyActivated)
    {
        var activated = initiallyActivated.ToHashSet();
        var queue = new Queue<Match3Position>(clear);
        while (queue.Count > 0)
        {
            var position = queue.Dequeue();
            var tile = board[Match3Rules.ToIndex(position)];
            if (tile is null || tile.Value.Special == Match3SpecialKind.None || !activated.Add(position))
            {
                continue;
            }

            var additions = new HashSet<Match3Position>();
            switch (tile.Value.Special)
            {
                case Match3SpecialKind.RowClear:
                    AddRows(board, additions, position.Row);
                    break;
                case Match3SpecialKind.ColumnClear:
                    AddColumns(board, additions, position.Column);
                    break;
                case Match3SpecialKind.AreaBomb:
                    AddRectangle(board, additions, position, radius: 1);
                    break;
                case Match3SpecialKind.Rainbow:
                    var color = FindMostFrequentColor(board);
                    additions.UnionWith(Enumerable.Range(0, Match3Rules.CellCount)
                        .Where(index => board[index]?.Kind == color)
                        .Select(Match3Rules.ToPosition));
                    break;
            }

            foreach (var addition in additions)
            {
                if (clear.Add(addition))
                {
                    queue.Enqueue(addition);
                }
            }
        }
    }

    private static Match3GemKind FindMostFrequentColor(IReadOnlyList<Match3Tile?> board) =>
        Kinds.Select(kind => new
        {
            Kind = kind,
            Count = board.Count(tile => tile?.Kind == kind),
        })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Kind)
            .First().Kind;

    private static void ClearAndRefill(
        Match3Tile?[] board,
        IEnumerable<Match3Position> clear,
        IMatch3RandomSource randomSource)
    {
        foreach (var position in clear)
        {
            board[Match3Rules.ToIndex(position)] = null;
        }

        for (var column = 0; column < Match3Rules.BoardSize; column++)
        {
            var writeRow = Match3Rules.BoardSize - 1;
            for (var row = Match3Rules.BoardSize - 1; row >= 0; row--)
            {
                var tile = board[Match3Rules.ToIndex(new Match3Position(row, column))];
                if (tile is null)
                {
                    continue;
                }

                board[Match3Rules.ToIndex(new Match3Position(writeRow, column))] = tile;
                if (writeRow != row)
                {
                    board[Match3Rules.ToIndex(new Match3Position(row, column))] = null;
                }

                writeRow--;
            }

            while (writeRow >= 0)
            {
                var kind = Kinds[Match3BoardGenerator.NextValidated(randomSource, Kinds.Length)];
                board[Match3Rules.ToIndex(new Match3Position(writeRow, column))] = Match3Tile.Normal(kind);
                writeRow--;
            }
        }
    }

    private static bool IsLine(Match3SpecialKind special) =>
        special is Match3SpecialKind.RowClear or Match3SpecialKind.ColumnClear;

    private static void AddEntireBoard(IReadOnlyList<Match3Tile?> board, HashSet<Match3Position> clear)
    {
        for (var index = 0; index < board.Count; index++)
        {
            if (board[index] is not null)
            {
                clear.Add(Match3Rules.ToPosition(index));
            }
        }
    }

    private static void AddRows(
        IReadOnlyList<Match3Tile?>? board,
        HashSet<Match3Position> clear,
        params int[] rows)
    {
        foreach (var row in rows.Where(row => row >= 0 && row < Match3Rules.BoardSize))
        {
            for (var column = 0; column < Match3Rules.BoardSize; column++)
            {
                var position = new Match3Position(row, column);
                if (board is null || board[Match3Rules.ToIndex(position)] is not null)
                {
                    clear.Add(position);
                }
            }
        }
    }

    private static void AddColumns(
        IReadOnlyList<Match3Tile?>? board,
        HashSet<Match3Position> clear,
        params int[] columns)
    {
        foreach (var column in columns.Where(column => column >= 0 && column < Match3Rules.BoardSize))
        {
            for (var row = 0; row < Match3Rules.BoardSize; row++)
            {
                var position = new Match3Position(row, column);
                if (board is null || board[Match3Rules.ToIndex(position)] is not null)
                {
                    clear.Add(position);
                }
            }
        }
    }

    private static void AddRectangle(
        IReadOnlyList<Match3Tile?> board,
        HashSet<Match3Position> clear,
        Match3Position center,
        int radius)
    {
        for (var row = center.Row - radius; row <= center.Row + radius; row++)
        {
            for (var column = center.Column - radius; column <= center.Column + radius; column++)
            {
                var position = new Match3Position(row, column);
                if (Match3Rules.IsInside(position) && board[Match3Rules.ToIndex(position)] is not null)
                {
                    clear.Add(position);
                }
            }
        }
    }
}
