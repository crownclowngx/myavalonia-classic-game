namespace ClassicGamePlugin.Features.Tetris.Domain;

/// <summary>
/// 将经过时间转换成离散重力和锁定事务。调用方可以来自 Avalonia 的 16ms 计时器，也可以是单元测试传入的任意时间片；
/// 本类不读取系统时钟，因此暂停、大时间片补偿和 15 次锁定重置上限都能确定验证。
/// </summary>
internal sealed class TetrisGameLoop
{
    internal static readonly TimeSpan LockDelay = TimeSpan.FromMilliseconds(500);
    internal const int MaximumLockResets = 15;

    private TimeSpan _gravityElapsed;
    private TimeSpan _lockElapsed;
    private int _lockResetCount;
    private bool _softDropActive;

    internal TetrisGameLoop(TetrisGame game) => Game = game ?? throw new ArgumentNullException(nameof(game));

    internal TetrisGame Game { get; }
    internal TimeSpan GravityElapsed => _gravityElapsed;
    internal TimeSpan LockElapsed => _lockElapsed;
    internal int LockResetCount => _lockResetCount;
    internal double FallProgress => Game.State == TetrisGameState.Playing && !Game.IsGrounded
        ? Math.Clamp(_gravityElapsed.TotalMilliseconds / GetGravityInterval(Game.Level, false).TotalMilliseconds, 0, 1)
        : 0;

    internal IReadOnlyList<TetrisTransition> Advance(TimeSpan elapsed, bool softDrop)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "游戏循环不能倒退时间。");
        }

        var transitions = new List<TetrisTransition>();
        if (Game.State != TetrisGameState.Playing || elapsed == TimeSpan.Zero)
        {
            return transitions;
        }

        if (_softDropActive != softDrop)
        {
            // 两种重力周期不能直接共用毫秒余量，否则按下软降时，普通重力已经累计的 600ms 会被错误解释为十二个软降周期。
            // 键盘按下会先执行一个单格软降，因此切换速度时清零不会制造可感知的输入延迟。
            _gravityElapsed = TimeSpan.Zero;
            _softDropActive = softDrop;
        }

        var remaining = elapsed;
        while (remaining > TimeSpan.Zero && Game.State == TetrisGameState.Playing)
        {
            if (Game.IsGrounded)
            {
                var untilLock = LockDelay - _lockElapsed;
                var consumed = remaining < untilLock ? remaining : untilLock;
                _lockElapsed += consumed;
                remaining -= consumed;
                if (_lockElapsed < LockDelay)
                {
                    break;
                }

                var transition = Game.LockActivePiece();
                ResetForNewPiece();
                if (transition is not null)
                {
                    transitions.Add(transition);
                }

                continue;
            }

            _lockElapsed = TimeSpan.Zero;
            var interval = GetGravityInterval(Game.Level, softDrop);
            var untilStep = interval - _gravityElapsed;
            var gravityConsumed = remaining < untilStep ? remaining : untilStep;
            _gravityElapsed += gravityConsumed;
            remaining -= gravityConsumed;
            if (_gravityElapsed < interval)
            {
                break;
            }

            _gravityElapsed = TimeSpan.Zero;
            Game.TryStepDown(softDrop);
        }

        return transitions;
    }

    internal bool MoveHorizontal(int delta) => Adjust(() => Game.TryMoveHorizontal(delta));

    internal bool Rotate(bool clockwise) => Adjust(() => Game.TryRotate(clockwise, out _));

    internal bool Hold()
    {
        if (!Game.Hold())
        {
            return false;
        }

        ResetForNewPiece();
        return true;
    }

    internal TetrisTransition? HardDrop()
    {
        var transition = Game.HardDrop();
        if (transition is not null)
        {
            ResetForNewPiece();
        }

        return transition;
    }

    internal bool SoftDropStep()
    {
        if (!Game.TryStepDown(awardSoftDropPoint: true))
        {
            return false;
        }

        _gravityElapsed = TimeSpan.Zero;
        if (Game.IsGrounded)
        {
            _lockElapsed = TimeSpan.Zero;
        }

        return true;
    }

    internal bool TogglePause() => Game.TogglePause();

    internal bool Pause() => Game.Pause();

    internal void Restart()
    {
        Game.StartNewGame();
        ResetForNewPiece();
    }

    internal static TimeSpan GetGravityInterval(int level, bool softDrop)
    {
        if (level < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        var seconds = Math.Pow(0.8 - ((level - 1) * 0.007), level - 1);
        var normalMilliseconds = Math.Max(16, seconds * 1000);
        return TimeSpan.FromMilliseconds(softDrop ? Math.Max(1, normalMilliseconds / 20) : normalMilliseconds);
    }

    private bool Adjust(Func<bool> adjustment)
    {
        var wasGrounded = Game.IsGrounded;
        if (!adjustment())
        {
            return false;
        }

        if (!Game.IsGrounded)
        {
            _lockElapsed = TimeSpan.Zero;
            return true;
        }

        if (!wasGrounded)
        {
            _lockElapsed = TimeSpan.Zero;
            return true;
        }

        if (_lockResetCount < MaximumLockResets)
        {
            _lockElapsed = TimeSpan.Zero;
            _lockResetCount++;
        }

        return true;
    }

    private void ResetForNewPiece()
    {
        _gravityElapsed = TimeSpan.Zero;
        _lockElapsed = TimeSpan.Zero;
        _lockResetCount = 0;
        _softDropActive = false;
    }
}
