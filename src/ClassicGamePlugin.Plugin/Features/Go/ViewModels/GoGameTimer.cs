namespace ClassicGamePlugin.Features.Go.ViewModels;

/// <summary>
/// 使用 <see cref="TimeProvider"/> 累计有效行棋时间。Avalonia 定时器只刷新数字，
/// 数子、终局和窗口释放时停止计时，不会因 UI 卡顿改变真实累计值。
/// </summary>
internal sealed class GoGameTimer(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private TimeSpan _accumulated;
    private long? _startedTimestamp;

    internal bool IsRunning => _startedTimestamp.HasValue;
    internal int ElapsedSeconds => (int)Math.Floor(GetElapsed().TotalSeconds);

    internal void Start() => _startedTimestamp ??= _timeProvider.GetTimestamp();

    internal void Stop()
    {
        if (_startedTimestamp is not { } started)
        {
            return;
        }

        _accumulated += _timeProvider.GetElapsedTime(started, _timeProvider.GetTimestamp());
        _startedTimestamp = null;
    }

    internal void Reset()
    {
        _accumulated = TimeSpan.Zero;
        _startedTimestamp = null;
    }

    private TimeSpan GetElapsed() => _startedTimestamp is { } started
        ? _accumulated + _timeProvider.GetElapsedTime(started, _timeProvider.GetTimestamp())
        : _accumulated;
}
