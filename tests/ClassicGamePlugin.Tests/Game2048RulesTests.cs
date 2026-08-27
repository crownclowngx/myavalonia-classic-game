using ClassicGamePlugin.Features.Game2048.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class Game2048RulesTests
{
    [Theory]
    [MemberData(nameof(LineCases))]
    public void 向左移动先压缩且每个方块每步最多合并一次(
        int[] source,
        int[] expected,
        int expectedScore)
    {
        var board = Board(source, EmptyLine, EmptyLine, EmptyLine);

        var result = Game2048Rules.ProjectMove(board, Game2048Direction.Left);

        Assert.Equal(Board(expected, EmptyLine, EmptyLine, EmptyLine), result.Cells);
        Assert.Equal(expectedScore, result.ScoreDelta);
        Assert.Equal(!source.SequenceEqual(expected), result.HasChanged);
    }

    [Fact]
    public void 四个方向都朝指定边缘压缩并合并()
    {
        var horizontal = Board([0, 2, 2, 0], EmptyLine, EmptyLine, EmptyLine);
        var vertical = Board([0, 2, 0, 0], [0, 2, 0, 0], EmptyLine, EmptyLine);

        Assert.Equal(
            Board([4, 0, 0, 0], EmptyLine, EmptyLine, EmptyLine),
            Game2048Rules.ProjectMove(horizontal, Game2048Direction.Left).Cells);
        Assert.Equal(
            Board([0, 0, 0, 4], EmptyLine, EmptyLine, EmptyLine),
            Game2048Rules.ProjectMove(horizontal, Game2048Direction.Right).Cells);
        Assert.Equal(
            Board([0, 4, 0, 0], EmptyLine, EmptyLine, EmptyLine),
            Game2048Rules.ProjectMove(vertical, Game2048Direction.Up).Cells);
        Assert.Equal(
            Board(EmptyLine, EmptyLine, EmptyLine, [0, 4, 0, 0]),
            Game2048Rules.ProjectMove(vertical, Game2048Direction.Down).Cells);
    }

    [Fact]
    public void 满盘但存在相邻同值时仍有合法移动()
    {
        var board = Board(
            [2, 2, 4, 8],
            [4, 8, 16, 32],
            [8, 16, 32, 64],
            [16, 32, 64, 128]);

        Assert.True(Game2048Rules.HasAvailableMove(board));
    }

    [Fact]
    public void 满盘且水平垂直均无同值时没有合法移动()
    {
        Assert.False(Game2048Rules.HasAvailableMove(Checkerboard));
    }

    [Fact]
    public void 普通移动记录静止与位移方块的准确轨迹()
    {
        var board = Board([2, 0, 4, 0], EmptyLine, EmptyLine, EmptyLine);

        var result = Game2048Rules.ProjectMove(board, Game2048Direction.Left);

        Assert.Equal(
            [
                new Game2048TileMotion(
                    new Game2048Position(0, 0),
                    new Game2048Position(0, 0),
                    2,
                    false),
                new Game2048TileMotion(
                    new Game2048Position(0, 2),
                    new Game2048Position(0, 1),
                    4,
                    false),
            ],
            result.Motions);
        Assert.Empty(result.MergedPositions);
    }

    [Fact]
    public void 两个合并来源记录同一目标且目标只出现一次()
    {
        var board = Board([0, 2, 2, 0], EmptyLine, EmptyLine, EmptyLine);

        var result = Game2048Rules.ProjectMove(board, Game2048Direction.Left);

        Assert.Equal(2, result.Motions.Count);
        Assert.All(result.Motions, motion =>
        {
            Assert.Equal(new Game2048Position(0, 0), motion.Target);
            Assert.True(motion.IsMergeParticipant);
        });
        Assert.Equal([new Game2048Position(0, 0)], result.MergedPositions);
    }

    [Theory]
    [InlineData((int)Game2048Direction.Left, 1, 0)]
    [InlineData((int)Game2048Direction.Right, 1, 3)]
    [InlineData((int)Game2048Direction.Up, 0, 1)]
    [InlineData((int)Game2048Direction.Down, 3, 1)]
    public void 四方向轨迹目标与移动边缘一致(
        int directionValue,
        int targetRow,
        int targetColumn)
    {
        var board = Board(
            EmptyLine,
            [0, 2, 0, 0],
            EmptyLine,
            EmptyLine);

        var result = Game2048Rules.ProjectMove(board, (Game2048Direction)directionValue);

        var motion = Assert.Single(result.Motions);
        Assert.Equal(new Game2048Position(targetRow, targetColumn), motion.Target);
    }

    public static TheoryData<int[], int[], int> LineCases => new()
    {
        { [0, 2, 0, 2], [4, 0, 0, 0], 4 },
        { [2, 2, 2, 2], [4, 4, 0, 0], 8 },
        { [2, 2, 4, 0], [4, 4, 0, 0], 4 },
        { [4, 4, 4, 0], [8, 4, 0, 0], 8 },
        { [2, 4, 8, 16], [2, 4, 8, 16], 0 },
    };

    internal static int[] Checkerboard => Board(
        [2, 4, 2, 4],
        [4, 2, 4, 2],
        [2, 4, 2, 4],
        [4, 2, 4, 2]);

    internal static int[] EmptyLine => [0, 0, 0, 0];

    internal static int[] Board(params int[][] rows)
    {
        Assert.Equal(Game2048Rules.BoardSize, rows.Length);
        Assert.All(rows, row => Assert.Equal(Game2048Rules.BoardSize, row.Length));
        return rows.SelectMany(row => row).ToArray();
    }
}
