using ClassicGamePlugin.Features.Go.Domain;
using ClassicGamePlugin.Features.Go.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GoViewModelTests
{
    [Fact]
    public void 本地双人落子更新回合记录计时与展示投影()
    {
        var time = new ManualTimeProvider();
        using var viewModel = new GoViewModel(time, false);

        viewModel.PlayPosition(3, 3);
        time.Advance(TimeSpan.FromSeconds(12.8));
        viewModel.RefreshElapsedTime();

        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal("白方回合", viewModel.CurrentTurnText);
        Assert.Equal("对局进行中", viewModel.StatusText);
        Assert.Equal(12, viewModel.ElapsedSeconds);
        Assert.True(viewModel.IsTimerRunning);
        Assert.Contains("D16", Assert.Single(viewModel.HistoryItems).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void 非法自杀只显示原因且不增加手数与历史()
    {
        var snapshot = GoRulesTests.Snapshot(
            GoRulesTests.Board(
                (new GoPosition(0, 1), GoStone.White),
                (new GoPosition(1, 0), GoStone.White),
                (new GoPosition(1, 2), GoStone.White),
                (new GoPosition(2, 1), GoStone.White)),
            GoStone.Black);
        using var viewModel = new GoViewModel(new ManualTimeProvider(), false, new GoGame(snapshot));

        viewModel.PlayPosition(1, 1);

        Assert.Equal(0, viewModel.MoveCount);
        Assert.Empty(viewModel.HistoryItems);
        Assert.Contains("无气", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public void 两次停手停止计时并开放标死确认与恢复命令()
    {
        var time = new ManualTimeProvider();
        using var viewModel = new GoViewModel(time, false);
        viewModel.PlayPosition(3, 3);
        time.Advance(TimeSpan.FromSeconds(5));

        viewModel.PassCommand.Execute(null);
        viewModel.PassCommand.Execute(null);

        Assert.True(viewModel.IsScoring);
        Assert.True(viewModel.CanMarkDead);
        Assert.False(viewModel.CanPlay);
        Assert.False(viewModel.IsTimerRunning);
        Assert.True(viewModel.ConfirmScoreCommand.CanExecute(null));
        Assert.True(viewModel.ResumePlayCommand.CanExecute(null));

        viewModel.PlayPosition(3, 3);
        Assert.True(viewModel.CurrentSnapshot.IsMarkedDead(new GoPosition(3, 3)));

        viewModel.ResumePlayCommand.Execute(null);
        Assert.False(viewModel.IsScoring);
        Assert.True(viewModel.IsTimerRunning);
        Assert.False(viewModel.CurrentSnapshot.IsMarkedDead(new GoPosition(3, 3)));
    }

    [Fact]
    public void 确认空盘数子由白方贴目获胜且撤销可恢复数子()
    {
        using var viewModel = new GoViewModel(new ManualTimeProvider(), false);
        viewModel.PassCommand.Execute(null);
        viewModel.PassCommand.Execute(null);

        viewModel.ConfirmScoreCommand.Execute(null);

        Assert.True(viewModel.IsFinished);
        Assert.Contains("白 7.5", viewModel.ResultText, StringComparison.Ordinal);
        Assert.Contains("白方获胜", viewModel.StatusText, StringComparison.Ordinal);

        viewModel.UndoCommand.Execute(null);
        Assert.True(viewModel.IsScoring);
        Assert.False(viewModel.IsFinished);
    }

    [Fact]
    public void 认输撤销和重开保持不限步历史语义()
    {
        using var viewModel = new GoViewModel(new ManualTimeProvider(), false);
        viewModel.PlayPosition(3, 3);
        viewModel.ResignCommand.Execute(null);
        Assert.True(viewModel.IsFinished);
        Assert.Contains("黑方中盘胜", viewModel.ResultText, StringComparison.Ordinal);

        viewModel.UndoCommand.Execute(null);
        Assert.False(viewModel.IsFinished);
        Assert.Equal("白方回合", viewModel.CurrentTurnText);

        viewModel.RestartCommand.Execute(null);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.False(viewModel.CanUndo);
        Assert.Empty(viewModel.HistoryItems);
        Assert.Equal(0, viewModel.ElapsedSeconds);
    }

    [Fact]
    public void 有订阅者时动画锁定棋盘而完成或关闭动画会立即解锁()
    {
        using var viewModel = new GoViewModel(new ManualTimeProvider(), false);
        GoAnimationPlan? requested = null;
        viewModel.AnimationRequested += (_, plan) => requested = plan;

        viewModel.PlayPosition(3, 3);

        Assert.NotNull(requested);
        Assert.True(viewModel.IsAnimationRunning);
        Assert.False(viewModel.CanBoardInteract);
        viewModel.CompleteAnimation();
        Assert.True(viewModel.CanBoardInteract);

        viewModel.PlayPosition(4, 4);
        Assert.True(viewModel.IsAnimationRunning);
        viewModel.AnimationsEnabled = false;
        Assert.False(viewModel.IsAnimationRunning);
        Assert.True(viewModel.CanBoardInteract);
    }

    [Fact]
    public void 释放会停止计时并拒绝后续棋盘输入()
    {
        var viewModel = new GoViewModel(new ManualTimeProvider(), false);
        viewModel.PlayPosition(3, 3);
        Assert.True(viewModel.IsTimerRunning);

        viewModel.Dispose();
        viewModel.PlayPosition(4, 4);

        Assert.False(viewModel.IsTimerRunning);
        Assert.Equal(1, viewModel.MoveCount);
        Assert.False(viewModel.CanBoardInteract);
    }
}
