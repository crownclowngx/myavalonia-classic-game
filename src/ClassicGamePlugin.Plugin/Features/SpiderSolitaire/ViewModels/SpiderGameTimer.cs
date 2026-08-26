namespace ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;

/// <summary>
/// 基于 <see cref="TimeProvider"/> 累计真实游戏时间。DispatcherTimer 只触发界面刷新，
/// 因而窗口卡顿不会让计时变慢，测试也可以使用手工时间源而不依赖 Sleep。
/// </summary>
internal sealed class SpiderGameTimer
{
    private readonly TimeProvider _timeProvider;
    private TimeSpan _accumulated;
    private long _startedTimestamp;

    internal SpiderGameTimer(TimeProvider timeProvider) =>
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    internal bool IsRunning { get; private set; }

    internal int ElapsedSeconds =>
        (int)Math.Floor(GetElapsed().TotalSeconds);

    internal void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _startedTimestamp = _timeProvider.GetTimestamp();
        IsRunning = true;
    }

    internal void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        _accumulated += _timeProvider.GetElapsedTime(_startedTimestamp);
        IsRunning = false;
    }

    internal void Reset()
    {
        _accumulated = TimeSpan.Zero;
        _startedTimestamp = 0;
        IsRunning = false;
    }

    private TimeSpan GetElapsed() =>
        IsRunning
            ? _accumulated + _timeProvider.GetElapsedTime(_startedTimestamp)
            : _accumulated;
}
