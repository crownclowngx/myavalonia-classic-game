namespace ClassicGamePlugin.Features.Sudoku.Domain;

/// <summary>为 ViewModel 提供即时题库新局和可取消的运行时生成，不暴露随机与求解细节。</summary>
internal interface ISudokuPuzzleProvider
{
    SudokuPuzzle GetBuiltInPuzzle(SudokuDifficulty difficulty, string? excludedPuzzleId = null);
    Task<SudokuPuzzle> GeneratePuzzleAsync(SudokuDifficulty difficulty, CancellationToken cancellationToken);
}

/// <summary>
/// 组合内置题库、随机终盘和唯一解挖空算法。题库路径始终同步且快速；生成路径放到后台线程，
/// 只有完整题目通过范围和唯一性检查后才返回，从而让 ViewModel 可以原子替换当前局。
/// </summary>
internal sealed class SudokuPuzzleProvider : ISudokuPuzzleProvider
{
    private const int MaximumGenerationAttempts = 10;
    private const string BasePuzzle =
        "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
    private const string BaseSolution =
        "534678912672195348198342567859761423426853791713924856961537284287419635345286179";

    private readonly SudokuSolver _solver;
    private readonly Random _random;
    private readonly IReadOnlyList<SudokuPuzzle> _builtInPuzzles;

    internal SudokuPuzzleProvider()
        : this(new SudokuSolver(), Random.Shared)
    {
    }

    internal SudokuPuzzleProvider(SudokuSolver solver, Random random)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _builtInPuzzles = BuildPuzzleBank();
    }

    internal IReadOnlyList<SudokuPuzzle> BuiltInPuzzles => _builtInPuzzles;

    public SudokuPuzzle GetBuiltInPuzzle(SudokuDifficulty difficulty, string? excludedPuzzleId = null)
    {
        var candidates = _builtInPuzzles
            .Where(puzzle => puzzle.Difficulty == difficulty && puzzle.Id != excludedPuzzleId)
            .ToArray();
        if (candidates.Length == 0)
        {
            candidates = _builtInPuzzles.Where(puzzle => puzzle.Difficulty == difficulty).ToArray();
        }

        return candidates[_random.Next(candidates.Length)];
    }

    public Task<SudokuPuzzle> GeneratePuzzleAsync(
        SudokuDifficulty difficulty,
        CancellationToken cancellationToken) =>
        Task.Run(() => GeneratePuzzle(difficulty, cancellationToken), cancellationToken);

    private SudokuPuzzle GeneratePuzzle(SudokuDifficulty difficulty, CancellationToken cancellationToken)
    {
        var profile = SudokuDifficultyProfile.For(difficulty);
        for (var attempt = 0; attempt < MaximumGenerationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var solution = new int[SudokuRules.CellCount];
            if (!FillRandomSolution(solution, cancellationToken))
            {
                continue;
            }

            var puzzle = solution.ToArray();
            var targetClues = _random.Next(profile.MinimumClues, profile.MaximumClues + 1);
            var positions = Enumerable.Range(0, SudokuRules.CellCount).ToArray();
            Shuffle(positions);
            var clueCount = SudokuRules.CellCount;
            foreach (var index in positions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (clueCount <= targetClues)
                {
                    break;
                }

                var previous = puzzle[index];
                puzzle[index] = 0;
                if (_solver.CountSolutions(puzzle, 2, cancellationToken) == 1)
                {
                    clueCount--;
                }
                else
                {
                    puzzle[index] = previous;
                }
            }

            if (clueCount >= profile.MinimumClues && clueCount <= profile.MaximumClues)
            {
                return new SudokuPuzzle(
                    $"generated-{difficulty.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}",
                    difficulty,
                    SudokuPuzzleSource.Generated,
                    puzzle,
                    solution);
            }
        }

        throw new InvalidOperationException("在限定尝试次数内未能生成符合当前难度且具有唯一解的数独题目。");
    }

    private bool FillRandomSolution(int[] board, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bestIndex = -1;
        var bestMask = 0;
        var bestCount = int.MaxValue;
        for (var index = 0; index < SudokuRules.CellCount; index++)
        {
            if (board[index] != 0)
            {
                continue;
            }

            var mask = SudokuRules.GetCandidateMask(board, SudokuRules.FromIndex(index));
            var count = SudokuRules.CountCandidates(mask);
            if (count < bestCount)
            {
                bestIndex = index;
                bestMask = mask;
                bestCount = count;
            }
        }

        if (bestIndex < 0)
        {
            return true;
        }

        var values = Enumerable.Range(1, SudokuRules.BoardSize)
            .Where(value => (bestMask & (1 << value)) != 0)
            .ToArray();
        Shuffle(values);
        foreach (var value in values)
        {
            board[bestIndex] = value;
            if (FillRandomSolution(board, cancellationToken))
            {
                return true;
            }
        }

        board[bestIndex] = 0;
        return false;
    }

    private void Shuffle<T>(T[] values)
    {
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static IReadOnlyList<SudokuPuzzle> BuildPuzzleBank()
    {
        var solution = ParseBoard(BaseSolution);
        var puzzles = new List<SudokuPuzzle>(24);
        foreach (var difficulty in Enum.GetValues<SudokuDifficulty>())
        {
            var profile = SudokuDifficultyProfile.For(difficulty);
            var baseBoard = ParseBoard(BasePuzzle);
            AddSolutionClues(baseBoard, solution, profile.MinimumClues);
            for (var variant = 0; variant < 8; variant++)
            {
                var transformedPuzzle = ShiftDigits(baseBoard, variant);
                var transformedSolution = ShiftDigits(solution, variant);
                puzzles.Add(new SudokuPuzzle(
                    $"builtin-{difficulty.ToString().ToLowerInvariant()}-{variant + 1}",
                    difficulty,
                    SudokuPuzzleSource.BuiltIn,
                    transformedPuzzle,
                    transformedSolution));
            }
        }

        return puzzles.AsReadOnly();
    }

    private static int[] ParseBoard(string text)
    {
        if (text.Length != SudokuRules.CellCount || text.Any(character => character < '0' || character > '9'))
        {
            throw new InvalidOperationException("内置数独题目必须是恰好 81 位的数字字符串。");
        }

        return text.Select(character => character - '0').ToArray();
    }

    private static void AddSolutionClues(int[] puzzle, IReadOnlyList<int> solution, int targetClues)
    {
        var clueCount = puzzle.Count(value => value != 0);
        for (var index = 0; index < puzzle.Length && clueCount < targetClues; index++)
        {
            if (puzzle[index] == 0)
            {
                puzzle[index] = solution[index];
                clueCount++;
            }
        }
    }

    private static int[] ShiftDigits(IReadOnlyList<int> board, int shift) =>
        board.Select(value => value == 0 ? 0 : ((value - 1 + shift) % SudokuRules.BoardSize) + 1).ToArray();
}
