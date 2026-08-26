namespace ClassicGamePlugin.Features.Reversi.ViewModels;

/// <summary>
/// 使用 <see cref="TimeProvider"/> 记录一局黑白棋的累计真实耗时。
/// UI 定时器只负责刷新显示，因而界面卡顿不会改变计时结果。
/// </summary>
internal sealed class ReversiGameTimer(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private TimeSpan _accumulated;
    private long? _startedTimestamp;

    internal bool IsRunning => _startedTimestamp.HasValue;

    internal int ElapsedSeconds => (int)Math.Floor(GetElapsed().TotalSeconds);

    internal void Start()
    {
        _startedTimestamp ??= _timeProvider.GetTimestamp();
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
