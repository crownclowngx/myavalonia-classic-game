using System.Collections.ObjectModel;
using Avalonia.Threading;
using ClassicGamePlugin.Features.RubiksCube.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassicGamePlugin.Features.RubiksCube.ViewModels;

/// <summary>
/// 编排页面用例，不计算公式或三维几何。领域状态仅由播放器提交；求解任务只读快照，
/// 完成时同时检查取消源身份和棋局版本，避免重置、手动转面或关闭后的旧结果回写。
/// </summary>
public sealed partial class RubiksCubeViewModel : ObservableObject, IDisposable
{
    private readonly CubeGame _game = new();
    private readonly ICubeTeachingSolver _solver;
    private readonly Random _random;
    private readonly Func<Action, Task> _dispatch;
    private CancellationTokenSource? _solverCancellation;
    private SolvePlan? _plan;
    private int _cursor;
    private int _rewindIndex;
    private CubeState? _rewindTarget;
    private Operation _operation;
    private bool _continuous;
    private bool _pauseAfterGroup;
    private bool _disposed;
    private int _highlightedAction = -1;
    private readonly Dictionary<IRelayCommand, bool> _commandAvailability = [];

    [ObservableProperty] private bool _isThinking;
    [ObservableProperty] private string _message = "魔方已还原。点击打乱，或使用下方按钮自由转面。";
    [ObservableProperty] private string _scrambleText = "尚未打乱";
    [ObservableProperty] private CubeStepItem? _currentStep;
    [ObservableProperty] private int _selectedSpeed = 1;

    public RubiksCubeViewModel() : this(new CubeTeachingSolver(), TimeProvider.System, Random.Shared, DispatchUiAsync) { }

    internal RubiksCubeViewModel(ICubeTeachingSolver solver, TimeProvider timeProvider, Random random, Func<Action, Task> dispatch)
    {
        _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        ArgumentNullException.ThrowIfNull(timeProvider);
        Player = new CubePlaybackController(_game, timeProvider);
        Player.Changed += OnPlayerChanged;
        Player.Completed += OnPlayerCompleted;
        RestartCommand = new RelayCommand(Restart, () => !_disposed);
        ScrambleCommand = new RelayCommand(Scramble, () => CanEdit);
        TurnCommand = new RelayCommand<string>(Turn, _ => CanEdit);
        NextGroupCommand = new RelayCommand(() => BeginTeaching(false), () => CanStartTeaching);
        PlayAllCommand = new RelayCommand(() => BeginTeaching(true), () => CanStartTeaching);
        PauseCommand = new RelayCommand(Pause, () => !_disposed && Player.IsRunning && !Player.PauseRequested && _operation != Operation.Rewind);
        ResumeCommand = new RelayCommand(Resume, () => !_disposed && (Player.IsPaused || (_pauseAfterGroup && CanStartTeaching)));
        PreviousGroupCommand = new RelayCommand(Rewind, CanRewind);
        foreach (var command in new IRelayCommand[] { RestartCommand, ScrambleCommand, TurnCommand, NextGroupCommand,
                     PlayAllCommand, PauseCommand, ResumeCommand, PreviousGroupCommand })
            _commandAvailability.Add(command, command.CanExecute(null));
    }

    public IRelayCommand RestartCommand { get; }
    public IRelayCommand ScrambleCommand { get; }
    public IRelayCommand<string> TurnCommand { get; }
    public IRelayCommand NextGroupCommand { get; }
    public IRelayCommand PlayAllCommand { get; }
    public IRelayCommand PauseCommand { get; }
    public IRelayCommand ResumeCommand { get; }
    public IRelayCommand PreviousGroupCommand { get; }
    public ObservableCollection<CubeStepItem> Steps { get; } = [];
    public ObservableCollection<CubeActionItem> Actions { get; } = [];
    public ObservableCollection<string> HistoryItems { get; } = [];
    public IReadOnlyList<string> SpeedOptions { get; } = Array.AsReadOnly(new[] { "0.5×", "1×", "2×" });
    public string ProgressText => $"教学进度 {_cursor} / {Steps.Count} 组 · 已执行 {_game.History.Count} 个 90°动作";
    public string NextPreview
    {
        get
        {
            var next = _cursor + (_operation == Operation.Teaching ? 1 : 0);
            return _plan is not null && next < Steps.Count ? $"下一组：{Steps[next].Purpose}"
                : _game.State.IsSolved ? "六面已经还原"
                : _operation == Operation.Teaching ? "本组完成后结束教学"
                : IsThinking ? "正在生成教学步骤…" : "等待教学方案";
        }
    }
    public string CurrentMoveText => Player.CurrentMove is { } move
        ? $"动作 {Player.CompletedCount + 1} / {Player.TotalCount}：{CubeTeachingText.Move(move)}" : "可拖动观察 · 转面方向按该面外侧正视判断";
    public string PlaybackText => Player.IsPaused ? "已暂停，点击继续" : Player.PauseRequested ? "当前 90°动作结束后暂停" : _operation switch
    {
        Operation.Scramble => "正在打乱",
        Operation.Manual => "手动转面",
        Operation.Teaching => "正在执行教学组",
        Operation.Rewind => "正在逆序退回",
        _ => IsThinking ? "正在分析当前魔方" : "准备就绪",
    };

    internal CubePlaybackController Player { get; }
    internal CubeState State => _game.State;
    internal SolvePlan? Plan => _plan;
    internal int CompletedGroups => _cursor;
    internal CubeVector? Target => CurrentStep?.Group.Target;
    internal Task PendingSolveTask { get; private set; } = Task.CompletedTask;
    private bool CanEdit => !_disposed && !IsThinking && !Player.IsActive;
    private bool CanStartTeaching => CanEdit && _plan is { Success: true } && _cursor < Steps.Count;

    partial void OnSelectedSpeedChanged(int value)
    {
        Player.Speed = value switch { 0 => 0.5, 2 => 2, _ => 1 };
    }

    private static async Task DispatchUiAsync(Action action) => await Dispatcher.UIThread.InvokeAsync(action);

    private void Restart()
    {
        if (_disposed) return;
        CancelSolver();
        _operation = Operation.None;
        _continuous = false;
        _pauseAfterGroup = false;
        Player.Cancel();
        _game.Reset();
        ClearPlan();
        HistoryItems.Clear();
        ScrambleText = "尚未打乱";
        Message = "魔方已重置为还原状态。";
        Refresh();
    }

    private void Scramble()
    {
        if (!CanEdit) return;
        var moves = CubeGame.Scramble(_random);
        ScrambleText = CubeRules.Format(moves);
        BeginExternal(Operation.Scramble, moves, "连续播放 25 个打乱指令；完成后生成分组教学。");
    }

    private void Turn(string? formula)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(formula)) return;
        var moves = CubeRules.Parse(formula);
        BeginExternal(Operation.Manual, moves, $"手动操作：{formula}；完成后根据当前状态重新分析。");
    }

    private void BeginExternal(Operation operation, IReadOnlyList<CubeMove> moves, string message)
    {
        CancelSolver();
        ClearPlan();
        _operation = operation;
        _continuous = false;
        _pauseAfterGroup = false;
        Message = message;
        SetActions(CubeRules.Expand(moves));
        Player.Start(moves);
    }

    private void BeginTeaching(bool continuous)
    {
        if (!CanStartTeaching) return;
        _continuous = continuous;
        _pauseAfterGroup = false;
        StartNextGroup();
    }

    private void StartNextGroup()
    {
        if (_disposed || _plan is null || _cursor >= Steps.Count) return;
        var step = Steps[_cursor];
        if (!State.Equals(step.Group.Before))
        {
            Fail("当前状态与下一规则组的起点不一致，请重新生成方案。");
            return;
        }
        CurrentStep = step;
        step.Status = "执行中";
        step.ActualResult = "";
        _operation = Operation.Teaching;
        Message = step.Purpose;
        SetActions(step.Group.Moves);
        Player.Start(step.Group.Moves);
    }

    private void Pause()
    {
        if (_disposed || !Player.IsRunning || _operation == Operation.Rewind) return;
        _pauseAfterGroup = true;
        Player.Pause();
    }

    private void Resume()
    {
        if (_disposed) return;
        if (Player.IsPaused)
        {
            _pauseAfterGroup = false;
            Player.Resume();
        }
        else if (_pauseAfterGroup && CanStartTeaching)
        {
            _pauseAfterGroup = false;
            StartNextGroup();
        }
    }

    private bool CanRewind() => !_disposed && !IsThinking && _plan is not null &&
        ((!Player.IsActive && _cursor > 0) ||
         (_operation == Operation.Teaching && Player.IsPaused && Player.Progress == 0));

    private void Rewind()
    {
        if (!CanRewind()) return;
        var partial = Player.IsActive;
        _rewindIndex = partial ? _cursor : _cursor - 1;
        var group = Steps[_rewindIndex].Group;
        var executed = partial ? Player.ExecutedMoves : group.Moves;
        var reverse = Array.AsReadOnly(executed.Reverse().Select(move => move.Inverse).ToArray());
        _rewindTarget = group.Before;
        _continuous = false;
        _pauseAfterGroup = false;
        Player.Cancel();
        _operation = Operation.Rewind;
        CurrentStep = Steps[_rewindIndex];
        CurrentStep.Status = "退回中";
        CurrentStep.ActualResult = "";
        Message = "按逆序逐个播放已完成动作，回到本组起点。";
        SetActions(reverse);
        if (reverse.Count == 0) FinishRewind();
        else Player.Start(reverse);
    }

    private void FinishRewind()
    {
        if (!State.Equals(_rewindTarget)) { Fail("回退状态与组起点不一致，已停止播放。"); return; }
        _cursor = _rewindIndex;
        for (var index = _cursor; index < Steps.Count; index++)
        {
            Steps[index].Status = "待执行";
            Steps[index].ActualResult = "";
        }
        _operation = Operation.None;
        Message = "已回到本组起点，可点击“按照规则下一步”重新观看。";
        HistoryItems.Add($"退回第 {_cursor + 1} 组起点。");
        SetActions(Steps[_cursor].Group.Moves);
        Refresh();
    }

    private void OnPlayerChanged(object? sender, EventArgs args) => Refresh();

    private void OnPlayerCompleted(object? sender, EventArgs args)
    {
        if (_disposed) return;
        if (_operation == Operation.Rewind) { FinishRewind(); return; }
        if (_operation == Operation.Teaching)
        {
            var step = Steps[_cursor];
            if (!CubeRules.VerifyGroup(step.Group, State)) { Fail("本组实际状态未通过目标和阶段保护检查。"); return; }
            step.Status = "已完成";
            step.ActualResult = "已验证：" + CubeTeachingText.Result(step.Group);
            HistoryItems.Add($"第 {_cursor + 1} 组：{step.Formula} → {step.ActualResult}");
            _cursor++;
            _operation = Operation.None;
            Message = State.IsSolved ? "六面全部还原！每组动作与结果均已验证。" : step.ActualResult;
            if (_continuous && !_pauseAfterGroup && _cursor < Steps.Count) StartNextGroup();
            else Refresh();
            return;
        }
        HistoryItems.Add((_operation == Operation.Scramble ? "打乱：" : "手动：") + CubeRules.Format(Player.ExecutedMoves));
        _operation = Operation.None;
        _pauseAfterGroup = false;
        RequestPlan();
    }

    private void RequestPlan()
    {
        CancelSolver();
        IsThinking = true;
        Message = "正在按层先法分析当前状态并验证完整教学方案…";
        var cancellation = new CancellationTokenSource();
        _solverCancellation = cancellation;
        var input = State;
        var version = _game.Version;
        Refresh();
        PendingSolveTask = SolveAsync(input, version, cancellation);
    }

    private async Task SolveAsync(CubeState input, long version, CancellationTokenSource cancellation)
    {
        try
        {
            var plan = await Task.Run(() => _solver.Solve(input, cancellation.Token), cancellation.Token).ConfigureAwait(false);
            await _dispatch(() =>
            {
                if (!IsCurrentRequest(version, cancellation)) return;
                _solverCancellation = null;
                IsThinking = false;
                if (!plan.Success) { Fail("无法生成教学方案：" + plan.Error); return; }
                if (!ValidatePlan(input, plan)) { Fail("求解结果未通过完整重放检查，已拒绝执行。"); return; }
                _plan = plan;
                _cursor = 0;
                Steps.Clear();
                for (var index = 0; index < plan.Groups.Count; index++) Steps.Add(new CubeStepItem(index + 1, plan.Groups[index]));
                CurrentStep = Steps.FirstOrDefault();
                SetActions(CurrentStep?.Group.Moves ?? Array.Empty<CubeMove>());
                Message = State.IsSolved ? "当前魔方已经还原。" : $"已生成 {Steps.Count} 组教学步骤。点击下一步，完成一个小目标。";
                Refresh();
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            await _dispatch(() =>
            {
                if (IsCurrentRequest(version, cancellation)) Fail("教学分析失败：" + exception.Message);
            }).ConfigureAwait(false);
        }
        finally
        {
            await _dispatch(() =>
            {
                if (!ReferenceEquals(_solverCancellation, cancellation)) return;
                _solverCancellation = null;
                IsThinking = false;
                if (!_disposed) Refresh();
            }).ConfigureAwait(false);
            cancellation.Dispose();
        }
    }

    private bool IsCurrentRequest(long version, CancellationTokenSource cancellation) => !_disposed &&
        !cancellation.IsCancellationRequested && ReferenceEquals(_solverCancellation, cancellation) && _game.Version == version;

    private static bool ValidatePlan(CubeState initial, SolvePlan plan)
    {
        if (!initial.Equals(plan.Initial)) return false;
        var state = initial;
        foreach (var group in plan.Groups)
        {
            if (!state.Equals(group.Before) || group.Moves.Count == 0) return false;
            state = CubeRules.Apply(state, group.Moves);
            if (!CubeRules.VerifyGroup(group, state)) return false;
        }
        return state.IsSolved;
    }

    private void CancelSolver()
    {
        _solverCancellation?.Cancel();
        _solverCancellation = null;
        IsThinking = false;
    }

    private void ClearPlan()
    {
        _plan = null;
        _cursor = 0;
        Steps.Clear();
        CurrentStep = null;
        Actions.Clear();
        _highlightedAction = -1;
    }

    private void Fail(string message)
    {
        _operation = Operation.None;
        _continuous = false;
        _pauseAfterGroup = false;
        Player.Cancel();
        ClearPlan();
        Message = message;
        Refresh();
    }

    private void SetActions(IReadOnlyList<CubeMove> moves)
    {
        Actions.Clear();
        for (var index = 0; index < moves.Count; index++) Actions.Add(new CubeActionItem(index + 1, moves[index]));
        _highlightedAction = -1;
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(CurrentMoveText));
        OnPropertyChanged(nameof(PlaybackText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(NextPreview));
        var current = _operation != Operation.None ? Player.CompletedCount : CurrentStep?.Status == "已完成" ? Actions.Count : -1;
        if (current != _highlightedAction)
        {
            for (var index = 0; index < Actions.Count; index++)
            {
                Actions[index].IsCurrent = index == current && Player.IsActive;
                Actions[index].IsComplete = index < current;
            }
            _highlightedAction = current;
        }
        RefreshCommands();
    }

    /// <summary>
    /// 帧刷新不等于命令状态变化。仅当 CanExecute 真正改变时通知，避免每帧让 Host 重新投影菜单。
    /// 缓存先更新再发事件，重入查询仍能观察到最新值；容器只保存本实例命令，不跨 Document 共享。
    /// </summary>
    private void RefreshCommands()
    {
        foreach (var (command, previous) in _commandAvailability)
        {
            var current = command.CanExecute(null);
            if (current == previous) continue;
            _commandAvailability[command] = current;
            command.NotifyCanExecuteChanged();
        }
    }

    internal void Tick() { if (!_disposed) Player.Tick(); }
    internal void SuspendView() { if (!_disposed) Player.Suspend(); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelSolver();
        Player.Changed -= OnPlayerChanged;
        Player.Completed -= OnPlayerCompleted;
        Player.Cancel();
        Refresh();
    }

    private enum Operation { None, Scramble, Manual, Teaching, Rewind }
}
