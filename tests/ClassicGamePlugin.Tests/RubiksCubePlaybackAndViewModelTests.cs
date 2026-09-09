using ClassicGamePlugin.Features.RubiksCube.Domain;
using ClassicGamePlugin.Features.RubiksCube.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class RubiksCubePlaybackAndViewModelTests
{
    [Fact]
    public void 一百八十度有两个时间轴且卡顿不吞动作或提前提交()
    {
        var game = new CubeGame();
        var time = new CubeTestTimeProvider();
        var player = new CubePlaybackController(game, time);
        var completions = 0;
        player.Completed += (_, _) => completions++;
        player.Start(CubeRules.Parse("R2 U'"));
        time.Advance(TimeSpan.FromMinutes(1));
        player.Tick();
        Assert.Equal(0, player.CompletedCount);
        Assert.True(game.State.IsSolved);
        Assert.InRange(player.Progress, 0.16, 0.17);
        Assert.Equal(3, player.TotalCount);
        for (var index = 0; index < 5; index++) player.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(1, player.CompletedCount);
        Assert.Equal(CubeRules.Apply(CubeRules.Solved(), new CubeMove(CubeFace.R)), game.State);
        Finish(player);
        Assert.Equal(1, completions);
        Assert.Equal(3, game.History.Count);
        Assert.Equal(CubeRules.Apply(CubeRules.Solved(), CubeRules.Parse("R2 U'")), game.State);
        player.Tick();
        Assert.Equal(1, completions);
        Assert.Throws<ArgumentException>(() => new CubeAnimationPlan(new CubeMove(CubeFace.R, 2)));
    }

    [Fact]
    public void 暂停在合法边界而隐藏保留角度且恢复不追赶墙钟()
    {
        var game = new CubeGame();
        var time = new CubeTestTimeProvider();
        var player = new CubePlaybackController(game, time);
        player.Start(CubeRules.Parse("F U R"));
        player.Advance(TimeSpan.FromMilliseconds(50));
        player.Pause();
        Assert.False(player.IsPaused);
        Finish(player);
        Assert.True(player.IsPaused);
        Assert.Equal(1, player.CompletedCount);
        Assert.Equal(0, player.Progress);
        player.Resume();
        player.Advance(TimeSpan.FromMilliseconds(50));
        var progress = player.Progress;
        var state = game.State;
        player.Suspend();
        player.Suspend();
        time.Advance(TimeSpan.FromDays(1));
        player.Tick();
        Assert.Equal(progress, player.Progress);
        Assert.Equal(state, game.State);
        player.Resume();
        player.Tick();
        Assert.Equal(progress, player.Progress);
        Finish(player);
        Assert.Equal(CubeRules.Apply(CubeRules.Solved(), CubeRules.Parse("F U R")), game.State);
    }

    [Theory]
    [InlineData(0.5, 12)]
    [InlineData(1, 6)]
    [InlineData(2, 3)]
    public void 调速只改变时长且每个动作恰好提交一次(double speed, int ticks)
    {
        var game = new CubeGame();
        var player = new CubePlaybackController(game, TimeProvider.System) { Speed = speed };
        player.Start(CubeRules.Parse("U"));
        for (var index = 0; index < ticks - 1; index++) player.Advance(TimeSpan.FromMilliseconds(50));
        Assert.True(game.State.IsSolved);
        player.Advance(TimeSpan.FromMilliseconds(50));
        Assert.False(player.IsActive);
        Assert.Single(game.History);
    }

    [Fact]
    public async Task 分组回退保持原方案且连续播放最终还原()
    {
        using var vm = Create();
        await Prepare(vm);
        var plan = Assert.IsType<SolvePlan>(vm.Plan);
        var before = vm.State;
        vm.NextGroupCommand.Execute(null);
        Assert.False(vm.NextGroupCommand.CanExecute(null));
        Assert.False(vm.ScrambleCommand.CanExecute(null));
        Assert.False(vm.TurnCommand.CanExecute("R"));
        var total = vm.Player.TotalCount;
        vm.NextGroupCommand.Execute(null);
        Assert.Equal(total, vm.Player.TotalCount);
        Finish(vm.Player);
        Assert.Equal(1, vm.CompletedGroups);
        Assert.Equal("已完成", vm.Steps[0].Status);
        Assert.StartsWith("已验证：", vm.Steps[0].ActualResult);
        Assert.True(vm.PreviousGroupCommand.CanExecute(null));
        vm.PreviousGroupCommand.Execute(null);
        Finish(vm.Player);
        Assert.Equal(before, vm.State);
        Assert.Equal(0, vm.CompletedGroups);
        Assert.Same(plan, vm.Plan);
        Assert.Equal("待执行", vm.Steps[0].Status);
        vm.PlayAllCommand.Execute(null);
        Finish(vm.Player);
        Assert.True(vm.State.IsSolved);
        Assert.Equal(plan.Groups.Count, vm.CompletedGroups);
        Assert.False(vm.NextGroupCommand.CanExecute(null));
        Assert.All(vm.Steps, step => Assert.Equal("已完成", step.Status));
    }

    [Fact]
    public async Task 组内暂停仅逆放已提交前缀且不丢失准备动作()
    {
        using var vm = Create();
        await Prepare(vm);
        while (vm.Plan!.Groups[vm.CompletedGroups].Moves.Count < 2)
        {
            vm.NextGroupCommand.Execute(null);
            Finish(vm.Player);
        }
        var cursor = vm.CompletedGroups;
        var before = vm.State;
        var group = vm.Plan.Groups[cursor];
        vm.NextGroupCommand.Execute(null);
        Assert.Equal(group.Moves.Count, vm.Actions.Count);
        vm.Player.Advance(TimeSpan.FromMilliseconds(50));
        vm.PauseCommand.Execute(null);
        Finish(vm.Player);
        Assert.True(vm.Player.IsPaused);
        Assert.Equal(1, vm.Player.CompletedCount);
        Assert.True(vm.PreviousGroupCommand.CanExecute(null));
        vm.PreviousGroupCommand.Execute(null);
        Assert.Equal(1, vm.Player.TotalCount);
        Assert.Equal(group.Moves[0].Inverse, vm.Player.CurrentMove);
        Finish(vm.Player);
        Assert.Equal(before, vm.State);
        Assert.Equal(cursor, vm.CompletedGroups);
        vm.NextGroupCommand.Execute(null);
        Finish(vm.Player);
        Assert.Equal(group.After, vm.State);
    }

    [Fact]
    public async Task 连续播放在组末暂停不会偷跑下一组且恢复保留连续模式()
    {
        using var vm = Create();
        await Prepare(vm);
        vm.PlayAllCommand.Execute(null);
        while (vm.Player.CompletedCount < vm.Player.TotalCount - 1) vm.Player.Advance(TimeSpan.FromMilliseconds(50));
        vm.Player.Advance(TimeSpan.FromMilliseconds(50));
        vm.PauseCommand.Execute(null);
        Finish(vm.Player);
        Assert.Equal(1, vm.CompletedGroups);
        Assert.False(vm.Player.IsActive);
        Assert.True(vm.ResumeCommand.CanExecute(null));
        vm.ResumeCommand.Execute(null);
        Finish(vm.Player);
        Assert.True(vm.State.IsSolved);
    }

    [Fact]
    public async Task 重置取消旧求解且迟到结果不能覆盖新方案()
    {
        using var solver = new DelayedFirstSolver();
        using var vm = Create(solver);
        vm.TurnCommand.Execute("R");
        Finish(vm.Player);
        await solver.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var oldTask = vm.PendingSolveTask;
        Assert.True(vm.IsThinking);
        vm.RestartCommand.Execute(null);
        Assert.True(solver.Token.IsCancellationRequested);
        Assert.True(vm.State.IsSolved);
        vm.TurnCommand.Execute("F");
        Finish(vm.Player);
        await vm.PendingSolveTask;
        var newPlan = vm.Plan;
        var state = vm.State;
        solver.Release.Set();
        await oldTask;
        Assert.Same(newPlan, vm.Plan);
        Assert.Equal(state, vm.State);
        Assert.False(vm.IsThinking);
    }

    [Fact]
    public async Task 关闭取消后台任务且所有命令失效并支持重复释放()
    {
        using var solver = new DelayedFirstSolver();
        var vm = Create(solver);
        vm.TurnCommand.Execute("U");
        Finish(vm.Player);
        await solver.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.Dispose();
        vm.Dispose();
        Assert.True(solver.Token.IsCancellationRequested);
        Assert.False(vm.RestartCommand.CanExecute(null));
        Assert.False(vm.ScrambleCommand.CanExecute(null));
        Assert.False(vm.Player.IsActive);
        solver.Release.Set();
        await vm.PendingSolveTask;
        Assert.Null(vm.Plan);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 求解失败或伪造空方案不会显示可执行教学(bool malformed)
    {
        using var vm = Create(new FailedSolver(malformed));
        vm.TurnCommand.Execute("R");
        Finish(vm.Player);
        await vm.PendingSolveTask;
        Assert.Empty(vm.Steps);
        Assert.False(vm.NextGroupCommand.CanExecute(null));
        Assert.False(vm.IsThinking);
        Assert.Contains(malformed ? "完整重放检查" : "测试失败", vm.Message);
        Assert.True(vm.ScrambleCommand.CanExecute(null));
    }

    internal static RubiksCubeViewModel Create(ICubeTeachingSolver? solver = null) => new(
        solver ?? new CubeTeachingSolver(), new CubeTestTimeProvider(), new Random(17), action => { action(); return Task.CompletedTask; });

    [Fact]
    public void 动画帧不重复广播工作台重置状态而释放会定向通知()
    {
        var vm = Create();
        var restartChanges = 0;
        var editChanges = 0;
        vm.RestartCommand.CanExecuteChanged += (_, _) => restartChanges++;
        vm.ScrambleCommand.CanExecuteChanged += (_, _) => editChanges++;
        vm.TurnCommand.Execute("R2 U2");
        for (var index = 0; index < 10; index++) vm.Player.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Equal(0, restartChanges);
        Assert.Equal(1, editChanges);
        vm.Dispose();
        Assert.Equal(1, restartChanges);
    }

    internal static async Task Prepare(RubiksCubeViewModel vm)
    {
        vm.ScrambleCommand.Execute(null);
        Assert.Equal(25, CubeRules.Parse(vm.ScrambleText).Count);
        Finish(vm.Player);
        await vm.PendingSolveTask;
        Assert.NotEmpty(vm.Steps);
        Assert.True(vm.NextGroupCommand.CanExecute(null), vm.Message);
    }

    internal static void Finish(CubePlaybackController player)
    {
        var ticks = 0;
        while (player.IsRunning)
        {
            player.Advance(TimeSpan.FromMilliseconds(50));
            Assert.True(++ticks < 20000, "播放器未在有限动作内停止。");
        }
    }

    private sealed class FailedSolver(bool malformed) : ICubeTeachingSolver
    {
        public SolvePlan Solve(CubeState state, CancellationToken cancellationToken) =>
            new(state, [], malformed ? null : "测试失败");
    }

    [Fact]
    public async Task 求解器意外异常会显示原因并恢复可编辑状态()
    {
        using var vm = Create(new ThrowingSolver());
        vm.TurnCommand.Execute("F");
        Finish(vm.Player);
        await vm.PendingSolveTask;
        Assert.Contains("测试求解异常", vm.Message);
        Assert.False(vm.IsThinking);
        Assert.True(vm.ScrambleCommand.CanExecute(null));
        Assert.Empty(vm.Steps);
    }

    private sealed class ThrowingSolver : ICubeTeachingSolver
    {
        public SolvePlan Solve(CubeState state, CancellationToken cancellationToken) => throw new InvalidOperationException("测试求解异常");
    }

    /// <summary>有意忽略首次取消并迟到返回，用来验证调用端必须检查版本而不只依赖协作取消。</summary>
    private sealed class DelayedFirstSolver : ICubeTeachingSolver, IDisposable
    {
        private int _calls;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(false);
        internal CancellationToken Token { get; private set; }
        public SolvePlan Solve(CubeState state, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                Token = cancellationToken;
                Started.SetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("测试未释放可控求解器。");
            }
            return new CubeTeachingSolver().Solve(state, CancellationToken.None);
        }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }
}

internal sealed class CubeTestTimeProvider : TimeProvider
{
    private long _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _timestamp;
    internal void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
}
