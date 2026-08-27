using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Tetris.Domain;

namespace ClassicGamePlugin.Features.Tetris.ViewModels;

/// <summary>
/// 将设备无关的玩家意图和时间增量交给领域游戏，并投影分数、等级、暂存及状态文案。ViewModel 不计算 SRS、碰撞、
/// T-Spin 或计分，也不依赖 Avalonia 计时器；视觉回放期间领域已经提交，只暂时阻止下一批输入和重力。
/// </summary>
public sealed partial class TetrisViewModel : ObservableObject
{
    private readonly TetrisGameLoop _loop;
    private bool _animationsEnabled = true;
    private bool _isAnimationRunning;
    private int _boardRevision;

    public TetrisViewModel()
        : this(new SevenBagTetrominoSource())
    {
    }

    internal TetrisViewModel(ITetrominoSource source)
    {
        _loop = new TetrisGameLoop(new TetrisGame(source));
        RefreshAll();
    }

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

    public int Score => Game.Score;
    public int Level => Game.Level;
    public int TotalLines => Game.TotalLines;
    public string ComboText => Game.Combo > 0 ? $"{Game.Combo} Combo" : "—";
    public string BackToBackText => Game.IsBackToBackActive ? "进行中" : "—";
    public bool IsPaused => Game.State == TetrisGameState.Paused;
    public bool IsGameOver => Game.State == TetrisGameState.GameOver;
    public bool CanPlay => Game.State == TetrisGameState.Playing && !_isAnimationRunning;
    public string PauseButtonText => IsPaused ? "继续" : "暂停";
    public string StatusText => Game.State switch
    {
        TetrisGameState.Playing when _isAnimationRunning => "正在落定方块…",
        TetrisGameState.Playing => "消除整行，连续困难消行可获得 Back-to-Back 奖励",
        TetrisGameState.Paused => "游戏已暂停",
        TetrisGameState.GameOver => "堆叠进入隐藏区，游戏结束",
        _ => throw new InvalidOperationException("遇到了未知的俄罗斯方块状态。"),
    };
    public string AccessibleBoardText =>
        $"俄罗斯方块，等级 {Level}，分数 {Score}，已消除 {TotalLines} 行，{StatusText}";

    internal TetrisGame Game => _loop.Game;
    internal TetrisGameLoop Loop => _loop;
    internal int BoardRevision => _boardRevision;
    internal bool IsAnimationRunning => _isAnimationRunning;

    internal event EventHandler<TetrisAnimationPlan>? AnimationRequested;
    internal event EventHandler? AnimationCancellationRequested;

    [RelayCommand]
    internal void MoveLeft() => PerformAdjustment(() => _loop.MoveHorizontal(-1));

    [RelayCommand]
    internal void MoveRight() => PerformAdjustment(() => _loop.MoveHorizontal(1));

    [RelayCommand]
    internal void SoftDrop() => PerformAdjustment(_loop.SoftDropStep);

    [RelayCommand]
    internal void RotateClockwise() => PerformAdjustment(() => _loop.Rotate(clockwise: true));

    [RelayCommand]
    internal void RotateCounterClockwise() => PerformAdjustment(() => _loop.Rotate(clockwise: false));

    [RelayCommand]
    internal void Hold()
    {
        if (CanPlay && _loop.Hold())
        {
            RefreshAll();
        }
    }

    [RelayCommand]
    internal void HardDrop()
    {
        if (!CanPlay)
        {
            return;
        }

        var transition = _loop.HardDrop();
        if (transition is not null)
        {
            PublishTransition(transition);
        }
    }

    [RelayCommand]
    internal void TogglePause()
    {
        CancelAnimation();
        if (_loop.TogglePause())
        {
            RefreshAll();
        }
    }

    [RelayCommand]
    internal void Restart()
    {
        CancelAnimation();
        _loop.Restart();
        RefreshAll();
    }

    internal void Advance(TimeSpan elapsed, bool softDrop)
    {
        if (!CanPlay)
        {
            return;
        }

        var transitions = _loop.Advance(elapsed, softDrop);
        if (transitions.Count == 0)
        {
            RefreshBoardOnly();
            return;
        }

        foreach (var transition in transitions)
        {
            PublishTransition(transition);
            if (_isAnimationRunning)
            {
                break;
            }
        }
    }

    /// <summary>视觉树隐藏或顶层窗口失活时暂停，不因普通按钮抢走棋盘焦点而暂停。</summary>
    internal void PauseForLifecycle()
    {
        CancelAnimation();
        if (_loop.Pause())
        {
            RefreshAll();
        }
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

    private void PerformAdjustment(Func<bool> action)
    {
        if (CanPlay && action())
        {
            RefreshAll();
        }
    }

    private void PublishTransition(TetrisTransition transition)
    {
        RefreshAll();
        var plan = new TetrisAnimationPlan(transition);
        if (!AnimationsEnabled || plan.TotalDuration == TimeSpan.Zero || AnimationRequested is null)
        {
            return;
        }

        _isAnimationRunning = true;
        RefreshSummary();
        AnimationRequested.Invoke(this, plan);
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

    private void RefreshBoardOnly()
    {
        _boardRevision++;
        OnPropertyChanged(nameof(BoardRevision));
        OnPropertyChanged(nameof(AccessibleBoardText));
    }

    private void RefreshAll()
    {
        RefreshBoardOnly();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(TotalLines));
        OnPropertyChanged(nameof(ComboText));
        OnPropertyChanged(nameof(BackToBackText));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsGameOver));
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(PauseButtonText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(AccessibleBoardText));
    }
}
