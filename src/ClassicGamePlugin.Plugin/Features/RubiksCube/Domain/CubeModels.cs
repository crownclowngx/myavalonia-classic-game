namespace ClassicGamePlugin.Features.RubiksCube.Domain;

/// <summary>固定持方坐标：上黄、右橙、前绿、下白、左红、后蓝；观察相机不改变这些身份。</summary>
internal enum CubeFace { U, R, F, D, L, B }

/// <summary>整数格点同时用于小块位置和贴纸法线，避免连续动画的浮点误差进入领域状态。</summary>
internal readonly record struct CubeVector(int X, int Y, int Z)
{
    internal int Dot(CubeVector other) => X * other.X + Y * other.Y + Z * other.Z;
}

/// <summary>
/// 标准转面指令。Turns 只能为 1、-1、2；正向始终是从该面的外侧正视时的顺时针。
/// 180°在播放器中展开为两个正向四分之一转，逆时针绝不替换成三个正向动作。
/// </summary>
internal readonly record struct CubeMove
{
    internal CubeMove(CubeFace face, int turns = 1)
    {
        if (!Enum.IsDefined(face) || turns is not (1 or -1 or 2))
            throw new ArgumentOutOfRangeException(nameof(turns), "转面必须使用六面之一及 1、-1、2 转数。");
        Face = face;
        Turns = turns;
    }

    internal CubeFace Face { get; }
    internal int Turns { get; }
    internal CubeMove Inverse => new(Face, Turns == 2 ? 2 : -Turns);
    public override string ToString() => Face + (Turns == -1 ? "'" : Turns == 2 ? "2" : "");
}

/// <summary>世界坐标下的一张贴纸槽位；位置和法线合起来唯一标识 54 个槽位之一。</summary>
internal readonly record struct CubeSlot(CubeVector Position, CubeVector Normal);

/// <summary>
/// 不可变的完整魔方快照。每个字节是贴纸的永久身份，而非仅有颜色；同一小块的身份和朝向
/// 可由它的两张或三张贴纸完整还原。构造时复制数组，外部不能修改内部存储。
/// </summary>
internal sealed class CubeState : IEquatable<CubeState>
{
    private readonly byte[] _stickers;

    internal CubeState(IEnumerable<byte> stickers)
    {
        _stickers = stickers.ToArray();
        if (_stickers.Length != 54 || _stickers.Distinct().Count() != 54 || _stickers.Any(value => value >= 54))
            throw new ArgumentException("快照必须完整保存 54 张互不重复的贴纸身份。", nameof(stickers));
    }

    internal byte this[int slot] => _stickers[slot];
    internal int FindSticker(int identity) => Array.IndexOf(_stickers, (byte)identity);
    internal CubeFace ColorAt(int slot) => (CubeFace)(_stickers[slot] / 9);
    internal bool IsSolved => _stickers.Where((identity, slot) => identity != slot).Any() == false;
    public bool Equals(CubeState? other) => other is not null && _stickers.AsSpan().SequenceEqual(other._stickers);
    public override bool Equals(object? obj) => obj is CubeState other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _stickers) hash.Add(value);
        return hash.ToHashCode();
    }
}

internal enum CubeStage { WhiteCross, WhiteCorners, MiddleEdges, YellowCross, YellowFace, TopCorners, TopEdges }

/// <summary>公式目录中的一项；准备动作也单独保留，便于教学层标注用途而不压缩动作。</summary>
internal sealed record CubeAlgorithm(string Key, IReadOnlyList<CubeMove> Moves, bool IsPreparation = false);

/// <summary>
/// 一组教学事务的不可变证据。ProtectedPieces 表示组结束时必须已经归位的累计目标；
/// 顶层过渡组使用阶段保护加精确 After 快照验证，不能把过渡结果误报为整个阶段完成。
/// </summary>
internal sealed record SolveGroup(
    CubeStage Stage,
    CubeVector? Target,
    IReadOnlyList<CubeAlgorithm> Algorithms,
    IReadOnlyList<CubeMove> Moves,
    CubeState Before,
    CubeState After,
    IReadOnlyList<CubeVector> ProtectedPieces);

internal sealed record SolvePlan(CubeState Initial, IReadOnlyList<SolveGroup> Groups, string? Error = null)
{
    internal bool Success => Error is null;
}

/// <summary>
/// 只负责求解的窄接口。实现不得修改输入，不依赖打乱历史；取消抛出取消异常，普通求解失败
/// 返回带错误信息的方案。结果中每组都必须有可重放、可验证的前后状态。
/// </summary>
internal interface ICubeTeachingSolver
{
    SolvePlan Solve(CubeState state, CancellationToken cancellationToken);
}
