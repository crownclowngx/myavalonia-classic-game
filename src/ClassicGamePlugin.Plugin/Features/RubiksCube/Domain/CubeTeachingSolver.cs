using System.Collections.Concurrent;

namespace ClassicGamePlugin.Features.RubiksCube.Domain;

/// <summary>
/// 层先法教学求解器。前三阶段把“已经完成的小块和本次目标”的位置朝向作为有限情况表，
/// 只允许当前阶段的准备动作和常规公式；顶层在小规模形态空间选择公式。
/// 搜索的是教学规则组合，不是全魔方最短解；任何阶段都不读取打乱或游戏历史。
/// </summary>
internal sealed class CubeTeachingSolver : ICubeTeachingSolver
{
    private static readonly ConcurrentDictionary<(CubeStage Stage, int Count), PatternTable> Tables = new();
    internal static readonly IReadOnlyList<CubeVector> CrossTargets = Array.AsReadOnly<CubeVector>(
        [new(0, -1, 1), new(1, -1, 0), new(0, -1, -1), new(-1, -1, 0)]);
    internal static readonly IReadOnlyList<CubeVector> CornerTargets = Array.AsReadOnly<CubeVector>(
        [new(1, -1, 1), new(1, -1, -1), new(-1, -1, -1), new(-1, -1, 1)]);
    internal static readonly IReadOnlyList<CubeVector> MiddleTargets = Array.AsReadOnly<CubeVector>(
        [new(1, 0, 1), new(1, 0, -1), new(-1, 0, -1), new(-1, 0, 1)]);

    /// <summary>
    /// 所有输出公式均保留原始动作。y 变体只把公式字母映射到固定的世界面，既不改变相机，
    /// 也不偷偷加入整颗魔方旋转。顶层公式来源与持方说明记录在专用文档中。
    /// </summary>
    internal static IReadOnlyList<CubeAlgorithm> Algorithms(CubeStage stage)
    {
        if (stage == CubeStage.WhiteCross)
            return CubeRules.AllMoves.Select(move => new CubeAlgorithm("cross", Array.AsReadOnly(new[] { move }))).ToArray();
        var result = new List<CubeAlgorithm>();
        if (stage != CubeStage.TopEdges)
            foreach (var turn in new[] { 1, -1, 2 })
                result.Add(new CubeAlgorithm("prepare", Array.AsReadOnly(new[] { new CubeMove(CubeFace.U, turn) }), true));
        var formulas = stage switch
        {
            CubeStage.WhiteCorners => new[] { ("corner-insert", "R U R' U'") },
            CubeStage.MiddleEdges => new[] { ("middle-right", "U R U' R' U' F' U F"), ("middle-left", "U' L' U L U F U' F'") },
            CubeStage.YellowCross => new[] { ("yellow-cross", "F R U R' U' F'") },
            CubeStage.YellowFace => new[] { ("yellow-face", "R U R' U R U2 R'") },
            CubeStage.TopCorners => new[] { ("corner-cycle", "F' L F' R2 F L' F' R2 F2") },
            CubeStage.TopEdges => new[] { ("edge-cycle", "F2 U L R' F2 L' R U F2") },
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };
        foreach (var (key, formula) in formulas)
            for (var yaw = 0; yaw < 4; yaw++)
            {
                var variant = yaw;
                var moves = CubeRules.Parse(formula).Select(move =>
                {
                    var normal = CubeRules.Rotate(CubeRules.Normals[(int)move.Face], CubeRules.Normals[0], variant);
                    var face = Enumerable.Range(0, 6).Single(index => CubeRules.Normals[index] == normal);
                    return new CubeMove((CubeFace)face, move.Turns);
                }).ToArray();
                result.Add(new CubeAlgorithm(key, Array.AsReadOnly(moves)));
            }
        return result.AsReadOnly();
    }

    public SolvePlan Solve(CubeState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        var initial = state;
        var groups = new List<SolveGroup>();
        try
        {
            SolvePieces(CubeStage.WhiteCross, CrossTargets, ref state, groups, cancellationToken);
            SolvePieces(CubeStage.WhiteCorners, CornerTargets, ref state, groups, cancellationToken);
            SolvePieces(CubeStage.MiddleEdges, MiddleTargets, ref state, groups, cancellationToken);
            foreach (var stage in new[] { CubeStage.YellowCross, CubeStage.YellowFace, CubeStage.TopCorners, CubeStage.TopEdges })
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = FindTopPath(state, stage, cancellationToken);
                var pending = new List<CubeAlgorithm>();
                foreach (var algorithm in path)
                {
                    pending.Add(algorithm);
                    if (algorithm.IsPreparation) continue;
                    AppendGroup(stage, null, pending, [], ref state, groups);
                    pending.Clear();
                }
                if (pending.Count > 0) AppendGroup(stage, null, pending, [], ref state, groups);
            }
            if (!state.IsSolved) throw new InvalidOperationException("教学步骤结束后魔方尚未完全还原。");
            return new SolvePlan(initial, groups.AsReadOnly());
        }
        catch (InvalidOperationException exception)
        {
            // 失败方案不保留半套步骤，避免用户把不完整的推导误当作可执行教学。
            return new SolvePlan(initial, Array.Empty<SolveGroup>(), exception.Message);
        }
    }

    private static void SolvePieces(CubeStage stage, IReadOnlyList<CubeVector> targets, ref CubeState state,
        List<SolveGroup> groups, CancellationToken token)
    {
        for (var count = 1; count <= targets.Count; count++)
        {
            token.ThrowIfCancellationRequested();
            var target = targets[count - 1];
            if (CubeRules.IsPieceSolved(state, target)) continue;
            var protectedPieces = targets.Take(count).ToArray();
            var references = protectedPieces.Select(piece => Enumerable.Range(0, 54)
                .Where(index => CubeRules.Slots[index].Position == piece)
                .OrderBy(index => CubeRules.Slots[index].Normal.Y == -1 ? 0 : 1).First()).ToArray();
            var algorithms = Algorithms(stage);
            // 仅完整建成的只读情况表进入共享缓存。取消中的半张表不会影响另一 Document。
            var table = Tables.GetOrAdd((stage, count), _ => PatternTable.Build(references, algorithms, token));
            var path = table.FindPath(state, references, algorithms, token);
            AppendGroup(stage, target, path, protectedPieces, ref state, groups);
        }
    }

    private static void AppendGroup(CubeStage stage, CubeVector? target, IEnumerable<CubeAlgorithm> algorithms,
        IEnumerable<CubeVector> protectedPieces, ref CubeState state, List<SolveGroup> groups)
    {
        var formulas = Array.AsReadOnly(algorithms.ToArray());
        var moves = CubeRules.Expand(formulas.SelectMany(algorithm => algorithm.Moves));
        if (moves.Count == 0) return;
        var after = CubeRules.Apply(state, moves);
        var group = new SolveGroup(stage, target, formulas, moves, state, after, Array.AsReadOnly(protectedPieces.ToArray()));
        if (!CubeRules.VerifyGroup(group, after))
            throw new InvalidOperationException("规则组未通过阶段保护或目标块验证，已停止生成。");
        groups.Add(group);
        state = after;
    }

    private static IReadOnlyList<CubeAlgorithm> FindTopPath(CubeState initial, CubeStage stage, CancellationToken token)
    {
        if (StageComplete(initial, stage)) return Array.Empty<CubeAlgorithm>();
        var algorithms = Algorithms(stage);
        var queue = new Queue<(CubeState State, int Parent, int Algorithm)>();
        var nodes = new List<(CubeState State, int Parent, int Algorithm)>();
        var seen = new HashSet<ulong> { TopKey(initial, stage) };
        queue.Enqueue((initial, -1, -1));
        while (queue.TryDequeue(out var node))
        {
            token.ThrowIfCancellationRequested();
            var index = nodes.Count;
            nodes.Add(node);
            if (StageComplete(node.State, stage))
            {
                var path = new List<CubeAlgorithm>();
                while (node.Parent >= 0)
                {
                    path.Add(algorithms[node.Algorithm]);
                    node = nodes[node.Parent];
                }
                path.Reverse();
                return path.AsReadOnly();
            }
            for (var algorithmIndex = 0; algorithmIndex < algorithms.Count; algorithmIndex++)
            {
                var next = CubeRules.Apply(node.State, algorithms[algorithmIndex].Moves);
                if (seen.Add(TopKey(next, stage))) queue.Enqueue((next, index, algorithmIndex));
            }
            if (seen.Count > 1024) throw new InvalidOperationException("顶层公式搜索超出预期情况范围。");
        }
        throw new InvalidOperationException("当前状态无法匹配本阶段教学公式。");
    }

    internal static bool StageComplete(CubeState state, CubeStage stage) => stage switch
    {
        CubeStage.WhiteCross => CrossTargets.All(target => CubeRules.IsPieceSolved(state, target)),
        CubeStage.WhiteCorners => CubeRules.FirstLayerSolved(state),
        CubeStage.MiddleEdges => CubeRules.FirstTwoLayersSolved(state),
        CubeStage.YellowCross => CubeRules.YellowCrossSolved(state),
        CubeStage.YellowFace => CubeRules.YellowFaceSolved(state),
        CubeStage.TopCorners => CubeRules.TopCornersSolved(state),
        CubeStage.TopEdges => state.IsSolved,
        _ => false,
    };

    /// <summary>
    /// 朝向阶段只编码黄色贴纸的位置；换位阶段编码顶层目标块身份。忽略无关块不会改变
    /// 同一公式对目标的作用，因而可把搜索限制在有限的朝向或置换情况中。
    /// </summary>
    private static ulong TopKey(CubeState state, CubeStage stage)
    {
        ulong key = 0;
        var corners = stage is CubeStage.YellowFace or CubeStage.TopCorners;
        var orientation = stage is CubeStage.YellowCross or CubeStage.YellowFace;
        for (var slot = 0; slot < 54; slot++)
        {
            var position = CubeRules.Slots[slot].Position;
            if (position.Y != 1 || (corners ? !CubeRules.IsCorner(position) : !CubeRules.IsEdge(position))) continue;
            if (orientation)
            {
                if (state.ColorAt(slot) == CubeFace.U) key |= 1UL << slot;
            }
            else if (slot < 9) key = (key << 6) | state[slot];
        }
        return key;
    }

    /// <summary>
    /// 一颗棱块或角块的一张有身份贴纸的位置已包含其完整朝向；最多四颗目标使用四段六位整数。
    /// 从目标反向枚举情况，记录应当执行的正向公式，因此输出始终使用目录中的教学公式。
    /// 这张表只保存整数和公式下标，不缓存 Document、棋局或任务。
    /// </summary>
    private sealed class PatternTable(int goal, Dictionary<int, byte> next, int[][] forwardMaps)
    {
        internal static PatternTable Build(int[] references, IReadOnlyList<CubeAlgorithm> algorithms, CancellationToken token)
        {
            var goal = Pack(references);
            var forward = algorithms.Select(algorithm => CubeRules.CreateMap(algorithm.Moves)).ToArray();
            var reverse = algorithms.Select(algorithm => CubeRules.CreateMap(algorithm.Moves.Reverse().Select(move => move.Inverse))).ToArray();
            var next = new Dictionary<int, byte> { [goal] = byte.MaxValue };
            var queue = new Queue<int>();
            queue.Enqueue(goal);
            var visited = 0;
            while (queue.TryDequeue(out var current))
            {
                if ((visited++ & 255) == 0) token.ThrowIfCancellationRequested();
                for (byte index = 0; index < reverse.Length; index++)
                {
                    var candidate = Transform(current, reverse[index], references.Length);
                    if (next.TryAdd(candidate, index)) queue.Enqueue(candidate);
                }
            }
            token.ThrowIfCancellationRequested();
            return new PatternTable(goal, next, forward);
        }

        internal IReadOnlyList<CubeAlgorithm> FindPath(CubeState state, int[] references,
            IReadOnlyList<CubeAlgorithm> algorithms, CancellationToken token)
        {
            var key = Pack(references.Select(state.FindSticker));
            var path = new List<CubeAlgorithm>();
            while (key != goal)
            {
                token.ThrowIfCancellationRequested();
                if (!next.TryGetValue(key, out var index) || path.Count > 100)
                    throw new InvalidOperationException("目标块不在本阶段的合法公式情况表中。");
                path.Add(algorithms[index]);
                key = Transform(key, forwardMaps[index], references.Length);
            }
            return path.AsReadOnly();
        }

        private static int Pack(IEnumerable<int> positions)
        {
            var key = 0;
            var shift = 0;
            foreach (var position in positions) { key |= position << shift; shift += 6; }
            return key;
        }

        private static int Transform(int key, int[] map, int count)
        {
            var transformed = 0;
            for (var index = 0; index < count; index++) transformed |= map[(key >> (index * 6)) & 63] << (index * 6);
            return transformed;
        }
    }
}
