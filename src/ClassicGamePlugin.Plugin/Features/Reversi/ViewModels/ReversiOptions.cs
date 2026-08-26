using ClassicGamePlugin.Features.Reversi.Domain;

namespace ClassicGamePlugin.Features.Reversi.ViewModels;

internal enum ReversiGameMode
{
    LocalTwoPlayer,
    HumanVsComputer,
}

/// <summary>向界面提供本地双人与人机对战选项。</summary>
public sealed class ReversiGameModeOption
{
    internal ReversiGameModeOption(ReversiGameMode definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal ReversiGameMode Definition { get; }
    public string DisplayName { get; }
}

/// <summary>向界面提供三级电脑难度选项。</summary>
public sealed class ReversiDifficultyOption
{
    internal ReversiDifficultyOption(ReversiAiDifficulty definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal ReversiAiDifficulty Definition { get; }
    public string DisplayName { get; }
}

/// <summary>向界面提供玩家执黑或执白选项。</summary>
public sealed class ReversiColorOption
{
    internal ReversiColorOption(ReversiDiscColor definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal ReversiDiscColor Definition { get; }
    public string DisplayName { get; }
}

/// <summary>表示操作记录中的一条只读中文说明。</summary>
public sealed record ReversiHistoryItem(string Text);
