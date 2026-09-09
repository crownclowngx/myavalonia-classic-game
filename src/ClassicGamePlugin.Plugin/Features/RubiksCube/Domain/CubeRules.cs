namespace ClassicGamePlugin.Features.RubiksCube.Domain;

/// <summary>
/// 三阶魔方的纯几何规则。一次转面同时置换目标层的全部贴纸，因而相邻面转移与朝向由同一个
/// 整数旋转得到，无需维护六套容易方向不一致的手写颜色交换代码。
/// </summary>
internal static class CubeRules
{
    internal static readonly IReadOnlyList<CubeVector> Normals = Array.AsReadOnly<CubeVector>(
        [new(0, 1, 0), new(1, 0, 0), new(0, 0, 1), new(0, -1, 0), new(-1, 0, 0), new(0, 0, -1)]);
    internal static readonly IReadOnlyList<CubeSlot> Slots = Array.AsReadOnly(CreateSlots());
    internal static readonly IReadOnlyList<CubeVector> Positions = Array.AsReadOnly(
        Slots.Select(slot => slot.Position).Distinct().ToArray());
    internal static readonly IReadOnlyList<CubeMove> AllMoves = Array.AsReadOnly(
        Enum.GetValues<CubeFace>().SelectMany(face => new[] { new CubeMove(face), new(face, -1), new(face, 2) }).ToArray());
    private static readonly IReadOnlyDictionary<CubeMove, int[]> Permutations = AllMoves.ToDictionary(move => move, CreatePermutation);

    internal static CubeState Solved() => new(Enumerable.Range(0, 54).Select(index => (byte)index));

    internal static CubeState Apply(CubeState state, CubeMove move)
    {
        var permutation = Permutations[move];
        var result = new byte[54];
        for (var source = 0; source < 54; source++) result[permutation[source]] = state[source];
        return new CubeState(result);
    }

    internal static CubeState Apply(CubeState state, IEnumerable<CubeMove> moves)
    {
        foreach (var move in moves) state = Apply(state, move);
        return state;
    }

    internal static IReadOnlyList<CubeMove> Parse(string formula) => Array.AsReadOnly(
        formula.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(token =>
        {
            if (token.Length > 2 || !Enum.TryParse<CubeFace>(token[..1], out var face) || !Enum.IsDefined(face) ||
                (token.Length == 2 && token[1] is not ('\'' or '2')))
                throw new FormatException("公式只接受 U R F D L B 及撇号、2 后缀。");
            return new CubeMove(face, token.EndsWith('2') ? 2 : token.EndsWith('\'') ? -1 : 1);
        }).ToArray());

    internal static IReadOnlyList<CubeMove> Expand(IEnumerable<CubeMove> moves) => Array.AsReadOnly(
        moves.SelectMany(move => move.Turns == 2 ? new[] { new CubeMove(move.Face), new(move.Face) } : [move]).ToArray());

    internal static string Format(IEnumerable<CubeMove> moves) => string.Join(' ', moves);

    internal static bool IsPieceSolved(CubeState state, CubeVector home) =>
        Enumerable.Range(0, 54).Where(slot => Slots[slot].Position == home).All(slot => state[slot] == slot);

    internal static bool FirstLayerSolved(CubeState state) =>
        Positions.Where(position => position.Y == -1).All(position => IsPieceSolved(state, position));

    internal static bool FirstTwoLayersSolved(CubeState state) =>
        Positions.Where(position => position.Y <= 0).All(position => IsPieceSolved(state, position));

    internal static bool YellowCrossSolved(CubeState state) =>
        Enumerable.Range(0, 9).Where(slot => IsEdge(Slots[slot].Position)).All(slot => state.ColorAt(slot) == CubeFace.U);

    internal static bool YellowFaceSolved(CubeState state) => Enumerable.Range(0, 9).All(slot => state.ColorAt(slot) == CubeFace.U);

    internal static bool TopCornersSolved(CubeState state) =>
        Positions.Where(position => position.Y == 1 && IsCorner(position)).All(position => IsPieceSolved(state, position));

    internal static bool IsEdge(CubeVector position) => Math.Abs(position.X) + Math.Abs(position.Y) + Math.Abs(position.Z) == 2;
    internal static bool IsCorner(CubeVector position) => Math.Abs(position.X) + Math.Abs(position.Y) + Math.Abs(position.Z) == 3;

    /// <summary>验证组级事务，而非依赖界面显示的阶段名称判断成功。</summary>
    internal static bool VerifyGroup(SolveGroup group, CubeState actual) => actual.Equals(group.After) &&
        group.ProtectedPieces.All(piece => IsPieceSolved(actual, piece)) && (group.Stage switch
        {
            CubeStage.WhiteCross => true,
            CubeStage.WhiteCorners => Positions.Where(position => position.Y == -1 && IsEdge(position)).All(piece => IsPieceSolved(actual, piece)),
            CubeStage.MiddleEdges => FirstLayerSolved(actual),
            CubeStage.YellowCross => FirstTwoLayersSolved(actual),
            CubeStage.YellowFace => FirstTwoLayersSolved(actual) && YellowCrossSolved(actual),
            CubeStage.TopCorners => FirstTwoLayersSolved(actual) && YellowFaceSolved(actual),
            CubeStage.TopEdges => FirstTwoLayersSolved(actual) && YellowFaceSolved(actual) && TopCornersSolved(actual),
            _ => false,
        });

    internal static int[] CreateMap(IEnumerable<CubeMove> moves)
    {
        var map = Enumerable.Range(0, 54).ToArray();
        foreach (var move in moves)
        {
            var step = Permutations[move];
            for (var index = 0; index < 54; index++) map[index] = step[map[index]];
        }
        return map;
    }

    /// <summary>绕外法线的负四分之一转即外侧观察的顺时针；逆时针只改变角度符号。</summary>
    internal static CubeVector Rotate(CubeVector value, CubeVector axis, int turns)
    {
        var count = (turns % 4 + 4) % 4;
        for (var index = 0; index < count; index++)
        {
            var dot = value.Dot(axis);
            value = new CubeVector(
                axis.X * dot - (axis.Y * value.Z - axis.Z * value.Y),
                axis.Y * dot - (axis.Z * value.X - axis.X * value.Z),
                axis.Z * dot - (axis.X * value.Y - axis.Y * value.X));
        }
        return value;
    }

    private static int[] CreatePermutation(CubeMove move)
    {
        var axis = Normals[(int)move.Face];
        return Slots.Select(slot => slot.Position.Dot(axis) == 1
            ? FindSlot(new CubeSlot(Rotate(slot.Position, axis, move.Turns), Rotate(slot.Normal, axis, move.Turns)))
            : FindSlot(slot)).ToArray();
    }

    private static int FindSlot(CubeSlot slot)
    {
        for (var index = 0; index < Slots.Count; index++) if (Slots[index] == slot) return index;
        throw new InvalidOperationException("旋转后贴纸没有对应的整数槽位。");
    }

    private static CubeSlot[] CreateSlots()
    {
        // 每面的行列都按从外侧正视排列。面坐标只在此定义，测试使用独立的邻面流向验算。
        CubeVector[] right = [new(1, 0, 0), new(0, 0, -1), new(1, 0, 0), new(1, 0, 0), new(0, 0, 1), new(-1, 0, 0)];
        CubeVector[] down = [new(0, 0, 1), new(0, -1, 0), new(0, -1, 0), new(0, 0, -1), new(0, -1, 0), new(0, -1, 0)];
        var slots = new List<CubeSlot>();
        for (var face = 0; face < 6; face++)
            for (var row = -1; row <= 1; row++)
                for (var column = -1; column <= 1; column++)
                {
                    var n = Normals[face];
                    var r = right[face];
                    var d = down[face];
                    slots.Add(new(new(n.X + column * r.X + row * d.X, n.Y + column * r.Y + row * d.Y, n.Z + column * r.Z + row * d.Z), n));
                }
        return slots.ToArray();
    }
}
