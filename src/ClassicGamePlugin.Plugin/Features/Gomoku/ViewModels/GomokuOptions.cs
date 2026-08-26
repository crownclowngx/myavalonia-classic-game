using ClassicGamePlugin.Features.Gomoku.Domain;

namespace ClassicGamePlugin.Features.Gomoku.ViewModels;

internal enum GomokuGameMode
{
    LocalTwoPlayer,
    HumanVsComputer,
}

internal enum GomokuAiDifficulty
{
    Easy,
    Medium,
    Hard,
}

public sealed class GomokuRuleOption
{
    internal GomokuRuleOption(GomokuRuleSet definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal GomokuRuleSet Definition { get; }
    public string DisplayName { get; }
}

public sealed class GomokuGameModeOption
{
    internal GomokuGameModeOption(GomokuGameMode definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal GomokuGameMode Definition { get; }
    public string DisplayName { get; }
}

public sealed class GomokuDifficultyOption
{
    internal GomokuDifficultyOption(GomokuAiDifficulty definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal GomokuAiDifficulty Definition { get; }
    public string DisplayName { get; }
}

public sealed class GomokuColorOption
{
    internal GomokuColorOption(GomokuStone definition, string displayName)
    {
        Definition = definition;
        DisplayName = displayName;
    }

    internal GomokuStone Definition { get; }
    public string DisplayName { get; }
}

public sealed record GomokuHistoryItem(string Text);
