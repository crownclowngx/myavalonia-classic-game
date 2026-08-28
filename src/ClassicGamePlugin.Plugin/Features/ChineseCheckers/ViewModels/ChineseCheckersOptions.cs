using ClassicGamePlugin.Features.ChineseCheckers.Domain;

namespace ClassicGamePlugin.Features.ChineseCheckers.ViewModels;

internal enum ChineseCheckersGameMode
{
    LocalTwoPlayer,
    HumanVsComputer,
}

public sealed class ChineseCheckersGameModeOption
{
    internal ChineseCheckersGameModeOption(ChineseCheckersGameMode definition, string displayName) =>
        (Definition, DisplayName) = (definition, displayName);

    internal ChineseCheckersGameMode Definition { get; }
    public string DisplayName { get; }
}

public sealed class ChineseCheckersDifficultyOption
{
    internal ChineseCheckersDifficultyOption(ChineseCheckersAiDifficulty definition, string displayName) =>
        (Definition, DisplayName) = (definition, displayName);

    internal ChineseCheckersAiDifficulty Definition { get; }
    public string DisplayName { get; }
}

public sealed class ChineseCheckersColorOption
{
    internal ChineseCheckersColorOption(ChineseCheckersSide definition, string displayName) =>
        (Definition, DisplayName) = (definition, displayName);

    internal ChineseCheckersSide Definition { get; }
    public string DisplayName { get; }
}

public sealed record ChineseCheckersHistoryItem(string Text);
