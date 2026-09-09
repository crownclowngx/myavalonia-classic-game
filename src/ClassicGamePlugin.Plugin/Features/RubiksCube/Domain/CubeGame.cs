namespace ClassicGamePlugin.Features.RubiksCube.Domain;

/// <summary>拥有唯一可变棋局。动画只在动作终点调用 Commit；版本用于拒绝旧求解结果。</summary>
internal sealed class CubeGame
{
    private readonly List<CubeMove> _history = [];
    internal CubeState State { get; private set; } = CubeRules.Solved();
    internal long Version { get; private set; }
    internal IReadOnlyList<CubeMove> History => _history.AsReadOnly();

    internal void Commit(CubeMove move)
    {
        State = CubeRules.Apply(State, move);
        _history.Add(move);
        Version++;
    }

    internal void Reset()
    {
        State = CubeRules.Solved();
        _history.Clear();
        Version++;
    }

    internal static IReadOnlyList<CubeMove> Scramble(Random random, int length = 25)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
        var moves = new List<CubeMove>();
        while (moves.Count < length)
        {
            var move = CubeRules.AllMoves[random.Next(CubeRules.AllMoves.Count)];
            if (moves.Count > 0 && moves[^1].Face == move.Face) continue;
            moves.Add(move);
        }
        return moves.AsReadOnly();
    }
}
