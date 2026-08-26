using Avalonia;
using ClassicGamePlugin.Features.SpiderSolitaire.Domain;
using ClassicGamePlugin.Features.SpiderSolitaire.ViewModels;
using ClassicGamePlugin.Features.SpiderSolitaire.Views;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SpiderSolitaireViewModelTests
{
    [Fact]
    public void 首次成功移动启动计时且撤销不回退耗时()
    {
        var timeProvider = new ManualTimeProvider();
        using var viewModel = CreateViewModel(timeProvider);
        var move = FindFirstLegalMove(viewModel);

        Assert.True(viewModel.Move(move.Source, move.Index, move.Destination));
        timeProvider.Advance(TimeSpan.FromSeconds(4.8));
        viewModel.RefreshElapsedTime();
        viewModel.UndoCommand.Execute(null);

        Assert.True(viewModel.IsTimerRunning);
        Assert.Equal(4, viewModel.ElapsedSeconds);
        Assert.Equal(2, viewModel.MoveCount);
        Assert.Equal(498, viewModel.Score);
    }

    [Fact]
    public void 点击先选择牌组再点击合法目标会完成一次移动()
    {
        using var viewModel = CreateViewModel();
        var move = FindFirstLegalMove(viewModel);

        viewModel.HandleColumnClick(move.Source, move.Index);
        Assert.Equal((move.Source, move.Index), viewModel.Selection);

        viewModel.HandleColumnClick(move.Destination, null);

        Assert.Null(viewModel.Selection);
        Assert.Equal(1, viewModel.MoveCount);
        Assert.Equal(SpiderGameState.Running, viewModel.GameState);
    }

    [Fact]
    public void 提示只更新说明与高亮而不计步扣分()
    {
        using var viewModel = CreateViewModel();

        viewModel.HintCommand.Execute(null);

        Assert.NotNull(viewModel.CurrentHint);
        Assert.StartsWith("提示：", viewModel.MessageText, StringComparison.Ordinal);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(500, viewModel.Score);
        Assert.False(viewModel.IsTimerRunning);
    }

    [Fact]
    public void 同局重开恢复牌序而新游戏和切换难度重置统计()
    {
        using var viewModel = CreateViewModel();
        var initialIds = GetTableauIds(viewModel);
        var move = FindFirstLegalMove(viewModel);
        viewModel.Move(move.Source, move.Index, move.Destination);

        viewModel.ReplaySameDealCommand.Execute(null);

        Assert.Equal(initialIds, GetTableauIds(viewModel));
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(0, viewModel.ElapsedSeconds);

        viewModel.SelectedDifficulty = viewModel.DifficultyOptions[1];

        Assert.Equal(SpiderSolitaireDifficulty.TwoSuits, viewModel.SelectedDifficulty.Definition);
        Assert.Equal(0, viewModel.MoveCount);
        Assert.Equal(5, viewModel.StockDealCount);
        Assert.Equal(SpiderGameState.Ready, viewModel.GameState);
    }

    [Fact]
    public void 动画计划按主动作翻牌收组胜利的稳定顺序生成()
    {
        var game = new SpiderSolitaireGame(
            SpiderSolitaireDifficulty.OneSuit,
            new IdentitySpiderCardShuffler());
        SpiderTestBoard.Clear(game);
        var columns = SpiderTestBoard.MutableColumns(game);
        for (var column = 0; column < 8; column++)
        {
            columns[column].AddRange(SpiderTestBoard.Run(column * 13));
        }

        columns[8].Add(SpiderTestBoard.Card(200, 9, faceUp: false));
        columns[8].Add(SpiderTestBoard.Card(201, 5));
        columns[9].Add(SpiderTestBoard.Card(202, 6));
        var transition = Assert.IsType<SpiderGameTransition>(game.Move(8, 1, 9));

        var plan = SpiderAnimationPlan.Create(transition);

        Assert.Equal(
            [
                SpiderAnimationStageKind.Move,
                SpiderAnimationStageKind.Flip,
                SpiderAnimationStageKind.CompleteRun,
                SpiderAnimationStageKind.Win,
            ],
            plan.Stages.Select(stage => stage.Kind));
    }

    [Fact]
    public void ViewModel发布动作动画且释放后停止计时并忽略操作()
    {
        var viewModel = CreateViewModel();
        SpiderAnimationPlan? requested = null;
        viewModel.AnimationRequested += (_, plan) => requested = plan;
        var move = FindFirstLegalMove(viewModel);
        viewModel.Move(move.Source, move.Index, move.Destination);

        Assert.NotNull(requested);
        Assert.True(viewModel.IsTimerRunning);
        var moveCount = viewModel.MoveCount;

        viewModel.Dispose();
        viewModel.Move(move.Source, move.Index, move.Destination);

        Assert.False(viewModel.IsTimerRunning);
        Assert.Equal(moveCount, viewModel.MoveCount);
    }

    [Theory]
    [InlineData(0, 0, 4, 4, false)]
    [InlineData(0, 0, 6, 0, true)]
    [InlineData(10, 10, 14, 14, false)]
    public void 拖拽阈值固定为六个DIP(
        double originX,
        double originY,
        double currentX,
        double currentY,
        bool expected)
    {
        Assert.Equal(expected, SpiderBoardControl.IsDragDistance(
            new Point(originX, originY),
            new Point(currentX, currentY)));
    }

    [Fact]
    public void 两个ViewModel的选择与棋局互不影响()
    {
        using var first = CreateViewModel();
        using var second = CreateViewModel();
        var move = FindFirstLegalMove(first);

        first.HandleColumnClick(move.Source, move.Index);

        Assert.NotNull(first.Selection);
        Assert.Null(second.Selection);
        Assert.Equal(0, second.MoveCount);
    }

    private static SpiderSolitaireViewModel CreateViewModel(TimeProvider? timeProvider = null) =>
        new(new IdentitySpiderCardShuffler(), timeProvider ?? new ManualTimeProvider(), false);

    private static (int Source, int Index, int Destination) FindFirstLegalMove(
        SpiderSolitaireViewModel viewModel)
    {
        for (var source = 0; source < 10; source++)
        {
            for (var index = 0; index < viewModel.CurrentSnapshot.Columns[source].Count; index++)
            {
                for (var destination = 0; destination < 10; destination++)
                {
                    if (viewModel.CanMove(source, index, destination))
                    {
                        return (source, index, destination);
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("测试牌序中没有合法移动。");
    }

    private static int[] GetTableauIds(SpiderSolitaireViewModel viewModel) =>
        viewModel.CurrentSnapshot.Columns.SelectMany(column => column).Select(card => card.Id).ToArray();
}
