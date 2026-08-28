namespace ClassicGamePlugin.Features.ChineseCheckers.ViewModels;

/// <summary>基于 TimeProvider 累计有效对局时间；DispatcherTimer 仅刷新展示，不参与真实计时。</summary>
internal sealed class ChineseCheckersGameTimer(TimeProvider timeProvider)
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
