using ClassicGamePlugin.Features.FreeCell.Domain;
using ClassicGamePlugin.Features.FreeCell.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class FreeCellViewModelTests
{
    [Fact]
    public async Task 编号初始化成功后原子替换牌局并重置统计()
    {
        var provider = new FixedFreeCellDealProvider(FreeCellTestData.Deal());
        using var viewModel = CreateViewModel(provider);

        await viewModel.InitializeAsync(7654321, CancellationToken.None);

        Assert.Equal(7654321, viewModel.DealNumber);
        Assert.Equal("7654321", viewModel.DealNumberText);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.False(viewModel.IsGenerating);
        Assert.Contains("已载入可解牌局", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 成功移动启动计时且撤销恢复步数但不倒退耗时()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(
            new FixedFreeCellDealProvider(FreeCellTestData.Deal()),
            timeProvider);
        await viewModel.InitializeAsync(42, CancellationToken.None);
        var move = FindMove(viewModel.CurrentSnapshot);

        Assert.True(viewModel.Move(move));
        timeProvider.Advance(TimeSpan.FromSeconds(5.8));
        viewModel.RefreshElapsedTime();
        viewModel.UndoCommand.Execute(null);

        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(5, viewModel.ElapsedSeconds);
        Assert.Contains("撤销", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 动画开启时发布纯计划关闭后直接显示最终状态()
    {
        using var viewModel = CreateViewModel(new FixedFreeCellDealProvider(FreeCellTestData.Deal()));
        await viewModel.InitializeAsync(42, CancellationToken.None);
        var plans = new List<FreeCellAnimationPlan>();
        viewModel.AnimationRequested += (_, plan) => plans.Add(plan);

        Assert.True(viewModel.Move(FindMove(viewModel.CurrentSnapshot)));
        viewModel.AreAnimationsEnabled = false;
        Assert.True(viewModel.Move(FindMove(viewModel.CurrentSnapshot)));

        Assert.Single(plans);
        Assert.False(viewModel.IsAnimationRunning);
        Assert.Equal(2, viewModel.MoveCount);
    }

    [Fact]
    public async Task 求解提示只高亮解路径第一步而不修改棋局()
    {
        var solver = new ScriptedFreeCellSolver(FreeCellSolveStatus.Solved);
        using var viewModel = new FreeCellViewModel(
            new FixedFreeCellDealProvider(FreeCellTestData.Deal()),
            solver,
            new ManualTimeProvider(),
            false);
        await viewModel.InitializeAsync(42, CancellationToken.None);
        var before = viewModel.CurrentSnapshot;

        viewModel.HintCommand.Execute(null);
        await Assert.IsAssignableFrom<Task>(viewModel.PendingHintTask);

        Assert.NotNull(viewModel.CurrentHint);
        Assert.Same(before, viewModel.CurrentSnapshot);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Contains("可通向胜利", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FreeCellSolveStatus.Unsolvable, "无解")]
    [InlineData(FreeCellSolveStatus.NodeLimitReached, "搜索上限")]
    internal async Task 求解提示区分无解与节点上限(FreeCellSolveStatus status, string expected)
    {
        using var viewModel = new FreeCellViewModel(
            new FixedFreeCellDealProvider(FreeCellTestData.Deal()),
            new ScriptedFreeCellSolver(status),
            new ManualTimeProvider(),
            false);
        await viewModel.InitializeAsync(42, CancellationToken.None);

        viewModel.HintCommand.Execute(null);
        await Assert.IsAssignableFrom<Task>(viewModel.PendingHintTask);

        Assert.Null(viewModel.CurrentHint);
        Assert.Contains(expected, viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 生成失败保留当前牌局且重新开放交互()
    {
        var provider = new FixedFreeCellDealProvider(FreeCellTestData.Deal())
        {
            Exception = new InvalidOperationException("限定候选均未能证明可解"),
        };
        using var viewModel = CreateViewModel(provider);
        var before = viewModel.CurrentSnapshot;

        await viewModel.InitializeAsync(99, CancellationToken.None);

        Assert.Same(before, viewModel.CurrentSnapshot);
        Assert.True(viewModel.CanInteract);
        Assert.False(viewModel.IsGenerating);
        Assert.Contains("未能证明", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 取消生成会保留原局并重新开放交互()
    {
        using var viewModel = CreateViewModel(new BlockingFreeCellDealProvider());
        var before = viewModel.CurrentSnapshot;
        var loading = viewModel.InitializeAsync(123, CancellationToken.None);
        Assert.True(viewModel.IsGenerating);

        viewModel.CancelGenerationCommand.Execute(null);
        await loading;

        Assert.Same(before, viewModel.CurrentSnapshot);
        Assert.True(viewModel.CanInteract);
        Assert.Contains("已取消生成", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 后发编号完成后旧生成结果不能覆盖新牌局()
    {
        var provider = new QueuedFreeCellDealProvider();
        using var viewModel = CreateViewModel(provider);
        var firstLoad = viewModel.InitializeAsync(10, CancellationToken.None);
        var secondLoad = viewModel.InitializeAsync(20, CancellationToken.None);
        Assert.Equal(2, provider.Requests.Count);

        provider.Requests[1].Completion.SetResult(FreeCellTestData.Deal(20));
        await secondLoad;
        provider.Requests[0].Completion.SetResult(FreeCellTestData.Deal(10));
        await firstLoad;

        Assert.Equal(20, viewModel.DealNumber);
        Assert.Equal("20", viewModel.DealNumberText);
    }

    [Fact]
    public async Task 同局重开恢复已接受牌序并清空计时选择和历史()
    {
        using var viewModel = CreateViewModel(new FixedFreeCellDealProvider(FreeCellTestData.Deal()));
        await viewModel.InitializeAsync(42, CancellationToken.None);
        var initial = viewModel.CurrentSnapshot.Tableaus.SelectMany(column => column).Select(card => card.Id).ToArray();
        Assert.True(viewModel.Move(FindMove(viewModel.CurrentSnapshot)));

        viewModel.ReplaySameDealCommand.Execute(null);

        Assert.Equal(initial, viewModel.CurrentSnapshot.Tableaus.SelectMany(column => column).Select(card => card.Id));
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(0, viewModel.ElapsedSeconds);
    }

    [Fact]
    public async Task 释放会停止计时取消后台入口并保持多实例隔离()
    {
        var first = CreateViewModel(new FixedFreeCellDealProvider(FreeCellTestData.Deal()));
        using var second = CreateViewModel(new FixedFreeCellDealProvider(FreeCellTestData.Deal()));
        await first.InitializeAsync(1, CancellationToken.None);
        await second.InitializeAsync(2, CancellationToken.None);
        Assert.True(first.Move(FindMove(first.CurrentSnapshot)));

        first.Dispose();

        Assert.False(first.CanInteract);
        Assert.Equal(0, second.MoveCount);
        Assert.Equal(2, second.DealNumber);
    }

    private static FreeCellViewModel CreateViewModel(
        IFreeCellDealProvider provider,
        TimeProvider? timeProvider = null) =>
        new(provider, new ScriptedFreeCellSolver(FreeCellSolveStatus.Solved),
            timeProvider ?? new ManualTimeProvider(), false);

    private static FreeCellMove FindMove(FreeCellSnapshot snapshot) =>
        FreeCellRules.EnumerateLegalMoves(snapshot, reduceSymmetricDestinations: false)
            .First(move => move.Destination.Kind != FreeCellLocationKind.Foundation);
}
