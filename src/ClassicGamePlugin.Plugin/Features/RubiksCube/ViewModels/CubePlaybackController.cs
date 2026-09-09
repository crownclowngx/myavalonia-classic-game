using ClassicGamePlugin.Features.RubiksCube.Domain;

namespace ClassicGamePlugin.Features.RubiksCube.ViewModels;

/// <summary>
/// 单实例串行动作播放器。它是动画提交棋局的唯一入口，不调度后台线程，也不创建 UI 定时器。
/// 每次 Tick 最多推进一个动作且最多消费 50ms，系统卡顿不会一次吞掉整个公式。
/// 隐藏时保留未完成角度；用户暂停则在当前四分之一转结束后停下，便于合法状态回退。
/// </summary>
internal sealed class CubePlaybackController(CubeGame game, TimeProvider timeProvider)
{
    private IReadOnlyList<CubeMove> _moves = Array.Empty<CubeMove>();
    private int _index;
    private double _elapsedMilliseconds;
    private long _lastTimestamp;
    private bool _pauseRequested;

    internal event EventHandler? Changed;
    internal event EventHandler? Completed;
    internal CubeState State => game.State;
    internal CubeState Before { get; private set; } = game.State;
    internal bool IsActive => _index < _moves.Count;
    internal bool IsPaused { get; private set; }
    internal bool IsRunning => IsActive && !IsPaused;
    internal bool PauseRequested => _pauseRequested;
    internal int CompletedCount => _index;
    internal int TotalCount => _moves.Count;
    internal double Progress => _elapsedMilliseconds / CubeAnimationPlan.Duration.TotalMilliseconds;
    internal CubeMove? CurrentMove => IsActive ? _moves[_index] : null;
    internal IReadOnlyList<CubeMove> ExecutedMoves => Array.AsReadOnly(_moves.Take(_index).ToArray());
    internal double Speed { get; set; } = 1;

    internal void Start(IReadOnlyList<CubeMove> moves)
    {
        if (IsActive) throw new InvalidOperationException("必须先结束或取消当前动作组。");
        if (moves.Count == 0) throw new ArgumentException("动作组不能为空。", nameof(moves));
        _moves = CubeRules.Expand(moves);
        _index = 0;
        _elapsedMilliseconds = 0;
        _pauseRequested = false;
        IsPaused = false;
        Before = game.State;
        _lastTimestamp = timeProvider.GetTimestamp();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Tick()
    {
        var now = timeProvider.GetTimestamp();
        var delta = timeProvider.GetElapsedTime(_lastTimestamp, now);
        _lastTimestamp = now;
        Advance(delta);
    }

    /// <summary>可控时间测试入口；多余墙钟时间不带入下一动作，保证每个动作都有实际播放过程。</summary>
    internal void Advance(TimeSpan delta)
    {
        if (!IsRunning) return;
        _elapsedMilliseconds += Math.Clamp(delta.TotalMilliseconds, 0, 50) * Math.Clamp(Speed, 0.5, 2);
        if (_elapsedMilliseconds >= CubeAnimationPlan.Duration.TotalMilliseconds)
        {
            game.Commit(_moves[_index]);
            _index++;
            _elapsedMilliseconds = 0;
            if (_pauseRequested && IsActive) IsPaused = true;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        if (!IsActive)
        {
            IsPaused = false;
            _pauseRequested = false;
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void Pause()
    {
        if (!IsRunning) return;
        _pauseRequested = true;
        if (_elapsedMilliseconds == 0) IsPaused = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Suspend()
    {
        if (!IsActive || IsPaused) return;
        IsPaused = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Resume()
    {
        if (!IsActive || !IsPaused) return;
        IsPaused = false;
        _pauseRequested = false;
        _lastTimestamp = timeProvider.GetTimestamp();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>只清理未执行队列，不回滚已提交动作；重置与组级回退由用例层明确决定。</summary>
    internal void Cancel()
    {
        _moves = Array.Empty<CubeMove>();
        _index = 0;
        _elapsedMilliseconds = 0;
        IsPaused = false;
        _pauseRequested = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
