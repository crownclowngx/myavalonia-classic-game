using System.Numerics;

namespace ClassicGamePlugin.Features.Sudoku.Domain;

/// <summary>
/// 数独的无状态纯规则。这里不保存题目、不选择随机数，也不产生 UI 文案，因此求解器、领域对局和测试可以共享同一真相。
/// </summary>
internal static class SudokuRules
{
    internal const int BoardSize = 9;
    internal const int BoxSize = 3;
    internal const int CellCount = BoardSize * BoardSize;
    internal const int AllCandidatesMask = 0b11_1111_1110;

    internal static int ToIndex(SudokuPosition position)
    {
        if (!IsInside(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "数独坐标必须位于 9×9 棋盘内。");
        }

        return (position.Row * BoardSize) + position.Column;
    }

    internal static SudokuPosition FromIndex(int index)
    {
        if (index < 0 || index >= CellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new SudokuPosition(index / BoardSize, index % BoardSize);
    }

    internal static bool IsInside(SudokuPosition position) =>
        position.Row >= 0 && position.Row < BoardSize &&
        position.Column >= 0 && position.Column < BoardSize;

    internal static bool ArePeers(SudokuPosition first, SudokuPosition second) =>
        first != second &&
        (first.Row == second.Row ||
         first.Column == second.Column ||
         first.Row / BoxSize == second.Row / BoxSize &&
         first.Column / BoxSize == second.Column / BoxSize);

    /// <summary>返回当前位置在当前盘面仍可使用的数字位掩码；第 n 位表示数字 n。</summary>
    internal static int GetCandidateMask(IReadOnlyList<int> board, SudokuPosition position)
    {
        ValidateBoard(board, nameof(board));
        var mask = AllCandidatesMask;
        for (var index = 0; index < CellCount; index++)
        {
            var value = board[index];
            if (value != 0 && ArePeers(position, FromIndex(index)))
            {
                mask &= ~(1 << value);
            }
        }

        return mask;
    }

    internal static IReadOnlySet<SudokuPosition> FindConflicts(IReadOnlyList<int> board)
    {
        ValidateBoard(board, nameof(board));
        var conflicts = new HashSet<SudokuPosition>();
        for (var first = 0; first < CellCount; first++)
        {
            if (board[first] == 0)
            {
                continue;
            }

            for (var second = first + 1; second < CellCount; second++)
            {
                if (board[first] == board[second] && ArePeers(FromIndex(first), FromIndex(second)))
                {
                    conflicts.Add(FromIndex(first));
                    conflicts.Add(FromIndex(second));
                }
            }
        }

        return conflicts;
    }

    internal static bool HasNoConflicts(IReadOnlyList<int> board) => FindConflicts(board).Count == 0;

    internal static bool IsCompleted(IReadOnlyList<int> board, IReadOnlyList<int> solution)
    {
        ValidateBoard(board, nameof(board));
        ValidateBoard(solution, nameof(solution));
        return board.SequenceEqual(solution) && board.All(value => value != 0);
    }

    internal static int CountCandidates(int mask) => BitOperations.PopCount((uint)mask);

    internal static void ValidateBoard(IReadOnlyList<int> board, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(board, parameterName);
        if (board.Count != CellCount)
        {
            throw new ArgumentException($"数独棋盘必须恰好包含 {CellCount} 个格子。", parameterName);
        }

        if (board.Any(value => value < 0 || value > BoardSize))
        {
            throw new ArgumentException("数独格子只能包含 0 到 9；0 表示空格。", parameterName);
        }
    }
}
