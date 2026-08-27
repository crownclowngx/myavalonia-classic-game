namespace ClassicGamePlugin.Features.Sokoban.Domain;

/// <summary>内置十二关的唯一目录；顺序即 UI 中的关卡顺序，稳定 ID 不随中文名称调整。</summary>
internal static class SokobanLevelCatalog
{
    internal static IReadOnlyList<SokobanLevelDefinition> Levels { get; } =
    [
        SokobanLevelParser.Parse("beginner-01", "初次推动", SokobanDifficulty.Beginner,
            "#####", "# @ #", "# $ #", "# . #", "#####"),
        SokobanLevelParser.Parse("beginner-02", "绕到上方", SokobanDifficulty.Beginner,
            "######", "#    #", "# @$ #", "#  . #", "######"),
        SokobanLevelParser.Parse("beginner-03", "向上归位", SokobanDifficulty.Beginner,
            "#####", "# . #", "# $ #", "# @ #", "#####"),
        SokobanLevelParser.Parse("beginner-04", "侧向入库", SokobanDifficulty.Beginner,
            "#######", "#     #", "# .$@ #", "#     #", "#######"),
        SokobanLevelParser.Parse("intermediate-01", "并肩箱子", SokobanDifficulty.Intermediate,
            "#######", "#  @  #", "# $$  #", "# ..  #", "#######"),
        SokobanLevelParser.Parse("intermediate-02", "分层运输", SokobanDifficulty.Intermediate,
            "########", "# @    #", "#  $ . #", "#  $ . #", "#      #", "########"),
        SokobanLevelParser.Parse("intermediate-03", "双列下行", SokobanDifficulty.Intermediate,
            "#######", "#  @  #", "# $$  #", "#     #", "# ..  #", "#######"),
        SokobanLevelParser.Parse("intermediate-04", "双列上行", SokobanDifficulty.Intermediate,
            "########", "#      #", "# .  . #", "# $  $ #", "# @    #", "########"),
        SokobanLevelParser.Parse("challenge-01", "三箱下行", SokobanDifficulty.Challenge,
            "########", "#  @   #", "# $$$  #", "#      #", "# ...  #", "########"),
        SokobanLevelParser.Parse("challenge-02", "三箱上行", SokobanDifficulty.Challenge,
            "########", "# ...  #", "# $$$  #", "#  @   #", "#      #", "########"),
        SokobanLevelParser.Parse("challenge-03", "逐层横移", SokobanDifficulty.Challenge,
            "#########", "# @     #", "#  $ .  #", "#  $ .  #", "#  $ .  #", "#       #", "#########"),
        SokobanLevelParser.Parse("challenge-04", "四箱归位", SokobanDifficulty.Challenge,
            "#########", "# @     #", "# $$$$  #", "#       #", "# ....  #", "#########"),
    ];
}
