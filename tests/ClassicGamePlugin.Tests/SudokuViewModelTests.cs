using ClassicGamePlugin.Features.Sudoku.Domain;
using ClassicGamePlugin.Features.Sudoku.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SudokuViewModelTests
{
    [Fact]
    public void 默认简单题且首次有效操作才启动计时()
    {
        var time = new ManualTimeProvider();
        using var viewModel = new SudokuViewModel(new StubSudokuPuzzleProvider(), time, false);

        Assert.Equal("简单", viewModel.DifficultyText);
        Assert.False(viewModel.IsTimerRunning);
        viewModel.EnterNumberCommand.Execute(4);
        time.Advance(TimeSpan.FromSeconds(3.8));
        viewModel.RefreshElapsedTime();

        Assert.True(viewModel.IsTimerRunning);
        Assert.Equal(3, viewModel.ElapsedSeconds);
    }

    [Fact]
    public void 难度切换立即创建题库新局且重新开始保留题目()
    {
        var provider = new StubSudokuPuzzleProvider();
        using var viewModel = new SudokuViewModel(provider, new ManualTimeProvider(), false);
        var medium = viewModel.DifficultyOptions.Single(option => option.DisplayName == "中等");

        viewModel.SelectedDifficulty = medium;
        var mediumId = viewModel.CurrentPuzzleId;
        viewModel.RestartCommand.Execute(null);

        Assert.Equal("中等", viewModel.DifficultyText);
        Assert.Equal(mediumId, viewModel.CurrentPuzzleId);
        Assert.Equal(2, provider.BuiltInCallCount);
    }

    [Fact]
    public void 新游戏排除当前题目并清空历史计时与笔记模式()
    {
        var time = new ManualTimeProvider();
        using var viewModel = new SudokuViewModel(new StubSudokuPuzzleProvider(), time, false);
        var firstId = viewModel.CurrentPuzzleId;
        viewModel.IsNotesMode = true;
        viewModel.EnterNumberCommand.Execute(4);
        time.Advance(TimeSpan.FromSeconds(5));

        viewModel.NewGameCommand.Execute(null);

        Assert.NotEqual(firstId, viewModel.CurrentPuzzleId);
        Assert.False(viewModel.IsNotesMode);
        Assert.False(viewModel.CanUndo);
        Assert.Equal(0, viewModel.ElapsedSeconds);
    }

    [Fact]
    public void 动画默认开启且关闭会发出取消通知()
    {
        using var viewModel = new SudokuViewModel(
            new StubSudokuPuzzleProvider(),
            new ManualTimeProvider(),
            false);
        var cancellations = 0;
        viewModel.AnimationCancellationRequested += (_, _) => cancellations++;

        viewModel.AnimationsEnabled = false;

        Assert.Equal(1, cancellations);
    }

    [Fact]
    public async Task 后台生成成功后原子替换为生成题目()
    {
        using var viewModel = new SudokuViewModel(
            new StubSudokuPuzzleProvider(),
            new ManualTimeProvider(),
            false);

        await viewModel.GeneratePuzzleCommand.ExecuteAsync(null);

        Assert.Equal(SudokuPuzzleSource.Generated, viewModel.CurrentPuzzleSource);
        Assert.Equal("运行时生成", viewModel.SourceText);
        Assert.False(viewModel.IsGenerating);
    }

    [Fact]
    public async Task 生成失败保留当前局与当前进度()
    {
        var provider = new StubSudokuPuzzleProvider((_, _) =>
            Task.FromException<SudokuPuzzle>(new InvalidOperationException("测试失败")));
        using var viewModel = new SudokuViewModel(provider, new ManualTimeProvider(), false);
        var originalId = viewModel.CurrentPuzzleId;
        viewModel.EnterNumberCommand.Execute(4);

        await viewModel.GeneratePuzzleCommand.ExecuteAsync(null);

        Assert.Equal(originalId, viewModel.CurrentPuzzleId);
        Assert.Equal(4, viewModel.BoardCells[2].Value);
        Assert.Contains("生成失败", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 生成取消保留当前局并结束生成状态()
    {
        var provider = new StubSudokuPuzzleProvider(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return SudokuTestPuzzles.Create(source: SudokuPuzzleSource.Generated);
        });
        using var viewModel = new SudokuViewModel(provider, new ManualTimeProvider(), false);
        var originalId = viewModel.CurrentPuzzleId;
        var execution = viewModel.GeneratePuzzleCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsGenerating);

        viewModel.GeneratePuzzleCancelCommand.Execute(null);
        await execution;

        Assert.Equal(originalId, viewModel.CurrentPuzzleId);
        Assert.False(viewModel.IsGenerating);
        Assert.Contains("取消", viewModel.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public void 选择移动限制在棋盘内且刷新关联格投影()
    {
        using var viewModel = new SudokuViewModel(
            new StubSudokuPuzzleProvider(),
            new ManualTimeProvider(),
            false);
        viewModel.SelectCell(new SudokuPosition(0, 0));

        viewModel.MoveSelection(-1, -1);

        Assert.Equal(new SudokuPosition(0, 0), viewModel.SelectedPosition);
        Assert.True(viewModel.BoardCells[1].IsRelated);
        Assert.True(viewModel.BoardCells[9].IsRelated);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 50 && !predicate(); attempt++)
        {
            await Task.Yield();
        }

        Assert.True(predicate());
    }
}
