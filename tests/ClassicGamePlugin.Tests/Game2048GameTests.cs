using ClassicGamePlugin.Features.Game2048.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Game2048GameTests
{
    [Fact]
    public void 新局生成两个方块且只允许经典数值()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2, 4);

        var game = new Game2048Game(strategy);

        Assert.Equal(2, strategy.CallCount);
        Assert.Equal(2, game.Cells.Count(value => value != 0));
        Assert.Equal(1, game.Cells.Count(value => value == 2));
        Assert.Equal(1, game.Cells.Count(value => value == 4));
        Assert.Equal(0, game.Score);
        Assert.Equal(Game2048GameState.Playing, game.State);
    }

    [Fact]
    public void 随机策略均匀索引空格并覆盖二与四的概率分界()
    {
        var positions = new[] { new Game2048Position(0, 0), new Game2048Position(3, 3) };
        var fourStrategy = new RandomTileSpawnStrategy(new SequenceRandom(1, 0));
        var twoStrategy = new RandomTileSpawnStrategy(new SequenceRandom(0, 9));

        Assert.Equal(
            new Game2048TileSpawn(new Game2048Position(3, 3), 4),
            fourStrategy.CreateSpawn(positions));
        Assert.Equal(
            new Game2048TileSpawn(new Game2048Position(0, 0), 2),
            twoStrategy.CreateSpawn(positions));
    }

    [Fact]
    public void 有效移动提交合并分数并只生成一个方块()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2);
        var game = CreateGame(
            Game2048RulesTests.Board(
                [2, 2, 4, 4],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine),
            strategy);

        var transition = Assert.IsType<Game2048Transition>(game.Move(Game2048Direction.Left));

        Assert.Equal(1, strategy.CallCount);
        Assert.Equal(12, game.Score);
        Assert.Equal([4, 8, 2, 0], game.Cells.Take(4));
        Assert.Equal(new Game2048Position(0, 2), transition.SpawnedTile.Position);
        Assert.Equal(2, transition.SpawnedTile.Value);
        Assert.Equal([2, 2, 4, 4], transition.Before.Cells.Take(4));
        Assert.Equal([4, 8, 2, 0], transition.After.Cells.Take(4));
    }

    [Fact]
    public void 无效移动不生成方块不加分也不改变状态()
    {
        var strategy = new FirstEmptyTileSpawnStrategy();
        var board = Game2048RulesTests.Board(
            [2, 4, 8, 16],
            Game2048RulesTests.EmptyLine,
            Game2048RulesTests.EmptyLine,
            Game2048RulesTests.EmptyLine);
        var game = CreateGame(board, strategy, initialScore: 30);

        Assert.Null(game.Move(Game2048Direction.Left));

        Assert.Equal(0, strategy.CallCount);
        Assert.Equal(30, game.Score);
        Assert.Equal(Game2048GameState.Playing, game.State);
        Assert.Equal(board, game.Cells);
    }

    [Theory]
    [InlineData(-1, 0, 2)]
    [InlineData(0, 3, 2)]
    [InlineData(0, 0, 8)]
    public void 非法生成结果拒绝整步且真实棋盘保持不变(int row, int column, int value)
    {
        var board = Game2048RulesTests.Board(
            [2, 0, 0, 0],
            Game2048RulesTests.EmptyLine,
            Game2048RulesTests.EmptyLine,
            Game2048RulesTests.EmptyLine);
        var strategy = new QueuedTileSpawnStrategy(
            new Game2048TileSpawn(new Game2048Position(row, column), value));
        var game = CreateGame(board, strategy, initialScore: 7);

        Assert.Throws<InvalidOperationException>(() => game.Move(Game2048Direction.Right));

        Assert.Equal(board, game.Cells);
        Assert.Equal(7, game.Score);
        Assert.Equal(Game2048GameState.Playing, game.State);
    }

    [Fact]
    public void 首次合成二零四八后暂停输入并可继续挑战()
    {
        var game = CreateGame(
            Game2048RulesTests.Board(
                [1024, 1024, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine),
            new FirstEmptyTileSpawnStrategy(2));

        Assert.NotNull(game.Move(Game2048Direction.Left));
        Assert.Equal(Game2048GameState.WonAwaitingContinue, game.State);
        Assert.Equal(2048, game.Score);
        var wonBoard = game.Cells.ToArray();
        Assert.Null(game.Move(Game2048Direction.Right));
        Assert.Equal(wonBoard, game.Cells);

        Assert.True(game.ContinueAfterWin());
        Assert.Equal(Game2048GameState.Continuing, game.State);
        Assert.NotNull(game.Move(Game2048Direction.Right));
        Assert.NotEqual(Game2048GameState.WonAwaitingContinue, game.State);
    }

    [Fact]
    public void 有效移动补满无解棋盘后立即结束()
    {
        var game = CreateGame(
            Game2048RulesTests.Board(
                [2, 4, 2, 0],
                [2, 4, 2, 4],
                [4, 2, 4, 2],
                [2, 4, 2, 4]),
            new QueuedTileSpawnStrategy(
                new Game2048TileSpawn(new Game2048Position(0, 0), 4)));

        Assert.NotNull(game.Move(Game2048Direction.Right));

        Assert.Equal(Game2048GameState.Lost, game.State);
        Assert.Equal(
            Game2048RulesTests.Board(
                [4, 2, 4, 2],
                [2, 4, 2, 4],
                [4, 2, 4, 2],
                [2, 4, 2, 4]),
            game.Cells);
        Assert.Null(game.Move(Game2048Direction.Left));
    }

    [Fact]
    public void 胜利确认时若已无解则继续操作进入结束状态()
    {
        var game = new Game2048Game(
            new FirstEmptyTileSpawnStrategy(),
            Game2048RulesTests.Checkerboard,
            initialState: Game2048GameState.WonAwaitingContinue);

        Assert.True(game.ContinueAfterWin());
        Assert.Equal(Game2048GameState.Lost, game.State);
    }

    [Fact]
    public void 重新开始清空分数状态并重新生成两个方块()
    {
        var strategy = new FirstEmptyTileSpawnStrategy(2, 4);
        var game = CreateGame(
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine),
            strategy,
            initialScore: 88,
            initialState: Game2048GameState.Continuing);

        game.StartNewGame();

        Assert.Equal(2, game.Cells.Count(value => value != 0));
        Assert.Equal(0, game.Score);
        Assert.Equal(Game2048GameState.Playing, game.State);
    }

    [Fact]
    public void 两个游戏实例的移动分数和棋盘完全隔离()
    {
        var first = CreateGame(
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine),
            new FirstEmptyTileSpawnStrategy());
        var secondBoard = Game2048RulesTests.Board(
            [2, 2, 0, 0],
            Game2048RulesTests.EmptyLine,
            Game2048RulesTests.EmptyLine,
            Game2048RulesTests.EmptyLine);
        var second = CreateGame(secondBoard, new FirstEmptyTileSpawnStrategy());

        first.Move(Game2048Direction.Left);

        Assert.Equal(4, first.Score);
        Assert.Equal(0, second.Score);
        Assert.Equal(secondBoard, second.Cells);
    }

    [Fact]
    public void Transition快照与后续真实棋盘修改保持隔离()
    {
        var game = CreateGame(
            Game2048RulesTests.Board(
                [2, 2, 0, 0],
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine,
                Game2048RulesTests.EmptyLine),
            new FirstEmptyTileSpawnStrategy(2, 2));
        var transition = Assert.IsType<Game2048Transition>(game.Move(Game2048Direction.Left));
        var after = transition.After.Cells.ToArray();

        game.Move(Game2048Direction.Right);

        Assert.Equal([2, 2, 0, 0], transition.Before.Cells.Take(4));
        Assert.Equal(after, transition.After.Cells);
        Assert.NotEqual(transition.After.Cells, game.Cells);
    }

    private static Game2048Game CreateGame(
        IReadOnlyList<int> cells,
        ITileSpawnStrategy strategy,
        int initialScore = 0,
        Game2048GameState initialState = Game2048GameState.Playing) =>
        new(strategy, cells, initialScore, initialState);
}
