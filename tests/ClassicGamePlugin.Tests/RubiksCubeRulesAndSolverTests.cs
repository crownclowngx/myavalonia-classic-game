using ClassicGamePlugin.Features.RubiksCube.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class RubiksCubeRulesAndSolverTests
{
    [Fact]
    public void 六面旋转的独立邻面流向与群恒等式正确()
    {
        // 独立列出正向旋转时的一颗棱块去向，避免只用实现自己的逆操作验证自己。
        (CubeFace Face, CubeVector From, CubeVector To)[] cases =
        [
            (CubeFace.U, new(0, 1, 1), new(-1, 1, 0)),
            (CubeFace.R, new(1, 1, 0), new(1, 0, -1)),
            (CubeFace.F, new(0, 1, 1), new(1, 0, 1)),
            (CubeFace.D, new(0, -1, 1), new(1, -1, 0)),
            (CubeFace.L, new(-1, 1, 0), new(-1, 0, 1)),
            (CubeFace.B, new(0, 1, -1), new(-1, 0, -1)),
        ];
        var solved = CubeRules.Solved();
        foreach (var (face, from, to) in cases)
        {
            var move = new CubeMove(face);
            var state = CubeRules.Apply(solved, move);
            var identity = Enumerable.Range(0, 54).First(slot => CubeRules.Slots[slot].Position == from);
            Assert.Equal(to, CubeRules.Slots[state.FindSticker(identity)].Position);
            Assert.Equal(solved, CubeRules.Apply(state, move.Inverse));
            Assert.Equal(solved, CubeRules.Apply(solved, Enumerable.Repeat(move, 4)));
            Assert.Equal(CubeRules.Apply(solved, new CubeMove(face, 2)), CubeRules.Apply(state, move));
            Assert.Equal(54, Enumerable.Range(0, 54).Select(slot => state[slot]).Distinct().Count());
            foreach (var color in Enum.GetValues<CubeFace>())
                Assert.Equal(9, Enumerable.Range(0, 54).Count(slot => state.ColorAt(slot) == color));
        }
        Assert.True(solved.IsSolved);
        Assert.Equal(26, CubeRules.Positions.Count);
    }

    [Fact]
    public void 常规公式在完整执行后保持前置阶段()
    {
        foreach (var stage in Enum.GetValues<CubeStage>().Skip(1))
            foreach (var algorithm in CubeTeachingSolver.Algorithms(stage))
            {
                var before = CubeRules.Solved();
                var after = CubeRules.Apply(before, algorithm.Moves);
                var group = new SolveGroup(stage, null, [algorithm], algorithm.Moves, before, after, []);
                Assert.True(CubeRules.VerifyGroup(group, after), $"{stage} {CubeRules.Format(algorithm.Moves)}");
            }
    }

    [Fact]
    public void 一千种打乱及附加手动操作均能逐组验证并最终还原()
    {
        var solver = new CubeTeachingSolver();
        for (var seed = 0; seed < 1000; seed++)
        {
            var moves = CubeGame.Scramble(new Random(seed));
            var state = CubeRules.Apply(CubeRules.Solved(), moves);
            if (seed % 5 == 0) state = CubeRules.Apply(state, CubeRules.Parse("F R' D2 L U B'"));
            var input = state;
            var plan = solver.Solve(state, CancellationToken.None);
            Assert.True(plan.Success, $"种子 {seed}：{plan.Error}");
            Assert.Equal(input, plan.Initial);
            foreach (var group in plan.Groups)
            {
                Assert.Equal(state, group.Before);
                Assert.NotEmpty(group.Moves);
                Assert.All(group.Moves, move => Assert.NotEqual(2, move.Turns));
                Assert.Equal(CubeRules.Expand(group.Algorithms.SelectMany(algorithm => algorithm.Moves)), group.Moves);
                state = CubeRules.Apply(state, group.Moves);
                Assert.True(CubeRules.VerifyGroup(group, state), $"种子 {seed}，阶段 {group.Stage}");
                Assert.Equal(group.Before, CubeRules.Apply(state, group.Moves.Reverse().Select(move => move.Inverse)));
            }
            Assert.True(state.IsSolved, $"种子 {seed} 未还原");
        }
    }

    [Fact]
    public void 已还原与预取消明确处理且快照不能被外部数组修改()
    {
        var values = Enumerable.Range(0, 54).Select(index => (byte)index).ToArray();
        var state = new CubeState(values);
        (values[0], values[1]) = (values[1], values[0]);
        Assert.True(state.IsSolved);
        var solver = new CubeTeachingSolver();
        Assert.Empty(solver.Solve(state, CancellationToken.None).Groups);
        Assert.Throws<OperationCanceledException>(() => solver.Solve(state, new CancellationToken(true)));
        Assert.Throws<FormatException>(() => CubeRules.Parse("R3"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CubeMove(CubeFace.U, 3));
        Assert.Throws<ArgumentException>(() => new CubeState(new byte[54]));
    }

    [Fact]
    public void 单颗翻转的非法棱块不能得到虚假的成功方案()
    {
        var values = Enumerable.Range(0, 54).Select(index => (byte)index).ToArray();
        var edge = Enumerable.Range(0, 54).Where(index => CubeRules.Slots[index].Position == new CubeVector(0, 1, 1)).ToArray();
        (values[edge[0]], values[edge[1]]) = (values[edge[1]], values[edge[0]]);
        var plan = new CubeTeachingSolver().Solve(new CubeState(values), CancellationToken.None);
        Assert.False(plan.Success);
        Assert.NotEmpty(plan.Error!);
        Assert.Empty(plan.Groups);
    }
}
