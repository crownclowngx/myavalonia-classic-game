namespace ClassicGamePlugin.Features.Match3.Domain;

/// <summary>
/// 持有一局消消乐的已提交状态。完整回合由 Resolver 在副本中完成，只有成功返回后才同时覆盖棋盘、
/// 分数、步数和胜负，保证随机源或连锁异常时原局不变。
/// </summary>
internal sealed class Match3Game
{
    internal const int InitialMoves = 30;
    internal const int TargetScore = 1500;

    private readonly IMatch3RandomSource _randomSource;
    private readonly Match3BoardGenerator _boardGenerator;
    private readonly Match3TurnResolver _turnResolver;
    private Match3Tile?[] _board;

    internal Match3Game()
        : this(new SystemMatch3RandomSource())
    {
    }

    internal Match3Game(IMatch3RandomSource randomSource)
    {
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _boardGenerator = new Match3BoardGenerator();
        _turnResolver = new Match3TurnResolver(_boardGenerator);
        _board = _boardGenerator.Create(_randomSource);
        RemainingMoves = InitialMoves;
        State = Match3GameState.Playing;
    }

    internal Match3Game(
        IMatch3RandomSource randomSource,
        IReadOnlyList<Match3Tile?> board,
        int score = 0,
        int remainingMoves = InitialMoves)
    {
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _boardGenerator = new Match3BoardGenerator();
        _turnResolver = new Match3TurnResolver(_boardGenerator);
        Match3Rules.ValidateBoard(board);
        if (board.Any(tile => tile is null))
        {
            throw new ArgumentException("已提交的消消乐棋盘不能包含空格。", nameof(board));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingMoves);
        _board = board.ToArray();
        Score = score;
        RemainingMoves = remainingMoves;
        State = score >= TargetScore
            ? Match3GameState.Won
            : remainingMoves == 0 ? Match3GameState.Lost : Match3GameState.Playing;
    }

    internal IReadOnlyList<Match3Tile?> Board => _board;
    internal int Score { get; private set; }
    internal int RemainingMoves { get; private set; }
    internal Match3GameState State { get; private set; }

    internal Match3TurnTransition TrySwap(Match3Position source, Match3Position target)
    {
        if (State != Match3GameState.Playing)
        {
            return new Match3TurnTransition(source, target, false, _board, _board);
        }

        var transition = _turnResolver.Resolve(_board, source, target, _randomSource);
        if (!transition.IsAccepted)
        {
            return transition;
        }

        var nextScore = checked(Score + transition.ScoreDelta);
        var nextMoves = RemainingMoves - 1;
        var nextState = nextScore >= TargetScore
            ? Match3GameState.Won
            : nextMoves == 0 ? Match3GameState.Lost : Match3GameState.Playing;

        _board = transition.After.ToArray();
        Score = nextScore;
        RemainingMoves = nextMoves;
        State = nextState;
        return transition;
    }

    internal bool TryGetHint(out Match3Position source, out Match3Position target)
    {
        if (State == Match3GameState.Playing)
        {
            return Match3Rules.TryFindFirstLegalSwap(_board, out source, out target);
        }

        source = default;
        target = default;
        return false;
    }

    internal void StartNewGame()
    {
        var candidate = _boardGenerator.Create(_randomSource);
        _board = candidate;
        Score = 0;
        RemainingMoves = InitialMoves;
        State = Match3GameState.Playing;
    }
}
