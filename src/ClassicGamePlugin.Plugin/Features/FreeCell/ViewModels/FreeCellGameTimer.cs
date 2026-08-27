namespace ClassicGamePlugin.Features.FreeCell.ViewModels;

/// <summary>
/// 只累计真实游戏时间，不创建 UI 定时器。使用 <see cref="TimeProvider"/> 让测试可以精确推进时间，
/// 撤销只改变棋局快照而不会倒退已经消耗的时间。
/// </summary>
internal sealed class FreeCellGameTimer(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private TimeSpan _accumulated;
    private long _startedTimestamp;

    internal bool IsRunning { get; private set; }
    internal int ElapsedSeconds => (int)Math.Floor(Elapsed.TotalSeconds);
    private TimeSpan Elapsed => _accumulated +
        (IsRunning ? _timeProvider.GetElapsedTime(_startedTimestamp) : TimeSpan.Zero);

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

        _accumulated = Elapsed;
        IsRunning = false;
    }

    internal void Reset()
    {
        _accumulated = TimeSpan.Zero;
        _startedTimestamp = 0;
        IsRunning = false;
    }
}
