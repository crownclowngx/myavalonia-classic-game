using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Match3.Domain;

namespace ClassicGamePlugin.Features.Match3.ViewModels;

/// <summary>
/// 承担消消乐页面的选择、提示、命令和动画协调。匹配、计分、特殊组合与洗牌全部委托给领域对象，
/// ViewModel 只发布已经提交的棋盘修订和短暂交互状态。
/// </summary>
public sealed partial class Match3ViewModel : ObservableObject
{
    private readonly Match3Game _game;
    private Match3Position? _selectedPosition;
    private Match3Position? _hintSource;
    private Match3Position? _hintTarget;
    private bool _isAnimationRunning;
    private bool _animationsEnabled = true;
    private int _boardRevision;

    public Match3ViewModel()
        : this(new Match3Game())
    {
    }

    internal Match3ViewModel(IMatch3RandomSource randomSource)
        : this(new Match3Game(randomSource))
    {
    }

    internal Match3ViewModel(Match3Game game) =>
        _game = game ?? throw new ArgumentNullException(nameof(game));

    public int Score => _game.Score;
    public int TargetScore => Match3Game.TargetScore;
    public int RemainingMoves => _game.RemainingMoves;
    public string ScoreText => $"{Score} / {TargetScore}";
    public bool IsPlaying => _game.State == Match3GameState.Playing;
    public bool CanInteract => IsPlaying && !_isAnimationRunning;
    public string StatusText => _game.State switch
    {
        Match3GameState.Won => "目标达成！点击“重新开始”再来一局",
        Match3GameState.Lost => "步数用完了，再试一次吧",
        _ => "交换相邻棋子，组合特殊棋子可以获得更高分",
    };
    public string AccessibleBoardText =>
        $"消消乐八乘八棋盘，当前 {Score} 分，目标 {TargetScore} 分，剩余 {RemainingMoves} 步，{StatusText}";

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (!SetProperty(ref _animationsEnabled, value) || value || !_isAnimationRunning)
            {
                return;
            }

            AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
            if (_isAnimationRunning)
            {
                CompleteAnimation();
            }
        }
    }

    internal Match3Game Game => _game;
    internal Match3Position? SelectedPosition => _selectedPosition;
    internal bool IsAnimationRunning => _isAnimationRunning;
    internal int BoardRevision => _boardRevision;

    internal event EventHandler<Match3AnimationPlan>? AnimationRequested;
    internal event EventHandler? AnimationCancellationRequested;

    [RelayCommand]
    private void Restart()
    {
        CancelAnimation();
        ClearTransientSelection();
        _game.StartNewGame();
        RefreshAll();
    }

    [RelayCommand(CanExecute = nameof(CanRequestHint))]
    private void Hint()
    {
        if (_game.TryGetHint(out var source, out var target))
        {
            _selectedPosition = null;
            _hintSource = source;
            _hintTarget = target;
            NotifyBoardOnly();
        }
    }

    private bool CanRequestHint() => CanInteract;

    /// <summary>处理两次点击选择；相邻的第二次点击与拖动交换复用同一领域入口。</summary>
    internal bool HandleCellClick(Match3Position position)
    {
        if (!CanInteract || !Match3Rules.IsInside(position))
        {
            return false;
        }

        ClearHint();
        if (_selectedPosition is null)
        {
            _selectedPosition = position;
            NotifyBoardOnly();
            return true;
        }

        if (_selectedPosition == position)
        {
            _selectedPosition = null;
            NotifyBoardOnly();
            return true;
        }

        if (!Match3Rules.AreAdjacent(_selectedPosition.Value, position))
        {
            _selectedPosition = position;
            NotifyBoardOnly();
            return true;
        }

        var source = _selectedPosition.Value;
        _selectedPosition = null;
        return ExecuteSwap(source, position);
    }

    internal bool HandleDragSwap(Match3Position source, Match3Position target)
    {
        if (!CanInteract || !Match3Rules.AreAdjacent(source, target))
        {
            return false;
        }

        ClearTransientSelection();
        return ExecuteSwap(source, target);
    }

    internal bool IsHinted(Match3Position position) =>
        _hintSource == position || _hintTarget == position;

    internal string GetCellAccessibleText(Match3Position position)
    {
        var tile = _game.Board[Match3Rules.ToIndex(position)];
        var coordinate = $"第 {position.Row + 1} 行第 {position.Column + 1} 列";
        if (tile is null)
        {
            return $"{coordinate}，空格";
        }

        var kind = tile.Value.Kind switch
        {
            Match3GemKind.Ruby => "红宝石",
            Match3GemKind.Amber => "琥珀",
            Match3GemKind.Emerald => "祖母绿",
            Match3GemKind.Sapphire => "蓝宝石",
            Match3GemKind.Amethyst => "紫水晶",
            Match3GemKind.Pearl => "珍珠",
            null => "彩虹球",
            _ => throw new ArgumentOutOfRangeException(),
        };
        var special = tile.Value.Special switch
        {
            Match3SpecialKind.None => string.Empty,
            Match3SpecialKind.RowClear => "，横向消除",
            Match3SpecialKind.ColumnClear => "，纵向消除",
            Match3SpecialKind.AreaBomb => "，范围炸弹",
            Match3SpecialKind.Rainbow => string.Empty,
            _ => throw new ArgumentOutOfRangeException(),
        };
        return $"{coordinate}，{kind}{special}";
    }

    internal void CompleteAnimation()
    {
        if (!_isAnimationRunning)
        {
            return;
        }

        _isAnimationRunning = false;
        RefreshAll();
    }

    private bool ExecuteSwap(Match3Position source, Match3Position target)
    {
        var transition = _game.TrySwap(source, target);
        RefreshAll();
        if (!AnimationsEnabled || AnimationRequested is null)
        {
            return transition.IsAccepted;
        }

        _isAnimationRunning = true;
        OnPropertyChanged(nameof(CanInteract));
        HintCommand.NotifyCanExecuteChanged();
        AnimationRequested.Invoke(this, new Match3AnimationPlan(transition));
        return transition.IsAccepted;
    }

    private void CancelAnimation()
    {
        if (!_isAnimationRunning)
        {
            return;
        }

        AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
        _isAnimationRunning = false;
    }

    private void ClearHint()
    {
        _hintSource = null;
        _hintTarget = null;
    }

    private void ClearTransientSelection()
    {
        _selectedPosition = null;
        ClearHint();
    }

    private void NotifyBoardOnly()
    {
        _boardRevision++;
        OnPropertyChanged(nameof(BoardRevision));
    }

    private void RefreshAll()
    {
        NotifyBoardOnly();
        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(TargetScore));
        OnPropertyChanged(nameof(RemainingMoves));
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(AccessibleBoardText));
        HintCommand.NotifyCanExecuteChanged();
    }
}
