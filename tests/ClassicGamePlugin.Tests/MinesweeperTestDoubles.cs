using ClassicGamePlugin.Features.Minesweeper.Domain;
using Xunit;

namespace ClassicGamePlugin.Tests;

/// <summary>测试专用的确定性布雷策略，严格返回调用方给出的雷位。</summary>
internal sealed class FixedMinePlacementStrategy(params CellCoordinate[] mines) : IMinePlacementStrategy
{
    private readonly CellCoordinate[] _mines = mines;

    public IReadOnlyCollection<CellCoordinate> CreateMines(
        int rows,
        int columns,
        int mineCount,
        IReadOnlySet<CellCoordinate> excludedCoordinates)
    {
        Assert.Equal(mineCount, _mines.Length);
        Assert.All(_mines, coordinate =>
        {
            Assert.InRange(coordinate.Row, 0, rows - 1);
            Assert.InRange(coordinate.Column, 0, columns - 1);
            Assert.DoesNotContain(coordinate, excludedCoordinates);
        });
        return _mines;
    }
}

/// <summary>按行优先顺序选择首批可用格子，适合不关心具体雷位的测试。</summary>
internal sealed class SequentialMinePlacementStrategy : IMinePlacementStrategy
{
    public IReadOnlyCollection<CellCoordinate> CreateMines(
        int rows,
        int columns,
        int mineCount,
        IReadOnlySet<CellCoordinate> excludedCoordinates)
    {
        var result = new List<CellCoordinate>(mineCount);
        for (var row = 0; row < rows && result.Count < mineCount; row++)
        {
            for (var column = 0; column < columns && result.Count < mineCount; column++)
            {
                var coordinate = new CellCoordinate(row, column);
                if (!excludedCoordinates.Contains(coordinate))
                {
                    result.Add(coordinate);
                }
            }
        }

        return result;
    }
}

/// <summary>不预先校验输出的布雷策略，用于确认生产引擎会拒绝不可信实现。</summary>
internal sealed class UncheckedMinePlacementStrategy(params CellCoordinate[] mines) : IMinePlacementStrategy
{
    public IReadOnlyCollection<CellCoordinate> CreateMines(
        int rows,
        int columns,
        int mineCount,
        IReadOnlySet<CellCoordinate> excludedCoordinates) => mines;
}

/// <summary>通过手工推进时间戳，让计时测试不依赖睡眠和真实墙钟时间。</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    internal void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
}
