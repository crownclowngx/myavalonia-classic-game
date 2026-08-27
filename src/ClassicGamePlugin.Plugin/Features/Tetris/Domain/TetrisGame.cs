namespace ClassicGamePlugin.Features.Tetris.Domain;

/// <summary>
/// 持有一局俄罗斯方块的权威状态，并把移动、旋转、暂存和锁定作为原子事务提交。它不读取键盘、不创建计时器、
/// 不绘制动画；随机性只通过 <see cref="ITetrominoSource"/> 进入，便于规则测试精确复现每一枚方块。
/// </summary>
internal sealed class TetrisGame
{
    private readonly ITetrominoSource _source;
    private readonly Queue<TetrominoType> _next = [];
    private TetrominoType?[] _cells = new TetrominoType?[TetrisRules.BoardWidth * TetrisRules.BoardHeight];
    private bool _lastActionWasRotation;
    private int _lastRotationKickIndex = -1;

    internal TetrisGame(ITetrominoSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        StartNewGame();
    }

    internal IReadOnlyList<TetrominoType?> Cells => _cells;
    internal TetrisPiece ActivePiece { get; private set; }
    internal TetrominoType? HeldPiece { get; private set; }
    internal IReadOnlyList<TetrominoType> NextPieces => _next.Take(5).ToArray();
    internal TetrisGameState State { get; private set; }
    internal int Score { get; private set; }
    internal int TotalLines { get; private set; }
    internal int Level => (TotalLines / 10) + 1;
    internal int Combo { get; private set; } = -1;
    internal bool IsBackToBackActive { get; private set; }
    internal bool CanHold { get; private set; }
    internal bool IsGrounded => State == TetrisGameState.Playing && !CanPlace(ActivePiece with { Row = ActivePiece.Row + 1 });

    internal void StartNewGame()
    {
        _cells = new TetrominoType?[TetrisRules.BoardWidth * TetrisRules.BoardHeight];
        _next.Clear();
        HeldPiece = null;
        Score = 0;
        TotalLines = 0;
        Combo = -1;
        IsBackToBackActive = false;
        State = TetrisGameState.Playing;
        CanHold = true;
        ResetLastManeuver();
        SpawnNext();
    }

    internal bool TogglePause()
    {
        if (State == TetrisGameState.GameOver)
        {
            return false;
        }

        State = State == TetrisGameState.Paused ? TetrisGameState.Playing : TetrisGameState.Paused;
        return true;
    }

    internal bool Pause()
    {
        if (State != TetrisGameState.Playing)
        {
            return false;
        }

        State = TetrisGameState.Paused;
        return true;
    }

    internal bool TryMoveHorizontal(int delta)
    {
        if (delta is not (-1 or 1) || State != TetrisGameState.Playing)
        {
            return false;
        }

        var candidate = ActivePiece with { Column = ActivePiece.Column + delta };
        if (!CanPlace(candidate))
        {
            return false;
        }

        ActivePiece = candidate;
        ResetLastManeuver();
        return true;
    }

    internal bool TryStepDown(bool awardSoftDropPoint)
    {
        if (State != TetrisGameState.Playing)
        {
            return false;
        }

        var candidate = ActivePiece with { Row = ActivePiece.Row + 1 };
        if (!CanPlace(candidate))
        {
            return false;
        }

        ActivePiece = candidate;
        if (awardSoftDropPoint)
        {
            Score++;
        }

        // 垂直下降不抹掉最后一次旋转标记，使玩家旋转后通过重力或硬降落入槽位时仍能正确识别 T-Spin。
        return true;
    }

    internal bool TryRotate(bool clockwise, out int successfulKickIndex)
    {
        successfulKickIndex = -1;
        if (State != TetrisGameState.Playing)
        {
            return false;
        }

        var targetRotation = TetrisRules.Rotate(ActivePiece.Rotation, clockwise);
        var tests = TetrisRules.GetKickTests(ActivePiece.Type, ActivePiece.Rotation, targetRotation);
        for (var index = 0; index < tests.Count; index++)
        {
            var kick = tests[index];
            var candidate = ActivePiece with
            {
                Rotation = targetRotation,
                Row = ActivePiece.Row - kick.Y,
                Column = ActivePiece.Column + kick.X,
            };
            if (!CanPlace(candidate))
            {
                continue;
            }

            ActivePiece = candidate;
            _lastActionWasRotation = true;
            _lastRotationKickIndex = index;
            successfulKickIndex = index;
            return true;
        }

        return false;
    }

    internal bool Hold()
    {
        if (State != TetrisGameState.Playing || !CanHold)
        {
            return false;
        }

        var outgoing = ActivePiece.Type;
        if (HeldPiece is { } incoming)
        {
            HeldPiece = outgoing;
            ActivePiece = TetrisRules.CreateSpawnPiece(incoming);
            if (!CanPlace(ActivePiece))
            {
                State = TetrisGameState.GameOver;
            }
        }
        else
        {
            HeldPiece = outgoing;
            SpawnNext();
        }

        CanHold = false;
        ResetLastManeuver();
        return true;
    }

    internal TetrisPiece GetGhostPiece()
    {
        var ghost = ActivePiece;
        while (CanPlace(ghost with { Row = ghost.Row + 1 }))
        {
            ghost = ghost with { Row = ghost.Row + 1 };
        }

        return ghost;
    }

    internal TetrisTransition? HardDrop()
    {
        if (State != TetrisGameState.Playing)
        {
            return null;
        }

        var scoreBefore = Score;
        var startRow = ActivePiece.Row;
        var ghost = GetGhostPiece();
        ActivePiece = ghost;
        Score += Math.Max(0, ghost.Row - startRow) * 2;
        var transition = LockActivePiece(startRow);
        return transition is null ? null : transition with { ScoreGained = Score - scoreBefore };
    }

    internal TetrisTransition? LockActivePiece(int? dropStartRow = null)
    {
        if (State != TetrisGameState.Playing)
        {
            return null;
        }

        var before = (TetrominoType?[])_cells.Clone();
        var lockedPiece = ActivePiece;
        var spin = DetectSpin(lockedPiece);
        foreach (var position in TetrisRules.GetCells(lockedPiece))
        {
            _cells[TetrisRules.ToIndex(position.Row, position.Column)] = lockedPiece.Type;
        }

        var clearedRows = FindFullRows();
        if (clearedRows.Count > 0)
        {
            RemoveRows(clearedRows);
        }

        var levelBeforeClear = Level;
        var perfectClear = clearedRows.Count > 0 && _cells.All(cell => cell is null);
        var scoreBefore = Score;
        ApplyClearScore(clearedRows.Count, spin, perfectClear, levelBeforeClear);
        TotalLines += clearedRows.Count;

        CanHold = true;
        ResetLastManeuver();
        if (HasBlocksInHiddenRows())
        {
            State = TetrisGameState.GameOver;
        }
        else
        {
            SpawnNext();
        }

        return new TetrisTransition(
            lockedPiece,
            dropStartRow ?? lockedPiece.Row,
            clearedRows,
            before,
            (TetrominoType?[])_cells.Clone(),
            spin,
            perfectClear,
            IsBackToBackActive,
            Score - scoreBefore);
    }

    internal bool CanPlace(TetrisPiece piece)
    {
        foreach (var position in TetrisRules.GetCells(piece))
        {
            if (position.Row < 0 || position.Row >= TetrisRules.BoardHeight ||
                position.Column < 0 || position.Column >= TetrisRules.BoardWidth ||
                _cells[TetrisRules.ToIndex(position.Row, position.Column)] is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>测试入口一次装载完整权威状态，避免测试通过反射或大量无关按键才能构造边界棋盘。</summary>
    internal void LoadStateForTest(
        IReadOnlyList<TetrominoType?> cells,
        TetrisPiece activePiece,
        int score = 0,
        int totalLines = 0,
        bool backToBack = false,
        int combo = -1,
        int rotationKickIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count != _cells.Length)
        {
            throw new ArgumentException("测试棋盘尺寸必须等于 10×24。", nameof(cells));
        }

        _cells = cells.ToArray();
        ActivePiece = activePiece;
        Score = score;
        TotalLines = totalLines;
        Combo = combo;
        IsBackToBackActive = backToBack;
        State = CanPlace(activePiece) ? TetrisGameState.Playing : TetrisGameState.GameOver;
        CanHold = true;
        _lastActionWasRotation = rotationKickIndex >= 0;
        _lastRotationKickIndex = rotationKickIndex;
        EnsurePreview();
    }

    private void SpawnNext()
    {
        EnsurePreview(6);
        ActivePiece = TetrisRules.CreateSpawnPiece(_next.Dequeue());
        EnsurePreview();
        if (!CanPlace(ActivePiece))
        {
            State = TetrisGameState.GameOver;
        }
    }

    private void EnsurePreview(int count = 5)
    {
        while (_next.Count < count)
        {
            _next.Enqueue(_source.Next());
        }
    }

    private IReadOnlyList<int> FindFullRows()
    {
        var rows = new List<int>();
        for (var row = 0; row < TetrisRules.BoardHeight; row++)
        {
            var full = true;
            for (var column = 0; column < TetrisRules.BoardWidth; column++)
            {
                if (_cells[TetrisRules.ToIndex(row, column)] is null)
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private void RemoveRows(IReadOnlyCollection<int> rows)
    {
        var removed = rows.ToHashSet();
        var compacted = new TetrominoType?[_cells.Length];
        var targetRow = TetrisRules.BoardHeight - 1;
        for (var sourceRow = TetrisRules.BoardHeight - 1; sourceRow >= 0; sourceRow--)
        {
            if (removed.Contains(sourceRow))
            {
                continue;
            }

            Array.Copy(
                _cells,
                TetrisRules.ToIndex(sourceRow, 0),
                compacted,
                TetrisRules.ToIndex(targetRow, 0),
                TetrisRules.BoardWidth);
            targetRow--;
        }

        _cells = compacted;
    }

    private TetrisSpinKind DetectSpin(TetrisPiece piece)
    {
        if (piece.Type != TetrominoType.T || !_lastActionWasRotation)
        {
            return TetrisSpinKind.None;
        }

        var pivot = new TetrisPosition(piece.Row + 1, piece.Column + 1);
        var corners = new[]
        {
            new TetrisPosition(pivot.Row - 1, pivot.Column - 1),
            new TetrisPosition(pivot.Row - 1, pivot.Column + 1),
            new TetrisPosition(pivot.Row + 1, pivot.Column - 1),
            new TetrisPosition(pivot.Row + 1, pivot.Column + 1),
        };
        if (corners.Count(IsOccupiedOrOutside) < 3)
        {
            return TetrisSpinKind.None;
        }

        var front = piece.Rotation switch
        {
            TetrisRotation.Spawn => corners[..2],
            TetrisRotation.Right => [corners[1], corners[3]],
            TetrisRotation.Reverse => corners[2..],
            TetrisRotation.Left => [corners[0], corners[2]],
            _ => throw new ArgumentOutOfRangeException(),
        };
        return front.All(IsOccupiedOrOutside) || _lastRotationKickIndex == 4
            ? TetrisSpinKind.Full
            : TetrisSpinKind.Mini;
    }

    private bool IsOccupiedOrOutside(TetrisPosition position) =>
        position.Row < 0 || position.Row >= TetrisRules.BoardHeight ||
        position.Column < 0 || position.Column >= TetrisRules.BoardWidth ||
        _cells[TetrisRules.ToIndex(position.Row, position.Column)] is not null;

    private void ApplyClearScore(int lineCount, TetrisSpinKind spin, bool perfectClear, int level)
    {
        var baseScore = spin switch
        {
            TetrisSpinKind.Mini => lineCount switch { 0 => 100, 1 => 200, 2 => 400, _ => 0 },
            TetrisSpinKind.Full => lineCount switch { 0 => 400, 1 => 800, 2 => 1200, 3 => 1600, _ => 0 },
            _ => lineCount switch { 1 => 100, 2 => 300, 3 => 500, 4 => 800, _ => 0 },
        };
        var difficult = lineCount > 0 && (lineCount == 4 || spin != TetrisSpinKind.None);
        var receivesBackToBackBonus = difficult && IsBackToBackActive;
        if (receivesBackToBackBonus)
        {
            baseScore = (baseScore * 3) / 2;
        }

        if (lineCount > 0)
        {
            Combo++;
            if (!difficult)
            {
                IsBackToBackActive = false;
            }
            else
            {
                IsBackToBackActive = true;
            }
        }
        else
        {
            Combo = -1;
        }

        var comboBonus = lineCount > 0 ? 50 * Math.Max(0, Combo) : 0;
        var perfectClearBonus = perfectClear
            ? lineCount switch
            {
                1 => 800,
                2 => 1200,
                3 => 1800,
                4 when receivesBackToBackBonus => 3200,
                4 => 2000,
                _ => 0,
            }
            : 0;
        Score += (baseScore + comboBonus + perfectClearBonus) * level;
    }

    private bool HasBlocksInHiddenRows()
    {
        for (var row = 0; row < TetrisRules.HiddenRows; row++)
        {
            for (var column = 0; column < TetrisRules.BoardWidth; column++)
            {
                if (_cells[TetrisRules.ToIndex(row, column)] is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ResetLastManeuver()
    {
        _lastActionWasRotation = false;
        _lastRotationKickIndex = -1;
    }
}
