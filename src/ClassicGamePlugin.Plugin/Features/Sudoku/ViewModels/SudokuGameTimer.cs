namespace ClassicGamePlugin.Features.Sudoku.ViewModels;

/// <summary>
/// 使用可注入的 <see cref="TimeProvider"/> 计算有效游戏时间。UI 定时器只负责刷新显示，
/// 因而窗口暂时卡顿不会改变真实累计时间，测试也无需等待墙钟。
/// </summary>
internal sealed class SudokuGameTimer(TimeProvider timeProvider)
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
