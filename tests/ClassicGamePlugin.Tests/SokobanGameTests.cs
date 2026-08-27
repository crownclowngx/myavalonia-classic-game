using ClassicGamePlugin.Features.Sokoban.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SokobanGameTests
{
    [Fact]
    public void 普通移动只更新玩家与移动数()
    {
        var game = CreateGame();

        var result = Assert.IsType<SokobanMoveResult>(game.Move(SokobanDirection.Right));

        Assert.Equal(new SokobanPosition(1, 3), game.Player);
        Assert.Equal(1, game.MoveCount);
        Assert.Equal(0, game.PushCount);
        Assert.False(result.PushedBox);
        Assert.True(game.CanUndo);
    }

    [Fact]
    public void 推动箱子原子更新玩家箱子与计数()
    {
        var game = CreateGame();

        var result = Assert.IsType<SokobanMoveResult>(game.Move(SokobanDirection.Down));

        Assert.True(result.PushedBox);
        Assert.Equal(new SokobanPosition(2, 2), result.BoxFrom);
        Assert.Equal(new SokobanPosition(3, 2), result.BoxTo);
        Assert.True(game.HasBox(new SokobanPosition(3, 2)));
        Assert.Equal(1, game.MoveCount);
        Assert.Equal(1, game.PushCount);
    }

    [Fact]
    public void 撞墙与推动两个箱子都不改变状态或历史()
    {
        var wallGame = CreateGame();
        var stacked = new SokobanGame(SokobanLevelParser.Parse(
            "stacked", "双箱阻挡", SokobanDifficulty.Beginner,
            "#####", "# @ #", "# $ #", "# $ #", "#.. #", "#####"));

        Assert.Null(wallGame.Move(SokobanDirection.Up));
        Assert.False(wallGame.CanUndo);
        Assert.Equal(0, wallGame.MoveCount);
        Assert.Null(stacked.Move(SokobanDirection.Down));
        Assert.Equal(0, stacked.MoveCount);
        Assert.Equal(0, stacked.PushCount);
        Assert.False(stacked.CanUndo);
    }

    [Fact]
    public void 箱子可以进入非目标死角并由撤销完整恢复()
    {
        var game = new SokobanGame(SokobanLevelParser.Parse(
            "dead-corner", "死角", SokobanDifficulty.Beginner,
            "#######", "#     #", "# @$  #", "#   . #", "#######"));

        Assert.NotNull(game.Move(SokobanDirection.Right));
        Assert.NotNull(game.Move(SokobanDirection.Right));
        Assert.True(game.HasBox(new SokobanPosition(2, 5)));
        Assert.False(game.IsCompleted);

        Assert.True(game.Undo());
        Assert.True(game.Undo());
        Assert.Equal(new SokobanPosition(2, 2), game.Player);
        Assert.True(game.HasBox(new SokobanPosition(2, 3)));
        Assert.Equal(0, game.MoveCount);
        Assert.Equal(0, game.PushCount);
    }

    [Fact]
    public void 完成后锁定移动但撤销会重新开放棋局()
    {
        var game = new SokobanGame(SokobanLevelCatalog.Levels[0]);

        Assert.NotNull(game.Move(SokobanDirection.Down));
        Assert.True(game.IsCompleted);
        Assert.Null(game.Move(SokobanDirection.Up));

        Assert.True(game.Undo());
        Assert.False(game.IsCompleted);
        Assert.Equal(0, game.MoveCount);
        Assert.Equal(0, game.PushCount);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void 重开清空全部动态状态且不同棋局实例互不影响()
    {
        var first = CreateGame();
        var second = CreateGame();
        Assert.NotNull(first.Move(SokobanDirection.Right));
        Assert.NotNull(first.Move(SokobanDirection.Down));

        first.Restart();

        Assert.Equal(new SokobanPosition(1, 2), first.Player);
        Assert.Equal(0, first.MoveCount);
        Assert.False(first.CanUndo);
        Assert.Equal(new SokobanPosition(1, 2), second.Player);
        Assert.Equal(0, second.MoveCount);
    }

    internal static SokobanGame CreateGame() => new(SokobanLevelParser.Parse(
        "test", "测试", SokobanDifficulty.Beginner,
        "#######", "# @   #", "# $ . #", "#     #", "#######"));
}
