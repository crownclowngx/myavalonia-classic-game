namespace ClassicGamePlugin.Features.Sudoku.Domain;

/// <summary>数独固定使用的三个休闲难度档位。</summary>
internal enum SudokuDifficulty
{
    Easy,
    Medium,
    Hard,
}

/// <summary>区分立即可用的内置题目与玩家主动请求生成的题目。</summary>
internal enum SudokuPuzzleSource
{
    BuiltIn,
    Generated,
}

/// <summary>按行、列定位 9×9 棋盘中的一个格子。</summary>
internal readonly record struct SudokuPosition(int Row, int Column);

/// <summary>
/// 不可变数独题目。构造时复制题面和答案，避免题库、生成线程或对局通过共享数组相互污染。
/// </summary>
internal sealed class SudokuPuzzle
{
    private readonly int[] _givens;
    private readonly int[] _solution;

    internal SudokuPuzzle(
        string id,
        SudokuDifficulty difficulty,
        SudokuPuzzleSource source,
        IReadOnlyList<int> givens,
        IReadOnlyList<int> solution)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("数独题目必须具有非空身份。", nameof(id));
        }

        SudokuRules.ValidateBoard(givens, nameof(givens));
        SudokuRules.ValidateBoard(solution, nameof(solution));
        if (solution.Any(value => value == 0) || !SudokuRules.HasNoConflicts(solution))
        {
            throw new ArgumentException("数独答案必须是完整且无冲突的 9×9 终盘。", nameof(solution));
        }

        for (var index = 0; index < SudokuRules.CellCount; index++)
        {
            if (givens[index] != 0 && givens[index] != solution[index])
            {
                throw new ArgumentException("数独给定数字必须与答案位于同一位置的数字一致。", nameof(givens));
            }
        }

        Id = id;
        Difficulty = difficulty;
        Source = source;
        _givens = givens.ToArray();
        _solution = solution.ToArray();
    }

    internal string Id { get; }
    internal SudokuDifficulty Difficulty { get; }
    internal SudokuPuzzleSource Source { get; }
    internal IReadOnlyList<int> Givens => _givens;
    internal IReadOnlyList<int> Solution => _solution;
    internal int ClueCount => _givens.Count(value => value != 0);
}

/// <summary>集中定义难度名称和提示数范围，避免题库、生成器与界面出现不一致的魔法数字。</summary>
internal readonly record struct SudokuDifficultyProfile(
    SudokuDifficulty Difficulty,
    string DisplayName,
    int MinimumClues,
    int MaximumClues)
{
    internal static SudokuDifficultyProfile For(SudokuDifficulty difficulty) => difficulty switch
    {
        SudokuDifficulty.Easy => new(difficulty, "简单", 40, 45),
        SudokuDifficulty.Medium => new(difficulty, "中等", 33, 39),
        SudokuDifficulty.Hard => new(difficulty, "困难", 26, 32),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };
}

/// <summary>对局中一次成功操作的种类，用于测试、状态提示和动画选择。</summary>
internal enum SudokuMoveKind
{
    Value,
    Clear,
    Note,
    Hint,
    Undo,
}

/// <summary>领域操作提交后的只读摘要；动画只读取结果，不参与或回滚规则。</summary>
internal sealed record SudokuMoveResult(
    SudokuMoveKind Kind,
    SudokuPosition? Position,
    IReadOnlySet<SudokuPosition> Conflicts,
    bool IsCompleted);
