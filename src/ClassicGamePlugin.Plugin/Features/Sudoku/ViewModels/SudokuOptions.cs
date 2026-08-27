using ClassicGamePlugin.Features.Sudoku.Domain;

namespace ClassicGamePlugin.Features.Sudoku.ViewModels;

/// <summary>把内部难度定义投影为可绑定的中文下拉选项。</summary>
public sealed class SudokuDifficultyOption
{
    internal SudokuDifficultyOption(SudokuDifficulty difficulty)
    {
        Difficulty = difficulty;
        DisplayName = SudokuDifficultyProfile.For(difficulty).DisplayName;
    }

    internal SudokuDifficulty Difficulty { get; }
    public string DisplayName { get; }
}
