using ClassicGamePlugin.Features.Game2048.Domain;
using ClassicGamePlugin.Features.Game2048.ViewModels;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Game2048ViewModelTests
{
    [Fact]
    public void 新局固定投影十六格双方块和零分()
    {
        var viewModel = new Game2048ViewModel(new FirstEmptyTileSpawnStrategy(2, 4));

        Assert.Equal(16, viewModel.BoardCells.Count);
        Assert.Equal(2, viewModel.BoardCells.Count(cell => cell.Value != 0));
        Assert.Equal(0, viewModel.Score);
        Assert.True(viewModel.CanMove);
        Assert.True(viewModel.AnimationsEnabled);
        Assert.False(viewModel.IsAwaitingContinue);
        Assert.Equal("合并相同数字，挑战 2048", viewModel.StatusText);
    }

    [Fact]
    public void 方向命令刷新棋盘与合并分数()
    {
        var game = CreateGame(
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine));
        var viewModel = new Game2048ViewModel(game);

        viewModel.MoveLeftCommand.Execute(null);

        Assert.Equal(4, viewModel.Score);
        Assert.Equal(4, viewModel.BoardCells[0].Value);
        Assert.Equal(2, viewModel.BoardCells[1].Value);
    }

    [Fact]
    public void 胜利状态禁用移动并由继续命令恢复()
    {
        var game = CreateGame(
            Game2048RulesTests.Board(
                [1024, 1024, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine));
        var viewModel = new Game2048ViewModel(game);

        viewModel.MoveLeftCommand.Execute(null);

        Assert.False(viewModel.CanMove);
        Assert.True(viewModel.IsAwaitingContinue);
        Assert.Contains("已达成 2048", viewModel.StatusText, StringComparison.Ordinal);

        viewModel.ContinueGameCommand.Execute(null);

        Assert.True(viewModel.CanMove);
        Assert.False(viewModel.IsAwaitingContinue);
        Assert.Equal("继续挑战中", viewModel.StatusText);
    }

    [Fact]
    public void 重新开始命令替换棋盘并清空分数阶段()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2, 4);
        var game = new Game2048Game(
            strategy,
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine),
            initialScore: 99,
            initialState: Game2048GameState.Continuing);
        var viewModel = new Game2048ViewModel(game);

        viewModel.RestartCommand.Execute(null);

        Assert.Equal(0, viewModel.Score);
        Assert.Equal(2, viewModel.BoardCells.Count(cell => cell.Value != 0));
        Assert.Equal(Game2048GameState.Playing, viewModel.GameState);
    }

    [Fact]
    public void 格子投影为数值颜色字号和无障碍说明()
    {
        var cell = new Game2048CellViewModel(2, 3);

        cell.Refresh(2048);

        Assert.Equal("2048", cell.DisplayText);
        Assert.Equal(27, cell.FontSize);
        Assert.NotNull(cell.Background);
        Assert.NotNull(cell.Foreground);
        Assert.Contains("第 3 行", cell.AccessibleText, StringComparison.Ordinal);
        Assert.Contains("数值 2048", cell.AccessibleText, StringComparison.Ordinal);
    }

    [Fact]
    public void 有订阅者且开启动画时先请求回放再由完成回调刷新棋盘()
    {
        var viewModel = new Game2048ViewModel(CreateGame(
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine)));
        Game2048AnimationPlan? requested = null;
        viewModel.AnimationRequested += (_, plan) => requested = plan;

        viewModel.MoveLeftCommand.Execute(null);

        Assert.NotNull(requested);
        Assert.True(viewModel.IsAnimationRunning);
        Assert.Equal([2, 2, 0, 0], viewModel.BoardCells.Take(4).Select(cell => cell.Value));
        Assert.Equal([4, 2, 0, 0], requested.Transition.After.Cells.Take(4));

        viewModel.CompleteAnimation();

        Assert.False(viewModel.IsAnimationRunning);
        Assert.Equal([4, 2, 0, 0], viewModel.BoardCells.Take(4).Select(cell => cell.Value));
    }

    [Fact]
    public void 动画期间只缓存最后方向并在完成后开始下一段动画()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2, 2);
        var viewModel = new Game2048ViewModel(new Game2048Game(
            strategy,
            Game2048RulesTests.Board(
                [2, 0, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine)));
        var plans = new List<Game2048AnimationPlan>();
        viewModel.AnimationRequested += (_, plan) => plans.Add(plan);

        viewModel.MoveRightCommand.Execute(null);
        viewModel.MoveLeftCommand.Execute(null);
        viewModel.MoveDownCommand.Execute(null);

        Assert.Equal(Game2048Direction.Down, viewModel.QueuedDirection);

        viewModel.CompleteAnimation();

        Assert.Equal(2, plans.Count);
        Assert.Equal(Game2048Direction.Down, plans[1].Transition.Direction);
        Assert.True(viewModel.IsAnimationRunning);
        Assert.Null(viewModel.QueuedDirection);
    }

    [Fact]
    public void 关闭动画时移动立即刷新且不会发出回放请求()
    {
        var viewModel = new Game2048ViewModel(CreateGame(
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine)))
        {
            AnimationsEnabled = false,
        };
        var requestCount = 0;
        viewModel.AnimationRequested += (_, _) => requestCount++;

        viewModel.MoveLeftCommand.Execute(null);

        Assert.Equal(0, requestCount);
        Assert.False(viewModel.IsAnimationRunning);
        Assert.Equal(4, viewModel.BoardCells[0].Value);
    }

    [Fact]
    public void 播放中关闭动画会落定当前状态并无动画执行缓存方向()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2, 2);
        var viewModel = new Game2048ViewModel(new Game2048Game(
            strategy,
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine)));
        var requestCount = 0;
        var cancellationCount = 0;
        viewModel.AnimationRequested += (_, _) => requestCount++;
        viewModel.AnimationCancellationRequested += (_, _) =>
        {
            cancellationCount++;
            viewModel.CompleteAnimation();
        };

        viewModel.MoveLeftCommand.Execute(null);
        viewModel.MoveRightCommand.Execute(null);
        viewModel.AnimationsEnabled = false;

        Assert.Equal(1, requestCount);
        Assert.Equal(1, cancellationCount);
        Assert.False(viewModel.IsAnimationRunning);
        Assert.Null(viewModel.QueuedDirection);
        Assert.Equal([2, 0, 4, 2], viewModel.BoardCells.Take(4).Select(cell => cell.Value));
    }

    [Fact]
    public void 重开取消动画清空缓存且保持动画开关()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2, 2, 4);
        var viewModel = new Game2048ViewModel(new Game2048Game(
            strategy,
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine)));
        var cancellationCount = 0;
        viewModel.AnimationRequested += (_, _) => { };
        viewModel.AnimationCancellationRequested += (_, _) => cancellationCount++;

        viewModel.MoveLeftCommand.Execute(null);
        viewModel.MoveRightCommand.Execute(null);
        viewModel.RestartCommand.Execute(null);

        Assert.Equal(1, cancellationCount);
        Assert.False(viewModel.IsAnimationRunning);
        Assert.Null(viewModel.QueuedDirection);
        Assert.True(viewModel.AnimationsEnabled);
        Assert.Equal(0, viewModel.Score);
        Assert.Equal(2, viewModel.BoardCells.Count(cell => cell.HasValue));
    }

    [Fact]
    public void 胜利动画完成后丢弃已缓存方向()
    {
        var viewModel = new Game2048ViewModel(CreateGame(
            Game2048RulesTests.Board(
                [1024, 1024, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine)));
        var requestCount = 0;
        viewModel.AnimationRequested += (_, _) => requestCount++;

        viewModel.MoveLeftCommand.Execute(null);
        viewModel.MoveRightCommand.Execute(null);
        viewModel.CompleteAnimation();

        Assert.Equal(1, requestCount);
        Assert.False(viewModel.IsAnimationRunning);
        Assert.Null(viewModel.QueuedDirection);
        Assert.Equal(Game2048GameState.WonAwaitingContinue, viewModel.GameState);
        Assert.False(viewModel.CanMove);
    }

    private static Game2048Game CreateGame(IReadOnlyList<int> cells) =>
        new(new FirstEmptyTileSpawnStrategy(), cells);
}
