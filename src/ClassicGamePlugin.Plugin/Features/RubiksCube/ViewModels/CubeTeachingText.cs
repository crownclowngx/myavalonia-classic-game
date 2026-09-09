using ClassicGamePlugin.Features.RubiksCube.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassicGamePlugin.Features.RubiksCube.ViewModels;

/// <summary>领域结果的中文教学投影，避免公式搜索与坐标规则依赖界面文案。</summary>
internal static class CubeTeachingText
{
    internal static string Stage(CubeStage stage) => stage switch
    {
        CubeStage.WhiteCross => "① 白色十字",
        CubeStage.WhiteCorners => "② 底层角块",
        CubeStage.MiddleEdges => "③ 中层棱块",
        CubeStage.YellowCross => "④ 黄色十字",
        CubeStage.YellowFace => "⑤ 黄色面",
        CubeStage.TopCorners => "⑥ 顶角换位",
        CubeStage.TopEdges => "⑦ 顶棱换位",
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    internal static string Face(CubeFace face) => face switch
    {
        CubeFace.U => "上面（黄心）",
        CubeFace.R => "右面（橙心）",
        CubeFace.F => "前面（绿心）",
        CubeFace.D => "下面（白心）",
        CubeFace.L => "左面（红心）",
        CubeFace.B => "后面（蓝心）",
        _ => throw new ArgumentOutOfRangeException(nameof(face)),
    };

    internal static string Color(CubeFace face) => face switch
    {
        CubeFace.U => "黄",
        CubeFace.R => "橙",
        CubeFace.F => "绿",
        CubeFace.D => "白",
        CubeFace.L => "红",
        CubeFace.B => "蓝",
        _ => throw new ArgumentOutOfRangeException(nameof(face)),
    };

    internal static string Move(CubeMove move) => $"{move} · {Face(move.Face)}{(move.Turns == -1 ? "逆时针转 90°" : move.Turns == 2 ? "连续转两次，共 180°" : "顺时针转 90°")}";

    internal static string Piece(CubeVector target) => string.Join("", Enumerable.Range(0, 54)
        .Where(index => CubeRules.Slots[index].Position == target).Select(index => Color((CubeFace)(index / 9)))) +
        (CubeRules.IsCorner(target) ? "角块" : "棱块");

    internal static string Algorithm(CubeAlgorithm algorithm) => algorithm.Key switch
    {
        "prepare" => "顶层对位",
        "cross" => "十字取出 / 对位 / 插入",
        "corner-insert" => "底角插入公式",
        "middle-right" => "中层右插公式",
        "middle-left" => "中层左插公式",
        "yellow-cross" => "黄色十字公式",
        "yellow-face" => "顶角朝向公式",
        "corner-cycle" => "顶角循环换位公式",
        "edge-cycle" => "顶棱循环换位公式",
        _ => algorithm.Key,
    };

    internal static string Situation(SolveGroup group)
    {
        if (group.Target is { } target)
        {
            var identity = Enumerable.Range(0, 54).First(index => CubeRules.Slots[index].Position == target);
            var current = CubeRules.Slots[group.Before.FindSticker(identity)].Position;
            var location = current.Y == -1 ? "底层" : current.Y == 0 ? "中层" : "顶层";
            return $"{Piece(target)}当前位于{location}，位置或朝向尚未正确。组末保留此前已经完成的目标块。";
        }
        return group.Stage switch
        {
            CubeStage.YellowCross => $"顶面朝上的黄色棱块：{YellowCount(group.Before, false)} / 4。先根据形态对位，再执行完整公式。",
            CubeStage.YellowFace => $"顶面朝上的黄色角块：{YellowCount(group.Before, true)} / 4。保持前两层和黄色十字。",
            CubeStage.TopCorners => $"已归位顶角：{SolvedTop(group.Before, true)} / 4。黄色面的朝向已经正确。",
            CubeStage.TopEdges => $"已归位顶棱：{SolvedTop(group.Before, false)} / 4。保持前两层、黄色面和顶角位置。",
            _ => "按当前情况执行完整公式。",
        };
    }

    internal static string Purpose(SolveGroup group) => group.Target is { } target
        ? $"归位{Piece(target)}，并恢复已完成目标。"
        : group.After.IsSolved ? "完成本组还原，使六面全部恢复同色。"
        : CubeTeachingSolver.StageComplete(group.After, group.Stage)
            ? $"完成{Stage(group.Stage)[2..]}，进入下一阶段。"
            : $"调整当前{Stage(group.Stage)[2..]}情况，为下一组创造条件。";

    internal static string Result(SolveGroup group) => group.Target is { } target
        ? $"{Piece(target)}已归位；累计目标 {group.ProtectedPieces.Count} / 4，先前阶段保持完成。"
        : group.Stage switch
        {
            CubeStage.YellowCross => $"黄色棱块朝上 {YellowCount(group.After, false)} / 4；前两层保持还原。",
            CubeStage.YellowFace => $"黄色角块朝上 {YellowCount(group.After, true)} / 4；前两层和黄色十字保持完成。",
            CubeStage.TopCorners => $"顶角归位 {SolvedTop(group.After, true)} / 4；黄色面保持完成。",
            CubeStage.TopEdges => group.After.IsSolved ? "六面全部还原，教学完成。" : $"顶棱归位 {SolvedTop(group.After, false)} / 4；顶角与前两层保持完成。",
            _ => "目标已验证。",
        };

    private static int YellowCount(CubeState state, bool corners) => Enumerable.Range(0, 9).Count(index =>
        (corners ? CubeRules.IsCorner(CubeRules.Slots[index].Position) : CubeRules.IsEdge(CubeRules.Slots[index].Position)) && state.ColorAt(index) == CubeFace.U);
    private static int SolvedTop(CubeState state, bool corners) => CubeRules.Positions.Count(position => position.Y == 1 &&
        (corners ? CubeRules.IsCorner(position) : CubeRules.IsEdge(position)) && CubeRules.IsPieceSolved(state, position));
}

/// <summary>只向 AXAML 暴露教学文字和播放状态，内部保留领域规则组用于精确验证。</summary>
public sealed partial class CubeStepItem : ObservableObject
{
    internal CubeStepItem(int number, SolveGroup group)
    {
        Number = number;
        Group = group;
        Stage = CubeTeachingText.Stage(group.Stage);
        Purpose = CubeTeachingText.Purpose(group);
        Situation = CubeTeachingText.Situation(group);
        ExpectedResult = CubeTeachingText.Result(group);
        Formula = CubeRules.Format(group.Algorithms.SelectMany(algorithm => algorithm.Moves));
        Recipe = string.Join(Environment.NewLine, group.Algorithms.Select((algorithm, index) =>
            $"{index + 1}. {CubeTeachingText.Algorithm(algorithm)}：{CubeRules.Format(algorithm.Moves)}"));
        FormulaSummary = string.Join("；", group.Algorithms.Where(algorithm => !algorithm.IsPreparation)
            .GroupBy(algorithm => $"{CubeTeachingText.Algorithm(algorithm)} [{CubeRules.Format(algorithm.Moves)}]")
            .Select(set => $"{set.Key} × {set.Count()}"));
        Preparation = string.Join(" → ", group.Algorithms.Where(algorithm => algorithm.IsPreparation).Select(algorithm => CubeRules.Format(algorithm.Moves)));
        if (Preparation.Length == 0) Preparation = "准备动作已包含在下面的完整操作流程中。";
    }

    internal SolveGroup Group { get; }
    public int Number { get; }
    public string Title => $"第 {Number} 组 · {Stage}";
    public string Stage { get; }
    public string Purpose { get; }
    public string Situation { get; }
    public string ExpectedResult { get; }
    public string Formula { get; }
    public string Recipe { get; }
    public string FormulaSummary { get; }
    public string Preparation { get; }
    [ObservableProperty] private string _status = "待执行";
    [ObservableProperty] private string _actualResult = "";
}

/// <summary>当前规则组的原子动作展示，180°对应两个连续的 90°标记。</summary>
public sealed partial class CubeActionItem : ObservableObject
{
    internal CubeActionItem(int number, CubeMove move)
    {
        Number = number;
        Symbol = move.ToString();
        Description = CubeTeachingText.Move(move);
    }

    public int Number { get; }
    public string Symbol { get; }
    public string Description { get; }
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isComplete;
}
