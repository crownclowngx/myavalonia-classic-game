namespace ClassicGamePlugin.Features.ChineseCheckers.Domain;

internal enum ChineseCheckersSide
{
    Blue,
    Red,
}

internal enum ChineseCheckersGameState
{
    Ready,
    Running,
    Finished,
}

internal enum ChineseCheckersTerminationReason
{
    GoalFilled,
    BlockedHome,
}

internal enum ChineseCheckersMoveKind
{
    Step,
    Hop,
}

internal enum ChineseCheckersAiDifficulty
{
    Easy,
    Medium,
    Hard,
}

/// <summary>
/// 六角网格的立方坐标。三个分量之和恒为零，因此六个相邻方向和隔子跳跃都能用整数向量表达，
/// 领域层不需要依赖像素、行宽或棋盘朝向。
/// </summary>
internal readonly record struct ChineseCheckersPosition(int X, int Y, int Z)
{
    internal ChineseCheckersPosition(int x, int z)
        : this(x, -x - z, z)
    {
    }

    internal string DisplayName => $"({X},{Y},{Z})";

    internal ChineseCheckersPosition Add(ChineseCheckersPosition direction, int scale = 1) =>
        new(X + (direction.X * scale), Y + (direction.Y * scale), Z + (direction.Z * scale));
}

/// <summary>
/// 一次完整回合。路径始终包含起点和终点；单步恰有两个点，连跳保存 BFS 选出的稳定最短路径，
/// 供动画与操作记录复用，避免界面再次推导规则。
/// </summary>
internal sealed record ChineseCheckersMove(
    ChineseCheckersPosition From,
    ChineseCheckersPosition To,
    ChineseCheckersMoveKind Kind,
    IReadOnlyList<ChineseCheckersPosition> Path);

/// <summary>隔离保存棋盘、回合与终局信息；构造和复制都克隆数组，后台 AI 不会共享真实棋局引用。</summary>
internal sealed class ChineseCheckersGameSnapshot
{
    private readonly ChineseCheckersSide?[] _board;

    internal ChineseCheckersGameSnapshot(
        IEnumerable<ChineseCheckersSide?> board,
        ChineseCheckersSide currentSide,
        ChineseCheckersGameState state,
        int moveCount,
        ChineseCheckersMove? lastMove,
        ChineseCheckersSide? winner,
        ChineseCheckersTerminationReason? terminationReason)
    {
        _board = board?.ToArray() ?? throw new ArgumentNullException(nameof(board));
        if (_board.Length != ChineseCheckersRules.CellCount)
        {
            throw new ArgumentException("中国跳棋棋盘必须恰好包含 121 个棋位。", nameof(board));
        }

        CurrentSide = currentSide;
        State = state;
        MoveCount = moveCount;
        LastMove = lastMove;
        Winner = winner;
        TerminationReason = terminationReason;
    }

    internal ChineseCheckersSide CurrentSide { get; }
    internal ChineseCheckersGameState State { get; }
    internal int MoveCount { get; }
    internal ChineseCheckersMove? LastMove { get; }
    internal ChineseCheckersSide? Winner { get; }
    internal ChineseCheckersTerminationReason? TerminationReason { get; }

    internal ChineseCheckersSide? GetPiece(ChineseCheckersPosition position) =>
        ChineseCheckersRules.TryGetIndex(position, out var index)
            ? _board[index]
            : throw new ArgumentOutOfRangeException(nameof(position));

    internal ChineseCheckersSide?[] CopyBoard() => (ChineseCheckersSide?[])_board.Clone();
}

internal sealed record ChineseCheckersMoveResult(
    ChineseCheckersGameSnapshot Before,
    ChineseCheckersGameSnapshot After,
    ChineseCheckersMove Move,
    ChineseCheckersSide Side);
