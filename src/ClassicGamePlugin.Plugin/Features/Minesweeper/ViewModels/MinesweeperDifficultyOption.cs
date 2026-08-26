using ClassicGamePlugin.Features.Minesweeper.Domain;

namespace ClassicGamePlugin.Features.Minesweeper.ViewModels;

/// <summary>
/// 表示难度下拉框中的一个固定选项。公开类型仅用于 Avalonia 编译绑定，领域尺寸仍由内部定义统一维护。
/// </summary>
public sealed class MinesweeperDifficultyOption
{
    internal MinesweeperDifficultyOption(MinesweeperDifficultyDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>获取面向用户的难度名称。</summary>
    public string DisplayName => Definition.DisplayName;

    /// <summary>获取该选项对应的内部领域定义。</summary>
    internal MinesweeperDifficultyDefinition Definition { get; }
}
