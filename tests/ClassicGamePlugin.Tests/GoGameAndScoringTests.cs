using ClassicGamePlugin.Features.Go.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class GoGameAndScoringTests
{
    [Fact]
    public void 连续两次停一手进入数子且停手本身不受同形限制()
    {
        var game = new GoGame();

        Assert.True(game.Pass());
        Assert.Equal(GoGameState.Playing, game.State);
        Assert.Equal(1, game.ConsecutivePasses);
        Assert.Equal(GoStone.White, game.CurrentPlayer);

        Assert.True(game.Pass());
        Assert.Equal(GoGameState.Scoring, game.State);
        Assert.Equal(2, game.ConsecutivePasses);
        Assert.Equal(GoStone.Black, game.CurrentPlayer);
    }

    [Fact]
    public void 停一手之后合法落子会清零连续停手数()
    {
        var game = new GoGame();
        game.Pass();

        var result = game.PlaceStone(new GoPosition(3, 3));

        Assert.NotNull(result);
        Assert.Equal(0, game.ConsecutivePasses);
        Assert.Equal(GoGameState.Playing, game.State);
    }

    [Fact]
    public void 数子阶段点击棋子会整体标记和取消相连棋组()
    {
        var board = GoRulesTests.Board(
            (new GoPosition(3, 3), GoStone.Black),
            (new GoPosition(3, 4), GoStone.Black));
        var snapshot = new GoGameSnapshot(
            board, GoStone.Black, GoGameState.Scoring, 2, 4, 2, 0, 0, new GoPosition(3, 4));
        var game = new GoGame(snapshot);

        Assert.True(game.ToggleDeadGroup(new GoPosition(3, 3)));
        Assert.All(
            new[] { new GoPosition(3, 3), new GoPosition(3, 4) },
            position => Assert.True(game.CreateSnapshot().IsMarkedDead(position)));

        Assert.True(game.ToggleDeadGroup(new GoPosition(3, 4)));
        Assert.Empty(game.CreateSnapshot().GetDeadStones());
        Assert.False(game.ToggleDeadGroup(new GoPosition(5, 5)));
    }

    [Fact]
    public void 恢复对局会清除死子和连续停手并保留下一个自然回合()
    {
        var board = GoRulesTests.Board((new GoPosition(3, 3), GoStone.Black));
        var snapshot = new GoGameSnapshot(
            board,
            GoStone.White,
            GoGameState.Scoring,
            1,
            3,
            2,
            0,
            0,
            new GoPosition(3, 3),
            [new GoPosition(3, 3)]);
        var game = new GoGame(snapshot);

        Assert.True(game.ResumePlay());

        Assert.Equal(GoGameState.Playing, game.State);
        Assert.Equal(GoStone.White, game.CurrentPlayer);
        Assert.Equal(0, game.ConsecutivePasses);
        Assert.Empty(game.CreateSnapshot().GetDeadStones());
    }

    [Fact]
    public void 中国数子把单色围空归属该方并把双方接触区域记为中立()
    {
        var board = GoRulesTests.Board(
            (new GoPosition(1, 2), GoStone.Black),
            (new GoPosition(2, 1), GoStone.Black),
            (new GoPosition(2, 3), GoStone.Black),
            (new GoPosition(3, 2), GoStone.Black),
            (new GoPosition(18, 18), GoStone.White));
        var snapshot = new GoGameSnapshot(
            board, GoStone.Black, GoGameState.Scoring, 5, 7, 2, 0, 0, null);

        var score = GoScorer.Calculate(snapshot);

        Assert.Equal(4, score.BlackStones);
        Assert.Equal(1, score.BlackTerritory);
        Assert.Equal(1, score.WhiteStones);
        Assert.Equal(0, score.WhiteTerritory);
        Assert.Equal(355, score.NeutralPoints);
        Assert.Equal(5, score.BlackScore);
        Assert.Equal(8.5, score.WhiteScore);
        Assert.Equal(GoStone.Black, score.TerritoryOwners[new GoPosition(2, 2)]);
    }

    [Fact]
    public void 标记死子会先从棋盘移除再参与围空与活子计数()
    {
        var board = GoRulesTests.Board(
            (new GoPosition(1, 2), GoStone.Black),
            (new GoPosition(2, 1), GoStone.Black),
            (new GoPosition(2, 3), GoStone.Black),
            (new GoPosition(3, 2), GoStone.Black),
            (new GoPosition(2, 2), GoStone.White),
            (new GoPosition(18, 18), GoStone.White));
        var snapshot = new GoGameSnapshot(
            board,
            GoStone.Black,
            GoGameState.Scoring,
            6,
            8,
            2,
            0,
            0,
            null,
            [new GoPosition(2, 2)]);

        var score = GoScorer.Calculate(snapshot);

        Assert.Equal(1, score.WhiteStones);
        Assert.Equal(1, score.BlackTerritory);
        Assert.Equal(GoStone.Black, score.TerritoryOwners[new GoPosition(2, 2)]);
    }

    [Fact]
    public void 确认数子冻结分数胜者和七点五目贴目且撤销可回到数子阶段()
    {
        var game = new GoGame();
        game.Pass();
        game.Pass();

        Assert.True(game.ConfirmScore());
        Assert.Equal(GoGameState.Finished, game.State);
        Assert.Equal(GoFinishReason.Score, game.FinishReason);
        Assert.Equal(GoStone.White, game.Winner);
        Assert.Equal(7.5, game.Score!.WhiteScore);

        Assert.True(game.Undo());
        Assert.Equal(GoGameState.Scoring, game.State);
        Assert.Null(game.Score);
    }

    [Fact]
    public void 当前方认输立即判对方获胜且撤销恢复原回合()
    {
        var game = new GoGame();

        Assert.True(game.Resign());
        Assert.Equal(GoGameState.Finished, game.State);
        Assert.Equal(GoFinishReason.Resignation, game.FinishReason);
        Assert.Equal(GoStone.White, game.Winner);

        Assert.True(game.Undo());
        Assert.Equal(GoGameState.Ready, game.State);
        Assert.Equal(GoStone.Black, game.CurrentPlayer);
    }

    [Fact]
    public void 不限步撤销精确恢复落子停手标死与历史分支()
    {
        var game = new GoGame();
        game.PlaceStone(new GoPosition(3, 3));
        game.Pass();
        game.Pass();
        game.ToggleDeadGroup(new GoPosition(3, 3));

        Assert.True(game.CreateSnapshot().IsMarkedDead(new GoPosition(3, 3)));
        Assert.True(game.Undo());
        Assert.False(game.CreateSnapshot().IsMarkedDead(new GoPosition(3, 3)));
        Assert.True(game.Undo());
        Assert.Equal(GoGameState.Playing, game.State);
        Assert.Equal(1, game.ConsecutivePasses);
        Assert.True(game.Undo());
        Assert.Equal(0, game.ConsecutivePasses);
        Assert.True(game.Undo());
        Assert.Equal(GoGameState.Ready, game.State);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void 重新开始清空棋盘计数结果和撤销历史()
    {
        var game = new GoGame();
        game.PlaceStone(new GoPosition(3, 3));
        game.Resign();

        game.StartNewGame();

        Assert.Equal(GoGameState.Ready, game.State);
        Assert.Equal(0, game.MoveCount);
        Assert.Equal(0, game.BlackCaptures);
        Assert.Null(game.Winner);
        Assert.False(game.CanUndo);
        Assert.All(game.CreateSnapshot().CopyBoard(), stone => Assert.Null(stone));
    }
}
