namespace ClassicGamePlugin.Features.Sudoku.Domain;

/// <summary>
/// 持有一局数独的可变状态。所有有效操作先保存完整的 81 格轻量快照，再原子提交数字、笔记和提示标记；
/// 这种朴素快照比为多格候选清理维护逆操作更可靠，也让撤销不会遗漏间接受影响的同行、同列或同宫笔记。
/// </summary>
internal sealed class SudokuGame
{
    private readonly Stack<SudokuSnapshot> _history = new();
    private int[] _values = new int[SudokuRules.CellCount];
    private int[] _notes = new int[SudokuRules.CellCount];
    private bool[] _hintCells = new bool[SudokuRules.CellCount];

    internal SudokuGame(SudokuPuzzle puzzle) => StartPuzzle(puzzle);

    internal SudokuPuzzle Puzzle { get; private set; } = null!;
    internal IReadOnlyList<int> Values => _values;
    internal IReadOnlyList<int> Notes => _notes;
    internal IReadOnlyList<bool> HintCells => _hintCells;
    internal bool IsCompleted { get; private set; }
    internal bool CanUndo => _history.Count > 0;

    internal bool IsGiven(SudokuPosition position) => Puzzle.Givens[SudokuRules.ToIndex(position)] != 0;
    internal bool IsHint(SudokuPosition position) => _hintCells[SudokuRules.ToIndex(position)];
    internal bool IsEditable(SudokuPosition position) => !IsGiven(position) && !IsHint(position);

    internal SudokuMoveResult? SetValue(SudokuPosition position, int value)
    {
        if (value < 1 || value > SudokuRules.BoardSize)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var index = SudokuRules.ToIndex(position);
        if (IsCompleted || !IsEditable(position) || _values[index] == value)
        {
            return null;
        }

        SaveSnapshot();
        _values[index] = value;
        _notes[index] = 0;
        RemovePeerNote(position, value);
        return FinishMove(SudokuMoveKind.Value, position);
    }

    internal SudokuMoveResult? ClearValue(SudokuPosition position)
    {
        var index = SudokuRules.ToIndex(position);
        if (IsCompleted || !IsEditable(position) || _values[index] == 0)
        {
            return null;
        }

        SaveSnapshot();
        _values[index] = 0;
        return FinishMove(SudokuMoveKind.Clear, position);
    }

    internal SudokuMoveResult? ToggleNote(SudokuPosition position, int value)
    {
        if (value < 1 || value > SudokuRules.BoardSize)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var index = SudokuRules.ToIndex(position);
        if (IsCompleted || !IsEditable(position) || _values[index] != 0)
        {
            return null;
        }

        SaveSnapshot();
        _notes[index] ^= 1 << value;
        return FinishMove(SudokuMoveKind.Note, position);
    }

    internal SudokuMoveResult? RevealHint(SudokuPosition? preferredPosition)
    {
        var index = preferredPosition is { } preferred && SudokuRules.IsInside(preferred) &&
                    IsEditable(preferred) && _values[SudokuRules.ToIndex(preferred)] == 0
            ? SudokuRules.ToIndex(preferred)
            : FindFirstHintTarget();
        if (IsCompleted || index < 0)
        {
            return null;
        }

        var position = SudokuRules.FromIndex(index);
        SaveSnapshot();
        var value = Puzzle.Solution[index];
        _values[index] = value;
        _notes[index] = 0;
        _hintCells[index] = true;
        RemovePeerNote(position, value);
        return FinishMove(SudokuMoveKind.Hint, position);
    }

    internal SudokuMoveResult? Undo()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        var snapshot = _history.Pop();
        _values = snapshot.Values;
        _notes = snapshot.Notes;
        _hintCells = snapshot.HintCells;
        IsCompleted = snapshot.IsCompleted;
        return new SudokuMoveResult(
            SudokuMoveKind.Undo,
            null,
            SudokuRules.FindConflicts(_values),
            IsCompleted);
    }

    internal void Restart() => StartPuzzle(Puzzle);

    internal void StartPuzzle(SudokuPuzzle puzzle)
    {
        Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
        _values = puzzle.Givens.ToArray();
        _notes = new int[SudokuRules.CellCount];
        _hintCells = new bool[SudokuRules.CellCount];
        _history.Clear();
        IsCompleted = false;
    }

    private SudokuMoveResult FinishMove(SudokuMoveKind kind, SudokuPosition position)
    {
        IsCompleted = SudokuRules.IsCompleted(_values, Puzzle.Solution);
        return new SudokuMoveResult(kind, position, SudokuRules.FindConflicts(_values), IsCompleted);
    }

    private void RemovePeerNote(SudokuPosition position, int value)
    {
        var mask = ~(1 << value);
        for (var index = 0; index < SudokuRules.CellCount; index++)
        {
            if (SudokuRules.ArePeers(position, SudokuRules.FromIndex(index)))
            {
                _notes[index] &= mask;
            }
        }
    }

    private int FindFirstHintTarget()
    {
        for (var index = 0; index < _values.Length; index++)
        {
            if (_values[index] == 0 && IsEditable(SudokuRules.FromIndex(index)))
            {
                return index;
            }
        }

        return -1;
    }

    private void SaveSnapshot() => _history.Push(new SudokuSnapshot(
        _values.ToArray(),
        _notes.ToArray(),
        _hintCells.ToArray(),
        IsCompleted));

    private sealed record SudokuSnapshot(
        int[] Values,
        int[] Notes,
        bool[] HintCells,
        bool IsCompleted);
}
