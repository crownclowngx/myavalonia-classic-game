namespace ClassicGamePlugin.Features.Minesweeper.ViewModels;

/// <summary>
/// 记录单局游戏的有效运行时间。该类型只负责时间计算，不主动创建线程或 UI 定时器；
/// ViewModel 只需定期读取结果并通知界面刷新。使用 <see cref="TimeProvider"/> 可以让测试精确推进时间。
/// </summary>
internal sealed class GameTimer(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider ??
        throw new ArgumentNullException(nameof(timeProvider));
    private long? _startedTimestamp;
    private TimeSpan _elapsedBeforeStart;

    /// <summary>获取计时器当前是否正在累计时间。</summary>
    internal bool IsRunning => _startedTimestamp.HasValue;

    /// <summary>获取已累计的完整时间。</summary>
    internal TimeSpan Elapsed => _startedTimestamp is { } startedTimestamp
        ? _elapsedBeforeStart + _timeProvider.GetElapsedTime(startedTimestamp, _timeProvider.GetTimestamp())
        : _elapsedBeforeStart;

    /// <summary>获取向下取整后的经过秒数，避免界面因毫秒变化频繁刷新。</summary>
    internal int ElapsedSeconds => (int)Math.Floor(Elapsed.TotalSeconds);

    /// <summary>开始或继续计时；重复开始不会丢失原来的起点。</summary>
    internal void Start()
    {
        _startedTimestamp ??= _timeProvider.GetTimestamp();
    }

    /// <summary>停止计时并冻结当前耗时；重复停止不会重复累计。</summary>
    internal void Stop()
    {
        if (_startedTimestamp is not { } startedTimestamp)
        {
            return;
        }

        _elapsedBeforeStart += _timeProvider.GetElapsedTime(startedTimestamp, _timeProvider.GetTimestamp());
        _startedTimestamp = null;
    }

    /// <summary>清空累计时间并回到停止状态。</summary>
    internal void Reset()
    {
        _startedTimestamp = null;
        _elapsedBeforeStart = TimeSpan.Zero;
    }
}
