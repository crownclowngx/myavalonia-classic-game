using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Game2048.Domain;

namespace ClassicGamePlugin.Features.Game2048.ViewModels;

/// <summary>
/// 承担 2048 View 的可观察状态和用户命令。它把方向意图交给领域游戏，并把结果一次刷新到固定的 16 个格子；
/// ViewModel 不实现压缩、合并、随机生成或终局规则，也不依赖 Plugin SDK 和具体键盘设备。
/// </summary>
public sealed partial class Game2048ViewModel : ObservableObject
{
    private readonly Game2048Game _game;
    private Game2048Direction? _queuedDirection;
    private bool _isAnimationRunning;

    private bool _animationsEnabled = true;

    /// <summary>使用经典随机生成策略创建生产 ViewModel。</summary>
    public Game2048ViewModel()
        : this(new RandomTileSpawnStrategy())
    {
    }

    /// <summary>使用可控生成策略创建可测试 ViewModel。</summary>
    internal Game2048ViewModel(ITileSpawnStrategy tileSpawnStrategy)
        : this(new Game2048Game(tileSpawnStrategy))
    {
    }

    /// <summary>使用既定领域实例创建测试投影，便于覆盖胜利和终局边界。</summary>
    internal Game2048ViewModel(Game2048Game game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        for (var row = 0; row < Game2048Rules.BoardSize; row++)
        {
            for (var column = 0; column < Game2048Rules.BoardSize; column++)
            {
                BoardCells.Add(new Game2048CellViewModel(row, column));
            }
        }

        RefreshProjection();
    }

    /// <summary>获取固定按行优先排列的 16 个展示格子。</summary>
    public ObservableCollection<Game2048CellViewModel> BoardCells { get; } = [];

    /// <summary>
    /// 获取或设置当前 Document 是否播放方块动画。该偏好只属于本 ViewModel，不跨 Document 或应用重启持久化；
    /// 播放中关闭时会立即落定当前结果，并继续处理已经缓存的最后方向。
    /// </summary>
    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (SetProperty(ref _animationsEnabled, value))
            {
                OnAnimationsEnabledChanged(value);
            }
        }
    }

    /// <summary>获取当前累计合并分数。</summary>
    public int Score => _game.Score;

    /// <summary>获取棋盘是否接受方向输入。</summary>
    public bool CanMove => _game.State is Game2048GameState.Playing or Game2048GameState.Continuing;

    /// <summary>获取是否正在等待玩家确认继续挑战。</summary>
    public bool IsAwaitingContinue => _game.State == Game2048GameState.WonAwaitingContinue;

    /// <summary>获取当前对局阶段的中文说明。</summary>
    public string StatusText => _game.State switch
    {
        Game2048GameState.Playing => "合并相同数字，挑战 2048",
        Game2048GameState.WonAwaitingContinue => "已达成 2048！可以继续挑战更高数字",
        Game2048GameState.Continuing => "继续挑战中",
        Game2048GameState.Lost => "没有可移动的方块，游戏结束",
        _ => throw new InvalidOperationException("遇到了未知的 2048 游戏状态。"),
    };

    /// <summary>获取内部状态，供同程序集输入编排和单元测试验证。</summary>
    internal Game2048GameState GameState => _game.State;

    /// <summary>获取是否有一个视觉回放尚未完成。</summary>
    internal bool IsAnimationRunning => _isAnimationRunning;

    /// <summary>获取动画期间最后一次方向输入，供状态机测试验证覆盖语义。</summary>
    internal Game2048Direction? QueuedDirection => _queuedDirection;

    /// <summary>请求 View 回放一个已经提交的领域移动。</summary>
    internal event EventHandler<Game2048AnimationPlan>? AnimationRequested;

    /// <summary>请求 View 停止回放并立即确认当前领域最终状态。</summary>
    internal event EventHandler? AnimationCancellationRequested;

    [RelayCommand]
    private void MoveUp() => Move(Game2048Direction.Up);

    [RelayCommand]
    private void MoveDown() => Move(Game2048Direction.Down);

    [RelayCommand]
    private void MoveLeft() => Move(Game2048Direction.Left);

    [RelayCommand]
    private void MoveRight() => Move(Game2048Direction.Right);

    /// <summary>清空当前棋盘、分数和阶段，并重新生成两个初始方块。</summary>
    [RelayCommand]
    private void Restart()
    {
        _queuedDirection = null;
        CancelAnimationIfNeeded();
        _game.StartNewGame();
        RefreshProjection();
    }

    /// <summary>确认首次达成目标后继续；非等待状态调用不会改变棋盘。</summary>
    [RelayCommand]
    private void ContinueGame()
    {
        if (_game.ContinueAfterWin())
        {
            RefreshSummaryProperties();
        }
    }

    /// <summary>执行一个已经脱离具体输入设备的方向意图。</summary>
    internal void Move(Game2048Direction direction)
    {
        if (_isAnimationRunning)
        {
            // 只保留最后方向可以防止长队列让画面落后于玩家，同时不会完全丢失快速连续操作。
            _queuedDirection = direction;
            return;
        }

        var transition = _game.Move(direction);
        if (transition is null)
        {
            return;
        }

        if (!AnimationsEnabled || AnimationRequested is null)
        {
            RefreshProjection();
            return;
        }

        _isAnimationRunning = true;
        AnimationRequested.Invoke(this, new Game2048AnimationPlan(transition));
    }

    /// <summary>
    /// 由 View 在播放完成或主动取消后确认视觉状态。先投影已提交的最终棋盘，再取出最后缓存方向；
    /// 缓存动作若产生新 Transition，会自然开始下一段动画。
    /// </summary>
    internal void CompleteAnimation()
    {
        if (!_isAnimationRunning)
        {
            return;
        }

        _isAnimationRunning = false;
        RefreshProjection();

        var queuedDirection = _queuedDirection;
        _queuedDirection = null;
        if (queuedDirection is { } direction)
        {
            Move(direction);
        }
    }

    private void OnAnimationsEnabledChanged(bool value)
    {
        if (!value)
        {
            CancelAnimationIfNeeded();
        }
    }

    private void CancelAnimationIfNeeded()
    {
        if (!_isAnimationRunning)
        {
            return;
        }

        AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
        // 没有 View 订阅或订阅方已经卸载时，也必须自行落定，避免 ViewModel 永久停留在动画状态。
        if (_isAnimationRunning)
        {
            CompleteAnimation();
        }
    }

    private void RefreshProjection()
    {
        for (var index = 0; index < BoardCells.Count; index++)
        {
            BoardCells[index].Refresh(_game.Cells[index]);
        }

        RefreshSummaryProperties();
    }

    private void RefreshSummaryProperties()
    {
        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(CanMove));
        OnPropertyChanged(nameof(IsAwaitingContinue));
        OnPropertyChanged(nameof(StatusText));
    }
}
