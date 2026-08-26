namespace ClassicGamePlugin.Features.Minesweeper.Domain;

/// <summary>
/// 编排一局扫雷的全部领域规则。该类型不认识 Avalonia、Document、计时器或用户输入设备，
/// 因而可以通过普通单元测试验证所有状态转换。
/// </summary>
internal sealed class MinesweeperGame
{
    private readonly IMinePlacementStrategy _minePlacementStrategy;
    private readonly List<MinesweeperCell> _cells = [];
    private int _revealedSafeCellCount;

    /// <summary>使用指定布雷策略创建一局初始游戏。</summary>
    internal MinesweeperGame(
        MinesweeperDifficultyDefinition difficulty,
        IMinePlacementStrategy minePlacementStrategy)
    {
        _minePlacementStrategy = minePlacementStrategy ??
            throw new ArgumentNullException(nameof(minePlacementStrategy));
        StartNewGame(difficulty);
    }

    /// <summary>获取当前难度定义。</summary>
    internal MinesweeperDifficultyDefinition Difficulty { get; private set; } = null!;

    /// <summary>获取当前游戏状态。</summary>
    internal MinesweeperGameState State { get; private set; }

    /// <summary>获取按行优先顺序排列的只读格子集合。</summary>
    internal IReadOnlyList<MinesweeperCell> Cells => _cells;

    /// <summary>获取玩家已经插下的旗帜数。</summary>
    internal int FlagCount => _cells.Count(cell => cell.State == MinesweeperCellState.Flagged);

    /// <summary>获取“总雷数减旗帜数”，允许玩家插错旗但不改变胜利规则。</summary>
    internal int RemainingMineCount => Difficulty.MineCount - FlagCount;

    /// <summary>
    /// 使用指定难度重置整局状态。旧棋盘不会复用，以免残留雷位或已翻开状态。
    /// </summary>
    internal void StartNewGame(MinesweeperDifficultyDefinition difficulty)
    {
        Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        ValidateDifficulty(difficulty);

        _cells.Clear();
        _revealedSafeCellCount = 0;
        State = MinesweeperGameState.Ready;

        for (var row = 0; row < difficulty.Rows; row++)
        {
            for (var column = 0; column < difficulty.Columns; column++)
            {
                _cells.Add(new MinesweeperCell(row, column));
            }
        }
    }

    /// <summary>
    /// 翻开指定格子。点击已翻开的数字格时会尝试经典快速展开；点击旗帜或终局棋盘不会改变状态。
    /// </summary>
    /// <returns>本次操作是否改变了任意领域状态。</returns>
    internal bool Reveal(int row, int column)
    {
        var cell = GetCell(row, column);
        if (State is MinesweeperGameState.Won or MinesweeperGameState.Lost ||
            cell.State == MinesweeperCellState.Flagged)
        {
            return false;
        }

        if (cell.State == MinesweeperCellState.Revealed)
        {
            return Chord(cell);
        }

        if (State == MinesweeperGameState.Ready)
        {
            PlaceMines(cell.Coordinate);
            State = MinesweeperGameState.Running;
        }

        var changed = RevealFrom(cell);
        CompleteIfAllSafeCellsAreRevealed();
        return changed;
    }

    /// <summary>
    /// 在覆盖和旗帜状态之间切换。插旗属于准备动作，不会触发布雷或启动游戏。
    /// </summary>
    /// <returns>本次操作是否改变了格子状态。</returns>
    internal bool ToggleFlag(int row, int column)
    {
        var cell = GetCell(row, column);
        if (State is MinesweeperGameState.Won or MinesweeperGameState.Lost ||
            cell.State == MinesweeperCellState.Revealed)
        {
            return false;
        }

        cell.State = cell.State == MinesweeperCellState.Covered
            ? MinesweeperCellState.Flagged
            : MinesweeperCellState.Covered;
        return true;
    }

    /// <summary>获取指定坐标的格子，并统一执行越界检查。</summary>
    internal MinesweeperCell GetCell(int row, int column)
    {
        if (row < 0 || row >= Difficulty.Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (column < 0 || column >= Difficulty.Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        return _cells[(row * Difficulty.Columns) + column];
    }

    private static void ValidateDifficulty(MinesweeperDifficultyDefinition difficulty)
    {
        if (difficulty.Rows <= 0 || difficulty.Columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty), "棋盘行列必须为正数。");
        }

        // 首击最多排除九宫格，因此至少需要在剩余位置容纳所有雷。
        var largestSafeArea = Math.Min(3, difficulty.Rows) * Math.Min(3, difficulty.Columns);
        if (difficulty.MineCount <= 0 ||
            difficulty.MineCount > (difficulty.Rows * difficulty.Columns) - largestSafeArea)
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty), "雷数无法满足首击空白安全规则。");
        }
    }

    private void PlaceMines(CellCoordinate firstReveal)
    {
        var excludedCoordinates = GetNeighborsIncludingSelf(firstReveal).ToHashSet();
        var mines = _minePlacementStrategy.CreateMines(
            Difficulty.Rows,
            Difficulty.Columns,
            Difficulty.MineCount,
            excludedCoordinates);
        var uniqueMines = mines.ToHashSet();

        if (mines.Count != Difficulty.MineCount || uniqueMines.Count != Difficulty.MineCount ||
            uniqueMines.Any(excludedCoordinates.Contains) ||
            uniqueMines.Any(coordinate => !IsInside(coordinate.Row, coordinate.Column)))
        {
            throw new InvalidOperationException("布雷策略返回了数量、范围或首击安全约束不合法的雷位。");
        }

        foreach (var coordinate in uniqueMines)
        {
            GetCell(coordinate.Row, coordinate.Column).IsMine = true;
        }

        foreach (var cell in _cells.Where(cell => !cell.IsMine))
        {
            cell.AdjacentMineCount = GetNeighbors(cell.Coordinate).Count(neighbor => neighbor.IsMine);
        }
    }

    private bool RevealFrom(MinesweeperCell startingCell)
    {
        if (startingCell.IsMine)
        {
            startingCell.IsExploded = true;
            State = MinesweeperGameState.Lost;
            return true;
        }

        var changed = false;
        var pending = new Queue<MinesweeperCell>();
        var queued = new HashSet<CellCoordinate> { startingCell.Coordinate };
        pending.Enqueue(startingCell);

        while (pending.TryDequeue(out var cell))
        {
            if (cell.State != MinesweeperCellState.Covered || cell.IsMine)
            {
                continue;
            }

            cell.State = MinesweeperCellState.Revealed;
            _revealedSafeCellCount++;
            changed = true;

            if (cell.AdjacentMineCount != 0)
            {
                continue;
            }

            // 使用队列展开空白区，避免较大棋盘上递归调用导致栈深度与区域大小耦合。
            foreach (var neighbor in GetNeighbors(cell.Coordinate))
            {
                if (!neighbor.IsMine &&
                    neighbor.State == MinesweeperCellState.Covered &&
                    queued.Add(neighbor.Coordinate))
                {
                    pending.Enqueue(neighbor);
                }
            }
        }

        return changed;
    }

    private bool Chord(MinesweeperCell center)
    {
        if (State != MinesweeperGameState.Running || center.AdjacentMineCount == 0)
        {
            return false;
        }

        var neighbors = GetNeighbors(center.Coordinate).ToArray();
        if (neighbors.Count(cell => cell.State == MinesweeperCellState.Flagged) != center.AdjacentMineCount)
        {
            return false;
        }

        var changed = false;
        foreach (var neighbor in neighbors.Where(cell => cell.State == MinesweeperCellState.Covered))
        {
            changed |= RevealFrom(neighbor);
            if (State == MinesweeperGameState.Lost)
            {
                break;
            }
        }

        CompleteIfAllSafeCellsAreRevealed();
        return changed;
    }

    private void CompleteIfAllSafeCellsAreRevealed()
    {
        if (State == MinesweeperGameState.Running &&
            _revealedSafeCellCount == _cells.Count - Difficulty.MineCount)
        {
            State = MinesweeperGameState.Won;
        }
    }

    private IEnumerable<MinesweeperCell> GetNeighbors(CellCoordinate coordinate)
    {
        foreach (var neighborCoordinate in GetNeighborsIncludingSelf(coordinate))
        {
            if (neighborCoordinate != coordinate)
            {
                yield return GetCell(neighborCoordinate.Row, neighborCoordinate.Column);
            }
        }
    }

    private IEnumerable<CellCoordinate> GetNeighborsIncludingSelf(CellCoordinate coordinate)
    {
        for (var row = coordinate.Row - 1; row <= coordinate.Row + 1; row++)
        {
            for (var column = coordinate.Column - 1; column <= coordinate.Column + 1; column++)
            {
                if (IsInside(row, column))
                {
                    yield return new CellCoordinate(row, column);
                }
            }
        }
    }

    private bool IsInside(int row, int column) =>
        row >= 0 && row < Difficulty.Rows && column >= 0 && column < Difficulty.Columns;
}
