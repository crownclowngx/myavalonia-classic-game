using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Sokoban.Domain;

namespace ClassicGamePlugin.Features.Sokoban.ViewModels;

/// <summary>
/// 负责推箱子页面的命令、关卡切换和展示投影。所有墙体、推动、完成与撤销规则都委托给 <see cref="SokobanGame"/>；
/// ViewModel 只把设备无关的方向意图编排成领域事务，并协调领域已提交状态与短暂视觉回放。
/// </summary>
public sealed partial class SokobanViewModel : ObservableObject
{
    private readonly IReadOnlyList<SokobanLevelDefinition> _levels;
    private SokobanGame _game;
    private int _selectedLevelIndex;
    private SokobanDirection? _queuedDirection;
    private bool _isAnimationRunning;
    private bool _animationsEnabled = true;
    private int _boardRevision;

    public SokobanViewModel()
        : this(SokobanLevelCatalog.Levels)
    {
    }

    internal SokobanViewModel(IReadOnlyList<SokobanLevelDefinition> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count == 0)
        {
            throw new ArgumentException("推箱子至少需要一个关卡。", nameof(levels));
        }

        _levels = levels.ToArray();
        LevelOptions = new ReadOnlyObservableCollection<SokobanLevelOption>(
            new ObservableCollection<SokobanLevelOption>(
                _levels.Select((level, index) => new SokobanLevelOption(index, level))));
        _game = new SokobanGame(_levels[0]);
    }

    public ReadOnlyObservableCollection<SokobanLevelOption> LevelOptions { get; }

    public int SelectedLevelIndex
    {
        get => _selectedLevelIndex;
        set
        {
            if (value < 0 || value >= _levels.Count || value == _selectedLevelIndex)
            {
                return;
            }

            CancelAnimationAndDiscardQueuedInput();
            _selectedLevelIndex = value;
            OnPropertyChanged();
            _game = new SokobanGame(_levels[value]);
            RefreshAll();
        }
    }

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (!SetProperty(ref _animationsEnabled, value) || value)
            {
                return;
            }

            // 关闭动画属于显示偏好，不应丢掉玩家最后一次方向输入；先落定当前移动，再让缓存方向无动画执行。
            if (_isAnimationRunning)
            {
                AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
                if (_isAnimationRunning)
                {
                    CompleteAnimation();
                }
            }
        }
    }

    public string LevelName => _game.Level.Name;
    public string DifficultyText => GetDifficultyText(_game.Level.Difficulty);
    public int MoveCount => _game.MoveCount;
    public int PushCount => _game.PushCount;
    public int BoxesOnGoals => _game.BoxesOnGoals;
    public int GoalCount => _game.Level.GoalCount;
    public bool IsCompleted => _game.IsCompleted;
    public bool CanUndo => _game.CanUndo;
    public bool CanGoPrevious => SelectedLevelIndex > 0;
    public bool CanGoNext => SelectedLevelIndex < _levels.Count - 1;
    public string GoalProgressText => $"{BoxesOnGoals} / {GoalCount}";
    public string StatusText => IsCompleted ? "本关完成！可撤销或选择下一关" : "把所有箱子推到圆形目标点";
    public string AccessibleBoardText =>
        $"推箱子第 {SelectedLevelIndex + 1} 关，{LevelName}，{DifficultyText}，" +
        $"已归位 {BoxesOnGoals} 个，共 {GoalCount} 个箱子，移动 {MoveCount} 步";

    /// <summary>棋盘每次发生可见变化时递增，专用绘制控件只需监听属性变化而不复制领域集合。</summary>
    internal int BoardRevision => _boardRevision;
    internal SokobanGame Game => _game;
    internal bool IsAnimationRunning => _isAnimationRunning;
    internal SokobanDirection? QueuedDirection => _queuedDirection;

    internal event EventHandler<SokobanAnimationPlan>? AnimationRequested;
    internal event EventHandler? AnimationCancellationRequested;

    [RelayCommand]
    private void MoveUp() => Move(SokobanDirection.Up);

    [RelayCommand]
    private void MoveDown() => Move(SokobanDirection.Down);

    [RelayCommand]
    private void MoveLeft() => Move(SokobanDirection.Left);

    [RelayCommand]
    private void MoveRight() => Move(SokobanDirection.Right);

    [RelayCommand]
    internal void Undo()
    {
        CancelAnimationAndDiscardQueuedInput();
        if (_game.Undo())
        {
            RefreshAll();
        }
    }

    [RelayCommand]
    internal void Restart()
    {
        CancelAnimationAndDiscardQueuedInput();
        _game.Restart();
        RefreshAll();
    }

    [RelayCommand]
    private void PreviousLevel()
    {
        if (CanGoPrevious)
        {
            SelectedLevelIndex--;
        }
    }

    [RelayCommand]
    private void NextLevel()
    {
        if (CanGoNext)
        {
            SelectedLevelIndex++;
        }
    }

    internal void Move(SokobanDirection direction)
    {
        if (_isAnimationRunning)
        {
            // 只保留最后方向，既避免完全丢失快速输入，也不会让长队列使画面持续落后于领域状态。
            _queuedDirection = direction;
            return;
        }

        var result = _game.Move(direction);
        if (result is null)
        {
            return;
        }

        RefreshAll();
        if (!AnimationsEnabled || AnimationRequested is null)
        {
            return;
        }

        _isAnimationRunning = true;
        AnimationRequested.Invoke(this, new SokobanAnimationPlan(result));
    }

    /// <summary>
    /// 由 View 在视觉回放完成或被显示偏好取消后调用。完成关卡时方向输入已经没有意义；普通移动则执行动画期间
    /// 最后保存的方向。领域状态始终先于动画提交，因此这里不会重复提交刚才的移动。
    /// </summary>
    internal void CompleteAnimation()
    {
        if (!_isAnimationRunning)
        {
            return;
        }

        _isAnimationRunning = false;
        var queued = _queuedDirection;
        _queuedDirection = null;
        if (!_game.IsCompleted && queued is { } direction)
        {
            Move(direction);
        }
    }

    internal string GetCellAccessibleText(int row, int column)
    {
        var position = new SokobanPosition(row, column);
        var terrain = _game.Level.TerrainAt(position);
        var coordinate = $"第 {row + 1} 行第 {column + 1} 列";
        if (_game.Player == position)
        {
            return $"{coordinate}，玩家{(terrain == SokobanTerrain.Goal ? "站在目标点上" : string.Empty)}";
        }

        if (_game.HasBox(position))
        {
            return $"{coordinate}，{(terrain == SokobanTerrain.Goal ? "已归位箱子" : "箱子")}";
        }

        return terrain switch
        {
            SokobanTerrain.Wall => $"{coordinate}，墙",
            SokobanTerrain.Goal => $"{coordinate}，目标点",
            _ => $"{coordinate}，地面",
        };
    }

    internal static string GetDifficultyText(SokobanDifficulty difficulty) => difficulty switch
    {
        SokobanDifficulty.Beginner => "入门",
        SokobanDifficulty.Intermediate => "进阶",
        SokobanDifficulty.Challenge => "挑战",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };

    private void CancelAnimationAndDiscardQueuedInput()
    {
        _queuedDirection = null;
        if (!_isAnimationRunning)
        {
            return;
        }

        AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
        _isAnimationRunning = false;
    }

    private void RefreshAll()
    {
        _boardRevision++;
        OnPropertyChanged(nameof(BoardRevision));
        OnPropertyChanged(nameof(LevelName));
        OnPropertyChanged(nameof(DifficultyText));
        OnPropertyChanged(nameof(MoveCount));
        OnPropertyChanged(nameof(PushCount));
        OnPropertyChanged(nameof(BoxesOnGoals));
        OnPropertyChanged(nameof(GoalCount));
        OnPropertyChanged(nameof(GoalProgressText));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(AccessibleBoardText));
        UndoCommand.NotifyCanExecuteChanged();
        PreviousLevelCommand.NotifyCanExecuteChanged();
        NextLevelCommand.NotifyCanExecuteChanged();
    }
}
