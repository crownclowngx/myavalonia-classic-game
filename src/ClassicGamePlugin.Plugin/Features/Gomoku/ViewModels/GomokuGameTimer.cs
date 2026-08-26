namespace ClassicGamePlugin.Features.Gomoku.ViewModels;

/// <summary>使用单调时间累计有效对局时长，UI 刷新频率不会影响真实计时。</summary>
internal sealed class GomokuGameTimer(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private TimeSpan _accumulated;
    private long? _startedTimestamp;

    internal bool IsRunning => _startedTimestamp is not null;
    internal int ElapsedSeconds => (int)Math.Floor(GetElapsed().TotalSeconds);

    internal void Start()
    {
        if (_startedTimestamp is null)
        {
            _startedTimestamp = _timeProvider.GetTimestamp();
        }
    }

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
