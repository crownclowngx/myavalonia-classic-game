using ClassicGamePlugin.Features.ChineseCheckers.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class ChineseCheckersRulesTests
{
    [Fact]
    public void 初始棋盘包含一百二十一孔对角十子且蓝方先行()
    {
        var snapshot = ChineseCheckersRules.CreateInitialSnapshot();

        Assert.Equal(121, ChineseCheckersRules.AllPositions.Count);
        Assert.Equal(10, ChineseCheckersRules.BlueHome.Count);
        Assert.Equal(10, ChineseCheckersRules.RedHome.Count);
        Assert.Equal(10, ChineseCheckersRules.CountInHome(snapshot, ChineseCheckersSide.Blue));
        Assert.Equal(10, ChineseCheckersRules.CountInHome(snapshot, ChineseCheckersSide.Red));
        Assert.Equal(ChineseCheckersSide.Blue, snapshot.CurrentSide);
        Assert.NotEmpty(ChineseCheckersRules.GetLegalMoves(snapshot));
    }

    [Fact]
    public void 单步与隔任意颜色棋子的跳跃合法且不吃子()
    {
        var origin = new ChineseCheckersPosition(0, 0, 0);
        var middle = origin.Add(ChineseCheckersRules.Directions[0]);
        var landing = origin.Add(ChineseCheckersRules.Directions[0], 2);
        var snapshot = ChineseCheckersTestData.Snapshot(
            ChineseCheckersSide.Blue,
            (origin, ChineseCheckersSide.Blue),
            (middle, ChineseCheckersSide.Red));

        var moves = ChineseCheckersRules.GetLegalMoves(snapshot);
        var hop = Assert.Single(moves, move => move.To == landing);
        Assert.Equal(ChineseCheckersMoveKind.Hop, hop.Kind);
        var result = Assert.IsType<ChineseCheckersMoveResult>(
            ChineseCheckersRules.TryApplyMove(snapshot, origin, landing));
        Assert.Equal(ChineseCheckersSide.Red, result.After.GetPiece(middle));
        Assert.Null(result.After.GetPiece(origin));
        Assert.Equal(ChineseCheckersSide.Blue, result.After.GetPiece(landing));
        Assert.Contains(moves, move => move.Kind == ChineseCheckersMoveKind.Step);
    }

    [Fact]
    public void 连跳可转向并以固定方向BFS返回稳定最短路径()
    {
        var origin = new ChineseCheckersPosition(0, 0, 0);
        var firstMiddle = origin.Add(ChineseCheckersRules.Directions[0]);
        var firstLanding = origin.Add(ChineseCheckersRules.Directions[0], 2);
        var secondMiddle = firstLanding.Add(ChineseCheckersRules.Directions[2]);
        var destination = firstLanding.Add(ChineseCheckersRules.Directions[2], 2);
        var snapshot = ChineseCheckersTestData.Snapshot(
            ChineseCheckersSide.Blue,
            (origin, ChineseCheckersSide.Blue),
            (firstMiddle, ChineseCheckersSide.Blue),
            (secondMiddle, ChineseCheckersSide.Red));

        var first = Assert.Single(ChineseCheckersRules.GetLegalMoves(snapshot), move => move.To == destination);
        var second = Assert.Single(ChineseCheckersRules.GetLegalMoves(snapshot), move => move.To == destination);

        Assert.Equal([origin, firstLanding, destination], first.Path);
        Assert.Equal(first.Path, second.Path);
        Assert.Equal(first.Path.Count, first.Path.Distinct().Count());
    }

    [Fact]
    public void 进入目标营后所有后续合法终点都留在目标营()
    {
        var origin = ChineseCheckersRules.RedHome.First(position =>
            ChineseCheckersRules.Directions.Any(direction =>
                ChineseCheckersRules.TryGetIndex(position.Add(direction), out _) &&
                !ChineseCheckersRules.RedHome.Contains(position.Add(direction))));
        var snapshot = ChineseCheckersTestData.Snapshot(
            ChineseCheckersSide.Blue,
            (origin, ChineseCheckersSide.Blue));

        var moves = ChineseCheckersRules.GetRawLegalMoves(snapshot, ChineseCheckersSide.Blue);

        Assert.NotEmpty(moves);
        Assert.All(moves, move => Assert.Contains(move.To, ChineseCheckersRules.RedHome));
    }

    [Fact]
    public void 对手入营后存在撤营着法时当前方只能减少营内棋子()
    {
        var home = ChineseCheckersRules.BlueHome;
        var evacuee = home.First(position => ChineseCheckersRules.Directions.Any(direction =>
            ChineseCheckersRules.TryGetIndex(position.Add(direction), out _) && !home.Contains(position.Add(direction))));
        var intruder = home.First(position => position != evacuee);
        var center = new ChineseCheckersPosition(0, 0, 0);
        var snapshot = ChineseCheckersTestData.Snapshot(
            ChineseCheckersSide.Blue,
            (evacuee, ChineseCheckersSide.Blue),
            (intruder, ChineseCheckersSide.Red),
            (center, ChineseCheckersSide.Blue));

        var moves = ChineseCheckersRules.GetLegalMoves(snapshot);

        Assert.NotEmpty(moves);
        Assert.All(moves, move =>
        {
            Assert.Contains(move.From, home);
            Assert.DoesNotContain(move.To, home);
        });
    }

    [Fact]
    public void 填满目标营立即获胜且游戏快照可逐手撤销()
    {
        var target = ChineseCheckersRules.RedHome.First(position =>
            ChineseCheckersRules.Directions.Any(direction =>
                ChineseCheckersRules.TryGetIndex(position.Add(direction), out _) &&
                !ChineseCheckersRules.RedHome.Contains(position.Add(direction))));
        var source = ChineseCheckersRules.Directions
            .Select(direction => target.Add(direction))
            .First(position => ChineseCheckersRules.TryGetIndex(position, out _) &&
                !ChineseCheckersRules.RedHome.Contains(position));
        var pieces = ChineseCheckersRules.RedHome
            .Where(position => position != target)
            .Select(position => (position, ChineseCheckersSide.Blue))
            .Append((source, ChineseCheckersSide.Blue))
            .ToArray();
        var snapshot = ChineseCheckersTestData.Snapshot(ChineseCheckersSide.Blue, pieces);
        var game = new ChineseCheckersGame(snapshot);

        var result = Assert.IsType<ChineseCheckersMoveResult>(game.Move(source, target));

        Assert.Equal(ChineseCheckersGameState.Finished, result.After.State);
        Assert.Equal(ChineseCheckersTerminationReason.GoalFilled, result.After.TerminationReason);
        Assert.NotNull(game.Undo());
        Assert.Equal(ChineseCheckersGameState.Running, game.State);
        Assert.Equal(ChineseCheckersSide.Blue, game.Snapshot.GetPiece(source));
    }

    [Fact]
    public void 对手已经入营且当前方完全无法撤营时判堵塞胜利()
    {
        var source = ChineseCheckersRules.RedHome.First(position =>
            ChineseCheckersRules.Directions.Any(direction =>
                ChineseCheckersRules.RedHome.Contains(position.Add(direction))));
        var target = ChineseCheckersRules.Directions
            .Select(direction => source.Add(direction))
            .First(ChineseCheckersRules.RedHome.Contains);
        var pieces = ChineseCheckersRules.AllPositions
            .Where(position => position != target)
            .Select(position => (
                position,
                position == source
                    ? ChineseCheckersSide.Blue
                    : ChineseCheckersRules.RedHome.Contains(position)
                    ? ChineseCheckersSide.Red
                    : ChineseCheckersSide.Blue))
            .ToArray();
        var snapshot = ChineseCheckersTestData.Snapshot(ChineseCheckersSide.Blue, pieces);

        var result = Assert.IsType<ChineseCheckersMoveResult>(
            ChineseCheckersRules.TryApplyMove(snapshot, source, target));

        Assert.Equal(ChineseCheckersGameState.Finished, result.After.State);
        Assert.Equal(ChineseCheckersSide.Blue, result.After.Winner);
        Assert.Equal(ChineseCheckersTerminationReason.BlockedHome, result.After.TerminationReason);
    }

    [Fact]
    public void 非法移动不会改变快照或撤销历史()
    {
        var game = new ChineseCheckersGame();
        var before = game.Snapshot.CopyBoard();

        var result = game.Move(new ChineseCheckersPosition(0, 0, 0), new ChineseCheckersPosition(1, -1, 0));

        Assert.Null(result);
        Assert.Equal(before, game.Snapshot.CopyBoard());
        Assert.False(game.CanUndo);
    }
}
