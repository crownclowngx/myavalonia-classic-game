using ClassicGamePlugin.Features.SpiderSolitaire.Domain;

namespace ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;

/// <summary>面向难度下拉框的只读选项；领域枚举保持在功能域内部，不泄漏给 Host。</summary>
public sealed class SpiderSolitaireDifficultyOption
{
    internal SpiderSolitaireDifficultyOption(
        SpiderSolitaireDifficulty definition,
        string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal SpiderSolitaireDifficulty Definition { get; }
    public string DisplayName { get; }
}
