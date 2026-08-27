using ClassicGamePlugin.Features.Sudoku.Domain;

namespace ClassicGamePlugin.Tests;

internal static class SudokuTestPuzzles
{
    internal const string PuzzleText =
        "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
    internal const string SolutionText =
        "534678912672195348198342567859761423426853791713924856961537284287419635345286179";

    internal static SudokuPuzzle Create(
        SudokuDifficulty difficulty = SudokuDifficulty.Hard,
        SudokuPuzzleSource source = SudokuPuzzleSource.BuiltIn,
        string id = "test-puzzle") =>
        new(id, difficulty, source, Parse(PuzzleText), Parse(SolutionText));

    internal static int[] Parse(string text) => text.Select(character => character - '0').ToArray();
}

/// <summary>按调用参数返回可预测题目，并允许测试精确控制异步生成的成功、失败和取消。</summary>
internal sealed class StubSudokuPuzzleProvider : ISudokuPuzzleProvider
{
    private readonly Func<SudokuDifficulty, CancellationToken, Task<SudokuPuzzle>> _generate;
    private int _builtInSequence;

    internal StubSudokuPuzzleProvider(
        Func<SudokuDifficulty, CancellationToken, Task<SudokuPuzzle>>? generate = null)
    {
        _generate = generate ?? ((difficulty, _) => Task.FromResult(
            SudokuTestPuzzles.Create(difficulty, SudokuPuzzleSource.Generated, "generated-test")));
    }

    internal int BuiltInCallCount { get; private set; }
    internal int GenerateCallCount { get; private set; }

    public SudokuPuzzle GetBuiltInPuzzle(SudokuDifficulty difficulty, string? excludedPuzzleId = null)
    {
        BuiltInCallCount++;
        _builtInSequence++;
        return SudokuTestPuzzles.Create(difficulty, SudokuPuzzleSource.BuiltIn, $"builtin-{difficulty}-{_builtInSequence}");
    }

    public Task<SudokuPuzzle> GeneratePuzzleAsync(
        SudokuDifficulty difficulty,
        CancellationToken cancellationToken)
    {
        GenerateCallCount++;
        return _generate(difficulty, cancellationToken);
    }
}
