using ClassicGamePlugin.Features.Xiangqi.Domain;

namespace ClassicGamePlugin.Features.Xiangqi.ViewModels;

internal enum XiangqiGameMode
{
    LocalTwoPlayer,
    HumanVsComputer,
}

public sealed class XiangqiGameModeOption
{
    internal XiangqiGameModeOption(XiangqiGameMode definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal XiangqiGameMode Definition { get; }
    public string DisplayName { get; }
}

public sealed class XiangqiDifficultyOption
{
    internal XiangqiDifficultyOption(XiangqiAiDifficulty definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal XiangqiAiDifficulty Definition { get; }
    public string DisplayName { get; }
}

public sealed class XiangqiSideOption
{
    internal XiangqiSideOption(XiangqiSide definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal XiangqiSide Definition { get; }
    public string DisplayName { get; }
}

/// <summary>局内记录既包含标准中文着法，也包含撤销、提示和终局等操作说明。</summary>
public sealed record XiangqiHistoryItem(string Text, bool IsMove = false);
