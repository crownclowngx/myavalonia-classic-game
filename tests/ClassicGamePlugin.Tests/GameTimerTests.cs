using ClassicGamePlugin.Features.Minesweeper.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GameTimerTests
{
    [Fact]
    public void 开始停止和继续只累计有效运行时间()
    {
        var timeProvider = new ManualTimeProvider();
        var timer = new GameTimer(timeProvider);

        timer.Start();
        timeProvider.Advance(TimeSpan.FromSeconds(3.8));
        timer.Stop();
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.False(timer.IsRunning);
        Assert.Equal(3, timer.ElapsedSeconds);

        timer.Start();
        timeProvider.Advance(TimeSpan.FromSeconds(2.4));
        timer.Stop();

        Assert.Equal(6, timer.ElapsedSeconds);
    }

    [Fact]
    public void 重置会清除累计并停止计时()
    {
        var timeProvider = new ManualTimeProvider();
        var timer = new GameTimer(timeProvider);
        timer.Start();
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        timer.Reset();

        Assert.False(timer.IsRunning);
        Assert.Equal(0, timer.ElapsedSeconds);
    }
}
