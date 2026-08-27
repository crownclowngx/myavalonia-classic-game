namespace ClassicGamePlugin.Features.Sudoku.Domain;

/// <summary>
/// 使用最少候选格优先的朴素回溯求解器。它既负责为题库推导答案，也负责生成器的唯一解门禁；
/// 计数到调用方给定上限后立即返回，避免为“至少两个解”的判断继续枚举无意义的全部答案。
/// </summary>
internal sealed class SudokuSolver
{
    internal bool TrySolve(
        IReadOnlyList<int> puzzle,
        out int[] solution,
        CancellationToken cancellationToken = default)
    {
        SudokuRules.ValidateBoard(puzzle, nameof(puzzle));
        solution = puzzle.ToArray();
        if (!SudokuRules.HasNoConflicts(solution))
        {
            return false;
        }

        return SolveFirst(solution, cancellationToken);
    }

    internal int CountSolutions(
        IReadOnlyList<int> puzzle,
        int limit = 2,
        CancellationToken cancellationToken = default)
    {
        SudokuRules.ValidateBoard(puzzle, nameof(puzzle));
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var board = puzzle.ToArray();
        if (!SudokuRules.HasNoConflicts(board))
        {
            return 0;
        }

        var count = 0;
        Count(board, limit, ref count, cancellationToken);
        return count;
    }

    private static bool SolveFirst(int[] board, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var index = FindBestEmpty(board, out var candidates);
        if (index < 0)
        {
            return true;
        }

        if (candidates == 0)
        {
            return false;
        }

        for (var value = 1; value <= SudokuRules.BoardSize; value++)
        {
            if ((candidates & (1 << value)) == 0)
            {
                continue;
            }

            board[index] = value;
            if (SolveFirst(board, cancellationToken))
            {
                return true;
            }
        }

        board[index] = 0;
        return false;
    }

    private static void Count(
        int[] board,
        int limit,
        ref int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count >= limit)
        {
            return;
        }

        var index = FindBestEmpty(board, out var candidates);
        if (index < 0)
        {
            count++;
            return;
        }

        if (candidates == 0)
        {
            return;
        }

        for (var value = 1; value <= SudokuRules.BoardSize && count < limit; value++)
        {
            if ((candidates & (1 << value)) == 0)
            {
                continue;
            }

            board[index] = value;
            Count(board, limit, ref count, cancellationToken);
        }

        board[index] = 0;
    }

    /// <summary>
    /// 每层优先尝试候选最少的空格。这个局部剪枝不改变解集合，却显著降低困难题唯一性验证的搜索分支。
    /// </summary>
    private static int FindBestEmpty(IReadOnlyList<int> board, out int bestMask)
    {
        var bestIndex = -1;
        bestMask = 0;
        var bestCount = int.MaxValue;
        for (var index = 0; index < SudokuRules.CellCount; index++)
        {
            if (board[index] != 0)
            {
                continue;
            }

            var mask = SudokuRules.GetCandidateMask(board, SudokuRules.FromIndex(index));
            var count = SudokuRules.CountCandidates(mask);
            if (count >= bestCount)
            {
                continue;
            }

            bestIndex = index;
            bestMask = mask;
            bestCount = count;
            if (count <= 1)
            {
                break;
            }
        }

        return bestIndex;
    }
}
