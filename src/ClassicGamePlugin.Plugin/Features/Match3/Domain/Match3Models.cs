namespace ClassicGamePlugin.Features.Match3.Domain;

/// <summary>六种普通棋子。枚举顺序同时作为数量相同时的稳定裁决顺序。</summary>
internal enum Match3GemKind
{
    Ruby,
    Amber,
    Emerald,
    Sapphire,
    Amethyst,
    Pearl,
}

/// <summary>棋子的附加能力；彩虹球没有普通颜色。</summary>
internal enum Match3SpecialKind
{
    None,
    RowClear,
    ColumnClear,
    AreaBomb,
    Rainbow,
}

internal enum Match3GameState
{
    Playing,
    Won,
    Lost,
}

internal readonly record struct Match3Position(int Row, int Column);

/// <summary>不可变棋子值。除彩虹球外必须携带普通颜色。</summary>
internal readonly record struct Match3Tile(Match3GemKind? Kind, Match3SpecialKind Special)
{
    internal static Match3Tile Normal(Match3GemKind kind) => new(kind, Match3SpecialKind.None);
}

/// <summary>领域层唯一的随机依赖；测试可用确定序列替换系统随机。</summary>
internal interface IMatch3RandomSource
{
    int Next(int exclusiveMaximum);
}

internal sealed class SystemMatch3RandomSource : IMatch3RandomSource
{
    public int Next(int exclusiveMaximum) => Random.Shared.Next(exclusiveMaximum);
}

internal sealed record Match3MatchRun(bool IsHorizontal, IReadOnlyList<Match3Position> Positions);

internal sealed record Match3CreatedSpecial(Match3Position Position, Match3Tile Tile);

/// <summary>
/// 一波消除的只读视觉资料。BeforeClear 保留消除前棋盘，AfterRefill 是重力和补位完成后的棋盘；
/// View 只回放这些快照，不参与规则计算。
/// </summary>
internal sealed class Match3ResolutionStep
{
    internal Match3ResolutionStep(
        int cascadeLevel,
        IReadOnlyList<Match3Tile?> beforeClear,
        IEnumerable<Match3Position> clearedPositions,
        IEnumerable<Match3CreatedSpecial> createdSpecials,
        IReadOnlyList<Match3Tile?> afterRefill,
        int scoreDelta)
    {
        CascadeLevel = cascadeLevel;
        BeforeClear = Array.AsReadOnly(beforeClear.ToArray());
        ClearedPositions = Array.AsReadOnly(clearedPositions.Distinct().ToArray());
        CreatedSpecials = Array.AsReadOnly(createdSpecials.ToArray());
        AfterRefill = Array.AsReadOnly(afterRefill.ToArray());
        ScoreDelta = scoreDelta;
    }

    internal int CascadeLevel { get; }
    internal IReadOnlyList<Match3Tile?> BeforeClear { get; }
    internal IReadOnlyList<Match3Position> ClearedPositions { get; }
    internal IReadOnlyList<Match3CreatedSpecial> CreatedSpecials { get; }
    internal IReadOnlyList<Match3Tile?> AfterRefill { get; }
    internal int ScoreDelta { get; }
}

/// <summary>一次交换尝试的完整结果；无效交换也返回结果，以便 View 播放短促换回动画。</summary>
internal sealed class Match3TurnTransition
{
    internal Match3TurnTransition(
        Match3Position source,
        Match3Position target,
        bool isAccepted,
        IReadOnlyList<Match3Tile?> before,
        IReadOnlyList<Match3Tile?> after,
        IEnumerable<Match3ResolutionStep>? steps = null,
        int scoreDelta = 0,
        bool wasShuffled = false)
    {
        Source = source;
        Target = target;
        IsAccepted = isAccepted;
        Before = Array.AsReadOnly(before.ToArray());
        After = Array.AsReadOnly(after.ToArray());
        Steps = Array.AsReadOnly((steps ?? []).ToArray());
        ScoreDelta = scoreDelta;
        WasShuffled = wasShuffled;
    }

    internal Match3Position Source { get; }
    internal Match3Position Target { get; }
    internal bool IsAccepted { get; }
    internal IReadOnlyList<Match3Tile?> Before { get; }
    internal IReadOnlyList<Match3Tile?> After { get; }
    internal IReadOnlyList<Match3ResolutionStep> Steps { get; }
    internal int ScoreDelta { get; }
    internal bool WasShuffled { get; }
}
