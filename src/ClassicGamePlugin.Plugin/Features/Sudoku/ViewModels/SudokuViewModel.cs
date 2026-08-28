using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClassicGamePlugin.Features.Sudoku.Domain;

namespace ClassicGamePlugin.Features.Sudoku.ViewModels;

/// <summary>
/// 数独页面的可观察状态与命令编排器。它把数字、笔记、提示和撤销意图交给领域对局，把题目请求交给题源，
/// 自身只负责选择状态、计时显示、异步生命周期和动画通知，不复制求解或冲突规则。
/// </summary>
public sealed partial class SudokuViewModel : ObservableObject, IDisposable
{
    private readonly ISudokuPuzzleProvider _puzzleProvider;
    private readonly SudokuGame _game;
    private readonly SudokuGameTimer _gameTimer;
    private readonly DispatcherTimer? _displayRefreshTimer;
    private bool _disposed;
    private bool _suppressDifficultyChange;

    [ObservableProperty]
    private SudokuDifficultyOption _selectedDifficulty;

    private SudokuPosition? _selectedPosition;

    [ObservableProperty]
    private bool _isNotesMode;

    [ObservableProperty]
    private bool _animationsEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotGenerating))]
    private bool _isGenerating;

    [ObservableProperty]
    private int _elapsedSeconds;

    [ObservableProperty]
    private string _messageText = "选择空格后输入数字";

    public SudokuViewModel()
        : this(new SudokuPuzzleProvider(), TimeProvider.System, enableDisplayRefreshTimer: true)
    {
    }

    internal SudokuViewModel(
        ISudokuPuzzleProvider puzzleProvider,
        TimeProvider timeProvider,
        bool enableDisplayRefreshTimer)
    {
        _puzzleProvider = puzzleProvider ?? throw new ArgumentNullException(nameof(puzzleProvider));
        DifficultyOptions = Enum.GetValues<SudokuDifficulty>()
            .Select(difficulty => new SudokuDifficultyOption(difficulty))
            .ToArray();
        _selectedDifficulty = DifficultyOptions[0];
        _game = new SudokuGame(_puzzleProvider.GetBuiltInPuzzle(_selectedDifficulty.Difficulty));
        _gameTimer = new SudokuGameTimer(timeProvider);
        for (var row = 0; row < SudokuRules.BoardSize; row++)
        {
            for (var column = 0; column < SudokuRules.BoardSize; column++)
            {
                BoardCells.Add(new SudokuCellViewModel(row, column));
            }
        }

        SelectFirstEditableCell();
        RefreshProjection();
        if (enableDisplayRefreshTimer)
        {
            _displayRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _displayRefreshTimer.Tick += OnDisplayRefreshTimerTick;
        }
    }

    public IReadOnlyList<SudokuDifficultyOption> DifficultyOptions { get; }
    public ObservableCollection<SudokuCellViewModel> BoardCells { get; } = [];
    public bool IsNotGenerating => !IsGenerating;
    public bool IsCompleted => _game.IsCompleted;
    public bool CanInteract => !_game.IsCompleted;
    public bool CanUndo => _game.CanUndo;
    public bool CanHint => !_game.IsCompleted && _game.Values.Any(value => value == 0);
    public string DifficultyText => SudokuDifficultyProfile.For(_game.Puzzle.Difficulty).DisplayName;
    public string SourceText => _game.Puzzle.Source == SudokuPuzzleSource.BuiltIn ? "内置题库" : "运行时生成";
    public string StatusText => _game.IsCompleted ? "恭喜完成数独！" : "进行中";

    internal SudokuPosition? SelectedPosition
    {
        get => _selectedPosition;
        private set => SetProperty(ref _selectedPosition, value);
    }

    internal string CurrentPuzzleId => _game.Puzzle.Id;
    internal SudokuPuzzleSource CurrentPuzzleSource => _game.Puzzle.Source;
    internal bool IsTimerRunning => _gameTimer.IsRunning;
    internal event EventHandler<SudokuAnimationPlan>? AnimationRequested;
    internal event EventHandler? AnimationCancellationRequested;

    internal void SelectCell(SudokuPosition position)
    {
        if (_disposed || !SudokuRules.IsInside(position))
        {
            return;
        }

        SelectedPosition = position;
        RefreshProjection();
    }

    public void MoveSelection(int rowDelta, int columnDelta)
    {
        if (_disposed)
        {
            return;
        }

        var current = SelectedPosition ?? new SudokuPosition(0, 0);
        SelectCell(new SudokuPosition(
            Math.Clamp(current.Row + rowDelta, 0, SudokuRules.BoardSize - 1),
            Math.Clamp(current.Column + columnDelta, 0, SudokuRules.BoardSize - 1)));
    }

    [RelayCommand]
    private void EnterNumber(int number)
    {
        if (_disposed || SelectedPosition is not { } position)
        {
            return;
        }

        var result = IsNotesMode
            ? _game.ToggleNote(position, number)
            : _game.SetValue(position, number);
        ApplyMove(result, IsNotesMode ? "已更新候选笔记" : "已填入数字");
    }

    [RelayCommand]
    private void ClearSelected()
    {
        if (!_disposed && SelectedPosition is { } position)
        {
            ApplyMove(_game.ClearValue(position), "已清除数字");
        }
    }

    [RelayCommand]
    private void ToggleNotes() => IsNotesMode = !IsNotesMode;

    [RelayCommand]
    private void Hint()
    {
        var result = _disposed ? null : _game.RevealHint(SelectedPosition);
        if (result?.Position is { } position)
        {
            SelectedPosition = position;
        }

        ApplyMove(result, "已填入一个提示数字，可使用撤销恢复");
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var result = _disposed ? null : _game.Undo();
        if (result is null)
        {
            return;
        }

        if (!_game.IsCompleted)
        {
            StartTimerIfNeeded();
        }

        MessageText = "已撤销上一步";
        RefreshProjection();
    }

    [RelayCommand]
    private void Restart()
    {
        if (_disposed)
        {
            return;
        }

        _game.Restart();
        ResetForNewBoard("已重新开始当前题目");
    }

    [RelayCommand]
    private void NewGame()
    {
        if (_disposed || IsGenerating)
        {
            return;
        }

        var puzzle = _puzzleProvider.GetBuiltInPuzzle(
            SelectedDifficulty.Difficulty,
            _game.Puzzle.Id);
        _game.StartPuzzle(puzzle);
        ResetForNewBoard("已从内置题库开始新游戏");
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GeneratePuzzleAsync(CancellationToken cancellationToken)
    {
        if (_disposed || IsGenerating)
        {
            return;
        }

        IsGenerating = true;
        MessageText = "正在后台生成唯一解题目，当前棋盘仍可继续操作";
        try
        {
            var puzzle = await _puzzleProvider.GeneratePuzzleAsync(
                SelectedDifficulty.Difficulty,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed)
            {
                return;
            }

            _game.StartPuzzle(puzzle);
            ResetForNewBoard("已开始运行时生成的新题目");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!_disposed)
            {
                MessageText = "已取消生成，当前游戏保持不变";
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (!_disposed)
            {
                MessageText = $"生成失败，当前游戏保持不变：{exception.Message}";
            }
        }
        finally
        {
            if (!_disposed)
            {
                IsGenerating = false;
            }
        }
    }

    partial void OnSelectedDifficultyChanged(SudokuDifficultyOption value)
    {
        if (_disposed || _suppressDifficultyChange || value is null)
        {
            return;
        }

        if (IsGenerating)
        {
            var current = DifficultyOptions.Single(option => option.Difficulty == _game.Puzzle.Difficulty);
            _suppressDifficultyChange = true;
            SelectedDifficulty = current;
            _suppressDifficultyChange = false;
            return;
        }

        var puzzle = _puzzleProvider.GetBuiltInPuzzle(value.Difficulty, _game.Puzzle.Id);
        _game.StartPuzzle(puzzle);
        ResetForNewBoard($"已切换到{value.DisplayName}题目");
    }

    partial void OnAnimationsEnabledChanged(bool value)
    {
        if (!value)
        {
            AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnIsNotesModeChanged(bool value) =>
        MessageText = value ? "候选笔记模式已开启" : "普通填数模式已开启";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GeneratePuzzleCommand.Cancel();
        _displayRefreshTimer?.Stop();
        if (_displayRefreshTimer is not null)
        {
            _displayRefreshTimer.Tick -= OnDisplayRefreshTimerTick;
        }

        _gameTimer.Stop();
        AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
    }

    internal void RefreshElapsedTime() => ElapsedSeconds = _gameTimer.ElapsedSeconds;

    private void ApplyMove(SudokuMoveResult? result, string successMessage)
    {
        if (result is null)
        {
            return;
        }

        StartTimerIfNeeded();
        if (result.IsCompleted)
        {
            _gameTimer.Stop();
            _displayRefreshTimer?.Stop();
            RefreshElapsedTime();
            MessageText = "全部数字正确，数独完成";
        }
        else if (result.Conflicts.Count > 0)
        {
            MessageText = "已保留输入，请检查高亮的行、列或九宫格冲突";
        }
        else
        {
            MessageText = successMessage;
        }

        RefreshProjection();
        if (AnimationsEnabled && result.Kind is SudokuMoveKind.Value or SudokuMoveKind.Hint)
        {
            var plan = new SudokuAnimationPlan(result);
            if (plan.Stages.Count > 0)
            {
                AnimationRequested?.Invoke(this, plan);
            }
        }
    }

    private void StartTimerIfNeeded()
    {
        if (!_gameTimer.IsRunning && !_game.IsCompleted)
        {
            _gameTimer.Start();
            _displayRefreshTimer?.Start();
        }
    }

    private void ResetForNewBoard(string message)
    {
        AnimationCancellationRequested?.Invoke(this, EventArgs.Empty);
        _displayRefreshTimer?.Stop();
        _gameTimer.Reset();
        ElapsedSeconds = 0;
        IsNotesMode = false;
        MessageText = message;
        SelectFirstEditableCell();
        RefreshProjection();
    }

    private void SelectFirstEditableCell()
    {
        var index = Enumerable.Range(0, SudokuRules.CellCount)
            .FirstOrDefault(index => _game.Values[index] == 0, -1);
        SelectedPosition = index >= 0 ? SudokuRules.FromIndex(index) : null;
    }

    private void RefreshProjection()
    {
        var conflicts = SudokuRules.FindConflicts(_game.Values);
        var selectedValue = SelectedPosition is { } selected
            ? _game.Values[SudokuRules.ToIndex(selected)]
            : 0;
        for (var index = 0; index < BoardCells.Count; index++)
        {
            var position = SudokuRules.FromIndex(index);
            BoardCells[index].Refresh(
                _game.Values[index],
                _game.Notes[index],
                _game.IsGiven(position),
                _game.IsHint(position),
                conflicts.Contains(position),
                SelectedPosition == position,
                SelectedPosition is { } selectedCell && SudokuRules.ArePeers(selectedCell, position),
                selectedValue != 0 && _game.Values[index] == selectedValue && SelectedPosition != position);
        }

        OnPropertyChanged(nameof(BoardCells));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanUndo));
        UndoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanHint));
        OnPropertyChanged(nameof(DifficultyText));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnDisplayRefreshTimerTick(object? sender, EventArgs eventArgs) => RefreshElapsedTime();
}
