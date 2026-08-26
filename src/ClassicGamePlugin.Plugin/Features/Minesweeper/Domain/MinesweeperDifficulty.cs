namespace ClassicGamePlugin.Features.Minesweeper.Domain;

/// <summary>
/// 表示扫雷首版支持的固定难度。难度是领域事实，不依赖界面文案或控件。
/// </summary>
internal enum MinesweeperDifficulty
{
    Beginner,
    Intermediate,
    Expert,
}

/// <summary>
/// 保存一种固定难度对应的棋盘尺寸和雷数。
/// </summary>
/// <param name="Difficulty">难度身份。</param>
/// <param name="DisplayName">面向用户的中文名称。</param>
/// <param name="Rows">棋盘行数。</param>
/// <param name="Columns">棋盘列数。</param>
/// <param name="MineCount">雷的总数。</param>
internal sealed record MinesweeperDifficultyDefinition(
    MinesweeperDifficulty Difficulty,
    string DisplayName,
    int Rows,
    int Columns,
    int MineCount)
{
    /// <summary>获取初级难度：9×9，共 10 雷。</summary>
    internal static MinesweeperDifficultyDefinition Beginner { get; } =
        new(MinesweeperDifficulty.Beginner, "初级", 9, 9, 10);

    /// <summary>获取中级难度：16×16，共 40 雷。</summary>
    internal static MinesweeperDifficultyDefinition Intermediate { get; } =
        new(MinesweeperDifficulty.Intermediate, "中级", 16, 16, 40);

    /// <summary>获取高级难度：16×30，共 99 雷。</summary>
    internal static MinesweeperDifficultyDefinition Expert { get; } =
        new(MinesweeperDifficulty.Expert, "高级", 16, 30, 99);

    /// <summary>
    /// 按稳定难度身份取得定义。集中维护映射，避免界面和游戏引擎分别保存一套尺寸常量。
    /// </summary>
    internal static MinesweeperDifficultyDefinition From(MinesweeperDifficulty difficulty) =>
        difficulty switch
        {
            MinesweeperDifficulty.Beginner => Beginner,
            MinesweeperDifficulty.Intermediate => Intermediate,
            MinesweeperDifficulty.Expert => Expert,
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
        };
}
