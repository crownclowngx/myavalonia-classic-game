using ClassicGamePlugin.Features.Sokoban.Domain;

namespace ClassicGamePlugin.Features.Sokoban.ViewModels;

/// <summary>供关卡选择器绑定的稳定只读投影，不向 UI 暴露可修改的领域地图。</summary>
public sealed class SokobanLevelOption
{
    internal SokobanLevelOption(int index, SokobanLevelDefinition definition)
    {
        Index = index;
        Id = definition.Id;
        Name = definition.Name;
        DifficultyText = SokobanViewModel.GetDifficultyText(definition.Difficulty);
        DisplayText = $"{index + 1:D2} · {Name}（{DifficultyText}）";
    }

    public int Index { get; }
    public string Id { get; }
    public string Name { get; }
    public string DifficultyText { get; }
    public string DisplayText { get; }
}
