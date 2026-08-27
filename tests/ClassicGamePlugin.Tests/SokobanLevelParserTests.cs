using ClassicGamePlugin.Features.Sokoban.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

public sealed class SokobanLevelParserTests
{
    [Fact]
    public void 解析器识别全部七种地图字符并分离地形与动态对象()
    {
        var level = SokobanLevelParser.Parse(
            "all-symbols", "全部字符", SokobanDifficulty.Beginner,
            "#######", "#+*$.$#", "#     #", "#######");

        Assert.Equal(7, level.Width);
        Assert.Equal(4, level.Height);
        Assert.Equal(new SokobanPosition(1, 1), level.InitialPlayer);
        Assert.Equal(3, level.InitialBoxes.Count);
        Assert.Equal(3, level.GoalCount);
        Assert.Equal(SokobanTerrain.Goal, level.TerrainAt(new SokobanPosition(1, 1)));
        Assert.Equal(SokobanTerrain.Goal, level.TerrainAt(new SokobanPosition(1, 2)));
        Assert.Equal(SokobanTerrain.Floor, level.TerrainAt(new SokobanPosition(1, 3)));
        Assert.Equal(SokobanTerrain.Floor, level.TerrainAt(new SokobanPosition(2, 2)));
        Assert.Equal(SokobanTerrain.Wall, level.TerrainAt(new SokobanPosition(-1, 0)));
    }

    [Theory]
    [MemberData(nameof(InvalidMaps))]
    public void 解析器用中文错误拒绝非法地图(string expectedMessage, string[] rows)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SokobanLevelParser.Parse("invalid", "非法", SokobanDifficulty.Beginner, rows));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 内置目录包含十二个稳定且按难度递进的原创关卡()
    {
        Assert.Equal(12, SokobanLevelCatalog.Levels.Count);
        Assert.Equal(12, SokobanLevelCatalog.Levels.Select(level => level.Id).Distinct().Count());
        Assert.All(SokobanLevelCatalog.Levels.Take(4), level => Assert.Equal(SokobanDifficulty.Beginner, level.Difficulty));
        Assert.All(SokobanLevelCatalog.Levels.Skip(4).Take(4), level => Assert.Equal(SokobanDifficulty.Intermediate, level.Difficulty));
        Assert.All(SokobanLevelCatalog.Levels.Skip(8), level => Assert.Equal(SokobanDifficulty.Challenge, level.Difficulty));
    }

    [Theory]
    [MemberData(nameof(BuiltInSolutions))]
    public void 每张内置地图都能用已知答案完成(int index, string solution)
    {
        var game = new SokobanGame(SokobanLevelCatalog.Levels[index]);

        foreach (var symbol in solution)
        {
            Assert.NotNull(game.Move(ToDirection(symbol)));
        }

        Assert.True(game.IsCompleted);
        Assert.Equal(game.Level.GoalCount, game.BoxesOnGoals);
    }

    public static TheoryData<string, string[]> InvalidMaps => new()
    {
        { "至少需要三行", ["###", "#@#"] },
        { "行宽一致", ["#####", "#@$ #", "####"] },
        { "边界必须全部由墙", ["#####", " @$.#", "#####"] },
        { "不支持的字符", ["#####", "#@x$#", "# . #", "#####"] },
        { "只能包含一个玩家", ["######", "#@@$.#", "######"] },
        { "必须包含一个玩家", ["#####", "# $ #", "# . #", "#####"] },
        { "至少需要一个箱子", ["#####", "# @ #", "# . #", "#####"] },
        { "必须等于目标数", ["######", "# @$ #", "# .. #", "######"] },
    };

    public static TheoryData<int, string> BuiltInSolutions => new()
    {
        { 0, "D" },
        { 1, "URD" },
        { 2, "U" },
        { 3, "L" },
        { 4, "DULD" },
        { 5, "DRRULLDDRR" },
        { 6, "DDUULDD" },
        { 7, "UDRRRU" },
        { 8, "DDUULDDUURRDD" },
        { 9, "UDLUDRRU" },
        { 10, "DRRULLDDRRUULLDDDRR" },
        { 11, "DDUURDDUURDDUURDD" },
    };

    private static SokobanDirection ToDirection(char symbol) => symbol switch
    {
        'U' => SokobanDirection.Up,
        'D' => SokobanDirection.Down,
        'L' => SokobanDirection.Left,
        'R' => SokobanDirection.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(symbol)),
    };
}
