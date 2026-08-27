using ClassicGamePlugin.Features.Match3.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Match3GameTests
{
    [Fact]
    public void 无效交换不扣步不计分也不改变棋盘()
    {
        var board = Match3Boards.Stable();
        var game = new Match3Game(new CyclingMatch3Random(), board);

        var transition = game.TrySwap(new Match3Position(7, 6), new Match3Position(7, 7));

        Assert.False(transition.IsAccepted);
        Assert.Equal(30, game.RemainingMoves);
        Assert.Equal(0, game.Score);
        Assert.Equal(board, game.Board);
    }

    [Fact]
    public void 有效交换完整解析后只扣一步并按连锁层级计分()
    {
        var game = new Match3Game(new CyclingMatch3Random(), Match3Boards.Stable());

        var transition = game.TrySwap(new Match3Position(0, 1), new Match3Position(1, 1));

        Assert.True(transition.IsAccepted);
        Assert.Equal(29, game.RemainingMoves);
        Assert.NotEmpty(transition.Steps);
        Assert.Equal(transition.Steps.Sum(step => step.ScoreDelta), transition.ScoreDelta);
        Assert.All(transition.Steps, step =>
            Assert.True(step.ScoreDelta >= step.CascadeLevel * 30));
        Assert.Equal(transition.ScoreDelta, game.Score);
    }

    [Fact]
    public void 补位随机源失败时整回合保持原子不提交()
    {
        var board = Match3Boards.Stable();
        var game = new Match3Game(new ThrowingMatch3Random(0), board);

        Assert.Throws<InvalidOperationException>(() =>
            game.TrySwap(new Match3Position(0, 1), new Match3Position(1, 1)));

        Assert.Equal(board, game.Board);
        Assert.Equal(0, game.Score);
        Assert.Equal(30, game.RemainingMoves);
        Assert.Equal(Match3GameState.Playing, game.State);
    }

    [Fact]
    public void 病态补位造成无休止匹配时由连锁上限拒绝且不提交()
    {
        var board = Match3Boards.Stable();
        var game = new Match3Game(new ConstantMatch3Random(0), board);

        var error = Assert.Throws<InvalidOperationException>(() =>
            game.TrySwap(new Match3Position(0, 1), new Match3Position(1, 1)));

        Assert.Contains("安全上限", error.Message, StringComparison.Ordinal);
        Assert.Equal(board, game.Board);
        Assert.Equal(0, game.Score);
        Assert.Equal(30, game.RemainingMoves);
    }

    [Fact]
    public void 达到目标分在完整回合后胜利且优先于步数耗尽()
    {
        var game = new Match3Game(
            new CyclingMatch3Random(),
            Match3Boards.Stable(),
            score: Match3Game.TargetScore - 10,
            remainingMoves: 1);

        game.TrySwap(new Match3Position(0, 1), new Match3Position(1, 1));

        Assert.Equal(Match3GameState.Won, game.State);
        Assert.Equal(0, game.RemainingMoves);
    }

    [Fact]
    public void 最后一步未达目标进入失败且不再接受交换()
    {
        var game = new Match3Game(
            new CyclingMatch3Random(),
            Match3Boards.Stable(),
            remainingMoves: 1);

        game.TrySwap(new Match3Position(0, 1), new Match3Position(1, 1));
        var rejected = game.TrySwap(new Match3Position(0, 0), new Match3Position(0, 1));

        Assert.Equal(Match3GameState.Lost, game.State);
        Assert.False(rejected.IsAccepted);
        Assert.Equal(0, game.RemainingMoves);
    }

    [Fact]
    public void 提示稳定返回第一组合法交换且新局重置状态()
    {
        var game = new Match3Game(new CyclingMatch3Random(), Match3Boards.Stable(), score: 200, remainingMoves: 8);

        Assert.True(game.TryGetHint(out var source, out var target));
        Assert.Equal(new Match3Position(0, 1), source);
        Assert.Equal(new Match3Position(1, 1), target);

        game.StartNewGame();

        Assert.Equal(0, game.Score);
        Assert.Equal(30, game.RemainingMoves);
        Assert.Equal(Match3GameState.Playing, game.State);
        Assert.False(Match3Rules.HasAnyMatch(game.Board));
        Assert.True(Match3Rules.TryFindFirstLegalSwap(game.Board, out _, out _));
    }

    [Fact]
    public void 多个对局实例棋盘分数和步数彼此隔离()
    {
        var first = new Match3Game(new CyclingMatch3Random(), Match3Boards.Stable());
        var second = new Match3Game(new CyclingMatch3Random(), Match3Boards.Stable());

        first.TrySwap(new Match3Position(0, 1), new Match3Position(1, 1));

        Assert.NotEqual(first.Score, second.Score);
        Assert.Equal(30, second.RemainingMoves);
        Assert.Equal(Match3Boards.Stable(), second.Board);
    }
}
